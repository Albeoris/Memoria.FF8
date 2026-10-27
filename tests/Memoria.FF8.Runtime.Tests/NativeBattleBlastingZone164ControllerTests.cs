using System.Buffers.Binary;
using Memoria.FF8.Runtime;
using Xunit;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleBlastingZone164ControllerTests
{
    [Fact]
    public void SelectedVerticesConsumeTheDispatchersByteDisplacement()
    {
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown().Write(0x23C9DD8, Point(123, -456, 789));
        NativeBattleVector vertex = NativeBattleBlastingZone164Packets.ReadSelectedVertex(staging, 0x23C9DC8, 16);
        Assert.Equal(new NativeBattleVector(123, -456, 789), vertex);
    }

    [Fact]
    public void RingProducerPreservesAppendLinksAndConsumesFourRandomDraws()
    {
        (NativeBattleBlastingZone164Frame frame, NativeBattleEffectTaskPool pool) = Frame(0x5F16A0, 1);
        pool = NativeBattleTaskFixture.EditPool(pool, data => data with { Slots = pool.Slots.SetItem(2, pool.Slots[2].WriteWord(34, 0xABCD)) });
        NativeCrtRandomState random = Draw(frame.Random, 4);
        NativeBattleBlastingZone164ChildStep step = NativeBattleBlastingZone164Controller.Advance5F16A0(frame, pool, 0, Inputs(0, []));
        Assert.Equal(random, step.Frame.Random);
        Assert.Equal(1, step.Pool.Slots[0].Next);
        Assert.Equal(2, step.Pool.Slots[1].Next);
        Assert.Equal(0x5F1800u, step.Pool.Slots[1].Callback);
        Assert.Equal(0x5F1970u, step.Pool.Slots[2].Callback);
        Assert.Equal(1365, step.Pool.Slots[1].ReadWord(34));
        Assert.Equal(0xABCD, step.Pool.Slots[2].ReadWord(34));
        Assert.Equal(2, step.Pool.Slots[0].ReadWord(12));
    }

    [Fact]
    public void PausedExpandingMeshStillDrawsWithoutChangingItsTask()
    {
        (NativeBattleBlastingZone164Frame frame, NativeBattleEffectTaskPool pool) = Frame(0x5F1800, 6);
        pool = NativeBattleTaskFixture.EditPool(pool, data => data with { Slots = pool.Slots.SetItem(0, pool.Slots[0].WriteWord(28, 100).WriteWord(32, 200)) });
        List<NativeBattleBlastingZone164Operation> operations = [];
        NativeBattleBlastingZone164ChildStep step = NativeBattleBlastingZone164Controller.AdvanceExpandingMesh(frame, pool, 0, Inputs(0x200, operations), Identity());
        NativeBattleBlastingZone164MeshDraw mesh = Assert.IsType<NativeBattleBlastingZone164MeshDraw>(Assert.Single(operations));
        Assert.Equal(0xD948C4u, mesh.Address);
        Assert.Equal(0xF3u, mesh.Context.Flags);
        Assert.Equal(0, mesh.Context.Fade);
        Assert.Equal(pool, step.Pool);
        Assert.Equal(0, step.Result);
    }

    [Fact]
    public void MorphUsesAsymmetricYEquationAndRetainsVertexPadding()
    {
        (NativeBattleBlastingZone164Frame frame, NativeBattleEffectTaskPool pool) = Frame(0x5F4720, 0);
        frame = frame with { Staging = frame.Staging.Write(0x23C9DA8, new byte[0x28]).Write(0x23C9DC8, new byte[] { 0, 0, 0, 0, 0, 0, 0xCA, 0xFE }).Write(0xD8E410, Point(10, 20, 30)).Write(0xD8E760, Point(100, 200, 300)).Write(0xD8D91C, new byte[] { 1, 0, 0, 0 }) };
        pool = NativeBattleTaskFixture.EditPool(pool, data => data with { Slots = pool.Slots.SetItem(0, pool.Slots[0].WriteWord(28, 4096).WriteWord(32, 4096)) });
        List<NativeBattleBlastingZone164Operation> operations = [];
        NativeBattleBlastingZone164ChildStep step = NativeBattleBlastingZone164Controller.Advance5F4720(frame, pool, 0, Inputs(0, operations), Identity());
        Assert.Equal(10, Short(step.Frame.Staging, 0x23C9DC8));
        Assert.Equal(-160, Short(step.Frame.Staging, 0x23C9DCA));
        Assert.Equal(30, Short(step.Frame.Staging, 0x23C9DCC));
        Assert.Equal(new byte[] { 0xCA, 0xFE }, step.Frame.Staging.Read(0x23C9DCE, 2));
        Assert.Equal(0x23C9DC8u, Assert.IsType<NativeBattleBlastingZone164MeshDraw>(Assert.Single(operations)).SelectedVerticesAddress);
    }

    [Fact]
    public void ScreenMotesUseCommonAbsoluteBucketAndExactSpawnRandomOrder()
    {
        (NativeBattleBlastingZone164Frame frame, NativeBattleEffectTaskPool pool) = Frame(0x5F4330, 0);
        frame = frame with { Staging = frame.Staging.Write(0x23C7C38, new byte[4800]) };
        List<NativeBattleBlastingZone164Operation> operations = [];
        NativeBattleBlastingZone164ChildStep step = NativeBattleBlastingZone164Controller.Advance5F4330(frame, pool, 0, Inputs(0, operations));
        NativeBattleBlastingZone164Tiles tiles = Assert.IsType<NativeBattleBlastingZone164Tiles>(Assert.Single(operations));
        Assert.Equal(160, tiles.Batch.Primitives.Length);
        Assert.Equal(1920, tiles.Batch.AllocationBytes);
        Assert.All(tiles.Batch.Primitives, primitive => Assert.Equal(4382, primitive.Primitive.Bucket));
        Assert.Equal(Draw(frame.Random, 480), step.Frame.Random);
        Assert.Equal(1, step.Pool.Slots[0].ReadWord(12));
    }

    [Fact]
    public void ShakeWritesOnPausedFramesAndDoesNotAdvance()
    {
        (NativeBattleBlastingZone164Frame frame, NativeBattleEffectTaskPool pool) = Frame(0x5F5A50, 0);
        List<NativeBattleBlastingZone164Operation> operations = [];
        NativeBattleBlastingZone164ChildStep step = NativeBattleBlastingZone164Controller.Advance5F5A50(frame, pool, 0, Inputs(1, operations));
        Assert.Equal(-140, Assert.IsType<NativeBattleBlastingZone164Shake>(Assert.Single(operations)).Offset);
        Assert.Equal(pool, step.Pool);
    }

    [Fact]
    public void TextureWaiterReadsFullCompletionWordAndRunsWhilePaused()
    {
        (NativeBattleBlastingZone164Frame frame, NativeBattleEffectTaskPool pool) = Frame(0x5F5AD0, 1);
        uint completion = 0;
        NativeBattleBlastingZone164ChildStep step = NativeBattleBlastingZone164Controller.Advance5F5AD0(frame, pool, 0, (current, address) =>
        {
            completion = address;
            return current with { Effect = NativeBattleBlastingZone164Controller.WriteCompletionByte(current.Effect, address, 1) };
        });
        Assert.Equal(0x23C8F5Eu, completion);
        Assert.Equal(2, step.Result);
        Assert.Equal(2, step.Pool.Slots[0].ReadWord(12));
        Assert.Equal(1, step.Pool.Slots[0].ReadWord(14));
    }

    [Fact]
    public void ExpiredScrollingParticleDrawsBeforeClearingItsMarker()
    {
        (NativeBattleBlastingZone164Frame frame, NativeBattleEffectTaskPool pool) = Frame(0x5F1190, 50);
        byte[] rows = new byte[2400];
        rows[0] = 2;
        rows[4] = 16;
        frame = frame with { Staging = frame.Staging.Write(0x23C6978, rows) };
        List<NativeBattleBlastingZone164Operation> operations = [];
        NativeBattleBlastingZone164ChildStep step = NativeBattleBlastingZone164Controller.Advance5F1190(frame, pool, 0, Inputs(0, operations), Identity());
        Assert.IsType<NativeBattleBlastingZone164MeshDraw>(Assert.Single(operations));
        Assert.Equal(new byte[4], step.Frame.Staging.Read(0x23C6978, 4));
        Assert.Equal(frame.Random, step.Frame.Random);
    }

    private static NativeBattleBlastingZone164Inputs Inputs(uint flags, List<NativeBattleBlastingZone164Operation> operations) => new(flags, () => 0, _ => 4096, _ => new(new(0, 0, 0), 0, -4096, 0, null, null), (frame, operation) =>
    {
        operations.Add(operation);
        return frame;
    });

    private static (NativeBattleBlastingZone164Frame Frame, NativeBattleEffectTaskPool Pool) Frame(uint callback, ushort counter)
    {
        NativeBattleEffectTaskPool main = NativeBattleEffectTaskPool.Initialize(0x23C6968, 0x23C6958, 16, 1);
        (main, _) = main.Append(0x5F5B10);
        NativeBattleEffectTaskPool children = NativeBattleEffectTaskPool.Initialize(0x23C8F40, 0x23C8F50, 36, 100);
        (children, _) = children.Append(callback);
        children = NativeBattleTaskFixture.EditPool(children, data => data with { Slots = children.Slots.SetItem(0, children.Slots[0].WriteWord(12, counter)) });
        NativeBattleEffectParameters parameters = new(0, 0, 8, 0, 0, 0);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { BlastingZone164 = new(true, parameters, 3, null, null) };
        NativeBattleOriginalEffectState effect = new(164, 162, new(164, 0x5F0260, 0x5F0240), parameters, 0x23C6968) { Pools = [main, children] };
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown().Write(0x23C9D98, new byte[8]);
        return (new(memory, effect, staging, NativeCrtRandomState.FromColdProcess()), children);
    }

    private static NativeCrtRandomState Draw(NativeCrtRandomState random, int count)
    {
        for (int index = 0; index < count; index++)
        {
            random = random.Draw(out _);
        }
        return random;
    }
    private static NativeBattleActionMatrix Identity() => NativeBattleGfMatrices.Euler56CE30(new(0, 0, 0));
    private static short Short(NativeBattleFileStaging staging, uint address) => BinaryPrimitives.ReadInt16LittleEndian(staging.Read(address, 2));
    private static byte[] Point(short x, short y, short z)
    {
        byte[] bytes = new byte[8];
        BinaryPrimitives.WriteInt16LittleEndian(bytes, x);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(2), y);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(4), z);
        return bytes;
    }
}
