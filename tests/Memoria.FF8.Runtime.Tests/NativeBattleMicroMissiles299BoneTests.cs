using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleMicroMissiles299BoneTests
{
    [Fact]
    public void Valid_bone_uses_current_LONG_translation_and_retains_only_native_temporary_stores()
    {
        Fixture fixture = new();
        NativeBattleMicroMissiles299BoneStep result = NativeBattleMicroMissiles299Bone.Query67E550(fixture.Frame, fixture.Model, fixture.Source, 1);
        Assert.Equal(0, result.Result);
        Assert.Equal(new NativeBattleVector(unchecked((short)140000), 0, 0), result.Point);
        Assert.Equal(new[] { 100000, -30000, 12345 }, Enumerable.Range(0, 3).Select(axis => BinaryPrimitives.ReadInt32LittleEndian(result.Frame.Staging!.Read(NativeBattleTemporaryMemory.Address + (uint)(axis * 4), 4))));
        Assert.Equal(Enumerable.Repeat((byte)0x7C, 8), result.Frame.Staging!.Read(NativeBattleTemporaryMemory.Address + 12, 8));
        Assert.Equal(NativeBattleTemporaryMemory.Address, BinaryPrimitives.ReadUInt32LittleEndian(result.Frame.Staging.Read(NativeBattleTemporaryMemory.Cursor, 4)));
        Assert.Equal(NativeBattleTestSnapshots.Json(fixture.Frame.Memory), NativeBattleTestSnapshots.Json(result.Frame.Memory));
    }

    [Fact]
    public void Negative_parent_releases_temporary_without_output_or_pose_matrix_reads()
    {
        Fixture fixture = new();
        NativeBattleGraphicsModel absentMatrices = fixture.Model with { GeometryWorldMatrix = null, CurrentBodyBoneMatrices = null };
        NativeBattleMicroMissiles299BoneStep result = NativeBattleMicroMissiles299Bone.Query67E550(fixture.Frame, absentMatrices, fixture.Source, 0);
        Assert.Equal(1, result.Result);
        Assert.Null(result.Point);
        Assert.Equal(fixture.Frame.Staging!.Read(NativeBattleTemporaryMemory.Address, 20), result.Frame.Staging!.Read(NativeBattleTemporaryMemory.Address, 20));
        Assert.Equal(fixture.Frame.Staging.Read(NativeBattleTemporaryMemory.Cursor, 4), result.Frame.Staging.Read(NativeBattleTemporaryMemory.Cursor, 4));
    }

    [Fact]
    public void Header_alias_is_raw_BYTE_and_does_not_apply_502170_high_bit_semantics()
    {
        Fixture fixture = new();
        Assert.Equal(0, NativeBattleMicroMissiles299Bone.Query67E550(fixture.Frame, fixture.Model, fixture.Source, 240).Result);
        NativeBattlePoseSource invalid = fixture.Source with { Skeleton = fixture.Source.Skeleton.SetItem(5, 0x81) };
        Assert.Throws<InvalidDataException>(() => NativeBattleMicroMissiles299Bone.Query67E550(fixture.Frame, fixture.Model, invalid, 241));
    }

    [Fact]
    public void Missing_retained_body_publication_fails_without_reconstructing_pose()
    {
        Fixture fixture = new();
        Assert.Throws<NotSupportedException>(() => NativeBattleMicroMissiles299Bone.Query67E550(fixture.Frame, fixture.Model with { CurrentBodyBoneMatrices = null }, fixture.Source, 1));
    }

    [Fact]
    public void Cold_restored_owners_make_the_same_next_query()
    {
        Fixture fixture = new();
        NativeBattleMicroMissiles299Frame restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleMicroMissiles299Frame>(NativeBattleTestSnapshots.Json(fixture.Frame))!;
        Assert.Equal(NativeBattleTestSnapshots.Json(NativeBattleMicroMissiles299Bone.Query67E550(fixture.Frame, fixture.Model, fixture.Source, 1)), NativeBattleTestSnapshots.Json(NativeBattleMicroMissiles299Bone.Query67E550(restored, fixture.Model, fixture.Source, 1)));
    }

    private sealed class Fixture
    {
        public NativeBattlePoseSource Source { get; }
        public NativeBattleGraphicsModel Model { get; }
        public NativeBattleMicroMissiles299Frame Frame { get; }
        public Fixture()
        {
            byte[] skeleton = new byte[112];
            skeleton[0] = 2;
            skeleton[4] = 1;
            BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(16), -1);
            Source = new(skeleton.ToImmutableArray(), [1]);
            NativeBattleActionMatrix identity = NativeBattleCuragaMatrices.Identity8DD770();
            Model = NativeBattleBodyBoneMatrices.PublishLoadedSkeleton(new(3, 0), Source);
            Model = Model with { GeometryWorldMatrix = identity with { X = 40000, Y = 30000, Z = -12345 }, CurrentBodyBoneMatrices = Model.CurrentBodyBoneMatrices! with { Matrices = [identity, identity with { X = 100000, Y = -30000, Z = 12345 }] } };
            NativeBattleOriginalEffectState effect = new(299, 299, new(299, NativeBattleMicroMissiles299Profile.Entry, NativeBattleMicroMissiles299Profile.Initializer), null, null);
            Frame = new(NativeBattleEffectMemory.FromColdProcess(), effect, new(1), NativeBattleMicroMissiles299State.FromColdProcess()) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()).Write(NativeBattleTemporaryMemory.Address, Enumerable.Repeat((byte)0x7C, 20).ToArray()) };
        }
    }
}
