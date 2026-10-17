using System.Buffers.Binary;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleQuezacotlFirstWindowIoTests
{
    [Theory]
    [InlineData(30, new short[] { 508, 509, 510 }, new short[] { 2, 1, 3 }, new uint[] { 0x2108954, 0x2108954, 0x2108954 })]
    [InlineData(60, new short[] { 511, 512 }, new short[] { 1, 5 }, new uint[] { 0x2108954, 0x2111154 })]
    [InlineData(76, new short[] { 513 }, new short[] { 5 }, new uint[] { 0x1298C68 })]
    [InlineData(85, new short[] { 514, 515 }, new short[] { 0, 5 }, new uint[] { 0x2108954, 0x12A1164 })]
    [InlineData(161, new short[] { 516, 517, 518, 519, 520 }, new short[] { 0, 5, 1, 1, 3 }, new uint[] { 0x2108954, 0x12A9664, 0x2108954, 0x2108954, 0x2108954 })]
    [InlineData(211, new short[] { 521, 522 }, new short[] { 5, 1 }, new uint[] { 0x1298C68, 0x2108954 })]
    [InlineData(256, new short[] { 523, 524 }, new short[] { 1, 3 }, new uint[] { 0x2108954, 0x2108954 })]
    [InlineData(276, new short[] { 525, 526 }, new short[] { 5, 2 }, new uint[] { 0x12A9664, 0x2108954 })]
    [InlineData(302, new short[] { 527 }, new short[] { 0x10 }, new uint[] { 0x2108954 })]
    public void NativeBatchWaitsForPollThenPreservesOriginalOrderWidthsTypesAndDestinations(ushort phase, short[] files, short[] types, uint[] destinations)
    {
        NativeBattleEffectMemory memory = Memory();
        NativeBattleQuezacotlFirstWindowIoStep step = NativeBattleQuezacotlFirstWindowIo.Advance(memory, phase);
        Assert.True(step.AdvancePhase);
        Assert.Equal(new NativeBattleEffectFileRequest(files[0], destinations[0], 0x6C3580), step.Request);
        byte[] header = step.Memory.Arena!.Read(64, 5);
        Assert.Equal(new byte[] { 1, 0, 1, checked((byte)files.Length), 0 }, header);
        for (int index = 0; index < files.Length; index++)
        {
            byte[] descriptor = step.Memory.Arena.Read(72 + index * 8, 8);
            Assert.Equal(types[index], BinaryPrimitives.ReadInt16LittleEndian(descriptor));
            Assert.Equal(files[index], BinaryPrimitives.ReadInt16LittleEndian(descriptor.AsSpan(2)));
            Assert.Equal(destinations[index], BinaryPrimitives.ReadUInt32LittleEndian(descriptor.AsSpan(4)));
        }
        NativeBattleEffectMemory restored = JsonSerializer.Deserialize<NativeBattleEffectMemory>(JsonSerializer.Serialize(step.Memory))!;
        NativeBattleQuezacotlFirstWindowIoStep busy = NativeBattleQuezacotlFirstWindowIo.Advance(restored, phase);
        Assert.False(busy.AdvancePhase);
        Assert.Null(busy.Request);
        Assert.Equal(header, busy.Memory.Arena!.Read(64, 5));
    }

    [Fact]
    public void BusyPollConsumesByteDelayWithoutAppendingOrAdvancingButDefaultPhaseDoesNotPoll()
    {
        NativeBattleEffectMemory memory = Memory();
        memory = memory with { Arena = memory.Arena!.Write(64, [0, 1, 1, 0, 0]) };
        NativeBattleQuezacotlFirstWindowIoStep waiting = NativeBattleQuezacotlFirstWindowIo.Advance(memory, 30);
        Assert.False(waiting.AdvancePhase);
        Assert.Equal(new byte[] { 0, 0, 1, 0, 0 }, waiting.Memory.Arena!.Read(64, 5));
        NativeBattleQuezacotlFirstWindowIoStep ready = NativeBattleQuezacotlFirstWindowIo.Advance(waiting.Memory, 30);
        Assert.True(ready.AdvancePhase);
        Assert.Equal((short)508, ready.Request!.FileId);
        NativeBattleQuezacotlFirstWindowIoStep defaultBranch = NativeBattleQuezacotlFirstWindowIo.Advance(memory, 31);
        Assert.True(defaultBranch.AdvancePhase);
        Assert.Null(defaultBranch.Request);
        Assert.Equal(new byte[] { 0, 0, 1, 0, 0 }, defaultBranch.Memory.Arena!.Read(64, 5));
    }

    [Theory]
    [InlineData((ushort)69, 0)]
    [InlineData((ushort)69, 1)]
    [InlineData((ushort)212, 0)]
    [InlineData((ushort)212, 1)]
    public void Stream_tail69_and212_advance_without_ready_poll_and_allocate_automatically_even_when_paused(ushort phase, int pause)
    {
        NativeBattleEffectMemory memory = Memory();
        NativeBattleSummonStreamState stream = new(690007, 0x2108954);
        memory = memory with { Arena = memory.Arena!.Write(64, [1, 1, 1, 0, 0]), QuezacotlPause = pause, SummonStream = stream };
        NativeBattleQuezacotlFirstWindowIoStep io = NativeBattleQuezacotlFirstWindowIo.Advance(memory, phase);
        Assert.True(io.AdvancePhase);
        Assert.Null(io.Request);
        Assert.Equal(new byte[] { 1, 1, 1, 0, 0 }, io.Memory.Arena!.Read(64, 5));
        Assert.Same(stream, io.Memory.SummonStream);
        NativeBattleModuleSound request = io.Memory.SummonStream!.Play5018C0(128, 127);
        (NativeGlobalSoundProcess process, NativeBattleModuleSound committed) = request.Commit46B2A0(NativeGlobalSoundProcess.FromBufferlessColdProcess());
        Assert.Equal(0u, committed.Flags);
        Assert.Equal(128u, committed.RawPan);
        Assert.Equal(127u, committed.Volume);
        Assert.Equal((byte)26, committed.ReadCommittedVoice());
        Assert.Equal((byte)2, process.Rotation);
        Assert.Equal((uint?)690007, process.OriginalEffectIds[26]);
        NativeBattleEffectMemory restored = JsonSerializer.Deserialize<NativeBattleEffectMemory>(JsonSerializer.Serialize(io.Memory))!;
        Assert.Equal(request, restored.SummonStream!.Play5018C0(128, 127));
        Assert.Throws<NotSupportedException>(() => request.Commit46B2A0(NativeGlobalSoundProcess.UnknownBufferless()));
    }

    [Fact]
    public void Phase276ResetsItsActualHeadAndImpactFlagOnlyAfterPollIsReady()
    {
        NativeBattleEffectMemory memory = Memory();
        memory = memory with { Arena = memory.Arena!.Write(0x28E50, [7, 0, 0, 0]).Write(64, [0, 1, 1, 0, 0]), QuezacotlSecondaryHeads = memory.QuezacotlSecondaryHeads!.Value.SetItem(3, 0x12345678) };
        NativeBattleQuezacotlFirstWindowIoStep busy = NativeBattleQuezacotlFirstWindowIo.Advance(memory, 276);
        Assert.False(busy.AdvancePhase);
        Assert.Equal(0x12345678u, busy.Memory.QuezacotlSecondaryHeads!.Value[3]);
        Assert.Equal((byte)7, busy.Memory.Arena!.Read(0x28E50, 1)[0]);
        NativeBattleQuezacotlFirstWindowIoStep ready = NativeBattleQuezacotlFirstWindowIo.Advance(busy.Memory, 276);
        Assert.True(ready.AdvancePhase);
        Assert.Equal(0u, ready.Memory.QuezacotlSecondaryHeads!.Value[3]);
        Assert.Equal(new byte[4], ready.Memory.Arena!.Read(0x28E50, 4));
    }

    [Fact]
    public void Phase302WaitsBeforeResettingOnlyThreeHeadsAndPreservesEveryPhysicalTaskAndUnrelatedDescriptor()
    {
        NativeBattleEffectMemory memory = Memory();
        memory = memory with { Arena = memory.Arena!.Write(64, [0, 1, 1, 0, 0]), QuezacotlSecondaryHeads = [11, 22, 33, 44, 55] };
        NativeBattleQuezacotlFirstWindowIoStep busy = NativeBattleQuezacotlFirstWindowIo.Advance(memory, 302);
        Assert.False(busy.AdvancePhase);
        Assert.Equal(new uint[] { 11, 22, 33, 44, 55 }, busy.Memory.QuezacotlSecondaryHeads!.Value);
        NativeBattleQuezacotlFirstWindowIoStep ready = NativeBattleQuezacotlFirstWindowIo.Advance(busy.Memory, 302);
        Assert.True(ready.AdvancePhase);
        Assert.Equal(new uint[] { 0, 0, 0, 44, 55 }, ready.Memory.QuezacotlSecondaryHeads!.Value);
        Assert.Equal(new NativeBattleEffectFileRequest(527, 0x2108954, 0x6C3580), ready.Request);
        NativeBattleEffectTaskPool ribbon = Pool(0x2521728);
        NativeBattleEffectTaskPool particles = Pool(0x2521718);
        NativeBattleEffectTaskPool paths = Pool(0x2521708);
        NativeBattleEffectTaskPool unrelated = Pool(0x2521738);
        NativeBattleOriginalEffectState effect = new(116, 115, new(116, 0x6C3550, 0x6C3560), null, null) { Pools = [ribbon, particles, paths, unrelated] };
        NativeBattleOriginalEffectState reset = NativeBattleQuezacotlFirstWindowIo.ResetPools6C4D3C(effect);
        foreach (NativeBattleEffectTaskPool pool in reset.Pools.Take(3))
        {
            Assert.Null(pool.Head);
            Assert.Null(pool.Tail);
            Assert.Equal(JsonSerializer.Serialize(effect.Pools.Single(before => before.DescriptorAddress == pool.DescriptorAddress).Slots), JsonSerializer.Serialize(pool.Slots));
            Assert.Equal((ushort)1, pool.Slots[0].Flags);
            pool.Validate();
        }
        Assert.Same(unrelated, reset.Pools[3]);
        Assert.Equal(JsonSerializer.Serialize(reset), JsonSerializer.Serialize(JsonSerializer.Deserialize<NativeBattleOriginalEffectState>(JsonSerializer.Serialize(reset))));
        Assert.Throws<NotSupportedException>(() => NativeBattleQuezacotlFirstWindowIo.ResetPools6C4D3C(effect with { Pools = [ribbon, paths] }));

        static NativeBattleEffectTaskPool Pool(uint descriptor)
        {
            NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(descriptor, 0x211ECB8, 32, 2);
            (pool, _) = pool.Append(0x6C7800);
            (pool, _) = pool.Append(0x6C7800);
            return pool with { Slots = pool.Slots.SetItem(0, pool.Slots[0].WriteWord(12, 0xA5A5)) };
        }
    }

    private static NativeBattleEffectMemory Memory()
    {
        NativeBattleEffectMemory memory = NativeBattleEffectIo.Initialize534110(NativeBattleEffectMemory.FromColdProcess().BeginLoad(116), 64);
        return memory with { EffectIo = memory.EffectIo! with { Callback = 0x6C3580 } };
    }

    [Fact]
    public void Phase318_only_waits_for_the_existing_batch_without_queuing_or_resetting_any_list()
    {
        NativeBattleEffectMemory memory = Memory();
        memory = memory with { Arena = memory.Arena!.Write(64, [0, 1, 1, 0, 0]), QuezacotlSecondaryHeads = [11, 22, 33, 44, 55] };
        NativeBattleQuezacotlFirstWindowIoStep waiting = NativeBattleQuezacotlFirstWindowIo.Advance(memory, 318);
        Assert.False(waiting.AdvancePhase);
        Assert.Null(waiting.Request);
        Assert.Equal(new byte[] { 0, 0, 1, 0, 0 }, waiting.Memory.Arena!.Read(64, 5));
        NativeBattleQuezacotlFirstWindowIoStep ready = NativeBattleQuezacotlFirstWindowIo.Advance(waiting.Memory, 318);
        Assert.True(ready.AdvancePhase);
        Assert.Null(ready.Request);
        Assert.Equal(new byte[] { 0, 0, 1, 0, 0 }, ready.Memory.Arena!.Read(64, 5));
        Assert.Equal(new uint[] { 11, 22, 33, 44, 55 }, ready.Memory.QuezacotlSecondaryHeads!.Value);
    }
}
