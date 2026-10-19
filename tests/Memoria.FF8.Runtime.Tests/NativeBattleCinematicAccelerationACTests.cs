using System.Buffers.Binary;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicAccelerationACTests
{
    private static readonly NativeBattleWaterSlotBank Bank = new(0x100, 0x4000, 8, 0x2000);
    private static readonly NativeBattleWaterScriptProgram Program = new(0x1000, 20, 0x1000);

    [Theory]
    [InlineData(true, 6000, 0, 16000)]
    [InlineData(false, 6000, 0, 16000)]
    [InlineData(true, -6000, 0, -16000)]
    [InlineData(false, -6000, 0, -16000)]
    [InlineData(false, 6000, 2000, 10666)]
    [InlineData(true, 6000, 2000, 16000)]
    [InlineData(true, -1, 0, -3)]
    public void Signed_solver_preserves_target_mode_fixed_point_rounding_and_unselected_lanes(bool immediate, short endpoint, short position, short expected)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(immediate ? (ushort)0x82AC : (ushort)0x80AC, 3, endpoint);
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0x50, unchecked((uint)(position << 16)));
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x206C, 20).MarkUnknown(0x3054, 20) };
        for (int lane = 1; lane < 6; lane++)
        {
            frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x80 + lane * 2, 0);
        }
        if (immediate)
        {
            frame = frame with { Arena = frame.Arena.MarkUnknown(0x2050, 4) };
        }
        NativeBattleWaterExecutionFrame Run(NativeBattleWaterExecutionFrame current) => NativeBattleCinematicAccelerationAC.Execute(current, Program, Bank, (_, selector) =>
        {
            Assert.False(immediate);
            Assert.Equal(-2, selector);
            return Address(0x3000);
        });
        NativeBattleWaterExecutionFrame result = Run(frame);
        Assert.Equal(unchecked((ushort)expected), WaterSchedulerFixture.ReadSlotWord(result, 0, 0x80));
        Assert.Equal((byte)1, result.Arena.Read(0x201A, 1)[0]);
        Assert.Equal(0x1006u, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
        Assert.Same(frame.Random, result.Random);
        Assert.Same(frame.TemporaryFrame, result.TemporaryFrame);
        Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(Run(JsonSerializer.Deserialize<NativeBattleWaterExecutionFrame>(JsonSerializer.Serialize(frame))!)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Zero_denominator_faults_only_when_a_lane_is_selected(short duration)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x02AC, duration, 10);
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x1004, 16).MarkUnknown(0x2050, 48) };
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicAccelerationAC.Execute(frame, Program, Bank, (_, _) => throw new InvalidOperationException("Immediate mode does not resolve another slot."));
        Assert.Equal(0x1004u, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
        NativeBattleWaterExecutionFrame selected = Fixture(0x82AC, duration, 10);
        Assert.Throws<DivideByZeroException>(() => NativeBattleCinematicAccelerationAC.Execute(selected, Program, Bank, (_, _) => 0));
    }

    [Theory]
    [InlineData(true, 3, int.MaxValue, 0, 0, (ushort)0xAAAA)]
    [InlineData(true, -2, 0, 0, 6000, (ushort)0x7700)]
    [InlineData(false, 32767, int.MinValue, int.MaxValue, int.MinValue, (ushort)0)]
    [InlineData(false, 3, int.MinValue, 0, 0, (ushort)0)]
    public void Native_low_DWORD_products_signed_intermediates_and_final_WORD_wrap_are_preserved(bool immediate, short duration, int velocity, int position, int endpoint, ushort expected)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(immediate ? (ushort)0x82AC : (ushort)0x80AC, duration, unchecked((short)endpoint));
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0x68, unchecked((uint)velocity));
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0x50, unchecked((uint)position));
        if (!immediate)
        {
            frame = frame with { Arena = frame.Arena.Write(0x3050, BitConverter.GetBytes(endpoint)) };
        }
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicAccelerationAC.Execute(frame, Program, Bank, (_, _) => Address(0x3000));
        Assert.Equal(expected, WaterSchedulerFixture.ReadSlotWord(result, 0, 0x80));
        Assert.Equal(expected == 0 ? (byte)0 : (byte)1, result.Arena.Read(0x201A, 1)[0]);
    }

    [Fact]
    public void Immediate_operands_are_consumed_in_selected_lane_order_and_refresh_both_acceleration_groups()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x1EAC, 1, 10);
        frame = frame with { Arena = frame.Arena.Write(0x1006, BitConverter.GetBytes((short)-20)).Write(0x1008, BitConverter.GetBytes((short)30)) };
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicAccelerationAC.Execute(frame, Program, Bank, (_, _) => throw new InvalidOperationException("No reference operand exists."));
        Assert.Equal((ushort)160, WaterSchedulerFixture.ReadSlotWord(result, 0, 0x86));
        Assert.Equal(unchecked((ushort)-320), WaterSchedulerFixture.ReadSlotWord(result, 0, 0x88));
        Assert.Equal((ushort)480, WaterSchedulerFixture.ReadSlotWord(result, 0, 0x8A));
        Assert.Equal((byte)8, result.Arena.Read(0x201A, 1)[0]);
        Assert.Equal(0x100Au, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
    }

    private static NativeBattleWaterExecutionFrame Fixture(ushort instruction, short duration, short endpoint)
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create().WriteTemporaryWord(0x14A, instruction);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797450, 0x1000);
        frame = frame with { Arena = frame.Arena.Write(0x1000, new byte[20]).Write(0x1002, BitConverter.GetBytes(duration)).Write(0x1004, BitConverter.GetBytes((instruction & 0x200) != 0 ? endpoint : (short)-2)).Write(0x3050, BitConverter.GetBytes(endpoint << 16)) };
        for (int lane = 0; lane < 6; lane++)
        {
            frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x80 + lane * 2, 0);
            frame = WaterSchedulerFixture.SlotDword(frame, 0, 0x68 + lane * 4, 0);
        }
        return frame;
    }

    private static uint Address(int offset) => unchecked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)offset);
}
