using System.Buffers.Binary;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicScale87Tests
{
    private static readonly NativeBattleWaterScriptProgram Program = new(0x1000, 6, 0x1000);

    [Fact]
    public void Mask_comes_from_temporary_frame_and_unselected_unknown_lanes_are_neither_read_nor_written()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(384, 3, 0x8400);
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0x50, unchecked((uint)(-3 << 16)));
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0x64, 7u << 16);
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x2054, 16).MarkUnknown(0x206C, 16).MarkUnknown(0x14A, 2) };
        NativeBattleWaterExecutionFrame restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleWaterExecutionFrame>(NativeBattleTestSnapshots.Json(frame))!;
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicScale87.Execute(frame, Program);
        Assert.Equal(unchecked((uint)-32768), WaterSchedulerFixture.ReadSlotDword(result, 0, 0x68));
        Assert.Equal(76458u, WaterSchedulerFixture.ReadSlotDword(result, 0, 0x7C));
        Assert.Throws<NotSupportedException>(() => result.Arena.Read(0x206C, 16));
        Assert.Throws<NotSupportedException>(() => result.Arena.Read(0x2054, 16));
        Assert.Equal(0x1006u, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
        Assert.Same(frame.TemporaryFrame, result.TemporaryFrame);
        Assert.Same(frame.Random, result.Random);
        Assert.Equal(NativeBattleTestSnapshots.Json(result), NativeBattleTestSnapshots.Json(NativeBattleCinematicScale87.Execute(restored, Program)));
    }

    [Theory]
    [InlineData(-1, 3, -1, 21930)]
    [InlineData(-1, -3, -1, -21930)]
    [InlineData(32767, 7, 32767, 304396032)]
    [InlineData(-32768, 7, 32767, -305575643)]
    public void Signed_operands_arithmetic_source_shift_and_wrapping_products_preserve_native_IDIV(int scale, int divisor, int source, int expected)
    {
        NativeBattleWaterExecutionFrame frame = Fixture((short)scale, (short)divisor, 0x8000);
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0x50, unchecked((uint)((source << 16) | 0xBEEF)));
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicScale87.Execute(frame, Program);
        Assert.Equal(unchecked((uint)expected), WaterSchedulerFixture.ReadSlotDword(result, 0, 0x68));
    }

    [Fact]
    public void All_six_mask_bits_write_only_the_six_scale_DWORDs()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(257, 1, 0xFC00);
        int[] source = [1, -2, 3, -4, 5, -6];
        for (int lane = 0; lane < source.Length; lane++)
        {
            frame = WaterSchedulerFixture.SlotDword(frame, 0, 0x50 + lane * 4, unchecked((uint)(source[lane] << 16)));
        }
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x2080, 4) };
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicScale87.Execute(frame, Program);
        for (int lane = 0; lane < source.Length; lane++)
        {
            Assert.Equal(unchecked((uint)(source[lane] * 256)), WaterSchedulerFixture.ReadSlotDword(result, 0, 0x68 + lane * 4));
        }
        Assert.Throws<NotSupportedException>(() => result.Arena.Read(0x2080, 4));
    }

    [Fact]
    public void Zero_divisor_is_not_evaluated_when_mask_selects_no_lanes()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(100, 0, 0x03FF);
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x2050, 48) };
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicScale87.Execute(frame, Program);
        Assert.Equal(0x1006u, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
        Assert.Throws<NotSupportedException>(() => result.Arena.Read(0x2050, 48));
    }

    [Fact]
    public void Native_zero_divisor_and_signed_overflow_fault_without_returning_published_state()
    {
        NativeBattleWaterExecutionFrame zero = Fixture(257, 0, 0x8000);
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicScale87.Execute(zero, Program));
        Assert.Equal(0x1000u, WaterSchedulerFixture.ReadCoreDword(zero, 0x2797450));
        NativeBattleWaterExecutionFrame overflow = Fixture(0, -1, 0x8000);
        overflow = WaterSchedulerFixture.SlotDword(overflow, 0, 0x50, 0x80000000);
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicScale87.Execute(overflow, Program));
        Assert.Equal(0x1000u, WaterSchedulerFixture.ReadCoreDword(overflow, 0x2797450));
    }

    [Fact]
    public void Later_selected_unknown_source_fault_does_not_publish_earlier_local_writes_or_advance_PC()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(384, 2, 0xC000);
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0x50, 4u << 16);
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0x68, 0xDEADBEEF);
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x2054, 4) };
        using (NativeBattleEffectArenaTransaction transaction = new(frame.Arena))
        {
            Assert.Throws<NotSupportedException>(() => NativeBattleCinematicScale87.Execute(frame with { Arena = transaction.View }, Program));
        }
        Assert.Equal(0xDEADBEEFu, WaterSchedulerFixture.ReadSlotDword(frame, 0, 0x68));
        Assert.Equal(0x1000u, WaterSchedulerFixture.ReadCoreDword(frame, 0x2797450));
    }

    private static NativeBattleWaterExecutionFrame Fixture(short scale, short divisor, ushort mask)
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create().WriteTemporaryWord(0x14A, mask);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x27973B8, frame.TemporaryFrame.Address + 0x100);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797450, 0x1000);
        byte[] bytes = new byte[6];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, 0x87);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(2), scale);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(4), divisor);
        return frame with { Arena = frame.Arena.Write(0x1000, bytes) };
    }

    [Fact]
    public void Wrong_callback_owner_fails_before_using_temporary_instruction_mask()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(257, 0, 0x8000);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x27973B8, frame.TemporaryFrame.Address + 0x104);
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicScale87.Execute(frame, Program));
        Assert.Equal(0x1000u, WaterSchedulerFixture.ReadCoreDword(frame, 0x2797450));
    }
}
