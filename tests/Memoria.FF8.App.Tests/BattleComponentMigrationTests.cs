using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

/// <summary>Explicit migration of this task's declared frame0 component; this does not reconstruct an exploration save.</summary>
public sealed class BattleComponentMigrationTests
{
    [Fact]
    public void MigrateOnlyTheNamedInitialComponentAndValidateIndependentLoad()
    {
        string? input = Environment.GetEnvironmentVariable("MEMORIA_FF8_COMPONENT_MIGRATION_SOURCE");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_COMPONENT_MIGRATION_OUTPUT");
        Assert.SkipUnless(input is not null && output is not null, "Explicit original and separately named migration paths are required.");
        Assert.NotEqual(Path.GetFullPath(input!), Path.GetFullPath(output!));
        BattleSnapshot original = JsonSerializer.Deserialize<BattleSnapshot>(File.ReadAllText(input!))!;
        Assert.Equal(0, original.Frame);
        Assert.Equal(512, original.Definition.EncounterId);
        Assert.Empty(original.QueuedActions);
        Assert.Null(original.ActiveAction);
        Assert.All(original.Combatants, actor => Assert.Empty(actor.StatusEffects));
        using GameAssets assets = new(LaunchOptions.DefaultGameDirectory);
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals().AcquireGuardianForce(0).ToggleGuardianForceJunction(0, 0).RebuildParty().Rest();
        GameSession session = new(new GameState { Scene = SceneKind.Field, FieldId = 999, CharacterVitals = vitals, CharacterAppearance = assets.ReadNewGameCharacterAppearance(), Economy = assets.ReadNewGameEconomy(), BattleVariables = NativeBattleVariableState.CreateNewGame() });
        using GameWorld world = new(session, assets.GameDirectory);
        BattleDefinition installed = world.CreateEncounterBattle(512);
        Assert.Equal(original.Items, installed.NativeItemInventory!.Where(stock => stock.ItemId != 0 && stock.Count != 0).Select(stock => new BattleStock(stock.ItemId, stock.Count)));
        BattleCombatantDefinition[] definitions = original.Definition.Combatants.Select(source =>
        {
            BattleCombatantDefinition current = installed.Combatants.Single(actor => actor.Id == source.Id);
            Assert.Equal(source.MaximumHp, current.MaximumHp);
            Assert.Equal(source.NativeAi?.Texts, current.NativeAi?.Texts);
            return source with { NativeAi = current.NativeAi, NativeTemplate = current.NativeTemplate, NativeCombatantFlags = current.NativeCombatantFlags, NativeStatus0 = current.NativeStatus0, NativeStatus1 = current.NativeStatus1, NativeStatusResistances = current.NativeStatusResistances, NativeMagicInventory = current.NativeMagicInventory, NativeCommands = current.NativeCommands, NativeCommandSlots = current.NativeCommandSlots, NativeLimit = current.NativeLimit, GuardianForceCompatibility = current.GuardianForceCompatibility, NativeCharacterFlag100 = current.NativeCharacterFlag100, NativeKillCount = current.NativeKillCount };
        }).ToArray();
        BattleDefinition definition = installed with { Combatants = definitions, InitialRandom = original.Definition.InitialRandom, NativeSeed = original.Definition.NativeSeed, NativeExamTimerAtEntry = original.Definition.NativeExamTimerAtEntry };
        BattleSnapshot migrated = original with
        {
            Definition = definition,
            GuardianForces = installed.GuardianForces.ToArray(),
            NativeItemInventory = installed.NativeItemInventory!.ToArray(),
            Items = [],
            Combatants = original.Combatants.Select(saved =>
            {
                BattleCombatantDefinition actor = definitions.Single(candidate => candidate.Id == saved.Id);
                Assert.Equal(actor.NativeMagicInventory?.Where(stock => stock.SpellId != 0 && stock.Count != 0).Select(stock => new BattleStock(stock.SpellId, stock.Count)) ?? actor.MagicStocks, saved.MagicStocks);
                NativeBattleParticipantSnapshot? native = saved.NativeParticipant;
                if (native is not null)
                {
                    uint lifecycle = (native.Active ? 1u : 0) | (native.Hidden ? 2u : 0) | (native.Untargetable ? 0x40u : 0) | (native.Suspended ? 0x80u : 0);
                    native = native with { NativeCombatantFlags = actor.NativeCombatantFlags | lifecycle, ElementDefenses = actor.ElementDefenses.ToArray(), KillCount = actor.NativeKillCount };
                }
                NativeMagicStock[] slots = actor.NativeMagicInventory ?? saved.MagicStocks.Select(stock => new NativeMagicStock(checked((byte)stock.Id), checked((byte)stock.Count))).Concat(Enumerable.Repeat(new NativeMagicStock(0, 0), 32 - saved.MagicStocks.Length)).ToArray();
                return saved with { NativeParticipant = native, NativeMagicInventory = slots, MagicStocks = [], NativeStatus = new(actor.NativeStatus0, actor.NativeStatus1, Enumerable.Repeat((short)-1111, 14).ToArray()) };
            }).ToArray()
        };
        BattleSession restored = BattleSession.Restore(migrated);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output!))!);
        File.WriteAllText(output!, JsonSerializer.Serialize(restored.Capture(), new JsonSerializerOptions { WriteIndented = true }));
        BattleSession independent = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(File.ReadAllText(output!))!);
        Assert.Equal(JsonSerializer.Serialize(restored.Capture()), JsonSerializer.Serialize(independent.Capture()));
        restored.Tick();
        independent.Tick();
        Assert.Equal(JsonSerializer.Serialize(restored.Capture()), JsonSerializer.Serialize(independent.Capture()));
    }
}
