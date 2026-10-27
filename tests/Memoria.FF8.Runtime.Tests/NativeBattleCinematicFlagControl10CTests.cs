using System.Buffers.Binary;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicFlagControl10CTests
{
    [Theory]
    [InlineData(0, 0x8001)]
    [InlineData(1, 0)]
    [InlineData(6, 0x8001)]
    [InlineData(15, 0x8001)]
    public void Default_modes_set_and_mode_one_clears_wrapped_WORD_flags_without_wait_reads(int mode, int expected)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(mode, 1, unchecked((short)0x8001), -4);
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x2000 + 0xC8, 2).MarkUnknown(0x3004, 2) };
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicFlagControl10C.Execute(frame, Program());
        Assert.Equal(expected, WaterSchedulerFixture.ReadSlotWord(result, 0, 0x4A));
        Assert.Equal(Address(0x3004), WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
        Assert.Equal(0xBEEF, result.ReadTemporaryWord(0x13E));
    }

    [Theory]
    [InlineData(2, 1, 0x2FFC)]
    [InlineData(2, 0, 0x3006)]
    [InlineData(3, 1, 0x3006)]
    [InlineData(3, 0, 0x2FFC)]
    public void Conditional_branches_use_signed_relative_displacement_and_six_byte_fallthrough(int mode, int flags, int expected)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(mode, unchecked((ushort)flags), 1, -4);
        if (expected == 0x3006)
        {
            frame = frame with { Arena = frame.Arena.MarkUnknown(0x3004, 2) };
        }
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicFlagControl10C.Execute(frame, Program());
        Assert.Equal(Address(expected), WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
        Assert.Equal(flags, WaterSchedulerFixture.ReadSlotWord(result, 0, 0x4A));
        Assert.Equal(0xBEEF, result.ReadTemporaryWord(0x13E));
    }

    [Theory]
    [InlineData(4, 1, true)]
    [InlineData(4, 0, false)]
    [InlineData(5, 1, false)]
    [InlineData(5, 0, true)]
    public void Wait_modes_publish_raw_C8_only_while_waiting_and_keep_the_same_PC(int mode, int flags, bool waits)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(mode, unchecked((ushort)flags), 1, -4);
        if (!waits)
        {
            frame = frame with { Arena = frame.Arena.MarkUnknown(0x2000 + 0xC8, 2) };
        }
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicFlagControl10C.Execute(frame, Program());
        Assert.Equal(Address(waits ? 0x3000 : 0x3004), WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
        Assert.Equal(waits ? 0x8000 : 0xBEEF, result.ReadTemporaryWord(0x13E));
        Assert.Equal(flags, WaterSchedulerFixture.ReadSlotWord(result, 0, 0x4A));
    }

    [Fact]
    public void Negative_mask_tests_high_WORD_bits_and_snapshot_repeats_the_wait()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(4, 0x8000, short.MinValue, 0);
        NativeBattleWaterExecutionFrame restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleWaterExecutionFrame>(NativeBattleTestSnapshots.Json(frame))!;
        Assert.Equal(NativeBattleTestSnapshots.Json(NativeBattleCinematicFlagControl10C.Execute(frame, Program())), NativeBattleTestSnapshots.Json(NativeBattleCinematicFlagControl10C.Execute(restored, Program())));
        Assert.Equal(Address(0x3000), WaterSchedulerFixture.ReadCoreDword(NativeBattleCinematicFlagControl10C.Execute(restored, Program()), 0x2797450));
    }

    private static NativeBattleWaterExecutionFrame Fixture(int mode, ushort flags, short operand, short displacement)
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create().WriteTemporaryWord(0x14A, unchecked((ushort)((mode << 12) | 0x10C))).WriteTemporaryWord(0x13E, 0xBEEF);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x4A, flags);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0xC8, 0x8000);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797450, Address(0x3000));
        byte[] bytes = new byte[6];
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(2), operand);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(4), displacement);
        return frame with { Arena = frame.Arena.Write(0x3000, bytes) };
    }

    private static NativeBattleWaterScriptProgram Program() => new(0x3000, 6, Address(0x3000));
    private static uint Address(int offset) => unchecked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)offset);
}
