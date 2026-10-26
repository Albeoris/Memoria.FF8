using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCurrentSkeletonPublicationTests
{
    [Fact]
    public void UnselectedAnimationRestoresActualSkeletonAnglesAndRootWords()
    {
        NativeBattlePoseSource source = Source(false);
        NativeBattleGraphicsModel model = Model();

        NativeBattleGraphicsModel restored = NativeBattleBodyBoneMatrices.Publish508C90(model, source);

        NativeBattleActionMatrix root = NativeBattleGfMatrices.Euler56CE30(new(0, 1024, 0)) with { X = -208, Y = 272, Z = 304 };
        NativeBattleActionMatrix child = NativeBattleGfMatrices.Compose56C2F0(root, NativeBattleGfMatrices.Euler56CE30(new(0, 0, 0)) with { Z = 80 });
        ImmutableArray<NativeBattleActionMatrix> actual = NativeBattleBodyBoneMatrices.Require(restored, source);
        Assert.Equal(root.Rotation, actual[0].Rotation);
        Assert.Equal((root.X, root.Y, root.Z), (actual[0].X, actual[0].Y, actual[0].Z));
        Assert.Equal(child.Rotation, actual[1].Rotation);
        Assert.Equal((child.X, child.Y, child.Z), (actual[1].X, actual[1].Y, actual[1].Z));
        Assert.Same(model.SequenceState, restored.SequenceState);
        Assert.Null(restored.SequenceState!.Pose);
        Assert.Null(restored.SequenceState.DecodedPose);
    }

    [Fact]
    public void SkeletonScaleBranchConsumesOriginalPerBoneScaleWords()
    {
        NativeBattlePoseSource source = Source(true);
        NativeBattleGraphicsModel restored = NativeBattleBodyBoneMatrices.Publish508C90(Model(), source);
        ImmutableArray<NativeBattleActionMatrix> matrices = NativeBattleBodyBoneMatrices.Require(restored, source);

        Assert.Equal(new short[] { 2048, 0, 0, 0, 4096, 0, 0, 0, 8192 }, matrices[0].Rotation);
        Assert.Equal(new short[] { 2048, 0, 0, 0, 2048, 0, 0, 0, 8192 }, matrices[1].Rotation);
        Assert.Equal((-208, 272, 464), (matrices[1].X, matrices[1].Y, matrices[1].Z));
    }

    [Fact]
    public void PublishedAnimatedPoseRemainsTheCurrentOwner()
    {
        NativeBattlePoseSource source = Source(false);
        NativeBattleDecodedPose pose = new(new(3, 5, 7), [new(new(0, 0, 0), new(1024, 1024, 1024)), new(new(0, 0, 0), new(1024, 1024, 1024))], false);
        NativeBattleGraphicsModel model = Model() with { SequenceState = new() { DecodedPose = pose } };
        NativeBattleGraphicsModel restored = NativeBattleBodyBoneMatrices.Publish508C90(model, source);

        NativeBattleActionMatrix root = restored.CurrentBodyBoneMatrices!.Matrices[0];
        Assert.Equal((48, 80, 112), (root.X, root.Y, root.Z));
        Assert.Equal(new short[] { 4096, 0, 0, 0, 4096, 0, 0, 0, 4096 }, root.Rotation);
        Assert.Same(model.SequenceState, restored.SequenceState);
    }

    [Fact]
    public void UnknownConsumedSkeletonAngleCannotBecomeAnIdentityPose()
    {
        NativeBattlePoseSource source = Source(false);
        bool[] known = Enumerable.Repeat(true, source.Skeleton.Length).ToArray();
        known[20] = false;
        source = source with { SkeletonKnownBytes = known.ToImmutableArray() };

        Assert.Throws<NotSupportedException>(() => NativeBattleBodyBoneMatrices.Publish508C90(Model(), source));
    }

    [Fact]
    public void UnconsumedMatrixPaddingDoesNotRequireAPoseSelector()
    {
        NativeBattlePoseSource source = Source(false);
        bool[] known = Enumerable.Repeat(true, source.Skeleton.Length).ToArray();
        known[50] = false;
        known[51] = false;
        source = source with { SkeletonKnownBytes = known.ToImmutableArray() };

        NativeBattleGraphicsModel restored = NativeBattleBodyBoneMatrices.Publish508C90(Model(), source);
        Assert.Equal(2, NativeBattleBodyBoneMatrices.Require(restored, source).Length);
        Assert.Null(restored.SequenceState!.Pose);
    }

    [Fact]
    public void PhysicalBoneTemporaryUsesTheSameCurrentSkeletonWithoutSelectingAnimation()
    {
        NativeBattlePoseSource source = Source(false);
        NativeBattleEffectArena temporary = NativeBattleEffectArena.Clear().Write(0, Enumerable.Repeat((byte)0xA5, 88).ToArray());

        NativeBattleBodyBonePreparation prepared = NativeBattleBodyBoneTemporary.Prepare508C90(Model(), source, temporary);

        Assert.Null(prepared.Model.SequenceState!.Pose);
        Assert.Equal(2, NativeBattleBodyBoneMatrices.Require(prepared.Model, source).Length);
        Assert.Equal(80, BinaryPrimitives.ReadInt32LittleEndian(prepared.Temporary58.Read(0x1C, 4)));
        Assert.Equal(4096, BinaryPrimitives.ReadInt32LittleEndian(prepared.Temporary58.Read(0x40, 4)));
        Assert.Equal(4096, BinaryPrimitives.ReadInt32LittleEndian(prepared.Temporary58.Read(0x44, 4)));
        Assert.Equal(4096, BinaryPrimitives.ReadInt32LittleEndian(prepared.Temporary58.Read(0x48, 4)));
        Assert.Equal(Enumerable.Repeat((byte)0xA5, 12), prepared.Temporary58.Read(0x4C, 12));
    }

    private static NativeBattleGraphicsModel Model() => new(6, 15) { ModelId4 = 143, SequenceState = new() };

    private static NativeBattlePoseSource Source(bool scaled)
    {
        byte[] skeleton = new byte[112];
        skeleton[0] = 2;
        skeleton[1] = scaled ? (byte)1 : (byte)0;
        Word(2, 4096);
        Word(8, -13);
        Word(10, 17);
        Word(12, 19);
        Word(16, -1);
        Word(18, 80);
        Word(22, scaled ? (short)0 : (short)1024);
        Word(26, 512);
        Word(28, 1024);
        Word(30, 2048);
        Word(64, 0);
        Word(66, 40);
        Word(74, 1024);
        Word(76, 512);
        Word(78, 1024);
        return new(skeleton.ToImmutableArray(), [1]);

        void Word(int offset, short value)
        {
            BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(offset), value);
        }
    }
}
