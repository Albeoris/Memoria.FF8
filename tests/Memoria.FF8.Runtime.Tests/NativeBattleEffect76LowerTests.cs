using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect76Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect76Lower;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect76LowerTests
{
    [Theory]
    [InlineData(0x620D60u, false, 0xF0, 2048, 3072)]
    [InlineData(0x6210A0u, true, 0xF1, 0, 1664)]
    public void Factory_observes_native_query_allocation_order_and_preserves_point_W(uint factory, bool allocatedBeforeQuery, int selector, int distance, int size)
    {
        NativeBattleEffect76Frame frame = Frame();
        frame = WriteBytes(frame, Point, Word(NativeBattleEffectArena.Clear(), 6, 0x6A5B), 8);
        NativeBattleEffectTaskPool pool = frame.Effect.Pools[0];
        pool = pool with { Slots = pool.Slots.SetItem(0, pool.Slots[0].WriteWord(0x16, 0x4A3B)) };
        frame = frame with { Effect = frame.Effect with { Pools = [pool] } };
        NativeCrtRandomState expectedRandom = frame.Random;
        if (allocatedBeforeQuery)
        {
            expectedRandom = expectedRandom.Draw(out _);
        }
        int queries = 0;
        NativeBattleEffect76Inputs inputs = Inputs((current, actor, actualSelector, actualDistance, retained) =>
        {
            queries++;
            Assert.Equal(5, actor);
            Assert.Equal(selector, actualSelector);
            Assert.Equal(distance, actualDistance);
            Assert.Equal(allocatedBeforeQuery, current.Effect.Pools[0].Head is not null);
            Assert.Equal(allocatedBeforeQuery ? 0x4A3B : 0x6A5B, Short(retained, 6));
            return new(current, PutVector(retained, 0, 11, 22, 33));
        }, (_, _) => throw new InvalidOperationException());
        NativeBattleEffect76FactoryStep result = NativeBattleEffect76Bursts.Factory(frame, factory, inputs);
        NativeBattleModulePoolTask task = Read(result.Frame, result.Address);
        Assert.Equal(1, queries);
        Assert.Equal(expectedRandom, result.Frame.Random);
        Assert.Equal(new NativeBattleVector(11, 22, 33), NativeBattleEffect76Lower.Vector(TaskView(task, 0x10, 8), 0));
        Assert.Equal((ushort)(allocatedBeforeQuery ? 0x4A3B : 0x6A5B), task.ReadWord(0x16));
        Assert.Equal((ushort)size, task.ReadWord(0x1C));
        Assert.Equal((ushort)size, task.ReadWord(0x1E));
        Assert.Equal(0, task.ReadWord(12));
    }

    [Fact]
    public void Full_factory_pool_still_samples_the_preallocation_bone_but_does_not_consume_random()
    {
        NativeBattleEffect76Frame frame = Frame();
        (NativeBattleEffectTaskPool pool, _) = frame.Effect.Pools[0].Append(0x620BD0);
        frame = frame with { Effect = frame.Effect with { Pools = [pool] } };
        int queries = 0;
        NativeBattleEffect76Inputs inputs = Inputs((current, _, _, _, retained) =>
        {
            queries++;
            return new(current, retained);
        }, (_, _) => throw new InvalidOperationException());
        Assert.Throws<InvalidDataException>(() => NativeBattleEffect76Bursts.Factory(frame, 0x620D60, inputs));
        Assert.Equal(1, queries);
    }

    [Fact]
    public void Cleanup_queues_only_at_age_one_and_observes_synchronous_completion_before_increment()
    {
        NativeBattleEffect76Frame frame = Frame();
        (frame, uint address) = Append(frame, 0x24C5830, 0x622560);
        frame = Write(frame, address, Read(frame, address).WriteWord(12, 1).WriteWord(14, 0).WriteWord(16, 0x55AA));
        frame = WriteBytes(frame, 0x24C66A0, Long(NativeBattleEffectArena.Clear(), 0, 0x1000), 4);
        int calls = 0;
        NativeBattleEffect76Inputs inputs = Inputs(null!, (current, operation) =>
        {
            calls++;
            NativeBattleEffect76CommonTextureLoad508630 load = Assert.IsType<NativeBattleEffect76CommonTextureLoad508630>(operation);
            Assert.Equal(0x19000u, load.SourceAddress);
            Assert.Equal(address + 14, load.CompletionAddress);
            return Write(current, address, Read(current, address).WriteWord(14, 1));
        });
        NativeBattleEffect76Step result = NativeBattleEffect76Lower.Advance(frame, address, null!, inputs);
        Assert.Equal(2, result.Result);
        Assert.Equal(2, Read(result.Frame, address).ReadWord(12));
        Assert.Equal(0x55AA, Read(result.Frame, address).ReadWord(16));
        result = NativeBattleEffect76Lower.Advance(result.Frame, address, null!, inputs);
        Assert.Equal(1, calls);
        Assert.Equal(3, Read(result.Frame, address).ReadWord(12));
    }

    [Fact]
    public void Paused_empty_particle_pass_retains_age_random_and_allocated_temporary_writes_after_release()
    {
        NativeBattleEffect76Frame frame = Frame() with { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
        (frame, uint address) = Append(frame, 0x24C5830, 0x6215E0);
        frame = Write(frame, address, Read(frame, address).WriteWord(12, 42));
        byte[] cursor = frame.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4).ToArray();
        NativeBattleActionMatrix identity = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
        NativeBattleEffect76Inputs inputs = Inputs(null!, (_, _) => throw new InvalidOperationException()) with { ReadFlags = _ => 0x201, ReadView = _ => NativeBattlePackedMatrix32.FromMatrix(identity, null) };
        NativeBattleEffect76Step result = NativeBattleEffect76Particles.Advance(frame, address, 4, null!, inputs);
        Assert.Equal(0, result.Result);
        Assert.Equal(42, Read(result.Frame, address).ReadWord(12));
        Assert.Equal(frame.Random, result.Frame.Random);
        Assert.Equal(cursor, result.Frame.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4).ToArray());
        Assert.Equal(new byte[6], result.Frame.Staging.Read(NativeBattleTemporaryMemory.Address, 6).ToArray());
    }

    [Theory]
    [InlineData(0x620BD0u, 5, 686)]
    [InlineData(0x620BD0u, 6, 256)]
    [InlineData(0x620BD0u, 7, 2048)]
    [InlineData(0x620BD0u, 344, 0)]
    [InlineData(0x620F60u, 1, 2048)]
    [InlineData(0x620F60u, 15, 682)]
    public void Burst_fade_keeps_native_boundary_rounding(uint callback, int age, int cue)
    {
        NativeBattleEffectArena context = NativeBattleEffect76Bursts.Cue(NativeBattleEffectArena.Clear().MarkUnknown(12, 4), callback, (short)age);
        Assert.Equal(0xF3u, Dword(context, 0x1C));
        Assert.Equal((uint)cue, Dword(context, 12));
    }

    private static NativeBattleEffect76Frame Frame()
    {
        NativeBattleEffect76Frame frame = NativeBattleEffect76ParticleTests.Frame();
        frame = frame with { Effect = frame.Effect with { Pools = [NativeBattleEffectTaskPool.Initialize(0x24C5830, 0x1000, 36, 1)] } };
        return WriteBytes(frame, 0x24C6658, Long(NativeBattleEffectArena.Clear(), 0, 5), 4);
    }

    private static NativeBattleEffect76Inputs Inputs(Func<NativeBattleEffect76Frame, int, byte, short, NativeBattleEffectArena, NativeBattleEffect76PointStep> point, Func<NativeBattleEffect76Frame, NativeBattleEffect76Operation, NativeBattleEffect76Frame> execute) => new(Colors: NativeBattleGteColorInterpolation.CodeConstants45F270, ReadFlags: null!, ReadStep508500: null!, ReadPackets: null!, ReadModel: null!, ReadPoseSource: null!, ReadView: null!, ReadProjection: null!, ReadWorld: null!, ReadPosition8: null!, Point502170: point, ReadCameraPose: null!, ReadEyePadding: null!, ReadTargetPadding: null!, ReadVibrationPlaying4A2900: null!, ReadColor: null!, Execute: execute);
}
