using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleScriptedLightCommandsTests
{
    private static readonly NativeBattleWaterSlotBank Bank = new(0x2000, 0x400, 4, 0x2000);
    private static readonly NativeBattleWaterScriptProgram Program = new(0x1000, 10, 0x1000);
    private const uint Set = 0x27977F4;

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, 0)]
    [InlineData(0xFF, 0xFF)]
    [InlineData(0x100, 0)]
    [InlineData(0x80, 0x80)]
    [InlineData(0x10, 0x10)]
    public void Selection_uses_the_full_signed_operand_and_preserves_parent_relative_and_set_zero_modes(short operand, byte expected)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0xF0AA, operand, -1, -1, -1);
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x1004, 6).MarkUnknown(0x208C, 14) };
        NativeBattleWaterExecutionFrame result = Execute(frame);
        Assert.Equal(expected, result.Arena.Read(0x20E1, 1)[0]);
        Assert.Equal(0x1004u, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
        Assert.Equal(frame.ReadTemporaryWord(0x13E), result.ReadTemporaryWord(0x13E));
    }

    [Fact]
    public void Construction_preserves_unselected_columns_and_padding_and_uses_position_rows_with_angle_columns()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x93, 1, 101, -1, 102);
        frame = frame with { Core = frame.Core.MarkUnknown(Set + 0x22, 2).MarkUnknown(Set + 0x28, 2).MarkUnknown(Set + 0x2E, 2).MarkUnknown(Set + 0x32, 2).Write(Set + 0x14, Enumerable.Repeat((byte)0x71, 12).ToArray()).Write(Set + 0x34, Enumerable.Repeat((byte)0x82, 12).ToArray()).Write(Set + 0x4C, [1, 2, 3, 4]) };
        NativeBattleWaterExecutionFrame result = Execute(frame);
        short[] rows = [16, -32, 48, 0, 0, 0, -64, 80, -96];
        for (int lane = 0; lane < rows.Length; lane++)
        {
            Assert.Equal(unchecked((ushort)rows[lane]), result.ReadTemporaryWord(0x20 + lane * 2));
        }
        Assert.Equal((ushort)0x100, result.ReadTemporaryWord(0x50));
        Assert.Equal((ushort)0x200, result.ReadTemporaryWord(0x52));
        Assert.Equal((ushort)0x300, result.ReadTemporaryWord(0x54));
        NativeBattleActionMatrix basis = new([.. rows], 0, 0, 0, 4096);
        NativeBattleActionMatrix expected = NativeBattleGfMatrices.Multiply56C090(basis, NativeBattleGfMatrices.Euler56CD50(new(0x100, 0x200, 0x300)));
        for (int lane = 0; lane < 9; lane++)
        {
            Assert.Equal(unchecked((ushort)expected.Rotation[lane]), Word(result, Set + (uint)(2 * lane)));
        }
        for (int row = 0; row < 3; row++)
        {
            Assert.Equal(unchecked((ushort)((7 + row) << 4)), Word(result, Set + 0x20u + (uint)(6 * row)));
            Assert.Equal(unchecked((ushort)((-10 - row) << 4)), Word(result, Set + 0x24u + (uint)(6 * row)));
            Assert.Throws<NotSupportedException>(() => result.Core.Read(Set + 0x22u + (uint)(6 * row), 2));
        }
        Assert.Throws<NotSupportedException>(() => result.Core.Read(Set + 18, 2));
        Assert.Throws<NotSupportedException>(() => result.Core.Read(Set + 0x32, 2));
        Assert.Equal(frame.Core.Read(Set + 0x14, 12), result.Core.Read(Set + 0x14, 12));
        Assert.Equal(frame.Core.Read(Set + 0x34, 12), result.Core.Read(Set + 0x34, 12));
        Assert.Equal(frame.Core.Read(Set + 0x4C, 4), result.Core.Read(Set + 0x4C, 4));
        Assert.Equal(unchecked((uint)(-3 << 4)), WaterSchedulerFixture.ReadCoreDword(result, Set + 0x40));
        Assert.Equal(64u, WaterSchedulerFixture.ReadCoreDword(result, Set + 0x44));
        Assert.Equal(unchecked((uint)(-5 << 4)), WaterSchedulerFixture.ReadCoreDword(result, Set + 0x48));
        Assert.Equal(frame.Arena.Read(0x2000, 0x300), result.Arena.Read(0x2000, 0x300));
        Assert.Equal(0x100Au, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
    }

    [Fact]
    public void Current_position_high_bytes_are_unconsumed_and_json_restoration_retains_sparse_color_columns()
    {
        NativeBattleWaterExecutionFrame first = Fixture(0x93, 1, -1, -1, -1);
        first = first with { Core = first.Core.MarkUnknown(Set + 0x20, 20), Arena = first.Arena.MarkUnknown(0x2095, 1).MarkUnknown(0x2097, 1).MarkUnknown(0x2099, 1) };
        NativeBattleWaterExecutionFrame restored = JsonSerializer.Deserialize<NativeBattleWaterExecutionFrame>(JsonSerializer.Serialize(first))!;
        Assert.Equal(JsonSerializer.Serialize(Execute(first)), JsonSerializer.Serialize(Execute(restored)));
        NativeBattleWaterExecutionFrame result = Execute(first);
        Assert.All(result.Core.Read(Set, 18), value => Assert.Equal(0, value));
        Assert.Throws<NotSupportedException>(() => result.Core.Read(Set + 0x20, 1));
    }

    private static NativeBattleWaterExecutionFrame Fixture(ushort instruction, params short[] operands)
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create().WriteTemporaryWord(0x14A, instruction);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797450, 0x1000);
        frame = frame with { Core = frame.Core.Write(0x2797454, [0, 1, 2, 0xFF]) };
        byte[] program = new byte[10];
        BinaryPrimitives.WriteUInt16LittleEndian(program, instruction);
        for (int index = 0; index < operands.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(program.AsSpan(2 + 2 * index), operands[index]);
        }
        frame = frame with { Arena = frame.Arena.Write(0x1000, program) };
        for (int index = 0; index < 3; index++)
        {
            frame = WaterSchedulerFixture.SlotWord(frame, index, 0x12, checked((ushort)(100 + index)));
        }
        frame = Vector(frame, 0, 0x94, new(0x7110, 0x7220, 0x7330));
        frame = Vector(frame, 0, 0x8C, new(-3, 4, -5));
        frame = Vector(frame, 1, 0x94, new(1, -2, 3));
        frame = Vector(frame, 1, 0x8C, new(7, 8, 9));
        frame = Vector(frame, 2, 0x94, new(-4, 5, -6));
        return Vector(frame, 2, 0x8C, new(-10, -11, -12));
    }

    private static NativeBattleWaterExecutionFrame Vector(NativeBattleWaterExecutionFrame frame, int index, int offset, NativeBattleVector value)
    {
        frame = WaterSchedulerFixture.SlotWord(frame, index, offset, unchecked((ushort)value.X));
        frame = WaterSchedulerFixture.SlotWord(frame, index, offset + 2, unchecked((ushort)value.Y));
        return WaterSchedulerFixture.SlotWord(frame, index, offset + 4, unchecked((ushort)value.Z));
    }

    private static ushort Word(NativeBattleWaterExecutionFrame frame, uint address) => BinaryPrimitives.ReadUInt16LittleEndian(frame.Core.Read(address, 2));
    private static NativeBattleWaterExecutionFrame Execute(NativeBattleWaterExecutionFrame frame) => NativeBattleScriptedLightCommands.Execute(frame, Program, Bank, 0);
}
