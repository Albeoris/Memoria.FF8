using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

internal static class NativeBattleLifecycleComponentSources
{
    internal static bool Supports(ushort effect) => effect is 17 or 18 or 110 or 168 or 179 or 180 or 248;

    internal static BattleSnapshot? Prepare(ushort effect, string gameDirectory, string output)
    {
        if (!Supports(effect))
        {
            return null;
        }
        Directory.CreateDirectory(output);
        string intermediate = Path.Combine(output, "entry");
        if (Directory.Exists(intermediate))
        {
            throw new IOException("Lifecycle component preparation requires a fresh owned entry directory.");
        }
        BattleSnapshot prepared = effect switch
        {
            17 or 18 => PrepareTower(effect, gameDirectory, intermediate),
            110 or 248 => PrepareControl(effect, gameDirectory),
            _ => PrepareEnemy(effect, gameDirectory, intermediate)
        };
        if (Directory.Exists(intermediate))
        {
            foreach (string snapshot in Directory.EnumerateFiles(intermediate, "*.battle.json", SearchOption.AllDirectories))
            {
                File.Delete(snapshot);
            }
        }
        return prepared;
    }

    private static BattleSnapshot PrepareTower(ushort effect, string gameDirectory, string output)
    {
        string[] variables = ["MEMORIA_FF8_GAME_DIR", "MEMORIA_FF8_TOWER_CONTROL_EXPORT"];
        string?[] previous = variables.Select(Environment.GetEnvironmentVariable).ToArray();
        Environment.SetEnvironmentVariable(variables[0], gameDirectory);
        Environment.SetEnvironmentVariable(variables[1], output);
        try
        {
            new NativeBattleTowerControlFixtureExportTests().Export_sparse_original_tower_control17_and18_components();
            return NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(output, $"control{effect}-queued-action104.battle.json"));
        }
        finally
        {
            for (int index = 0; index < variables.Length; index++)
            {
                Environment.SetEnvironmentVariable(variables[index], previous[index]);
            }
        }
    }

    private static BattleSnapshot PrepareEnemy(ushort effect, string gameDirectory, string output)
    {
        NativeEffectFixtureRequest request = effect switch
        {
            168 => new("original168", 168, 8, 113, 0, 1200, 240) { EncounterId = 34, CasterActorIndex = 3, PreserveOriginalFormationEnemies = true },
            179 => new("original179", 179, 8, 82, 0, 1200, 240) { EncounterId = 36, CasterActorIndex = 4, PreserveOriginalFormationEnemies = true },
            180 => new("original180", 180, 8, 62, 0, 1200, 240) { EncounterId = 36, CasterActorIndex = 4, PreserveOriginalFormationEnemies = true },
            _ => throw new ArgumentOutOfRangeException(nameof(effect))
        };
        NativeEffectFixtureExportResult exported = NativeBattleEffectFixtureExporter.Export(request, gameDirectory, output);
        if (exported.Status == "blocked")
        {
            throw new InvalidDataException($"Original lifecycle component failed at {exported.Boundary}: {exported.FirstBlocker}");
        }
        return NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(output, "queued-action104.battle.json"));
    }

    private static BattleSnapshot PrepareControl(ushort effect, string gameDirectory)
    {
        using GameAssets game = new(gameDirectory);
        using BattleAssets assets = new(gameDirectory);
        int model = effect == 110 ? 74 : 71;
        const int level = 20;
        NativeBattleAiDefinition ai = assets.ReadEnemyAi(model, level);
        int slot = Enumerable.Range(0, 16).First(index => ai.Abilities[16 * ai.LevelBand + index].Id != 0);
        NativeBattleAiAbility ability = ai.Abilities[16 * ai.LevelBand + slot];
        BattleNativeAction original = assets.NativeActions.Single(action => action.Command == ability.Command && action.ActionId == ability.Id);
        BattleNativeAction control = original with { Command = 252, ActionId = 1, MagicId = effect, AttackType = 0 };
        BattleDefinition definition = NativeBattleRegionalMonsterPresentationTests.CreateDefinition(game, assets, model, level, slot, ai, control);
        BattleSession battle = new(definition, BattleAtbMode.Normal);
        for (int callback = 0; callback < 1200; callback++)
        {
            BattleSnapshot current = battle.Capture();
            NativeBattleGraphicsState graphics = current.NativeGraphics ?? throw new InvalidDataException("The original lifecycle component lost its graphics owner.");
            if (graphics.OriginalEffect is { ReturnedDescriptor: null } admitted && admitted.RequestedEffect == effect && admitted.Pools.IsEmpty || graphics.Descriptors.Queue.Any(index => graphics.Descriptors.Packets[index].Command == 104 && HeaderEffect(current, graphics.Descriptors.Packets[index].Argument) == effect))
            {
                return current;
            }
            battle.Tick();
        }
        throw new InvalidDataException("The original lifecycle driver did not produce the selected native control within its explicit entry budget.");
    }

    private static ushort HeaderEffect(BattleSnapshot source, uint address)
    {
        if (address < 0x1D280C4 || (address - 0x1D280C4) % 20 != 0)
        {
            throw new InvalidDataException("The lifecycle descriptor does not reference its physical native header table.");
        }
        int index = checked((int)((address - 0x1D280C4) / 20));
        NativeBattleActionPacketsState packets = source.NativeActionPackets ?? throw new InvalidDataException("The lifecycle source lost its native action packets.");
        if (index >= packets.ActionCount)
        {
            throw new InvalidDataException("The lifecycle descriptor references an unpublished native header.");
        }
        return NativeBattleActionGraphics.Word(packets.Headers[index], 6);
    }
}
