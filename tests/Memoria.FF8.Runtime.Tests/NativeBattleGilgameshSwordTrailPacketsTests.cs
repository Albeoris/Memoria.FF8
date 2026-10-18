using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGilgameshSwordTrailPacketsTests
{
    [Fact]
    public void Thirty_pairs_use_current_DPCS_four_FIFO_depths_and_extra36_final_cursor_without_using_point31()
    {
        NativeBattleGilgameshSwordTrailState state = Curves();
        NativeBattleGilgameshSwordTrailPacketResult result = Emit(state);
        Assert.Equal(60, result.Batch.Attempts.Length);
        Assert.Equal(2160, result.Batch.AllocationBytes);
        Assert.Equal(2196, result.PoolCursorAdvance);
        uint source = 0x3A303090;
        Assert.Equal(source, result.Batch.Attempts[0].Primitive!.Words[1]);
        Assert.Equal(source, result.Batch.Attempts[0].Primitive!.Words[3]);
        Assert.Equal(0x3A000000u, result.Batch.Attempts[1].Primitive!.Words[1]);
        Assert.Equal(0x3A000000u, result.Batch.Attempts[1].Primitive!.Words[3]);
        Assert.Equal(NativeBattleGteColorInterpolation.CodeConstants45F270.Interpolate(source, 29 * 4096 / 30, 0), result.Batch.Attempts[58].Primitive!.Words[3]);
        Assert.All(result.Batch.Attempts, attempt =>
        {
            Assert.Equal(new[] { 3000, 3000, 3000, 3000 }, attempt.Primitive!.VertexPayload4.ToArray());
            Assert.All(attempt.WriteMask, mask => Assert.Equal(NativeBattleGfPacketByteState.Known, mask));
        });
        state = state with { Memory = Point(state.Memory, NativeBattleGilgameshSwordTrailState.CurveA, 31, new(0, 0, -4000)) };
        state = state with { Memory = Point(state.Memory, NativeBattleGilgameshSwordTrailState.CurveB, 31, new(0, 0, -4000)) };
        Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(Emit(state)));
    }

    [Fact]
    public void Rejected_pair_only_writes_first_header_reuses_cursor_and_preserves_previous_end_color()
    {
        NativeBattleGilgameshSwordTrailState state = Curves();
        state = state with { Memory = Point(state.Memory, NativeBattleGilgameshSwordTrailState.CurveB, 1, new(100, 0, -4000)) };
        NativeBattleGilgameshSwordTrailPacketResult result = Emit(state);
        Assert.Equal(59, result.Batch.Attempts.Length);
        Assert.Equal(29 * 72, result.Batch.AllocationBytes);
        Assert.Equal(29 * 72 + 36, result.PoolCursorAdvance);
        Assert.Equal(new[] { 3000, 3000, 3000, 0 }, result.Batch.Attempts[0].Primitive!.VertexPayload4.ToArray());
        NativeBattleGfGeometryAttempt skipped = result.Batch.Attempts[2];
        Assert.Null(skipped.Primitive);
        Assert.Equal(72, skipped.ByteOffsetRelative);
        Assert.Equal(72, result.Batch.Attempts[3].ByteOffsetRelative);
        Assert.Equal(0x3A303090u, result.Batch.Attempts[3].Primitive!.Words[1]);
        Assert.Equal(NativeBattleGteColorInterpolation.CodeConstants45F270.Interpolate(0x3A303090, 2 * 4096 / 30, 0), result.Batch.Attempts[3].Primitive!.Words[3]);
        Assert.Equal(0x08000000u, BinaryPrimitives.ReadUInt32LittleEndian(skipped.Data.AsSpan()));
        Assert.All(skipped.WriteMask.Take(4), mask => Assert.Equal(NativeBattleGfPacketByteState.Known, mask));
        Assert.All(skipped.WriteMask.Skip(4), mask => Assert.Equal(NativeBattleGfPacketByteState.Untouched, mask));
    }

    [Fact]
    public void All_rejected_pairs_still_publish_extra36_and_only_overwrite_first_tag_in_actual_common_memory()
    {
        NativeBattleGilgameshSwordTrailState state = Curves();
        NativeBattleGilgameshSwordTrailPacketResult result = NativeBattleGilgameshSwordTrailPackets.Emit(state, View() with { Z = -1000 }, Projection(), null, NativeBattleGteColorInterpolation.CodeConstants45F270, 0x3A303090);
        Assert.Equal(30, result.Batch.Attempts.Length);
        Assert.Equal(0, result.Batch.AllocationBytes);
        Assert.Equal(36, result.PoolCursorAdvance);
        Assert.All(result.Batch.Attempts, attempt => Assert.Equal(0, attempt.ByteOffsetRelative));
        NativeBattlePrimitiveProcess process = NativeBattlePrimitiveProcess.FromColdProcess().Initialize500400() with { PacketBank = "off_B6D070", ByteCursor = 32 };
        process = process.ApplyCommonWrite(32, Enumerable.Repeat((byte)0xA5, 36).ToImmutableArray(), Enumerable.Repeat(NativeBattleGfPacketByteState.Known, 36).ToImmutableArray());
        NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(process, NativeBattleLogicalOrdering.Clear(), result.Batch);
        Assert.Equal(new byte[] { 0, 0, 0, 8 }, written.Process.ReadCommonPacket(32, 4));
        Assert.Equal(Enumerable.Repeat((byte)0xA5, 32).ToArray(), written.Process.ReadCommonPacket(36, 32));
        Assert.Empty(written.Ordering.Proxies);
        Assert.Equal(32, written.Process.ByteCursor);
        Assert.Equal(68, 32 + result.PoolCursorAdvance);
    }

    private static NativeBattleGilgameshSwordTrailPacketResult Emit(NativeBattleGilgameshSwordTrailState state) => NativeBattleGilgameshSwordTrailPackets.Emit(state, View(), Projection(), Registers(), NativeBattleGteColorInterpolation.CodeConstants45F270, 0x3A303090);
    private static NativeBattleActionMatrix View() => new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 3000, 4096);
    private static NativeBattleCameraProjection Projection() => new(null, 288, 160, 108);
    private static NativeBattleScriptedGraphicsMemory Registers() => NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0();

    private static NativeBattleGilgameshSwordTrailState Curves()
    {
        NativeBattleEffectArena memory = NativeBattleEffectArena.Clear();
        for (int index = 0; index < 32; index++)
        {
            memory = Point(memory, NativeBattleGilgameshSwordTrailState.CurveA, index, new((short)(index * 2), -100, 0));
            memory = Point(memory, NativeBattleGilgameshSwordTrailState.CurveB, index, new((short)(index * 2 + 100), 100, 0));
        }
        return new(memory);
    }

    private static NativeBattleEffectArena Point(NativeBattleEffectArena memory, int bank, int index, NativeBattleVector point)
    {
        byte[] bytes = new byte[6];
        BinaryPrimitives.WriteInt16LittleEndian(bytes, point.X);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(2), point.Y);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(4), point.Z);
        return memory.Write(bank + index * 8, bytes).MarkUnknown(bank + index * 8 + 6, 2);
    }
}
