using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattlePsychoBlast23PresentationTests
{
    [Fact]
    public void Original_Psycho_Blast_component_continues_its_actual_activated_caster_standalone()
    {
        Assert.SkipUnless(Environment.GetEnvironmentVariable("MEMORIA_FF8_PSYCHO_BLAST23_SOURCE") is not null, "Select an explicitly migrated standalone with the original activated caster.");
        NativeBattleActorDirectorPresentationTests.RunSelected([23], "MEMORIA_FF8_PSYCHO_BLAST23", SelectOriginalFixture, PrepareStandalone);
    }

    private static NativeEffectFixtureRequest SelectOriginalFixture(BattleAssets assets, ushort effect)
    {
        Assert.Equal((ushort)23, effect);
        BattleNativeAction action = Assert.Single(assets.NativeActions, row => row.Command == 8 && row.ActionId == 168 && row.MagicId == effect && row.KernelSection == 3 && row.KernelIndex == 168);
        BattleEncounter formation = assets.LoadEncounter(63);
        Assert.Equal(13, formation.StageId);
        BattleEnemyPlacement[] enemies = NativeBattleEffectFixtureExporter.SelectEnemies(formation, true);
        Assert.Equal(new[] { 68, 81, 80, 77 }, enemies.Select(enemy => enemy.ModelId).ToArray());
        Assert.True(enemies[3].InitiallySuspended);
        Assert.True(enemies[3].Hidden);
        Assert.True(enemies[3].Untargetable);
        NativeBattleAiDefinition ai = assets.ReadEnemyAi(enemies[3].ModelId, enemies[3].Level);
        Assert.Equal((byte)17, NativeBattleEffectFixtureExporter.SelectEnemySequence(ai, action));
        return new("psycho-blast23", effect, action.Command, action.ActionId, 0, 800, 240) { EncounterId = formation.Id, CasterActorIndex = 6, PreserveOriginalFormationEnemies = true, CrisisLevel = action.CrisisLevel };
    }

    private static BattleSnapshot PrepareStandalone(GameAssets game, BattleAssets assets, NativeEffectFixtureRequest request)
    {
        string input = Environment.GetEnvironmentVariable("MEMORIA_FF8_PSYCHO_BLAST23_SOURCE") ?? throw new InvalidDataException("Psycho Blast requires an explicitly selected standalone.");
        BattleSnapshot source = NativeBattleEffectFamilyRunner.LoadSnapshot(input);
        string output = Environment.GetEnvironmentVariable("MEMORIA_FF8_PSYCHO_BLAST23_EVIDENCE") ?? throw new InvalidDataException("Select a distinct evidence directory.");
        File.WriteAllText(Path.Combine(output, "standalone-input.json"), JsonSerializer.Serialize(new { Sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(input))), source.Frame, NativeCallback = source.NativeGraphics?.Callback, Scenario = "Original activated caster standalone; controlled seven-HP post-formula packet; no ordinary action or formula claim" }));
        Assert.Equal(63, source.Definition.EncounterId);
        Assert.Null(source.ActiveAction);
        Assert.Empty(source.QueuedActions);
        NativeBattleGraphicsState graphics = source.NativeGraphics ?? throw new InvalidDataException("The standalone lacks its current graphics process.");
        Assert.False(graphics.Descriptors.Queue.Any(index => graphics.Descriptors.Packets[index].Command == 104));
        Assert.False(graphics.Descriptors.Tasks.Any(task => (task.Flags & 1) != 0 && task.Kind is NativeBattleDescriptorTaskKind.Action104 or NativeBattleDescriptorTaskKind.Callback502F30));
        Assert.Equal(new byte?[] { 0x54, 0x61, 0x60, 0x5D }, graphics.Models.Skip(3).Take(4).Select(model => model.ModelId4).ToArray());
        BattleCombatantSnapshot caster = source.Combatants.Single(actor => actor.NativeParticipant!.ActorIndex == 6);
        NativeBattleParticipantSnapshot participant = caster.NativeParticipant!;
        Assert.True(participant.Active);
        Assert.False(participant.Hidden);
        Assert.False(participant.Suspended);
        Assert.False(participant.Untargetable);
        Assert.False(participant.AllocationPendingStatistics);
        Assert.Equal((byte)0, graphics.Models[6].Kind5);
        Assert.Equal((ushort)2, (ushort)(graphics.Models[6].Flags0 & 6));
        Assert.Contains(source.Events, item => item.Kind == "Activated" && item.ActorId == caster.Id);
        NativeBattleEffectMemory memory = source.NativeEffectMemory ?? throw new InvalidDataException("The standalone lacks its current retained effect process.");
        Assert.Equal(0, memory.Active);
        _ = memory.RequirePsychoBlast23();
        NativeBattleOriginalEffectState previous = graphics.OriginalEffect ?? throw new InvalidDataException("The standalone must retain its completed original producer.");
        Assert.Equal((ushort)20, previous.Binding.Effect);
        Assert.Equal(0u, previous.ReturnedDescriptor);
        Assert.All(previous.Pools, pool => Assert.All(pool.Slots, task => Assert.Equal(0, task.Flags & 1)));
        BattleNativeAction action = NativeBattleEffectFixtureExporter.SelectAction(assets.NativeActions, request);
        BattleEnemyPlacement placement = NativeBattleEffectFixtureExporter.SelectEnemies(assets.LoadEncounter(63), true)[3];
        byte sequence = NativeBattleEffectFixtureExporter.SelectEnemySequence(assets.ReadEnemyAi(placement.ModelId, placement.Level), action);
        Assert.Equal((byte)17, sequence);
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([23])[22];
        ImmutableArray<NativeBattleEffectBinding> catalog = source.Definition.NativeEffectCatalog.SetItem(22, binding);
        NativeBattleActionPacketsState packets = (source.NativeActionPackets ?? throw new InvalidDataException("The standalone lacks its physical action arena.")).ResetCounts();
        (packets, int header) = packets.Prepare(new(6, action.Command, sequence, -1, action.ActionId, action.MagicId, null));
        BattleCombatantSnapshot target = source.Combatants.Single(actor => actor.NativeParticipant!.ActorIndex == 0);
        NativeBattleStatusSnapshot status = target.NativeStatus ?? throw new InvalidDataException("The current target lacks its actual status banks.");
        (packets, _) = packets.AppendContact(new(new(0, action.DamagePresentation, 0, 0, status.Saved, 7, status.Battle), new(255, 0, 0, 0, 0, 0, 0)));
        (packets, _) = packets.Publish(1);
        packets.Validate();
        (NativeBattleDescriptorState descriptors, _) = graphics.Descriptors.Enqueue(104, 128, checked(0x1D280C4u + (uint)header * 20));
        NativeBattleRetainedMemory retained = source.Definition.NativeRetainedMemory ?? throw new InvalidDataException("The standalone lacks its retained definition mirror.");
        BattleSnapshot prepared = source with { NativeActionPackets = packets, NativeGraphics = graphics with { Descriptors = descriptors }, Definition = source.Definition with { NativeEffectCatalog = catalog, NativeRetainedMemory = retained with { ActionPackets = packets } } };
        Assert.Equal(JsonSerializer.Serialize(source.Combatants), JsonSerializer.Serialize(prepared.Combatants));
        Assert.Equal(JsonSerializer.Serialize(source.NativeEffectMemory), JsonSerializer.Serialize(prepared.NativeEffectMemory));
        Assert.Equal(JsonSerializer.Serialize(source.NativeGlobalRandom), JsonSerializer.Serialize(prepared.NativeGlobalRandom));
        return prepared;
    }
}
