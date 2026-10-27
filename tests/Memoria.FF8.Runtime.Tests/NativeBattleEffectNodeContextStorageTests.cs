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
        using NativeBattleEffectNodeMemory view = new(memory, effect, address, 0, 8, 8, 8);
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
        Assert.Equal(0xAA1234DDu, view.ContextDword(0));
        task.WriteDword(0x38, 0xDEADBEEF);
        Assert.Equal(0u, view.ContextDword(4));
        Assert.Equal((ushort)7, task.State);
        Assert.Contains(task.EnumerateResourcePointers(), entry => entry.Key == 0x50);
        Assert.DoesNotContain(task.EnumerateResourcePointers(), entry => entry.Key == 0x54);
    }

    [Fact]
    public void Auxiliary_view_borrows_original_bytes_and_publishes_only_changed_lanes()
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear();
        arena.WriteUInt32(0, 0x11223344);
        arena.WriteResourcePointer(12, new(null, 0x5678));
        (NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect, uint address) = Fixture(arena);
        Traffic sink = new();
        using NativeBattleEffectNodeMemory view = CreateView();
        Assert.Equal(0L, sink.Bytes);
        arena.WriteUInt32(0, 0x99887766);
        Assert.Equal(0x11223344u, view.Dword(0));
        view.Word(4, 0xABCD);
        view.FlushStorage();
        Assert.Equal(0x99887766u, arena.ReadUInt32(0));
        Assert.Equal((ushort)0xABCD, arena.ReadUInt16(4));
        Assert.True(arena.TryGetResourcePointer(12, out _));
        arena.WriteUInt16(4, 0xEEEE);
        Assert.Equal((ushort)0xABCD, view.Word(4));
        view.Byte(6, 0x99);
        view.FlushStorage();
        Assert.Equal((ushort)0xEEEE, arena.ReadUInt16(4));
        Assert.Equal((byte)0x99, arena.ReadByte(6));

        NativeBattleEffectNodeMemory CreateView()
        {
            using IDisposable scope = NativeBattleTelemetry.BeginScope(sink);
            return new(memory, effect, address, 0, 0x25F8, 0x25F8, 0x16F80);
        }
    }

    [Fact]
    public void Auxiliary_private_suffix_preserves_hidden_bytes_and_forward_dword_overlap()
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().Write(0, [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12]);
        (NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect, uint address) = Fixture(arena);
        using (NativeBattleEffectNodeMemory view = new(memory, effect, address, 0, 8, 8, 8))
        {
            view.Copy(0, 4, 8);
            view.MarkUnknown(5, 1);
            Assert.Throws<NotSupportedException>(() => view.Byte(5));
            Assert.Equal((byte)6, arena.ReadByte(5));
            view.FlushStorage();
            (byte[] data, bool[] known) = arena.ReadSnapshot(4, 8);
            Assert.Equal(new byte[] { 1, 2, 3, 4, 1, 2, 3, 4 }, data);
            Assert.False(known[1]);
            Assert.True(known[5]);
            view.Byte(0, 99);
        }
        Assert.Equal((byte)1, arena.ReadByte(0));
    }

    private static (NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, uint Address) Fixture(NativeBattleEffectArena arena)
    {
        const uint address = 0x3000000;
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x3100000, address, 676, 1);
        pool.Append(0x1234);
        NativeBattleOriginalEffectState effect = new(1, 0, new(1, 0, 0), null, null) { Pools = [pool] };
        return (new(arena, 0, 0, [], 0, 0, 0, 0), effect, address);
    }

    private sealed class Traffic : INativeBattleTelemetrySink
    {
        internal long Bytes { get; private set; }

        public void Record(int siteId, NativeBattleTrafficKind kind, long payloadBytes)
        {
            Bytes += payloadBytes;
        }
    }
}
