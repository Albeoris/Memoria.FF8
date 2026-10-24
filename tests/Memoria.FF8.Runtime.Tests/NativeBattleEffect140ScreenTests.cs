using System.Buffers.Binary;
using static Memoria.FF8.Runtime.Tests.NativeBattleEffect140LowerFixture;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect140ScreenTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Zero_displacement_reserves_all_212_holes_without_packet_writes_or_delivery(int side)
    {
        NativeBattleEffect140Frame frame = Frame();
        frame = frame with { State = frame.State.WriteDword(0x2517B0C, 0x20DFAB8 + 100), Memory = frame.Memory with { PrimitiveProcess = frame.Memory.PrimitiveProcess! with { Buffer80 = (byte)side }, Arena = frame.Memory.Arena!.MarkUnknown(100, 212 * 24) } };
        NativeBattleEffect140Frame result = NativeBattleEffect140Screen.Scroll(frame, 0, 0, 3, null!, Inputs((_, _) => throw new InvalidOperationException()));
        Assert.Equal(0x20DFAB8u + 100 + 212 * 24, result.State.ReadDword(0x2517B0C));
        Assert.Same(frame.Memory.Arena, result.Memory.Arena);
        Assert.Equal(frame.Random, result.Random);
    }

    [Theory]
    [InlineData(0, 320)]
    [InlineData(1, 0)]
    public void Positive_scan_writes_shift_then_scratch_copy_in_prepend_order_and_preserves_real_side(int side, int baseX)
    {
        NativeBattleEffect140Frame frame = Frame();
        frame = frame with { State = frame.State.WriteDword(0x2517B0C, 0x20DFAB8 + 100), Memory = frame.Memory with { PrimitiveProcess = frame.Memory.PrimitiveProcess! with { Buffer80 = (byte)side } } };
        List<(uint Cursor, NativeBattleEffect140DrawBatch Draw)> draws = [];
        NativeBattleLogicalOrdering ordering = NativeBattleLogicalOrdering.Clear();
        NativeBattleEffect140Inputs inputs = Inputs((current, operation) =>
        {
            NativeBattleEffect140DrawBatch draw = Assert.IsType<NativeBattleEffect140DrawBatch>(operation);
            draws.Add((current.State.ReadDword(draw.CursorGlobal), draw));
            int offset = checked((int)(current.State.ReadDword(draw.CursorGlobal) - 0x20DFAB8));
            NativeBattleEffectPacketWrite appended = NativeBattleEffectPacketArena.Append(current.Memory, offset, ordering, draw.Batch, draw.Flags);
            ordering = appended.Ordering;
            return current with { Memory = appended.Memory, State = current.State.WriteDword(draw.CursorGlobal, 0x20DFAB8u + (uint)appended.Cursor) };
        });
        NativeBattleEffect140Frame result = NativeBattleEffect140Screen.Scroll(frame, 4096, 1024, 3, null!, inputs);
        Assert.True(draws.Count > 2);
        Assert.Equal(0x20DFAB8u + 124, draws[0].Cursor);
        Assert.Equal(draws[0].Cursor + 24, draws[1].Cursor);
        uint[] first = Words(draws[0].Draw);
        uint[] second = Words(draws[1].Draw);
        int displacement = unchecked(NativeBattleCameraMath.Sine(1152) * 4096) >> 20;
        Assert.Equal(0x01000000u, first[1]);
        Assert.Equal(0x80000000u, first[2]);
        Assert.Equal(320u | 251u << 16, first[3]);
        Assert.Equal((uint)(baseX + displacement) | 9u << 16, first[4]);
        Assert.Equal((uint)(320 - displacement) | 1u << 16, first[5]);
        Assert.Equal((uint)baseX | 9u << 16, second[3]);
        Assert.Equal(320u | 251u << 16, second[4]);
        Assert.All(draws, item =>
        {
            Assert.Equal(0, item.Draw.Flags);
            Assert.Equal(3, item.Draw.Batch.Attempts[0].Primitive!.Bucket);
            Assert.Equal(NativeBattleGfPacketByteState.Untouched, item.Draw.Batch.Attempts[0].WriteMask[0]);
        });
        Assert.True(result.State.ReadDword(0x2517B0C) >= 0x20DFAB8u + 100 + 212 * 24);
        Assert.Equal((byte)side, result.Memory.PrimitiveProcess!.Buffer80);
    }

    [Fact]
    public void Fullviewport_fade_uses_signed_truncation_and_flag_one()
    {
        NativeBattleEffect140DrawBatch? emitted = null;
        NativeBattleEffect140Frame frame = Frame();
        NativeBattleEffect140Inputs inputs = Inputs((current, operation) =>
        {
            emitted = Assert.IsType<NativeBattleEffect140DrawBatch>(operation);
            return current;
        });
        _ = NativeBattleEffect140Screen.Fade(frame, 127, 128, 255, 1, 3, null!, inputs);
        Assert.NotNull(emitted);
        Assert.Equal(1, emitted.Flags);
        Assert.Equal(24, emitted.Batch.AllocationBytes);
        Assert.Equal(0x2AAA5554u, Words(emitted)[1]);
        _ = NativeBattleEffect140Screen.Fade(frame, 255, 255, 255, 3, 3, null!, Inputs((_, _) => throw new InvalidOperationException()));
    }

    private static uint[] Words(NativeBattleEffect140DrawBatch draw) => Enumerable.Range(0, 6).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(draw.Batch.Attempts[0].Data.AsSpan().Slice(index * 4))).ToArray();
}
