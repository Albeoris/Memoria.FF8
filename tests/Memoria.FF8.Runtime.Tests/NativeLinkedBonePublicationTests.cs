using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeLinkedBonePublicationTests
{
    private static readonly NativeBattleActionMatrix Composed = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 1000, 2000, 3000, 4096);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Linked_advance_publishes_before_body_completion_or_sequence_services(bool bodyComplete)
    {
        NativeBattlePoseSource source = Source();
        NativeBattleGraphicsResource resource = Resource(source, [0xAA, 0xA1]);
        NativeBattleGraphicsModel model = Model(source, bodyComplete ? (byte)1 : (byte)0, 0);
        model = model with { CurrentLinkedBoneMatrices = null };
        int calls = 0;
        NativeBattleSequenceStep step = NativeBattleSequenceMachine.Advance(model, resource, new(null, null)
        {
            ApplyActionContacts = current =>
            {
                calls++;
                EqualLocal(current, source);
                Assert.Equal((byte)1, current.SequenceState!.LinkedPose!.Frame);
                return current;
            }
        });
        Assert.Equal(1, calls);
        EqualLocal(step.Model, source);
    }

    [Fact]
    public void Completed_body_refresh508F60_restores_unchanged_linked_pose_after_composition()
    {
        NativeBattlePoseSource source = Source();
        NativeBattleGraphicsModel model = Model(source, 1, 1);
        NativeBattleBodyBoneMatrices owner = model.CurrentLinkedBoneMatrices!;
        model = model with { CurrentLinkedBoneMatrices = owner with { Matrices = NativeBattlePoseMatrices.ComposeStageCamera(owner.Matrices, Composed, 0) } };
        NativeBattleSequenceStep step = NativeBattleSequenceMachine.Advance(model, Resource(source, [0xA1]), new(null, null));
        EqualLocal(step.Model, source);
        Assert.Equal(model.SequenceState!.LinkedPose, step.Model.SequenceState!.LinkedPose);
    }

    [Fact]
    public void Explicit_frozen_refresh_publishes_both_local_bodies_without_advancing_pose()
    {
        NativeBattlePoseSource source = Source();
        NativeBattleGraphicsModel model = Model(source, 1, 1);
        NativeBattleSequenceState sequence = model.SequenceState!;
        model = NativeBattleBodyBoneMatrices.Compose5095B0(model, Composed);
        model = model with { CurrentLinkedBoneMatrices = model.CurrentLinkedBoneMatrices! with { Matrices = [Composed] } };
        NativeBattleGraphicsModel refreshed = NativeBattleBodyBoneMatrices.Publish508F60(model, source, source);
        EqualLocal(refreshed, source);
        Assert.Equal(-48, NativeBattleBodyBoneMatrices.Require(refreshed, source)[0].Y);
        Assert.Same(sequence, refreshed.SequenceState);
    }

    [Fact]
    public void Initial_sequence_early_exit_preserves_current_linked_owner_without_inventing_refresh()
    {
        NativeBattlePoseSource source = Source();
        NativeBattleGraphicsModel model = Model(source, 1, 1) with { ResourceFlags44 = 3 };
        model = model with { CurrentLinkedBoneMatrices = model.CurrentLinkedBoneMatrices! with { Matrices = [Composed] } };
        NativeBattleSequenceStep step = NativeBattleSequenceMachine.Advance(model, Resource(source, [0xA1]), new(null, null));
        Assert.Same(model.CurrentLinkedBoneMatrices, step.Model.CurrentLinkedBoneMatrices);
    }

    [Fact]
    public void Actual_rig_load_publishes_new_linked_identity_and_unlinked_load_clears_old_owner()
    {
        NativeBattlePoseSource source = Source();
        NativeBattlePoseSource other = Source(2048);
        NativeBattleGraphicsModel loaded = NativeBattleBodyBoneMatrices.PublishLoadedSkeletons(Model(source, 1, 1), source, other);
        Assert.Equal(JsonSerializer.Serialize(NativeBattleBodyBoneMatrices.ReadLoadedSkeleton(other)), JsonSerializer.Serialize(loaded.CurrentLinkedBoneMatrices));
        Assert.Throws<NotSupportedException>(() => NativeBattleBodyBoneMatrices.Require(loaded with { CurrentBodyBoneMatrices = loaded.CurrentLinkedBoneMatrices }, source));
        Assert.Null(NativeBattleBodyBoneMatrices.PublishLoadedSkeletons(loaded, source, null).CurrentLinkedBoneMatrices);
    }

    [Fact]
    public void Independent_JSON_preserves_linked_publication_and_next_real_pose_producer()
    {
        NativeBattlePoseSource source = Source();
        NativeBattleGraphicsModel model = Model(source, 0, 0);
        NativeBattleGraphicsModel restored = JsonSerializer.Deserialize<NativeBattleGraphicsModel>(JsonSerializer.Serialize(model))!;
        Assert.Equal(JsonSerializer.Serialize(model.CurrentLinkedBoneMatrices), JsonSerializer.Serialize(restored.CurrentLinkedBoneMatrices));
        NativeBattleGraphicsResource resource = Resource(source, [0xA1]);
        Assert.Equal(JsonSerializer.Serialize(NativeBattleSequenceMachine.Advance(model, resource, new(null, null)).Model), JsonSerializer.Serialize(NativeBattleSequenceMachine.Advance(restored, resource, new(null, null)).Model));
    }

    [Fact]
    public void Missing_imported_linked_publication_is_unknown_until_an_actual_producer_runs()
    {
        NativeBattlePoseSource source = Source();
        NativeBattleGraphicsModel model = Model(source, 1, 1) with { CurrentLinkedBoneMatrices = null };
        string json = JsonSerializer.Serialize(model);
        Assert.DoesNotContain("CurrentLinkedBoneMatrices", json);
        NativeBattleGraphicsModel restored = JsonSerializer.Deserialize<NativeBattleGraphicsModel>(json)!;
        Assert.Null(restored.CurrentLinkedBoneMatrices);
        Assert.Throws<NotSupportedException>(() => NativeBattleBodyBoneMatrices.Require(restored with { CurrentBodyBoneMatrices = restored.CurrentLinkedBoneMatrices }, source));
        EqualLocal(NativeBattleBodyBoneMatrices.Publish508F60(restored, source, source), source);
    }

    private static void EqualLocal(NativeBattleGraphicsModel model, NativeBattlePoseSource source)
    {
        Assert.Equal(JsonSerializer.Serialize(NativeBattlePoseMatrices.StageBoneMatrices(source, NativeBattlePoseMatrices.ReadLinkedDecodedPose(model.SequenceState!, source))), JsonSerializer.Serialize(NativeBattleBodyBoneMatrices.Require(model with { CurrentBodyBoneMatrices = model.CurrentLinkedBoneMatrices }, source)));
    }

    private static NativeBattleGraphicsResource Resource(NativeBattlePoseSource source, byte[] program)
    {
        return new(new byte[] { 1, 0, 4, 0 }.Concat(program).ToImmutableArray()) { AnimationFrames = [1], PoseSource = source, LinkedAnimationFrames = [1], LinkedPoseSource = source };
    }

    private static NativeBattleGraphicsModel Model(NativeBattlePoseSource source, byte bodyFrame, byte linkedFrame)
    {
        NativeBattleGraphicsModel model = new(0, 3)
        {
            Flags2 = 0,
            Flags8 = 0,
            Sequence = 1,
            ResourceFlags44 = 0,
            SequenceState = new() { Pose = 0, PoseFrame114 = bodyFrame, PoseLimit115 = 1, DecodedPose = source.Sample(0, 1, 0), LinkedPose = new(0, linkedFrame, 1, 0), LinkedDecodedPose = source.Sample(0, 1, 0) }
        };
        return NativeBattleBodyBoneMatrices.Publish508F60(model, source, source);
    }

    private static NativeBattlePoseSource Source(short scale = 4096)
    {
        byte[] skeleton = new byte[64];
        skeleton[0] = 1;
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(2), scale);
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(16), -1);
        return new(skeleton.ToImmutableArray(), [1, 0, 0, 0, 8, 0, 0, 0, 1, 0x80, 2, 0]);
    }
}
