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
        NativeBattleEffectMemory restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffectMemory>(SnapshotJson(step.Memory))!);
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
        NativeBattleEffectMemory waitingSnapshot = NativeBattleStorageSnapshots.Capture(waiting.Memory);
        NativeBattleQuezacotlFirstWindowIoStep ready = NativeBattleQuezacotlFirstWindowIo.Advance(waiting.Memory, 30);
        Assert.True(ready.AdvancePhase);
        Assert.Equal((short)508, ready.Request!.FileId);
        NativeBattleQuezacotlFirstWindowIoStep defaultBranch = NativeBattleQuezacotlFirstWindowIo.Advance(NativeBattleStorageSnapshots.Restore(waitingSnapshot), 31);
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
        NativeBattleEffectMemory restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffectMemory>(SnapshotJson(io.Memory))!);
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
    public void Phase302WaitsBeforeRetiringThreeTaskViewsAndPreservesTheirPhysicalBodiesForResourceReuse()
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
        NativeBattleEffectArena arena = ready.Memory.Arena!;
        NativeBattleEffectTaskPool ribbon = Pool(0x2521728, 0x211B514, 0x80, 16);
        NativeBattleEffectTaskPool particles = Pool(0x2521718, 0x211BD34, 0x3C, 32);
        NativeBattleEffectTaskPool paths = Pool(0x2521708, 0x211ECB8, 0x20, 128);
        NativeBattleEffectTaskPool unrelated = Pool(0x2521738, NativeBattleEffectNodeMemory.ArenaAddress + 0x1000, 32, 2);
        NativeBattleOriginalEffectState effect = new(116, 115, new(116, 0x6C3550, 0x6C3560), null, null) { Pools = [ribbon, particles, paths, unrelated] };
        string before = SnapshotJson(arena);
        NativeBattleOriginalEffectState reset = NativeBattleQuezacotlFirstWindowIo.ResetPools6C4D3C(effect);
        Assert.Equal(before, SnapshotJson(arena));
        Assert.Same(unrelated, Assert.Single(reset.Pools));
        unrelated.Validate();
        foreach (NativeBattleEffectTaskPool retired in new[] { ribbon, particles, paths })
        {
            Assert.Throws<ObjectDisposedException>(() => retired.Slots[0].ReadWord(12));
        }
        Assert.Throws<NotSupportedException>(() => NativeBattleQuezacotlFirstWindowIo.ResetPools6C4D3C(reset));

        //Resource bytes now own the former task headers; their raw pointer-shaped words are not scheduler links.
        arena.Write(checked((int)(ribbon.BankAddress - NativeBattleEffectNodeMemory.ArenaAddress)), [0x87, 0x8F, 0x11, 0x22, 0x39, 0x32, 0x36, 0x36, 0x42, 0x35, 0x3E, 0x3A]);
        arena.Write(checked((int)(particles.BankAddress - NativeBattleEffectNodeMemory.ArenaAddress)), [0x8F, 0xA2, 0x33, 0x44, 0x43, 0x36, 0x39, 0x39, 0x46, 0x3A, 0x42, 0x3F]);
        NativeBattleQuezacotlWorkspaceWrite captured = NativeBattleStorageSnapshots.Capture(new NativeBattleQuezacotlWorkspaceWrite(ready.Memory, reset));
        string json = JsonSerializer.Serialize(captured);
        NativeBattleQuezacotlWorkspaceWrite first = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleQuezacotlWorkspaceWrite>(json)!);
        NativeBattleQuezacotlWorkspaceWrite second = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleQuezacotlWorkspaceWrite>(json)!);
        Assert.Equal(json, SnapshotJson(first));
        Assert.Equal(json, SnapshotJson(second));
        Assert.Single(first.Effect.Pools).Validate();
        int former = checked((int)(ribbon.BankAddress - NativeBattleEffectNodeMemory.ArenaAddress));
        NativeBattleEffectArena firstArena = first.Memory.Arena!;
        NativeBattleEffectArena secondArena = second.Memory.Arena!;
        Assert.Equal(0x36363239u, firstArena.ReadUInt32(former + 4));
        firstArena.WriteUInt32(former + 4, 77);
        Assert.Equal(0x36363239u, secondArena.ReadUInt32(former + 4));
        Assert.Equal(0x36363239u, arena.ReadUInt32(former + 4));
        NativeBattleEffectTaskPool reused = NativeBattleEffectTaskPool.Initialize(ribbon.DescriptorAddress, ribbon.BankAddress, ribbon.Stride, 16).BindArena(firstArena);
        reused.Append(0x6C8850);
        reused.Validate();
        Assert.Equal((ushort)1, reused.Slots[0].Flags);
        Assert.Equal(0x36363239u, secondArena.ReadUInt32(former + 4));

        NativeBattleEffectTaskPool Pool(uint descriptor, uint bank, ushort stride, ushort capacity)
        {
            NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(descriptor, bank, stride, capacity).BindArena(arena);
            pool.Append(0x6C7800);
            pool.Append(0x6C7800);
            pool.Slots[0].WriteWord(12, 0xA5A5);
            return pool;
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
    private static string SnapshotJson<T>(T value) => JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(value));

}
