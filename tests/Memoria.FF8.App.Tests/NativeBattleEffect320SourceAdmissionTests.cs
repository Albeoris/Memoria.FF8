using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect320SourceAdmissionTests
{
    [Fact]
    public void Fresh_original132_source_preserves_current_bones_and_cold_effect_owners()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT320_SOURCE_SNAPSHOT");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT320_ADMISSION_EVIDENCE");
        Assert.SkipUnless(directory is not null && path is not null && output is not null, "Select original resources, a fresh standalone and a new input-admission evidence directory.");
        Assert.False(Directory.Exists(output));
        using GameAssets game = new(directory!);
        using BattleAssets assets = new(directory!);
        BattleSnapshot source = NativeBattleEffectFamilyRunner.LoadSnapshot(path!);
        string unchanged = JsonSerializer.Serialize(source);
        BattleSession session = BattleSession.Restore(source);
        Assert.Equal(unchanged, JsonSerializer.Serialize(session.Capture()));
        Assert.True(session.NativeEntryReady);
        Assert.Equal(483, source.Definition.EncounterId);
        Assert.Equal(139, source.Definition.StageId);
        Assert.Null(source.NativeGraphics!.OriginalEffect);
        NativeBattleEffectMemory memory = source.NativeEffectMemory!;
        Assert.Equal(0, memory.Active);
        BattleCombatantSnapshot enemy = Assert.Single(source.Combatants, combatant => !session.GetCombatantDefinition(combatant.Id).IsPlayer && combatant.NativeParticipant is { Active: true });
        Assert.Equal((byte)3, enemy.NativeParticipant!.ActorIndex);
        Assert.False(enemy.NativeParticipant.Hidden);
        Assert.False(enemy.NativeParticipant.Suspended);
        Assert.False(enemy.NativeParticipant.Untargetable);
        BattleCombatantDefinition caster = session.GetCombatantDefinition(enemy.Id);
        Assert.Equal(132, caster.ModelId);
        Assert.Equal(0, caster.SceneSlot);
        NativeBattleGraphicsModel model = source.NativeGraphics.Models[3];
        Assert.Equal((byte)148, model.ModelId4);
        Assert.NotNull(model.GeometryWorldMatrix);
        Assert.NotNull(model.Transform);
        Assert.NotNull(model.SequenceState);
        NativeBattleGraphicsResource actorResource = assets.ReadNativeGraphicsResource(caster);
        NativeBattlePoseSource pose = actorResource.PoseSource ?? throw new NotSupportedException("The selected caster requires its actual current skeleton.");
        Assert.Equal(44, pose.BoneCount);
        ImmutableArray<NativeBattleActionMatrix> bones = NativeBattleBodyBoneMatrices.Require(model, pose);
        Assert.Equal(44, bones.Length);
        int start = actorResource.SequenceStart(12);
        int end = actorResource.SequenceEnd(12);
        ImmutableArray<byte> sequence = actorResource.SequenceSection.Slice(start, end - start);
        Assert.Equal(38, sequence.Length);
        Assert.Equal(new byte[] { 0xBB, 7, 8, 0xA0, 8 }, sequence.AsSpan().Slice(0, 5).ToArray());
        Assert.Equal((byte)0xA2, sequence[^1]);
        Assert.Contains(assets.NativeActions, action => action.Command == 8 && action.ActionId == 305 && action.MagicId == 320);
        NativeBattleEffect320Resources resources = game.ReadNativeEffect320Resources();
        resources.Validate();
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([320])[319];
        Assert.Same(resources, binding.Effect320Resources);
        Assert.Equal(NativeBattleEffect320Profile.Entry, binding.Entry);
        NativeBattleEffect320State state = memory.Effect320 ?? throw new NotSupportedException("The source requires its explicitly retained cold effect320 owner.");
        state.Validate();
        Assert.False(state.MutableAnchorSourceBound);
        Assert.Equal(new byte[20], state.ReadBank(0).Read(0, 20));
        foreach (NativeBattleCureFamilyPool pool in NativeBattleEffect320Profile.CommonPools)
        {
            Assert.Equal(new byte[8], state.ReadCommonQueue(pool.Descriptor).Read(0, 8));
        }
        foreach (uint address in new uint[] { 0xFF12A0, 0xFF12A8 })
        {
            short bone = BinaryPrimitives.ReadInt16LittleEndian(resources.ReadStatic(address + 6, 2));
            Assert.Equal(27, bone);
            Assert.InRange(bone, 0, bones.Length - 1);
        }
        Assert.NotNull(source.NativeCrtRandom);
        Assert.Equal(unchanged, JsonSerializer.Serialize(source));
        Assert.Equal(unchanged, JsonSerializer.Serialize(session.Capture()));
        Directory.CreateDirectory(output!);
        File.WriteAllText(Path.Combine(output!, "current-input-admission.json"), JsonSerializer.Serialize(new { Status = "passed", Scope = "Fresh original-resource component input only; later geometry, contact, camera and completion require actual lifecycle", source.Frame, source.NativeGraphics.Callback, source.Definition.EncounterId, source.Definition.StageId, PhysicalCaster = 3, caster.ModelId, caster.SceneSlot, caster.Level, ActorBones = bones.Length, CurrentWorld = model.GeometryWorldMatrix, model.SequenceState, OriginalKernelAction = 305, OriginalKernelEffect = 320, Sequence12 = sequence, BoneSelector = 27, ColdAnchorSourceBound = state.MutableAnchorSourceBound, SnapshotUnchanged = true }));
    }
}
