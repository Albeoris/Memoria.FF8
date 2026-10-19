using System.Buffers.Binary;
using Memoria.FF8.Runtime;
using Xunit;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleLionHeart165ControllerTests
{
    [Fact]
    public void FirstFloatTrailHistorySampleRetainsActualTemporaryPaddingAndBalancesAllocation()
    {
        (NativeBattleLionHeart165Frame frame, NativeBattleEffectTaskPool pool) = Frame(0x5E9E00, 0);
        List<NativeBattleLionHeart165Operation> operations = [];
        NativeBattleLionHeart165ChildStep step = NativeBattleLionHeart165Controller.Advance5E9E00(frame, pool, 0, Inputs(0, operations), Identity(), new(null, 288, 160, 108), NativeBattleGteColorInterpolation.CodeConstants45F270);
        Assert.Equal(1, Int(step.Frame.Staging, 0x23C6898));
        Assert.Equal(10, Short(step.Frame.Staging, 0x23C689C));
        Assert.Equal(30, Short(step.Frame.Staging, 0x23C68A0));
        Assert.Equal(-636, Short(step.Frame.Staging, 0x23C68A8));
        Assert.Equal(new byte[] { 0xA5, 0xA5 }, step.Frame.Staging.Read(0x23C68A2, 2));
        Assert.Equal(NativeBattleTemporaryMemory.Address, unchecked((uint)Int(step.Frame.Staging, NativeBattleTemporaryMemory.Cursor)));
        Assert.Equal(1, step.Pool.Slots[0].ReadWord(12));
        Assert.Empty(operations);
        Assert.Equal(frame.Random, step.Frame.Random);
    }

    [Fact]
    public void PausedFloatTrailStillRefreshesHistoryButRetainsItsCounter()
    {
        (NativeBattleLionHeart165Frame frame, NativeBattleEffectTaskPool pool) = Frame(0x5E9E00, 0);
        NativeBattleLionHeart165ChildStep step = NativeBattleLionHeart165Controller.Advance5E9E00(frame, pool, 0, Inputs(1, []), Identity(), new(null, 288, 160, 108), NativeBattleGteColorInterpolation.CodeConstants45F270);
        Assert.Equal(1, Int(step.Frame.Staging, 0x23C6898));
        Assert.Equal(0, step.Pool.Slots[0].ReadWord(12));
        Assert.Equal(0, step.Result);
    }

    [Fact]
    public void MainReleasesAfterExactly371TicksAndRestoresStageState()
    {
        (NativeBattleLionHeart165Frame frame, NativeBattleEffectTaskPool pool) = Frame(0x5E9650, 370);
        List<NativeBattleLionHeart165Operation> operations = [];
        NativeBattleLionHeart165ChildStep step = NativeBattleLionHeart165Controller.Advance5E9650(frame, pool, 0, Inputs(0, operations), Identity());
        Assert.Equal(371, step.Pool.Slots[0].ReadWord(12));
        Assert.Equal(2, step.Result);
        Assert.True(Assert.IsType<NativeBattleLionHeart165StageFlags>(operations[0]).Set);
        Assert.Equal(0, Assert.IsType<NativeBattleLionHeart165Stage>(operations[1]).Intensity);
    }

    [Fact]
    public void StageFadePublishesBeforePauseAndClearsStageBitsOnlyAtItsRealEnd()
    {
        (NativeBattleLionHeart165Frame frame, NativeBattleEffectTaskPool pool) = Frame(0x5E9D50, 15);
        pool = pool with { Slots = pool.Slots.SetItem(0, pool.Slots[0].WriteWord(14, 16).WriteWord(24, 2048).WriteWord(26, 1)) };
        List<NativeBattleLionHeart165Operation> operations = [];
        NativeBattleLionHeart165ChildStep paused = NativeBattleLionHeart165Controller.Advance5E9D50(frame, pool, 0, Inputs(1, operations));
        Assert.Equal(3968, Assert.IsType<NativeBattleLionHeart165Stage>(Assert.Single(operations)).Intensity);
        Assert.Equal(15, paused.Pool.Slots[0].ReadWord(12));
        operations.Clear();
        NativeBattleLionHeart165ChildStep active = NativeBattleLionHeart165Controller.Advance5E9D50(frame, pool, 0, Inputs(0, operations));
        Assert.Equal(2, active.Result);
        Assert.False(Assert.IsType<NativeBattleLionHeart165StageFlags>(operations[1]).Set);
    }

    [Fact]
    public void OpeningBoneUsesOriginal241SelectorAndZeroFraction()
    {
        (NativeBattleLionHeart165Frame frame, NativeBattleEffectTaskPool pool) = Frame(0x5E9650, 0);
        List<(byte Actor, byte Bone, short Fraction)> bones = [];
        NativeBattleLionHeart165Inputs inputs = Inputs(0, []) with { ReadBone = (actor, bone, fraction) =>
        {
            bones.Add((actor, bone, fraction));
            return new(10, 20, 30);
        } };
        NativeBattleLionHeart165ChildStep step = NativeBattleLionHeart165Controller.RunInitializer(frame, pool, 0x5EA850, inputs);
        Assert.Equal(new[] { ((byte)0, (byte)241, (short)0) }, bones);
        Assert.Equal(20, Short(step.Frame.Staging, 0x23C691A));
        Assert.Equal(0, step.Pool.Slots[1].ReadWord(18));
        Assert.Equal(256, step.Pool.Slots[1].ReadWord(32));
        Assert.Equal(1, step.Pool.Slots[0].Next);
    }

    [Fact]
    public void MirroredFlareInitializerRetainsBothSidesAndFourRandomDraws()
    {
        (NativeBattleLionHeart165Frame frame, NativeBattleEffectTaskPool pool) = Frame(0x5E9650, 0);
        NativeCrtRandomState expected = frame.Random;
        for (int index = 0; index < 4; index++)
        {
            expected = expected.Draw(out _);
        }
        NativeBattleLionHeart165ChildStep step = NativeBattleLionHeart165Controller.RunInitializer(frame, pool, 0x5EBEF0, Inputs(0, []));
        Assert.Equal(expected, step.Frame.Random);
        Assert.Equal(0, step.Pool.Slots[1].ReadWord(14));
        Assert.Equal(1, step.Pool.Slots[2].ReadWord(14));
        Assert.Equal(2, step.Pool.Slots[1].Next);
        Assert.Equal(3072, step.Pool.Slots[2].ReadWord(28));
    }

    [Fact]
    public void CommonBillboardWritesItsOriginalRadiusWordAndDepthShiftWhilePaused()
    {
        (NativeBattleLionHeart165Frame frame, NativeBattleEffectTaskPool pool) = Frame(0x5EE850, 16);
        pool = pool with { Slots = pool.Slots.SetItem(0, pool.Slots[0].WriteWord(28, 4096)) };
        List<NativeBattleLionHeart165Operation> operations = [];
        NativeBattleLionHeart165ChildStep step = NativeBattleLionHeart165Controller.AdvanceBillboardMesh(frame, pool, 0, Inputs(1, operations), Identity());
        NativeBattleLionHeart165MeshDraw mesh = Assert.IsType<NativeBattleLionHeart165MeshDraw>(Assert.Single(operations));
        Assert.True(mesh.Common);
        Assert.Equal(1, mesh.Context.DepthShift);
        Assert.Equal(0xF3u, mesh.Context.Flags);
        Assert.Equal(850, Short(step.Frame.Staging, 0x23C6936));
        Assert.Equal(16, step.Pool.Slots[0].ReadWord(12));
    }

    [Fact]
    public void FirstColumnParticleDrawsBeforeItsNativeScaleUpdateWithoutDrawingRandomNumbers()
    {
        (NativeBattleLionHeart165Frame frame, NativeBattleEffectTaskPool pool) = Frame(0x5EAAF0, 1);
        pool = pool with { Slots = pool.Slots.SetItem(0, pool.Slots[0].WriteWord(14, 7).WriteWord(16, 10).WriteWord(18, 20).WriteWord(20, 30)) };
        List<NativeBattleLionHeart165Operation> operations = [];
        NativeBattleLionHeart165ChildStep step = NativeBattleLionHeart165Controller.AdvanceMeshParticles(frame, pool, 0, Inputs(0, operations), Identity());
        NativeBattleLionHeart165MeshDraw mesh = Assert.IsType<NativeBattleLionHeart165MeshDraw>(Assert.Single(operations));
        Assert.Equal(0xD83244u, mesh.Address);
        Assert.Equal(1536, mesh.Matrix.Rotation[0]);
        Assert.Equal(10, mesh.Matrix.X);
        Assert.Equal(20, mesh.Matrix.Y);
        Assert.Equal(1, Short(step.Frame.Staging, 0x23C34C4));
        Assert.Equal(3072, Short(step.Frame.Staging, 0x23C34D0));
        Assert.Equal(1229, Short(step.Frame.Staging, 0x23C34D2));
        Assert.Equal(frame.Random, step.Frame.Random);
        Assert.Equal(NativeBattleTemporaryMemory.Address, unchecked((uint)Int(step.Frame.Staging, NativeBattleTemporaryMemory.Cursor)));
    }

    [Fact]
    public void FinalParticleRetiresBeforeItsProducerReleasesAndPausedProducerDoesNeither()
    {
        (NativeBattleLionHeart165Frame frame, NativeBattleEffectTaskPool pool) = Frame(0x5ED8E0, 13);
        pool = pool with { Slots = pool.Slots.SetItem(0, pool.Slots[0].WriteWord(14, 7)) };
        byte[] row = new byte[32];
        BinaryPrimitives.WriteInt32LittleEndian(row, 7);
        BinaryPrimitives.WriteInt16LittleEndian(row.AsSpan(4), 24);
        BinaryPrimitives.WriteInt16LittleEndian(row.AsSpan(6), 4096);
        frame = frame with { Staging = frame.Staging.Write(0x23C4780, row) };
        List<NativeBattleLionHeart165Operation> operations = [];
        NativeBattleLionHeart165ChildStep paused = NativeBattleLionHeart165Controller.AdvanceMeshParticles(frame, pool, 0, Inputs(1, operations), Identity());
        Assert.Single(operations);
        Assert.Equal(7, Int(paused.Frame.Staging, 0x23C4780));
        Assert.Equal(0, paused.Result);
        NativeBattleLionHeart165ChildStep step = NativeBattleLionHeart165Controller.AdvanceMeshParticles(paused.Frame, paused.Pool, 0, Inputs(0, []), Identity());
        Assert.Equal(0, Int(step.Frame.Staging, 0x23C4780));
        Assert.Equal(2, step.Result);
        Assert.Equal(14, step.Pool.Slots[0].ReadWord(12));
        Assert.Equal(frame.Random, step.Frame.Random);
    }

    private static NativeBattleLionHeart165Inputs Inputs(uint flags, List<NativeBattleLionHeart165Operation> operations) => new(flags, () => 0, actor => new(actor, 3) { Transform = new(new(0, 0, 0), new(0, 0, 0), 0, 0, 4096) }, _ => new(10, 20, 30, 0), (_, _, _) => new(10, 20, 30), _ => new(new(10, 20, 30), 0, 0, -4096, null, null), (frame, operation) =>
    {
        operations.Add(operation);
        return frame;
    });

    private static (NativeBattleLionHeart165Frame Frame, NativeBattleEffectTaskPool Pool) Frame(uint callback, ushort counter)
    {
        NativeBattleEffectTaskPool main = NativeBattleEffectTaskPool.Initialize(0x23C34B0, 0x23C34A0, 16, 1);
        (main, _) = main.Append(0x5F01F0);
        NativeBattleEffectTaskPool children = NativeBattleEffectTaskPool.Initialize(0x23C5910, 0x23C5920, 36, 100);
        (children, _) = children.Append(callback);
        children = children with { Slots = children.Slots.SetItem(0, children.Slots[0].WriteWord(12, counter)) };
        NativeBattleEffectParameters parameters = new(0, 0, 8, 0, 0, 0);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { LionHeart165 = new(true, parameters, 3, null, null) };
        NativeBattleOriginalEffectState effect = new(165, 164, new(165, 0x5E9470, 0x5E9450), parameters, 0x23C34B0) { Pools = [main, children] };
        NativeBattleFileStaging staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown().Write(0x23C3278, new byte[0x36D8]).Write(NativeBattleTemporaryMemory.Address, Enumerable.Repeat((byte)0xA5, NativeBattleTemporaryMemory.ReachedLength).ToArray()));
        byte[] matrix = new byte[32];
        for (int index = 0; index < 3; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(matrix.AsSpan(index * 8), 4096);
        }
        staging = staging.Write(0x23C58E0, matrix);
        return (new(memory, effect, staging, NativeCrtRandomState.FromColdProcess()), children);
    }
    private static NativeBattleActionMatrix Identity() => NativeBattleGfMatrices.Euler56CE30(new(0, 0, 0));
    private static short Short(NativeBattleFileStaging staging, uint address) => BinaryPrimitives.ReadInt16LittleEndian(staging.Read(address, 2));
    private static int Int(NativeBattleFileStaging staging, uint address) => BinaryPrimitives.ReadInt32LittleEndian(staging.Read(address, 4));
}
