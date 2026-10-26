using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleBodyBoneMatricesTests
{
    private static readonly NativeBattleActionMatrix Basis = new([0, 0, 4096, 0, 4096, 0, -4096, 0, 0], 1000, -2000, 3000, 4096);

    [Fact]
    public void Actual_local_producer_then_composition_then_same_pose_publication_restores_local_values()
    {
        NativeBattlePoseSource source = Source(2);
        NativeBattleGraphicsModel initial = Model(2);
        NativeBattleGraphicsModel local = NativeBattleBodyBoneMatrices.Publish508C90(initial, source);
        ImmutableArray<NativeBattleActionMatrix> expected = NativeBattlePoseMatrices.StageBoneMatrices(source, initial.SequenceState!.DecodedPose!);
        EqualMatrices(expected, NativeBattleBodyBoneMatrices.Require(local, source));
        NativeBattleGraphicsModel composed = NativeBattleBodyBoneMatrices.Compose5095B0(local, Basis);
        EqualMatrices(NativeBattlePoseMatrices.ComposeStageCamera(expected, Basis, 0), NativeBattleBodyBoneMatrices.Require(composed, source));
        Assert.NotEqual(JsonSerializer.Serialize(local.CurrentBodyBoneMatrices!.Matrices), JsonSerializer.Serialize(composed.CurrentBodyBoneMatrices!.Matrices));
        NativeBattleGraphicsModel refreshed = NativeBattleBodyBoneMatrices.Publish508C90(composed, source);
        EqualMatrices(expected, NativeBattleBodyBoneMatrices.Require(refreshed, source));
        Assert.NotSame(local.CurrentBodyBoneMatrices, refreshed.CurrentBodyBoneMatrices);
        Assert.Same(initial.SequenceState, refreshed.SequenceState);
        Assert.Same(initial.GeometryWorldMatrix, refreshed.GeometryWorldMatrix);
        Assert.Equal(initial.Transform, refreshed.Transform);
    }

    [Fact]
    public void Repeated_composition_uses_the_current_composed_owner_instead_of_decoded_pose()
    {
        NativeBattlePoseSource source = Source(2);
        NativeBattleGraphicsModel local = NativeBattleBodyBoneMatrices.Publish508C90(Model(2), source);
        NativeBattleGraphicsModel first = NativeBattleBodyBoneMatrices.Compose5095B0(local, Basis);
        NativeBattleGraphicsModel changedPose = first with { SequenceState = new() };
        NativeBattleActionMatrix secondBasis = Basis with { X = -700, Y = 900, Z = 1100 };
        NativeBattleGraphicsModel second = NativeBattleBodyBoneMatrices.Compose5095B0(changedPose, secondBasis);
        EqualMatrices(NativeBattlePoseMatrices.ComposeStageCamera(first.CurrentBodyBoneMatrices!.Matrices, secondBasis, 0), NativeBattleBodyBoneMatrices.Require(second, source));
        Assert.Same(changedPose.SequenceState, second.SequenceState);
    }

    [Fact]
    public void Missing_imported_publication_stays_null_even_when_decoded_pose_is_known()
    {
        NativeBattleGraphicsModel model = Model(2);
        string json = JsonSerializer.Serialize(model);
        Assert.DoesNotContain("CurrentBodyBoneMatrices", json);
        NativeBattleGraphicsModel imported = JsonSerializer.Deserialize<NativeBattleGraphicsModel>(json)!;
        Assert.Null(imported.CurrentBodyBoneMatrices);
        Assert.Throws<NotSupportedException>(() => NativeBattleBodyBoneMatrices.Require(imported, Source(2)));
        Assert.Throws<NotSupportedException>(() => NativeBattleBodyBoneMatrices.Compose5095B0(imported, Basis));
        Assert.Null(imported.CurrentBodyBoneMatrices);
    }

    [Fact]
    public void Rig_identity_and_count_are_both_required_and_animation_changes_do_not_invent_a_new_rig()
    {
        NativeBattlePoseSource source = Source(2);
        NativeBattleGraphicsModel local = NativeBattleBodyBoneMatrices.Publish508C90(Model(2), source);
        byte[] differentSkeleton = source.Skeleton.ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(differentSkeleton.AsSpan(0x12), 1234);
        Assert.Throws<NotSupportedException>(() => NativeBattleBodyBoneMatrices.Require(local, source with { Skeleton = differentSkeleton.ToImmutableArray() }));
        Assert.Throws<NotSupportedException>(() => NativeBattleBodyBoneMatrices.Require(local, Source(1)));
        EqualMatrices(local.CurrentBodyBoneMatrices!.Matrices, NativeBattleBodyBoneMatrices.Require(local, source with { Animations = [7, 8, 9] }));
    }

    [Fact]
    public void Current_matrix_reads_never_advance_or_redecode_the_pose_and_keep_arbitrary_composed_values()
    {
        NativeBattlePoseSource source = Source(1);
        NativeBattleGraphicsModel published = NativeBattleBodyBoneMatrices.Publish508C90(Model(1), source);
        NativeBattleActionMatrix retained = Basis with { X = int.MaxValue, Y = int.MinValue };
        NativeBattleGraphicsModel current = published with { SequenceState = null, CurrentBodyBoneMatrices = published.CurrentBodyBoneMatrices! with { Matrices = [retained] } };
        Assert.Same(retained, Assert.Single(NativeBattleBodyBoneMatrices.Require(current, source)));
        Assert.Null(current.SequenceState);
    }

    [Fact]
    public void Explicit_publication_requires_actual_pose_and_rejects_incomplete_current_owner()
    {
        NativeBattleGraphicsModel missing = Model(2) with { SequenceState = null };
        Assert.Throws<NotSupportedException>(() => NativeBattleBodyBoneMatrices.Publish508C90(missing, Source(2)));
        NativeBattleGraphicsModel valid = NativeBattleBodyBoneMatrices.Publish508C90(Model(2), Source(2));
        NativeBattleGraphicsModel empty = valid with { CurrentBodyBoneMatrices = valid.CurrentBodyBoneMatrices! with { Matrices = [] } };
        Assert.Throws<InvalidDataException>(() => NativeBattleBodyBoneMatrices.Require(empty, Source(2)));
        NativeBattleGraphicsModel invalidIdentity = valid with { CurrentBodyBoneMatrices = valid.CurrentBodyBoneMatrices! with { SkeletonSha256 = "unknown" } };
        Assert.Throws<InvalidDataException>(() => NativeBattleBodyBoneMatrices.Compose5095B0(invalidIdentity, Basis));
    }

    [Fact]
    public void Independent_JSON_preserves_composed_publication_and_next_actual_local_producer()
    {
        NativeBattlePoseSource source = Source(2);
        NativeBattleGraphicsModel local = NativeBattleBodyBoneMatrices.Publish508C90(Model(2), source);
        NativeBattleGraphicsModel current = NativeBattleBodyBoneMatrices.Compose5095B0(local, Basis);
        NativeBattleGraphicsModel restored = JsonSerializer.Deserialize<NativeBattleGraphicsModel>(JsonSerializer.Serialize(current))!;
        EqualMatrices(NativeBattleBodyBoneMatrices.Require(current, source), NativeBattleBodyBoneMatrices.Require(restored, source));
        Assert.Equal(JsonSerializer.Serialize(NativeBattleBodyBoneMatrices.Publish508C90(current, source)), JsonSerializer.Serialize(NativeBattleBodyBoneMatrices.Publish508C90(restored, source)));
    }

    [Fact]
    public void Live_matrix_stores_and_unknown_stack_padding_do_not_change_the_rig_identity()
    {
        NativeBattlePoseSource source = Source(2);
        NativeBattleGraphicsModel model = NativeBattleBodyBoneMatrices.Publish508C90(Model(2), source);
        byte[] bytes = source.Skeleton.ToArray();
        bool[] known = Enumerable.Repeat(true, bytes.Length).ToArray();
        for (int bone = 0; bone < source.BoneCount; bone++)
        {
            bytes.AsSpan(32 + bone * 48, 32).Fill(0xA5);
            known[50 + bone * 48] = false;
            known[51 + bone * 48] = false;
        }
        NativeBattlePoseSource live = source with { Skeleton = bytes.ToImmutableArray(), SkeletonKnownBytes = known.ToImmutableArray() };
        live.Validate();
        EqualMatrices(model.CurrentBodyBoneMatrices!.Matrices, NativeBattleBodyBoneMatrices.Require(model, live));
        NativeBattlePoseSource restored = JsonSerializer.Deserialize<NativeBattlePoseSource>(JsonSerializer.Serialize(live))!;
        Assert.False(restored.SkeletonKnownBytes[50]);
        Assert.Equal(NativeBattleBodyBoneMatrices.Identity(source), NativeBattleBodyBoneMatrices.Identity(restored));
        EqualMatrices(NativeBattleBodyBoneMatrices.Require(model, live), NativeBattleBodyBoneMatrices.Require(model, restored));
    }

    [Fact]
    public void Unknown_consumed_skeleton_fields_remain_unsupported()
    {
        NativeBattlePoseSource source = Source(1);
        bool[] known = Enumerable.Repeat(true, source.Skeleton.Length).ToArray();
        known[18] = false;
        Assert.Throws<NotSupportedException>(() => (source with { SkeletonKnownBytes = known.ToImmutableArray() }).Validate());
        known[18] = true;
        known[52] = false;
        Assert.Throws<NotSupportedException>(() => (source with { SkeletonKnownBytes = known.ToImmutableArray() }).Validate());
    }

    [Fact]
    public void Existing_complete_skeleton_identity_remains_readable_without_rewriting_the_publication()
    {
        NativeBattlePoseSource source = Source(1);
        NativeBattleGraphicsModel model = NativeBattleBodyBoneMatrices.Publish508C90(Model(1), source);
        string originalIdentity = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(source.Skeleton.AsSpan()));
        model = model with { CurrentBodyBoneMatrices = model.CurrentBodyBoneMatrices! with { SkeletonSha256 = originalIdentity } };
        Assert.Equal(originalIdentity, model.CurrentBodyBoneMatrices.SkeletonSha256);
        Assert.Single(NativeBattleBodyBoneMatrices.Require(model, source));
    }

    private static NativeBattlePoseSource Source(int count)
    {
        byte[] skeleton = new byte[0x10 + count * 0x30];
        skeleton[0] = checked((byte)count);
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(2), 4096);
        for (int index = 0; index < count; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(0x10 + index * 0x30), checked((short)(index - 1)));
            BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(0x12 + index * 0x30), checked((short)(100 + index * 50)));
        }
        return new(skeleton.ToImmutableArray(), [1]);
    }

    private static NativeBattleGraphicsModel Model(int count)
    {
        ImmutableArray<NativeBattleBonePose> bones = Enumerable.Range(0, count).Select(index => new NativeBattleBonePose(new(0, (short)(index * 128), 0), new(1024, 1024, 1024))).ToImmutableArray();
        return new(0, 3)
        {
            Transform = new(new(1, 2, 3), new(4, 5, 6), 7, 8, 4096),
            GeometryWorldMatrix = Basis,
            SequenceState = new() { DecodedPose = new(new(10, 20, 30), bones, false) }
        };
    }

    private static void EqualMatrices(ImmutableArray<NativeBattleActionMatrix> expected, ImmutableArray<NativeBattleActionMatrix> actual) => Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));
}
