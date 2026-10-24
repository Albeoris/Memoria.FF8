using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect54SourceAdmissionTests
{
    [Fact]
    public void Fresh_source_preserves_dormant_actor_and_has_current_caster_anchors_and_bounds()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT54_SOURCE_SNAPSHOT");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT54_ADMISSION_EVIDENCE");
        Assert.SkipUnless(directory is not null && path is not null && output is not null, "Select original resources, a fresh standalone and a new input-admission evidence directory.");
        Assert.False(Directory.Exists(output));
        using BattleAssets assets = new(directory!);
        BattleSnapshot source = NativeBattleEffectFamilyRunner.LoadSnapshot(path!);
        string unchanged = JsonSerializer.Serialize(source);
        BattleSession session = BattleSession.Restore(source);
        Assert.Equal(unchanged, JsonSerializer.Serialize(session.Capture()));
        Assert.True(session.NativeEntryReady);
        Assert.Equal(0, source.Frame);
        Assert.Equal(236, source.Definition.EncounterId);
        Assert.Equal(76, source.Definition.StageId);
        Assert.Null(source.NativeGraphics!.OriginalEffect);
        Assert.Equal(0, source.NativeEffectMemory!.Active);
        BattleCombatantDefinition caster = session.GetCombatantDefinition(source.NativeGraphics.ActorIds[3]!);
        Assert.Equal(56, caster.ModelId);
        Assert.Equal(1, caster.SceneSlot);
        Assert.True(caster.InitialActive);
        Assert.False(caster.InitialHidden);
        Assert.False(caster.InitialSuspended);
        Assert.False(caster.InitialUntargetable);
        BattleEncounter formation = assets.LoadEncounter(236);
        BattleEnemyPlacement dormant = Assert.Single(formation.Slots, actor => actor.ModelId == 83 && actor.Slot == 0);
        Assert.False(dormant.Enabled);
        Assert.True(dormant.Hidden);
        Assert.True(dormant.InitiallySuspended);
        Assert.True(dormant.Untargetable);
        Assert.DoesNotContain(source.Definition.Combatants, actor => !actor.IsPlayer && actor.ModelId == 83);
        NativeBattleGraphicsModel model = source.NativeGraphics.Models[3];
        Assert.Equal((byte)72, model.ModelId4);
        Assert.NotEqual(0, model.Flags0 & 2);
        Assert.NotNull(model.Transform);
        Assert.NotNull(model.GeometryWorldMatrix);
        Assert.NotNull(model.GeometryBounds52);
        NativeBattlePoseSource pose = assets.ReadNativeGraphicsResource(caster).PoseSource ?? throw new NotSupportedException("The selected caster requires its actual skeleton.");
        Assert.Equal(26, pose.BoneCount);
        ImmutableArray<NativeBattleActionMatrix> bones = NativeBattleBodyBoneMatrices.Require(model, pose);
        Assert.Equal(pose.BoneCount, bones.Length);
        NativeBattleBoneAnchorProducer first = NativeBattlePoseMatrices.ResolveBoneAnchor502170(model, pose, 240, 0);
        NativeBattleBoneAnchorProducer second = NativeBattlePoseMatrices.ResolveBoneAnchor502170(model, pose, 241, 0);
        Assert.True(first.Enabled);
        Assert.True(second.Enabled);
        NativeBattleVector firstPoint = NativeBattlePoseMatrices.ReadBone(model, pose, 240, 0);
        NativeBattleVector secondPoint = NativeBattlePoseMatrices.ReadBone(model, pose, 241, 0);
        NativeBattleDecodedPose decoded = NativeBattlePoseMatrices.ReadDecodedPose(model.SequenceState ?? throw new NotSupportedException("The selected caster requires its current decoded pose."), pose);
        NativeBattleEffectLocalBounds bounds = NativeBattlePoseMatrices.LocalBounds8DC870(pose, decoded);
        NativeBattleModulePoolTask retained = new(1, 0, null, 0, Enumerable.Repeat((byte)0xA5, 0x60 - 12).ToImmutableArray());
        NativeBattleModulePoolTask bounded = NativeBattleEffectModelHelpers.UpdateBounds8DC870(retained, pose, decoded);
        Assert.Equal(unchecked((ushort)bounds.Minimum.X), bounded.ReadWord(0x48));
        Assert.Equal(unchecked((ushort)bounds.Minimum.Y), bounded.ReadWord(0x4A));
        Assert.Equal(unchecked((ushort)bounds.Minimum.Z), bounded.ReadWord(0x4C));
        Assert.Equal(unchecked((ushort)bounds.Maximum.X), bounded.ReadWord(0x50));
        Assert.Equal(unchecked((ushort)bounds.Maximum.Y), bounded.ReadWord(0x52));
        Assert.Equal(unchecked((ushort)bounds.Maximum.Z), bounded.ReadWord(0x54));
        Assert.Equal((ushort)0xA5A5, bounded.ReadWord(0x4E));
        Assert.Equal((ushort)0xA5A5, bounded.ReadWord(0x56));
        Assert.Equal(unchanged, JsonSerializer.Serialize(source));
        Assert.Equal(unchanged, JsonSerializer.Serialize(session.Capture()));
        Directory.CreateDirectory(output!);
        File.WriteAllText(Path.Combine(output!, "current-input-admission.json"), JsonSerializer.Serialize(new { Status = "passed", Scope = "Fresh source current model, anchors and bounds only; action header preparation and later poses are separate", source.Frame, source.NativeGraphics.Callback, source.Definition.EncounterId, source.Definition.StageId, PhysicalCaster = 3, caster.ModelId, caster.SceneSlot, Bones = bones.Length, CurrentWorld = model.GeometryWorldMatrix, CurrentBounds = model.GeometryBounds52, LocalBounds = bounds, F0 = new { first.Distance, first.Matrix, Point = firstPoint }, F1 = new { second.Distance, second.Matrix, Point = secondPoint }, Dormant = new { dormant.ModelId, dormant.Slot, dormant.Enabled, dormant.Hidden, dormant.InitiallySuspended, dormant.Untargetable }, SnapshotUnchanged = true }));
    }
}
