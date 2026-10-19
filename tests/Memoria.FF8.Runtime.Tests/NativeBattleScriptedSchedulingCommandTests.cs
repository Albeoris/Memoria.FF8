using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleScriptedSchedulingCommandTests
{
    [Theory]
    [InlineData(0x0007, 0xA5)]
    [InlineData(0x8007, 0xFF)]
    [InlineData(0x4007, 0)]
    [InlineData(0xC007, 0)]
    public void Immunity_marks_the_live_order_entry_and_both_mode_bits_leave_filter_clear(ushort raw, byte filter)
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create().WriteTemporaryWord(0x14A, raw).WriteTemporaryByte(0x144, 2).WriteTemporaryByte(0x145, 0xA5);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797450, 0x1000);
        frame = frame with { Core = frame.Core.Write(0x2797454, [4, 8, 0x27, 0xFF]) };
        NativeBattleWaterExecutionFrame result = NativeBattleScriptedSchedulingCommands.Execute(frame, new(0, 0, 0));
        Assert.Equal(new byte[] { 4, 8, 0xA7, 0xFF }, result.Core.Read(0x2797454, 4));
        Assert.Equal(filter, result.ReadTemporaryByte(0x145));
        Assert.Equal(0x1002u, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
        NativeBattleWaterExecutionFrame restored = JsonSerializer.Deserialize<NativeBattleWaterExecutionFrame>(JsonSerializer.Serialize(frame))!;
        Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(NativeBattleScriptedSchedulingCommands.Execute(restored, new(0, 0, 0))));
    }

    [Fact]
    public void Plain_immunity_does_not_consume_an_unknown_filter_or_any_program_operands()
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create().WriteTemporaryWord(0x14A, 7).WriteTemporaryByte(0x144, 0);
        bool[] known = frame.TemporaryFrame.Known.ToArray();
        known[0x145] = false;
        frame = frame with { TemporaryFrame = frame.TemporaryFrame with { Known = known.ToImmutableArray() }, Core = frame.Core.Write(0x2797454, [1, 0xFF]) };
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797450, 0x1000);
        NativeBattleWaterExecutionFrame result = NativeBattleScriptedSchedulingCommands.Execute(frame, new(0, 0, 0));
        Assert.False(result.TemporaryFrame.Known[0x145]);
        Assert.Equal(0x81, result.Core.Read(0x2797454, 1)[0]);
    }

    [Theory]
    [InlineData(0, -4, 0x1FFCu)]
    [InlineData(0, 0x7A, 0x207Au)]
    [InlineData(0x2000, -4, 0x2004u)]
    public void Mode_jump_uses_signed_relative_offset_only_when_the_context_bit_is_clear(ushort flags, short displacement, uint expected)
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create().WriteTemporaryWord(0x14A, 0xA0);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797450, 0x2000);
        frame = WaterSchedulerFixture.CoreWord(frame, 0x2797690, flags);
        byte[] instruction = new byte[4];
        BinaryPrimitives.WriteUInt16LittleEndian(instruction, 0xA0);
        BinaryPrimitives.WriteInt16LittleEndian(instruction.AsSpan(2), displacement);
        frame = frame with { Arena = frame.Arena.Write(0x1000, instruction) };
        if (flags != 0)
        {
            frame = frame with { Arena = frame.Arena.MarkUnknown(0x1002, 2) };
        }
        NativeBattleWaterExecutionFrame result = NativeBattleScriptedSchedulingCommands.Execute(frame, new(0x1000, 4, 0x2000));
        Assert.Equal(expected, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
    }
}
