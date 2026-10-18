using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleWaterVectorCommandTests
{
    [Theory]
    [InlineData(10, 0x50, 4, 0, NativeBattleWaterVectorPostAction.RefreshPosition)]
    [InlineData(11, 0x68, 4, 0, NativeBattleWaterVectorPostAction.None)]
    [InlineData(12, 0x80, 2, 0, NativeBattleWaterVectorPostAction.RefreshAcceleration)]
    [InlineData(13, 0x50, 4, 1, NativeBattleWaterVectorPostAction.RefreshPosition)]
    [InlineData(14, 0x68, 4, 1, NativeBattleWaterVectorPostAction.None)]
    [InlineData(15, 0x80, 2, 1, NativeBattleWaterVectorPostAction.RefreshAcceleration)]
    [InlineData(16, 0x50, 4, 2, NativeBattleWaterVectorPostAction.RefreshPosition)]
    [InlineData(17, 0x68, 4, 0, NativeBattleWaterVectorPostAction.None)]
    [InlineData(18, 0x50, 4, 3, NativeBattleWaterVectorPostAction.RefreshPosition)]
    [InlineData(19, 0x68, 4, 3, NativeBattleWaterVectorPostAction.None)]
    [InlineData(20, 0x80, 2, 3, NativeBattleWaterVectorPostAction.RefreshAcceleration)]
    [InlineData(21, 0x68, 4, 3, NativeBattleWaterVectorPostAction.None)]
    [InlineData(22, 0x50, 4, 4, NativeBattleWaterVectorPostAction.RefreshPosition)]
    [InlineData(23, 0x50, 4, 5, NativeBattleWaterVectorPostAction.RefreshPosition)]
    public void Fourteen_finite_layouts_write_six_lanes_only_and_preserve_exact_operand_and_random_counts(int opcode, int destination, int stride, int operation, NativeBattleWaterVectorPostAction action)
    {
        int operands = operation is 1 or 4 ? 1 : operation == 5 ? 12 : 6;
        ImmutableArray<byte> slot = Enumerable.Repeat((byte)0xA5, 0x100).ToImmutableArray();
        ImmutableArray<byte> program = Program(opcode, 0xFC00, Enumerable.Repeat((short)32767, operands).ToArray(), 3);
        NativeBattleWaterVectorResult result = NativeBattleWaterVectorCommands.ExecuteAA1810(slot, program, 3, new(1));
        Assert.Equal(3 + 2 + operands * 2, result.NextOffset);
        Assert.Equal(action, result.PostAction);
        int draws = operation == 3 ? 6 : operation == 4 ? 1 : operation == 5 ? 12 : 0;
        Assert.Equal(AdvanceRandom(draws), result.Random);
        for (int index = 0; index < slot.Length; index++)
        {
            if (index < destination || index >= destination + 6 * stride)
            {
                Assert.Equal(slot[index], result.Slot[index]);
            }
        }
        for (int lane = 0; lane < 6; lane++)
        {
            Assert.False(slot.AsSpan().Slice(destination + lane * stride, stride).SequenceEqual(result.Slot.AsSpan().Slice(destination + lane * stride, stride)));
        }
    }

    [Theory]
    [InlineData(0x0000, 0)]
    [InlineData(0x0200, 0)]
    [InlineData(0x8000, 1)]
    [InlineData(0x4000, 1)]
    [InlineData(0x2000, 1)]
    [InlineData(0x1000, 1)]
    [InlineData(0x0800, 1)]
    [InlineData(0x0400, 1)]
    [InlineData(0xE000, 3)]
    [InlineData(0x1C00, 3)]
    [InlineData(0xFC00, 6)]
    public void Lane_masks_cover_both_triplets_and_only_selected_lanes_consume_operands(int mask, int selected)
    {
        NativeBattleWaterVectorResult result = Execute(10, mask, new byte[0x100].ToImmutableArray(), Enumerable.Repeat((short)1, selected).ToArray());
        Assert.Equal(2 + selected * 2, result.NextOffset);
        for (int lane = 0; lane < 6; lane++)
        {
            Assert.Equal((mask & (0x8000 >> lane)) == 0 ? 0u : 0x10000u, U32(result.Slot, 0x50 + lane * 4));
        }
        Assert.Equal(new NativeCrtRandomState(1), result.Random);
    }

    [Fact]
    public void Sentinel_is_only_assignment0_and_add2_and_WORD_assignment_uses_arithmetic_shift()
    {
        ImmutableArray<byte> slot = Enumerable.Repeat((byte)0xA5, 0x100).ToImmutableArray();
        Assert.Equal(0xA5A5A5A5u, U32(Execute(10, 0x8000, slot, 0x7654).Slot, 0x50));
        Assert.Equal(0xA5A5A5A5u, U32(Execute(16, 0x8000, slot, 0x7654).Slot, 0x50));
        Assert.Equal(0x76540000u, U32(Execute(13, 0x8400, slot, 0x7654).Slot, 0x50));
        Assert.Equal((ushort)0x0765, U16(Execute(15, 0x8000, slot, 0x7654).Slot, 0x80));
        Assert.Equal(ushort.MaxValue, U16(Execute(12, 0x8000, slot, -1).Slot, 0x80));
        NativeBattleWaterVectorResult randomSentinel = Execute(18, 0x8000, slot, 0x7654);
        Assert.Equal(AdvanceRandom(1), randomSentinel.Random);
        Assert.NotEqual(0xA5A5A5A5u, U32(randomSentinel.Slot, 0x50));
    }

    [Fact]
    public void DWORD_and_WORD_additions_wrap_at_their_physical_store_widths()
    {
        byte[] slot = new byte[0x100];
        BinaryPrimitives.WriteUInt32LittleEndian(slot.AsSpan(0x50), 0xFFFF0000);
        BinaryPrimitives.WriteUInt16LittleEndian(slot.AsSpan(0x80), 100);
        Assert.Equal(0u, U32(Execute(16, 0x8000, slot.ToImmutableArray(), 1).Slot, 0x50));
        Assert.Equal(unchecked((ushort)-540), U16(Execute(20, 0x8000, slot.ToImmutableArray(), 32767).Slot, 0x80));
        Assert.Equal(0x0100u, U32(Execute(11, 0x8000, slot.ToImmutableArray(), 1).Slot, 0x68));
        Assert.Equal(0x10000u, U32(Execute(17, 0x8000, slot.ToImmutableArray(), 1).Slot, 0x68));
    }

    [Fact]
    public void Zero_random_operands_and_empty_masks_have_distinct_native_consumption_rules()
    {
        ImmutableArray<byte> slot = new byte[0x100].ToImmutableArray();
        Assert.Equal(AdvanceRandom(2), Execute(18, 0xFC00, slot, 0, 32767, 0, 0, 32767, 0).Random);
        Assert.Equal(AdvanceRandom(1), Execute(22, 0, slot, 0).Random);
        Assert.Equal(4, Execute(22, 0, slot, 0).NextOffset);
        Assert.Equal(4, Execute(13, 0, slot, 7).NextOffset);
        Assert.Equal(2, Execute(23, 0, slot).NextOffset);
        Assert.Equal(AdvanceRandom(0), Execute(23, 0, slot).Random);
        Assert.Equal(NativeBattleWaterVectorPostAction.RefreshPosition, Execute(22, 0, slot, 0).PostAction);
        Assert.Equal(NativeBattleWaterVectorPostAction.RefreshAcceleration, Execute(12, 0, slot).PostAction);
        Assert.Throws<InvalidDataException>(() => Execute(13, 0, slot));
    }

    [Theory]
    [InlineData(10000, 0xEA780000u)]
    [InlineData(-10000, 0x15890000u)]
    public void Operation5_discards_first_random_return_and_uses_literal_first_operand_with_second_sign(short second, uint expected)
    {
        NativeBattleWaterVectorResult result = Execute(23, 0x8000, new byte[0x100].ToImmutableArray(), 123, second);
        Assert.Equal(expected, U32(result.Slot, 0x50));
        Assert.Equal(AdvanceRandom(2), result.Random);
        Assert.Equal(6, result.NextOffset);
    }

    [Fact]
    public void Negative_random_operand_uses_logical_product_shift_not_arithmetic_shift()
    {
        NativeBattleWaterVectorRandom draw = NativeBattleWaterVectorCommands.RandomB65740(-1, new(1));
        Assert.Equal(-131071, draw.Value);
        Assert.Equal(AdvanceRandom(1), draw.Random);
    }

    [Fact]
    public void JSON_next_keeps_retained_bytes_and_explicit_random_state_without_executing_a_post_action()
    {
        ImmutableArray<byte> first = Program(11, 0x8000, [7]);
        ImmutableArray<byte> second = Program(19, 0x0400, [-1]);
        ImmutableArray<byte> program = first.AddRange(second);
        NativeBattleWaterVectorResult current = NativeBattleWaterVectorCommands.ExecuteAA1810(Enumerable.Repeat((byte)0xA5, 0x100).ToImmutableArray(), program, 0, new(1));
        Assert.Equal(NativeBattleWaterVectorPostAction.None, current.PostAction);
        NativeBattleWaterVectorResult restored = JsonSerializer.Deserialize<NativeBattleWaterVectorResult>(JsonSerializer.Serialize(current))!;
        NativeBattleWaterVectorResult next = NativeBattleWaterVectorCommands.ExecuteAA1810(current.Slot, program, current.NextOffset, current.Random);
        Assert.Equal(JsonSerializer.Serialize(next), JsonSerializer.Serialize(NativeBattleWaterVectorCommands.ExecuteAA1810(restored.Slot, program, restored.NextOffset, restored.Random)));
        Assert.Equal(0x700u, U32(next.Slot, 0x68));
        Assert.Equal((byte)0xA5, next.Slot[0x18]);
        Assert.Throws<NotSupportedException>(() => Execute(9, 0, current.Slot));
        Assert.Throws<NotSupportedException>(() => Execute(24, 0, current.Slot));
    }

    private static NativeBattleWaterVectorResult Execute(int opcode, int mask, ImmutableArray<byte> slot, params short[] operands) => NativeBattleWaterVectorCommands.ExecuteAA1810(slot, Program(opcode, mask, operands), 0, new(1));
    private static ImmutableArray<byte> Program(int opcode, int mask, short[] operands, int prefix = 0)
    {
        byte[] bytes = new byte[prefix + 2 + operands.Length * 2];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(prefix), (ushort)(opcode | mask));
        for (int index = 0; index < operands.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(prefix + 2 + index * 2), operands[index]);
        }
        return bytes.ToImmutableArray();
    }
    private static NativeCrtRandomState AdvanceRandom(int count)
    {
        NativeCrtRandomState random = new(1);
        for (int index = 0; index < count; index++)
        {
            random = random.Draw(out _);
        }
        return random;
    }
    private static ushort U16(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan().Slice(offset));
    private static uint U32(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan().Slice(offset));
}
