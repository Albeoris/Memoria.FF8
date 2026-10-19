using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleScriptedSchedulingCommandTests
{
    [Theory]
    [InlineData(1u, 0)]
    [InlineData(100u, -1)]
    [InlineData(0xFFFFFFFFu, 1)]
    public void Random_jump_draws_once_and_compares_the_absolute_value_inclusively(uint seed, int adjustment)
    {
        NativeCrtRandomState random = new(seed);
        NativeBattleWaterVectorRandom sampled = NativeBattleWaterVectorCommands.RandomB65740(0x100, random);
        short threshold = checked((short)(Math.Abs(sampled.Value) + adjustment));
        NativeBattleWaterExecutionFrame frame = RandomJump(random, threshold, -8);
        if (adjustment < 0)
        {
            frame = frame with { Arena = frame.Arena.MarkUnknown(0x1004, 2) };
        }
        NativeBattleWaterExecutionFrame result = NativeBattleScriptedSchedulingCommands.Execute(frame, new(0x1000, 6, 0x2000));
        Assert.Equal(sampled.Random, result.Random);
        Assert.Equal(adjustment < 0 ? 0x2006u : 0x1FF8u, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
        Assert.Equal(frame.ReadTemporaryWord(0x13E), result.ReadTemporaryWord(0x13E));
        NativeBattleWaterExecutionFrame restored = JsonSerializer.Deserialize<NativeBattleWaterExecutionFrame>(JsonSerializer.Serialize(frame))!;
        Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(NativeBattleScriptedSchedulingCommands.Execute(restored, new(0x1000, 6, 0x2000))));
    }

    [Fact]
    public void Negative_random_threshold_falls_through_without_consuming_the_branch_offset()
    {
        NativeBattleWaterExecutionFrame frame = RandomJump(new(1), -1, 0);
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x1004, 2) };
        NativeBattleWaterExecutionFrame result = NativeBattleScriptedSchedulingCommands.Execute(frame, new(0x1000, 6, 0x2000));
        Assert.Equal(0x2006u, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
        Assert.NotEqual(frame.Random, result.Random);
    }

    private static NativeBattleWaterExecutionFrame RandomJump(NativeCrtRandomState random, short threshold, short displacement)
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create().WriteTemporaryWord(0x14A, 0xF0A1) with { Random = random };
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797450, 0x2000);
        byte[] program = new byte[6];
        BinaryPrimitives.WriteUInt16LittleEndian(program, 0xF0A1);
        BinaryPrimitives.WriteInt16LittleEndian(program.AsSpan(2), threshold);
        BinaryPrimitives.WriteInt16LittleEndian(program.AsSpan(4), displacement);
        return frame with { Arena = frame.Arena.Write(0x1000, program) };
    }

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
