using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleMiniPulseCannon286PresentationTests
{
    [Fact]
    public void Original_MiniPulseCannon286_native_allocated_caster_preserves_current_bones_and_cold_cycle()
    {
        NativeBattleActorDirectorPresentationTests.RunSelected([286], "MEMORIA_FF8_MINIPULSECANNON286", SelectOriginalFixture, PrepareDeclaredSource);
    }

    private static NativeEffectFixtureRequest SelectOriginalFixture(BattleAssets assets, ushort effect)
    {
        Assert.Equal((ushort)286, effect);
        BattleNativeAction action = Assert.Single(assets.NativeActions, row => row.Command == 8 && row.ActionId == 293 && row.MagicId == effect && row.KernelSection == 3 && row.KernelIndex == 293);
        Assert.Null(action.TargetFlags);
        BattleEncounter formation = assets.LoadEncounter(431);
        Assert.Equal(126, formation.StageId);
        BattleEnemyPlacement prototype = Assert.Single(formation.Slots, enemy => enemy.Slot == 0);
        Assert.Equal(110, prototype.ModelId);
        Assert.Equal(156, prototype.Level);
        Assert.Equal(new NativeBattleVector(-2000, 0, -4000), prototype.NativePosition);
        Assert.True(prototype.Hidden);
        Assert.True(prototype.InitiallySuspended);
        Assert.True(prototype.Untargetable);
        Assert.False(prototype.Enabled);
        Assert.Equal((byte)12, NativeBattleEffectFixtureExporter.SelectEnemySequence(assets.ReadEnemyAi(110, prototype.Level), action));
        return new("mini-pulse286", effect, 8, 293, 0, 1200, 240) { EncounterId = 431, CasterActorIndex = 4, PreserveOriginalFormationEnemies = true, ControlledContactValue = 7 };
    }

    private static BattleSnapshot PrepareDeclaredSource(GameAssets game, BattleAssets assets, NativeEffectFixtureRequest request)
    {
        string output = Environment.GetEnvironmentVariable("MEMORIA_FF8_MINIPULSECANNON286_EVIDENCE") ?? throw new InvalidDataException("Select an evidence directory for the original Mini Pulse Cannon component.");
        NativeEffectFixtureRequest baselineRequest = new("mini-pulse286-baseline", 101, 8, 290, 0, 1200, 240) { EncounterId = 431, CasterActorIndex = 3, PreserveOriginalFormationEnemies = true, AdmissionProbeMaximumCallbacks = 0 };
        _ = NativeBattleEffectFixtureExporter.Export(baselineRequest, Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? throw new InvalidDataException("Select installed original resources."), Path.Combine(output, "fixture"));
        BattleSnapshot original = NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(output, "fixture", "before-entry.battle.json"));
        (BattleSnapshot ready, int allocatedActor) = NativeBattleMiniPulseCannon286FixturePreparation.Prepare(game, assets, original, output);
        Assert.InRange(allocatedActor, 3, 6);
        BattleCombatantSnapshot target = ready.Combatants.Single(actor => actor.NativeParticipant!.ActorIndex == 0);
        NativeBattleStatusSnapshot status = target.NativeStatus ?? throw new InvalidDataException("The declared Mini Pulse Cannon contact requires its current target status owner.");
        target = target with { NativeStatus = status with { Presentation = new(status.Saved, status.Battle) } };
        ready = ready with { Combatants = ready.Combatants.Select(actor => actor.Id == target.Id ? target : actor).ToArray(), NativePresentationStatusMemory = (ready.NativePresentationStatusMemory ?? throw new InvalidDataException("Mini Pulse Cannon requires its current physical presentation-status bank.")).Observe(0, target.NativeStatus!.Presentation) };
        Assert.Equal(status.Saved, target.NativeStatus!.Presentation.Saved);
        Assert.Equal(status.Battle, target.NativeStatus!.Presentation.Battle);
        NativeBattleActionPacketsState packets = ready.NativeActionPackets!;
        Assert.Equal((byte)0, packets.ActionCount);
        (packets, int header) = packets.Prepare(new(checked((byte)allocatedActor), 8, 12, -1, 293, 286, null));
        (packets, _) = packets.AppendContact(new(new(0, assets.NativeActions.Single(row => row.Command == 8 && row.ActionId == 293 && row.MagicId == 286).DamagePresentation, 0, 0, status.Saved, 7, status.Battle), new(255, 0, 0, 0, 0, 0, 0)));
        (packets, _) = packets.Publish(1);
        (NativeBattleDescriptorState descriptors, _) = ready.NativeGraphics!.Descriptors.Enqueue(104, 128, checked(0x1D280C4u + (uint)header * 20));
        BattleSnapshot source = ready with { NativeActionPackets = packets, NativeGraphics = ready.NativeGraphics with { Descriptors = descriptors } };
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([286])[285];
        Assert.Equal(NativeBattleMiniPulseCannon286Profile.Entry, binding.Entry);
        Assert.Equal(NativeBattleMiniPulseCannon286Profile.Initializer, binding.Initializer);
        Assert.Equal(NativeBattleMiniPulseCannon286Profile.TextureLength, binding.InitializerAsset!.Data.Length);
        Assert.Same(game.ReadNativeMiniPulseCannon286Resources(), binding.MiniPulseCannon286Resources);
        Assert.True(source.Definition.NativeEffectCatalog.Length > 285);
        source = source with { Definition = source.Definition with { NativeEffectCatalog = source.Definition.NativeEffectCatalog.SetItem(285, binding) } };
        Assert.Same(binding.MiniPulseCannon286Resources, source.Definition.NativeEffectCatalog[285].MiniPulseCannon286Resources);
        BattleCombatantDefinition definition = BattleSession.Restore(source).GetCombatantDefinition(source.NativeGraphics.ActorIds[allocatedActor]!);
        Assert.Equal(110, definition.ModelId);
        NativeBattleGraphicsResource resource = assets.ReadNativeGraphicsResource(definition);
        NativeBattleGraphicsModel model = source.NativeGraphics.Models[allocatedActor];
        Assert.NotNull(resource.PoseSource);
        Assert.Equal(resource.PoseSource.BoneCount, NativeBattleBodyBoneMatrices.Require(model, resource.PoseSource).Length);
        Assert.NotNull(model.GeometryWorldMatrix);
        Assert.NotNull(model.Rotation14);
        Assert.NotNull(model.ActiveResourceRow100);
        Assert.True(resource.PoseSource.BoneCount > 2);
        NativeBattlePackedMatrix32 selected = NativeBattlePackedMatrix32.FromMatrix(NativeBattleBodyBoneMatrices.Require(model, resource.PoseSource)[2], null);
        Assert.False(selected.Known[18]);
        Assert.False(selected.Known[19]);
        Assert.Equal((byte)allocatedActor, packets.Headers[header].ReadByte(0));
        Assert.Equal((byte)12, packets.Headers[header].ReadByte(2));
        Assert.Equal((byte)1, packets.Headers[header].ReadByte(16));
        File.WriteAllText(Path.Combine(output, "controlled-contact.json"), JsonSerializer.Serialize(new { Scenario = "Original MiniPulseCannon286 command8/action293/sequence12; originalformation431 prototype0 allocated by real52(false), then activated by53; controlled preparation does not claim ordinary action290 execution; declared seven-HP physical party0 contact with logical HP/status retained", allocatedActor, Header = packets.Headers[header], Contact = packets.ReadContact(0), OriginalPrototype = assets.LoadEncounter(431).Slots.Single(enemy => enemy.Slot == 0) }));
        return source;
    }
}
