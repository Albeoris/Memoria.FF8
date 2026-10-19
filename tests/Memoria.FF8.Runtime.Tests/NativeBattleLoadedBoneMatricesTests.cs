using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleLoadedBoneMatricesTests
{
    [Fact]
    public void ClearingTheModelBankRetainsOnlyExplicitlySuppliedWorldMatrices()
    {
        Assert.All(NativeBattleGraphicsModels.ClearBank(), model => Assert.Null(model.GeometryWorldMatrix));
        NativeBattleRetainedMemory cold = NativeBattleRetainedMemory.FromColdProcess();
        Assert.NotNull(cold.ModelWorldMatrices64);
        NativeBattleGraphicsModel[] models = NativeBattleGraphicsModels.ClearBank(null, retainedWorldMatrices64: cold.ModelWorldMatrices64);
        Assert.All(models, model =>
        {
            Assert.NotNull(model.GeometryWorldMatrix);
            Assert.All(model.GeometryWorldMatrix.Rotation, value => Assert.Equal((short)0, value));
            Assert.Equal(0, model.GeometryWorldMatrix.X);
            Assert.Equal(0, model.GeometryWorldMatrix.Y);
            Assert.Equal(0, model.GeometryWorldMatrix.Z);
        });
        ImmutableArray<NativeBattleActionMatrix?> retained = cold.ModelWorldMatrices64.Value.SetItem(2, null).SetItem(0, cold.ModelWorldMatrices64.Value[0]! with { X = -123 });
        models = NativeBattleGraphicsModels.ClearBank(null, retainedWorldMatrices64: retained);
        Assert.Equal(-123, models[0].GeometryWorldMatrix!.X);
        Assert.Null(models[2].GeometryWorldMatrix);
        Assert.Throws<InvalidDataException>(() => NativeBattleGraphicsModels.ClearBank(null, retainedWorldMatrices64: retained.RemoveAt(6)));
    }

    [Fact]
    public void FirstSequenceSoundReadsTheActualLoadedRigBeforeAnyPoseSelection()
    {
        NativeBattlePoseSource source = Source();
        NativeBattleGraphicsModel model = Model();
        model = NativeBattleBodyBoneMatrices.PublishLoadedSkeleton(model, source);
        Assert.Null(model.SequenceState!.Pose);
        NativeBattleModuleSound sound = NativeBattleSequenceAudio.Resolve505AB0(new(1, false, 0, 0), 0, null, _ => throw new InvalidOperationException(), index =>
        {
            Assert.Equal(0, index);
            return NativeBattlePoseMatrices.ReadBone(model, source, 241, 0);
        }, point =>
        {
            Assert.Equal(new NativeBattleVector(140, 250, 360), point);
            return 159;
        }, null)!;
        Assert.Equal((byte)159, sound.Pan);
        Assert.Equal(new NativeBattleModuleAnchor(140, 250, 360, 0), sound.Anchor);
        Assert.Equal(new NativeBattleVector(140, 250, 460), NativeBattlePoseMatrices.ReadBone(model, source, 241, 2048));
    }

    [Fact]
    public void AZeroMatrixInTheOriginalRigRemainsZeroInsteadOfBecomingAnIdentityOrGuessedPose()
    {
        NativeBattlePoseSource source = Source();
        source = source with { Skeleton = source.Skeleton.SetItem(32, 0).SetItem(33, 0) };
        byte[] skeleton = source.Skeleton.ToArray();
        Array.Clear(skeleton, 32 + 48, 32);
        source = source with { Skeleton = skeleton.ToImmutableArray() };
        NativeBattleGraphicsModel model = NativeBattleBodyBoneMatrices.PublishLoadedSkeleton(Model(), source);
        Assert.All(model.CurrentBodyBoneMatrices!.Matrices[1].Rotation, lane => Assert.Equal((short)0, lane));
        Assert.Equal(new NativeBattleVector(100, 200, 300), NativeBattlePoseMatrices.ReadBone(model, source, 241, 4096));
        Assert.Null(model.SequenceState!.Pose);
    }

    [Fact]
    public void BoneReadsUseTheCurrentPublicationAndNeverReconstructAMissingImportedOwner()
    {
        NativeBattlePoseSource source = Source();
        NativeBattleGraphicsModel model = Model();
        Assert.Throws<NotSupportedException>(() => NativeBattlePoseMatrices.ReadBone(model, source, 241, 0));
        model = NativeBattleBodyBoneMatrices.PublishLoadedSkeleton(model, source);
        NativeBattleBodyBoneMatrices bank = model.CurrentBodyBoneMatrices!;
        model = model with { CurrentBodyBoneMatrices = bank with { Matrices = bank.Matrices.SetItem(1, bank.Matrices[1] with { X = -70 }) } };
        Assert.Equal(new NativeBattleVector(30, 250, 360), NativeBattlePoseMatrices.ReadBone(model, source, 241, 0));
        Assert.Throws<NotSupportedException>(() => NativeBattlePoseMatrices.ReadBone(model, source with { Skeleton = source.Skeleton.SetItem(7, 1) }, 241, 0));
    }

    private static NativeBattleGraphicsModel Model()
    {
        NativeBattleActionMatrix world = new(ImmutableArray.Create<short>(4096, 0, 0, 0, 4096, 0, 0, 0, 4096), 100, 200, 300, 4096);
        return new(0, 3) { ModelId4 = 7, SequenceState = new(), Transform = new(new(100, 200, 300), new(100, 200, 300), 0, 0, 4096), GeometryWorldMatrix = world };
    }

    private static NativeBattlePoseSource Source()
    {
        byte[] skeleton = new byte[16 + 48 * 2];
        skeleton[0] = 2;
        skeleton[5] = 1;
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(16), -1);
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(64), 0);
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(66), 200);
        for (int bone = 0; bone < 2; bone++)
        {
            int matrix = 32 + 48 * bone;
            for (int lane = 0; lane < 3; lane++)
            {
                BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(matrix + lane * 8), 4096);
                BinaryPrimitives.WriteInt32LittleEndian(skeleton.AsSpan(matrix + 20 + lane * 4), 10 + bone * 30 + lane * 10);
            }
        }
        return new(skeleton.ToImmutableArray(), [1]);
    }
}
