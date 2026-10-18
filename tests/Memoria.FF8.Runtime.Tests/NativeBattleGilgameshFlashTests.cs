using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGilgameshFlashTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(3, 191)]
    [InlineData(4, 255)]
    [InlineData(7, 255)]
    [InlineData(8, 255)]
    [InlineData(23, 16)]
    [InlineData(-1, 193)]
    [InlineData(-32768, 0)]
    public void Envelope_uses_signed_counter_and_truncates_toward_zero(short counter, byte expected)
    {
        NativeBattleGilgameshFlashStep result = NativeBattleGilgameshFlash.Step592BC0(Task(counter), 0);
        Assert.Equal(0x62000000u | expected | (uint)expected << 8 | (uint)expected << 16, result.Batch.Attempts[0].Primitive!.Words[1]);
        Assert.Equal(unchecked((ushort)(counter + 1)), result.UpdatedTask.ReadWord(12));
        Assert.Equal(counter == 23 ? 2 : 0, result.Result);
    }

    [Theory]
    [InlineData(1u)]
    [InlineData(0x200u)]
    public void Paused_callback_draws_without_changing_task(uint flags)
    {
        NativeBattleModulePoolTask task = Task(23);
        NativeBattleGilgameshFlashStep result = NativeBattleGilgameshFlash.Step592BC0(task, flags);
        Assert.Same(task, result.UpdatedTask);
        Assert.Equal(0, result.Result);
        Assert.Equal(24, result.Batch.AllocationBytes);
    }

    [Fact]
    public void Producer_stores_all24_bytes_in_physical_order_and_preserves_known_or_unknown_precision()
    {
        NativeBattleGfGeometryBatch batch = NativeBattleGilgameshFlash.Emit592C60(0x101, -1, 0x123, -2, [11, 22, 33, 44]);
        Assert.Equal(24, batch.AllocationBytes);
        Assert.Equal(new[] { 0, 16 }, batch.Attempts.Select(attempt => attempt.ByteOffsetRelative).ToArray());
        Assert.Equal(new uint[] { 0x03000000, 0x6223FF01, 0, 0x06C00A00 }, batch.Attempts[0].Primitive!.Words.ToArray());
        Assert.Equal(new uint[] { 0x01000000, 0xE1000220 }, batch.Attempts[1].Primitive!.Words.ToArray());
        Assert.Equal(new[] { 11, 22, 33, 44 }, batch.Attempts[0].Primitive!.VertexPayload4.ToArray());
        Assert.Empty(batch.Attempts[1].Primitive!.VertexPayload4);
        NativeBattleLogicalOrdering.Builder links = new(NativeBattleLogicalOrdering.Clear());
        foreach (NativeBattleGfGeometryAttempt attempt in batch.Attempts)
        {
            Assert.All(attempt.WriteMask, state => Assert.Equal(NativeBattleGfPacketByteState.Known, state));
            NativeBattleGeometryPrimitive primitive = attempt.Primitive!;
            Assert.Equal(15, primitive.Bucket);
            links.Prepend(primitive.Bucket, new("test", 0, attempt.ByteOffsetRelative), primitive.Words, 0, primitive.VertexPayload4);
        }
        NativeBattleLogicalOrdering ordering = links.ToImmutable();
        Assert.Equal(new NativeBattlePrimitiveLink(true, 1), ordering.Table[15]);
        Assert.Equal(new NativeBattlePrimitiveLink(true, 0), ordering.Packets[1].Next);
        Assert.All(ordering.Proxies, proxy => Assert.Equal((ushort)0, proxy.Flags20));
    }

    [Fact]
    public void Counter_wrap_and_json_restore_keep_exact_next_callback()
    {
        NativeBattleModulePoolTask task = Task(short.MaxValue);
        NativeBattleGilgameshFlashStep first = NativeBattleGilgameshFlash.Step592BC0(task, 0);
        Assert.Equal((ushort)0x8000, first.UpdatedTask.ReadWord(12));
        Assert.Equal(0, first.Result);
        NativeBattleModulePoolTask restored = JsonSerializer.Deserialize<NativeBattleModulePoolTask>(JsonSerializer.Serialize(first.UpdatedTask))!;
        Assert.Equal(JsonSerializer.Serialize(NativeBattleGilgameshFlash.Step592BC0(first.UpdatedTask, 0)), JsonSerializer.Serialize(NativeBattleGilgameshFlash.Step592BC0(restored, 0)));
    }

    [Fact]
    public void Common_append_links_E1_before_tile_and_advances_actual_cursor_by24()
    {
        NativeBattlePrimitiveProcess process = NativeBattlePrimitiveProcess.FromColdProcess().Initialize500400() with { PacketBank = "off_B6D070", ByteCursor = 32 };
        NativeBattleGfGeometryBatch batch = NativeBattleGilgameshFlash.Emit592C60(1, 2, 3, 0);
        NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(process, NativeBattleLogicalOrdering.Clear(), batch);
        Assert.Equal(56, written.Process.ByteCursor);
        Assert.Equal(new byte[] { 1, 2, 3, 0x62, 0, 0, 0, 0, 0, 10, 0xC0, 6 }, written.Process.ReadCommonPacket(36, 12));
        Assert.Equal(new byte[] { 0x20, 2, 0, 0xE1 }, written.Process.ReadCommonPacket(52, 4));
        Assert.Equal(new NativeBattlePrimitiveLink(true, 1), written.Ordering.Table[17]);
        Assert.Equal(new NativeBattlePrimitiveLink(true, 0), written.Ordering.Packets[1].Next);
        Assert.All(written.Ordering.Proxies, proxy => Assert.Empty(proxy.VertexPayload4));
    }
    private static NativeBattleModulePoolTask Task(short counter) => new NativeBattleModulePoolTask(5, 7, null, 0x592BC0, Enumerable.Repeat((byte)0xA5, 24).ToImmutableArray()).WriteWord(12, unchecked((ushort)counter));
}

