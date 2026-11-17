using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

internal static class NativeBattleComponentInputs
{
    internal static void ConfigureFieldMusic(EventEngine engine, byte[] initializationProgram, byte sourceBank, byte music203, int primaryChannel)
    {
        // Execute the actual verified New Game music reset in a separate fresh component; no historical selector is invented.
        GameSession initial = GameSession.CreateColdProcess();
        initial.StartNewGame();
        EventEngine bootstrap = new(initial);
        bootstrap.LoadField(74, initializationProgram, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        if (bootstrap.Scene.MusicProcess?.Bank201 != sourceBank || bootstrap.Scene.MusicChannel108?.ChannelId != primaryChannel || bootstrap.Scene.NativeBattleMusic203 != music203)
        {
            throw new InvalidOperationException("The component music inputs must match the executed native reset.");
        }
        JsonNode state = JsonNode.Parse(engine.Capture().GetRawText())!;
        state["Scene"]!["MusicProcess"] = JsonSerializer.SerializeToNode(bootstrap.Scene.MusicProcess);
        state["Scene"]!["MusicChannel108"] = JsonSerializer.SerializeToNode(bootstrap.Scene.MusicChannel108);
        state["Scene"]!["NativeBattleMusic203"] = music203;
        engine.Restore(JsonSerializer.SerializeToElement(state));
    }

    internal static BattleDefinition PrepareMusic(BattleDefinition definition, string gameDirectory, byte sourceBank, byte music203, int primaryChannel)
    {
        // Fresh installed-resource components supply the participant metadata required by5027D0.
        if (definition.Combatants.Any(actor => !actor.IsPlayer && actor.Information246 is null && actor.NativeTemplate?.Information246 is null))
        {
            using BattleAssets assets = new(gameDirectory);
            definition = definition with { Combatants = definition.Combatants.Select(actor => !actor.IsPlayer && actor.Information246 is null && actor.NativeTemplate?.Information246 is null ? actor with { Information246 = assets.ReadEnemyNativeInformation246(actor.ModelId) } : actor).ToArray() };
        }
        // Independent components declare their source selector and channel; the actual entry producer owns the resulting buffers and return selector.
        NativeMusicProcessState buffers = new(1, null, null, null, sourceBank, null);
        NativeGlobalSoundProcess sound = definition.NativeGlobalSoundProcess ?? throw new InvalidOperationException("The component must supply its physical sound process before music entry.");
        NativeBattleMusicEntryResources resources = new NativeMusicCatalog(gameDirectory).ReadBattleEntryResources();
        NativeBattleMusicEntryPreparation entry = NativeBattleMusicEntry.Prepare52CE30(checked((ushort)definition.EncounterId!.Value), definition.NativeFlags, buffers, music203, primaryChannel, sound, resources);
        return definition with { NativeMusicProcess = entry.MusicProcess, NativeGlobalSoundProcess = entry.SoundProcess };
    }

    internal static BattleEnemyPlacement[] SelectEnemies(BattleEncounter formation, bool preserveOriginalFormationEnemies)
    {
        BattleEnemyPlacement[] enemies = formation.Enemies.Where(enemy => enemy.Enabled && enemy.IsEnemyModel).ToArray();
        if (preserveOriginalFormationEnemies)
        {
            if (enemies.Length is < 1 or > 4 || enemies.Select(enemy => enemy.Slot).Distinct().Count() != enemies.Length)
            {
                throw new InvalidDataException("The preserved formation requires one to four distinct original enemy placements.");
            }
            return enemies.OrderBy(enemy => enemy.Slot).ToArray();
        }
        return enemies.Length == 1 ? enemies : throw new InvalidDataException("The two-actor component requires exactly one enabled original enemy placement.");
    }

    internal static BattleEnemyPlacement SelectEnemy(BattleEncounter formation, bool preserveOriginalFormationEnemies = false)
    {
        return SelectEnemies(formation, preserveOriginalFormationEnemies)[0];
    }

    internal static string ComponentEnemyName(string originalName, int modelId)
    {
        // An anonymous original DAT keeps its native name; this label identifies only the controlled host fixture row.
        return string.IsNullOrWhiteSpace(originalName) ? $"Original enemy model {modelId}" : originalName;
    }

    internal static byte SelectEnemySequence(NativeBattleAiDefinition ai, BattleNativeAction action)
    {
        byte[] sequences = ai.Abilities.Where(ability => ability.Command == action.Command && ability.Id == action.ActionId).Select(ability => ability.Sequence).Distinct().ToArray();
        return sequences.Length == 1 ? sequences[0] : throw new InvalidDataException("The enemy component requires one original AI sequence for its selected kernel command/action row.");
    }

    internal static byte SelectSequence(ImmutableArray<byte> sequences, byte command, ushort action)
    {
        return NativeBattleActionGraphics.ResolvePlayerSequence(command, 0, action, sequences);
    }
}
