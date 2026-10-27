namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffectNodeContextStorageTests
{
    [Fact]
    public void Context_stores_stage_overlaps_and_publish_only_touched_bytes_without_replacing_headers_or_unknown_lanes()
    {
        const uint address = 0x3000000;
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x3100000, address, 676, 1);
        pool.Append(0x1234);
        NativeBattleModulePoolTask task = pool.Slots[0];
        task.WriteDword(0x34, 0x11223344).MarkPayloadUnknown(0x35, 1);
        task.WriteResourcePointer(0x50, new(null, 0x1234));
        task.WriteResourcePointer(0x54, new(null, 0x5678));
        NativeBattleOriginalEffectState effect = new(1, 0, new(1, 0, 0), null, null) { Pools = [pool] };
        NativeBattleEffectMemory memory = new(NativeBattleEffectArena.Clear(), 0, 0, [], 0, 0, 0, 0);
        NativeBattleEffectNodeMemory view = new(memory, effect, address, 0, 8, 8, 8);
        view.ContextDword(0, 0xAABBCCDD);
        view.ContextWord(1, 0x1234);
        view.ContextDword(0x20, 0x5678);
        Assert.Equal(0xAA1234DDu, view.ContextDword(0));
        Assert.Equal(0x11223344u, System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(task.ReadRawPayloadSpan().Slice(0x34 - 12)));
        task.UpdateHeader(task.Flags, 7, task.Next, task.Callback);
        view.FlushStorage();
        Span<byte> data = stackalloc byte[4];
        Span<bool> known = stackalloc bool[4];
        task.ReadWithKnownness(0x34, data, known);
        Assert.Equal(0xAA1234DDu, System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(data));
        Assert.False(known[1]);
        Assert.True(known[0]);
        Assert.Equal((ushort)7, task.State);
        Assert.Contains(task.EnumerateResourcePointers(), entry => entry.Key == 0x50);
        Assert.DoesNotContain(task.EnumerateResourcePointers(), entry => entry.Key == 0x54);
    }
}
