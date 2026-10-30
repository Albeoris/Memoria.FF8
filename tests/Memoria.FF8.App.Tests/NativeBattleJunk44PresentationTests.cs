using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleJunk44PresentationTests
{
    [Fact]
    public void Original_Junk44_native_allocated_caster_preserves_COMMON_IO_and_cold_cycle()
    {
        NativeBattleActorDirectorPresentationTests.RunSelected([44], "MEMORIA_FF8_JUNK44", SelectOriginalFixture, PrepareDeclaredSource);
    }

    private static NativeEffectFixtureRequest SelectOriginalFixture(BattleAssets assets, ushort effect)
    {
        Assert.Equal((ushort)44, effect);
        BattleNativeAction action = Assert.Single(assets.NativeActions, row => row.Command == 8 && row.ActionId == 226 && row.MagicId == effect && row.KernelSection == 3 && row.KernelIndex == 226);
        Assert.Null(action.TargetFlags);
        BattleEncounter formation = assets.LoadEncounter(236);
        Assert.Equal(76, formation.StageId);
        BattleEnemyPlacement prototype = Assert.Single(formation.Slots, enemy => enemy.Slot == 0);
        Assert.Equal(83, prototype.ModelId);
        Assert.Equal(254, prototype.Level);
        Assert.Equal(new NativeBattleVector(0, 0, -2900), prototype.NativePosition);
        Assert.True(prototype.Hidden);
        Assert.True(prototype.InitiallySuspended);
        Assert.True(prototype.Untargetable);
        Assert.False(prototype.Enabled);
        Assert.Equal((byte)15, NativeBattleEffectFixtureExporter.SelectEnemySequence(assets.ReadEnemyAi(83, prototype.Level), action));
        return new("junk44", effect, 8, 226, 0, 1200, 240) { EncounterId = 236, CasterActorIndex = 0, PreserveOriginalFormationEnemies = true, ControlledContactValue = 7 };
    }

    private static BattleSnapshot PrepareDeclaredSource(GameAssets game, BattleAssets assets, NativeEffectFixtureRequest request)
    {
        string output = Environment.GetEnvironmentVariable("MEMORIA_FF8_JUNK44_EVIDENCE") ?? throw new InvalidDataException("Select an evidence directory for the original Junk component.");
        _ = NativeBattleEffectFixtureExporter.Export(request, Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? throw new InvalidDataException("Select installed original resources."), Path.Combine(output, "fixture"));
        BattleSnapshot original = NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(output, "fixture", "before-entry.battle.json"));
        (BattleSnapshot ready, int allocatedActor) = NativeBattleJunk44FixturePreparation.Prepare(game, assets, original, output);
        Assert.InRange(allocatedActor, 3, 6);
        BattleCombatantSnapshot target = ready.Combatants.Single(actor => actor.NativeParticipant!.ActorIndex == 0);
        NativeBattleStatusSnapshot status = target.NativeStatus ?? throw new InvalidDataException("The declared Junk contact requires its current target status owner.");
        target = target with { NativeStatus = status with { Presentation = new(status.Saved, status.Battle) } };
        NativeBattlePresentationStatusMemory presentationStatus = (ready.NativePresentationStatusMemory ?? throw new InvalidDataException("The declared Junk contact requires its current physical presentation-status bank.")).Observe(0, target.NativeStatus!.Presentation);
        ready = ready with { Combatants = ready.Combatants.Select(actor => actor.Id == target.Id ? target : actor).ToArray(), NativePresentationStatusMemory = presentationStatus };
        Assert.Equal(status.Saved, target.NativeStatus!.Presentation.Saved);
        Assert.Equal(status.Battle, target.NativeStatus!.Presentation.Battle);
        NativeBattleActionPacketsState packets = ready.NativeActionPackets!;
        Assert.Equal((byte)0, packets.ActionCount);
        (packets, int header) = packets.Prepare(new(checked((byte)allocatedActor), 8, 15, -1, 226, 44, null));
        (packets, _) = packets.AppendContact(new(new(0, assets.NativeActions.Single(row => row.Command == 8 && row.ActionId == 226 && row.MagicId == 44).DamagePresentation, 0, 0, status.Saved, 7, status.Battle), new(255, 0, 0, 0, 0, 0, 0)));
        (packets, _) = packets.Publish(1);
        (NativeBattleDescriptorState descriptors, _) = ready.NativeGraphics!.Descriptors.Enqueue(104, 128, checked(0x1D280C4u + (uint)header * 20));
        BattleSnapshot source = ready with { NativeActionPackets = packets, NativeGraphics = ready.NativeGraphics with { Descriptors = descriptors } };
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([44])[43];
        Assert.Equal(NativeBattleJunk44Profile.Entry, binding.Entry);
        Assert.Equal(NativeBattleJunk44Profile.Initializer, binding.Initializer);
        Assert.Equal(NativeBattleJunk44Profile.TextureLength, binding.InitializerAsset!.Data.Length);
        Assert.Same(game.ReadNativeJunk44Resources(), binding.Junk44Resources);
        BattleCombatantDefinition definition = BattleSession.Restore(source).GetCombatantDefinition(source.NativeGraphics.ActorIds[allocatedActor]!);
        Assert.Equal(83, definition.ModelId);
        NativeBattleGraphicsResource resource = assets.ReadNativeGraphicsResource(definition);
        NativeBattleGraphicsModel model = source.NativeGraphics.Models[allocatedActor];
        Assert.NotNull(resource.PoseSource);
        Assert.Equal(resource.PoseSource.BoneCount, NativeBattleBodyBoneMatrices.Require(model, resource.PoseSource).Length);
        Assert.NotNull(model.GeometryWorldMatrix);
        Assert.NotNull(model.Rotation14);
        Assert.NotNull(model.ActiveResourceRow100);
        Assert.Equal((byte)allocatedActor, packets.Headers[header].ReadByte(0));
        Assert.Equal((byte)15, packets.Headers[header].ReadByte(2));
        Assert.Equal((byte)1, packets.Headers[header].ReadByte(16));
        File.WriteAllText(Path.Combine(output, "controlled-contact.json"), JsonSerializer.Serialize(new { Scenario = "Original Junk44 command8/action226/sequence15; originalformation236 prototype0 allocated and activated by current native producer; controlled preparation does not claim twenty ordinary AI deaths; declared seven-HP physical party0 contact with logical HP/status retained", allocatedActor, Header = packets.Headers[header], Contact = packets.ReadContact(0), OriginalPrototype = assets.LoadEncounter(236).Slots.Single(enemy => enemy.Slot == 0) }));
        return source;
    }
}
