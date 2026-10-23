using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEvilEye282PresentationTests
{
    [Fact]
    public void Original_EvilEye282_native_allocated_caster_preserves_current_bones_stage_and_cold_cycle()
    {
        NativeBattleActorDirectorPresentationTests.RunSelected([282], "MEMORIA_FF8_EVILEYE282", SelectOriginalFixture, PrepareDeclaredSource);
    }

    private static NativeEffectFixtureRequest SelectOriginalFixture(BattleAssets assets, ushort effect)
    {
        Assert.Equal((ushort)282, effect);
        BattleNativeAction action = Assert.Single(assets.NativeActions, row => row.Command == 8 && row.ActionId == 152 && row.MagicId == effect && row.KernelSection == 3 && row.KernelIndex == 152);
        Assert.Null(action.TargetFlags);
        BattleEncounter formation = assets.LoadEncounter(436);
        Assert.Equal(125, formation.StageId);
        BattleEnemyPlacement prototype = Assert.Single(formation.Slots, enemy => enemy.Slot == 3);
        Assert.Equal(94, prototype.ModelId);
        Assert.Equal(154, prototype.Level);
        Assert.Equal(new NativeBattleVector(0, 0, -5300), prototype.NativePosition);
        Assert.True(prototype.Hidden);
        Assert.True(prototype.InitiallySuspended);
        Assert.True(prototype.Untargetable);
        Assert.False(prototype.Enabled);
        Assert.Equal((byte)12, NativeBattleEffectFixtureExporter.SelectEnemySequence(assets.ReadEnemyAi(94, prototype.Level), action));
        return new("evil-eye282", effect, 8, 152, 0, 1200, 240) { EncounterId = 436, CasterActorIndex = 6, PreserveOriginalFormationEnemies = true, ControlledContactValue = 7 };
    }

    private static BattleSnapshot PrepareDeclaredSource(GameAssets game, BattleAssets assets, NativeEffectFixtureRequest request)
    {
        string output = Environment.GetEnvironmentVariable("MEMORIA_FF8_EVILEYE282_EVIDENCE") ?? throw new InvalidDataException("Select an evidence directory for the original Evil-Eye component.");
        NativeEffectFixtureRequest baselineRequest = new("evil-eye282-baseline", 45, 8, 84, 0, 1200, 240) { EncounterId = 436, CasterActorIndex = 3, PreserveOriginalFormationEnemies = true, AdmissionProbeMaximumCallbacks = 0 };
        _ = NativeBattleEffectFixtureExporter.Export(baselineRequest, Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? throw new InvalidDataException("Select installed original resources."), Path.Combine(output, "fixture"));
        BattleSnapshot original = NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(output, "fixture", "before-entry.battle.json"));
        (BattleSnapshot ready, int allocatedActor) = NativeBattleEvilEye282FixturePreparation.Prepare(game, assets, original, output);
        Assert.InRange(allocatedActor, 3, 6);
        BattleCombatantSnapshot target = ready.Combatants.Single(actor => actor.NativeParticipant!.ActorIndex == 0);
        NativeBattleStatusSnapshot status = target.NativeStatus ?? throw new InvalidDataException("The declared Evil-Eye contact requires its current target status owner.");
        target = target with { NativeStatus = status with { Presentation = new(status.Saved, status.Battle) } };
        ready = ready with { Combatants = ready.Combatants.Select(actor => actor.Id == target.Id ? target : actor).ToArray() };
        Assert.Equal(status.Saved, target.NativeStatus!.Presentation.Saved);
        Assert.Equal(status.Battle, target.NativeStatus!.Presentation.Battle);
        NativeBattleActionPacketsState packets = ready.NativeActionPackets!;
        Assert.Equal((byte)0, packets.ActionCount);
        (packets, int header) = packets.Prepare(new(checked((byte)allocatedActor), 8, 12, -1, 152, 282, null));
        (packets, _) = packets.AppendContact(new(new(0, assets.NativeActions.Single(row => row.Command == 8 && row.ActionId == 152 && row.MagicId == 282).DamagePresentation, 0, 0, status.Saved, 7, status.Battle), new(255, 0, 0, 0, 0, 0, 0)));
        (packets, _) = packets.Publish(1);
        (NativeBattleDescriptorState descriptors, _) = ready.NativeGraphics!.Descriptors.Enqueue(104, 128, checked(0x1D280C4u + (uint)header * 20));
        BattleSnapshot source = ready with { NativeActionPackets = packets, NativeGraphics = ready.NativeGraphics with { Descriptors = descriptors } };
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([282])[281];
        Assert.Equal(NativeBattleEvilEye282Profile.Entry, binding.Entry);
        Assert.Equal(NativeBattleEvilEye282Profile.Initializer, binding.Initializer);
        Assert.Equal(NativeBattleEvilEye282Profile.TextureLength, binding.InitializerAsset!.Data.Length);
        Assert.Same(game.ReadNativeEvilEye282Resources(), binding.EvilEye282Resources);
        Assert.True(source.Definition.NativeEffectCatalog.Length > 281);
        source = source with { Definition = source.Definition with { NativeEffectCatalog = source.Definition.NativeEffectCatalog.SetItem(281, binding) } };
        Assert.Same(binding.EvilEye282Resources, source.Definition.NativeEffectCatalog[281].EvilEye282Resources);
        BattleCombatantDefinition definition = BattleSession.Restore(source).GetCombatantDefinition(source.NativeGraphics.ActorIds[allocatedActor]!);
        Assert.Equal(94, definition.ModelId);
        NativeBattleGraphicsResource resource = assets.ReadNativeGraphicsResource(definition);
        NativeBattleGraphicsModel model = source.NativeGraphics.Models[allocatedActor];
        Assert.NotNull(resource.PoseSource);
        Assert.Equal(resource.PoseSource.BoneCount, NativeBattleBodyBoneMatrices.Require(model, resource.PoseSource).Length);
        Assert.NotNull(model.GeometryWorldMatrix);
        Assert.NotNull(model.Rotation14);
        Assert.NotNull(model.ActiveResourceRow100);
        Assert.True(resource.PoseSource.BoneCount > 13);
        NativeBattlePackedMatrix32 selected = NativeBattlePackedMatrix32.FromMatrix(NativeBattleBodyBoneMatrices.Require(model, resource.PoseSource)[13], null);
        Assert.False(selected.Known[18]);
        Assert.False(selected.Known[19]);
        Assert.Equal((byte)allocatedActor, packets.Headers[header].ReadByte(0));
        Assert.Equal((byte)12, packets.Headers[header].ReadByte(2));
        Assert.Equal((byte)1, packets.Headers[header].ReadByte(16));
        File.WriteAllText(Path.Combine(output, "controlled-contact.json"), JsonSerializer.Serialize(new { Scenario = "Original EvilEye282 command8/action152/sequence12; originalformation436 prototype3 allocated and activated by current native producer; controlled preparation does not claim three ordinary AI deaths; declared seven-HP physical party0 contact with logical HP/status retained", allocatedActor, Header = packets.Headers[header], Contact = packets.ReadContact(0), OriginalPrototype = assets.LoadEncounter(436).Slots.Single(enemy => enemy.Slot == 3) }));
        return source;
    }
}
