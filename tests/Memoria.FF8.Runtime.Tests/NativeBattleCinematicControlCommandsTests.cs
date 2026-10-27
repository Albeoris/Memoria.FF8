using System.Buffers.Binary;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicControlCommandsTests
{
    private static readonly NativeBattleWaterScriptProgram Program = new(0x1000, 6, 0x1000);
    private const int Model = 0x18000;

    [Theory]
    [InlineData((ushort)0x10B7, true)]
    [InlineData((ushort)0x00B7, false)]
    [InlineData((ushort)0xF0B7, false)]
    public void Debug_B7_consumes_two_signed_operands_only_for_mode_one_without_changing_effect_owners(ushort instruction, bool consumes)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(instruction, 0x8000);
        frame = frame with { Arena = frame.Arena.Write(0x1004, BitConverter.GetBytes((short)-1)) };
        if (!consumes)
        {
            frame = frame with { Arena = frame.Arena.MarkUnknown(0x1002, 4) };
        }
        NativeBattleWaterExecutionFrame replayFrame = NativeBattleTestSnapshots.RestoreJson<NativeBattleWaterExecutionFrame>(NativeBattleTestSnapshots.Json(frame))!;
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicControlCommands.DebugParametersB7(frame, Program);
        Assert.Equal(consumes ? 0x1006u : 0x1000u, Pc(result));
        Assert.Same(frame.Arena, result.Arena);
        Assert.Same(frame.TemporaryFrame, result.TemporaryFrame);
        Assert.Same(frame.Random, result.Random);
        Assert.Equal(NativeBattleTestSnapshots.Json(result), NativeBattleTestSnapshots.Json(NativeBattleCinematicControlCommands.DebugParametersB7(replayFrame, Program)));
    }

    [Theory]
    [InlineData(-2, -32768, -1, false)]
    [InlineData(8, -1, -1, true)]
    [InlineData(8, 32767, -32768, true)]
    [InlineData(8, -32768, 32767, false)]
    public void WaitFB_reads_signed_relative_cached_word_and_yields_without_advancing_PC_only_while_at_least_threshold(short offset, short value, short threshold, bool wait)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0xF0FB, unchecked((ushort)offset));
        frame = frame with { Arena = frame.Arena.Write(0x1004, BitConverter.GetBytes(threshold)).Write(0x208C + offset, BitConverter.GetBytes(value)) };
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0xC8, 0xFF80);
        if (!wait)
        {
            frame = frame with { Arena = frame.Arena.MarkUnknown(0x20C8, 2) };
        }
        NativeBattleWaterExecutionFrame replayFrame = NativeBattleTestSnapshots.RestoreJson<NativeBattleWaterExecutionFrame>(NativeBattleTestSnapshots.Json(frame))!;
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicControlCommands.WaitWhileCachedAtLeastFB(frame, Program);
        Assert.Equal(wait ? 0x1000u : 0x1006u, Pc(result));
        Assert.Equal(wait ? (ushort)0xFF80 : frame.ReadTemporaryWord(0x13E), result.ReadTemporaryWord(0x13E));
        Assert.Same(frame.Arena, result.Arena);
        Assert.Same(frame.Random, result.Random);
        Assert.Equal(NativeBattleTestSnapshots.Json(result), NativeBattleTestSnapshots.Json(NativeBattleCinematicControlCommands.WaitWhileCachedAtLeastFB(replayFrame, Program)));
    }

    [Theory]
    [InlineData((byte)0)]
    [InlineData((byte)0xFF)]
    public void Render_mask_B2_stores_only_low_operand_BYTE_and_preserves_adjacent_manager_fields(byte value)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0xB2, value);
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x1003, 3), Core = frame.Core.MarkUnknown(0x2797690 + 0xA1, 1).MarkUnknown(0x2797690 + 0xA3, 1) };
        NativeBattleWaterExecutionFrame replayFrame = NativeBattleTestSnapshots.RestoreJson<NativeBattleWaterExecutionFrame>(NativeBattleTestSnapshots.Json(frame))!;
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicControlCommands.StoreRenderMaskB2(frame, Program);
        Assert.Equal(value, result.Core.Read(0x2797690 + 0xA2, 1)[0]);
        Assert.Throws<NotSupportedException>(() => result.Core.Read(0x2797690 + 0xA1, 1));
        Assert.Throws<NotSupportedException>(() => result.Core.Read(0x2797690 + 0xA3, 1));
        Assert.Equal(0x1004u, Pc(result));
        Assert.Same(frame.Arena, result.Arena);
        Assert.Same(frame.TemporaryFrame, result.TemporaryFrame);
        Assert.Equal(NativeBattleTestSnapshots.Json(result), NativeBattleTestSnapshots.Json(NativeBattleCinematicControlCommands.StoreRenderMaskB2(replayFrame, Program)));
    }

    [Theory]
    [InlineData((ushort)0x009B, (byte)0xFF)]
    [InlineData((ushort)0x409B, (byte)0xDF)]
    [InlineData((ushort)0xFE9B, (byte)0x80)]
    public void Clear9B_uses_only_the_seven_instruction_flag_bits_without_reading_an_operand(ushort instruction, byte expected)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(instruction, 0);
        frame = frame with { Arena = frame.Arena.Write(0x20DE, [0xFF]).MarkUnknown(0x1000, 6).MarkUnknown(0x20C0, 4) };
        NativeBattleWaterExecutionFrame replayFrame = NativeBattleTestSnapshots.RestoreJson<NativeBattleWaterExecutionFrame>(NativeBattleTestSnapshots.Json(frame))!;
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicControlCommands.ClearFlags9B(frame);
        Assert.Equal(expected, result.Arena.Read(0x20DE, 1)[0]);
        Assert.Equal(0x1002u, Pc(result));
        Assert.Same(frame.TemporaryFrame, result.TemporaryFrame);
        Assert.Throws<NotSupportedException>(() => result.Arena.Read(0x20C0, 4));
        Assert.Equal(NativeBattleTestSnapshots.Json(result), NativeBattleTestSnapshots.Json(NativeBattleCinematicControlCommands.ClearFlags9B(replayFrame)));
    }

    [Theory]
    [InlineData(-32768, -1, 32767)]
    [InlineData(0, 1, -2)]
    public void Cached_position_F2_bakes_signed_WORDs_into_motion_and_disables_update_without_an_operand(short x, short y, short z)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0xF0F2, 0);
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x1002, 4).MarkUnknown(0x205C, 12).MarkUnknown(0x2018, 1).Write(0x2094, BitConverter.GetBytes(x)).Write(0x2096, BitConverter.GetBytes(y)).Write(0x2098, BitConverter.GetBytes(z)) };
        NativeBattleWaterExecutionFrame replayFrame = NativeBattleTestSnapshots.RestoreJson<NativeBattleWaterExecutionFrame>(NativeBattleTestSnapshots.Json(frame))!;
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicControlCommands.BakeCachedPositionF2(frame);
        Assert.Equal(unchecked((uint)(x << 16)), BinaryPrimitives.ReadUInt32LittleEndian(result.Arena.Read(0x205C, 4)));
        Assert.Equal(unchecked((uint)(y << 16)), BinaryPrimitives.ReadUInt32LittleEndian(result.Arena.Read(0x2060, 4)));
        Assert.Equal(unchecked((uint)(z << 16)), BinaryPrimitives.ReadUInt32LittleEndian(result.Arena.Read(0x2064, 4)));
        Assert.Equal((byte)0, result.Arena.Read(0x2018, 1)[0]);
        Assert.Equal(0x1002u, Pc(result));
        Assert.Same(frame.Random, result.Random);
        Assert.Same(frame.TemporaryFrame, result.TemporaryFrame);
        Assert.Equal(NativeBattleTestSnapshots.Json(result), NativeBattleTestSnapshots.Json(NativeBattleCinematicControlCommands.BakeCachedPositionF2(replayFrame)));
    }

    [Theory]
    [InlineData(0x0000, 0, 0x1004u)]
    [InlineData(0xFF0F, 255, 0x1022u)]
    [InlineData(0x1234, 18, 0x100Cu)]
    public void Update_block_1A_stores_the_unmodified_high_byte_and_skips_only_the_low_nibble_WORD_count(ushort operand, byte kind, uint next)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0xF01A, operand);
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x2018, 1).MarkUnknown(0x20A0, 4).MarkUnknown(0x1004, 0x40) };
        NativeBattleWaterExecutionFrame replayFrame = NativeBattleTestSnapshots.RestoreJson<NativeBattleWaterExecutionFrame>(NativeBattleTestSnapshots.Json(frame))!;
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicControlCommands.RegisterUpdateBlock1A(frame, Program);
        Assert.Equal(kind, result.Arena.Read(0x2018, 1)[0]);
        Assert.Equal(0x1004u, BinaryPrimitives.ReadUInt32LittleEndian(result.Arena.Read(0x20A0, 4)));
        Assert.Equal(next, Pc(result));
        Assert.Same(frame.Random, result.Random);
        Assert.Same(frame.TemporaryFrame, result.TemporaryFrame);
        Assert.Equal(NativeBattleTestSnapshots.Json(result), NativeBattleTestSnapshots.Json(NativeBattleCinematicControlCommands.RegisterUpdateBlock1A(replayFrame, Program)));
    }

    [Theory]
    [InlineData(0x001C, 0xC8, 1)]
    [InlineData(0x021C, 0xC8, 2)]
    [InlineData(0x061C, 0xC8, 1)]
    [InlineData(0x041C, -2, 1)]
    public void Relative_store_1C_uses_signed_displacement_and_only_the_selected_operand_width(ushort instruction, short displacement, int width)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(instruction, unchecked((ushort)displacement));
        frame = frame with { Arena = frame.Arena.Write(0x1004, [0xA5, 0x5A]).MarkUnknown(0x2000 + displacement, 2) };
        if (width == 1)
        {
            frame = frame with { Arena = frame.Arena.MarkUnknown(0x1005, 1) };
        }
        NativeBattleWaterExecutionFrame replayFrame = NativeBattleTestSnapshots.RestoreJson<NativeBattleWaterExecutionFrame>(NativeBattleTestSnapshots.Json(frame))!;
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicControlCommands.StoreRelative1C(frame, Program);
        Assert.Equal(width == 1 ? new byte[] { 0xA5 } : new byte[] { 0xA5, 0x5A }, result.Arena.Read(0x2000 + displacement, width));
        if (width == 1)
        {
            Assert.Throws<NotSupportedException>(() => result.Arena.Read(0x2001 + displacement, 1));
        }
        Assert.Equal(0x1006u, Pc(result));
        Assert.Same(frame.Random, result.Random);
        Assert.Same(frame.TemporaryFrame, result.TemporaryFrame);
        Assert.Equal(NativeBattleTestSnapshots.Json(result), NativeBattleTestSnapshots.Json(NativeBattleCinematicControlCommands.StoreRelative1C(replayFrame, Program)));
    }

    [Theory]
    [InlineData(0, 7, 0xCAFE)]
    [InlineData(32767, 0, 0xCAFE)]
    [InlineData(32767, -3, 0xFF88)]
    [InlineData(-1, 1, 0xFFFF)]
    public void Random_wait_B0_draws_once_and_keeps_the_previous_wait_when_the_full_product_is_zero(short operand, short multiplier, ushort expected)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0xF0B0, unchecked((ushort)operand)).WriteTemporaryWord(0x13E, 0xCAFE);
        frame = frame with { Random = NativeCrtRandomState.FromColdProcess(), Arena = frame.Arena.Write(0x20C8, BitConverter.GetBytes(multiplier)).MarkUnknown(0x1004, 2) };
        NativeBattleWaterExecutionFrame replayFrame = NativeBattleTestSnapshots.RestoreJson<NativeBattleWaterExecutionFrame>(NativeBattleTestSnapshots.Json(frame))!;
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicControlCommands.WaitRandomB0(frame, Program);
        Assert.Equal(expected, result.ReadTemporaryWord(0x13E));
        Assert.Equal(0x1004u, Pc(result));
        Assert.Equal(2745024u, result.Random.Seed);
        Assert.Equal(NativeBattleTestSnapshots.Json(result), NativeBattleTestSnapshots.Json(NativeBattleCinematicControlCommands.WaitRandomB0(replayFrame, Program)));
    }

    [Theory]
    [InlineData(128, 1, -3, 1000, 0xDEFF0000u)]
    [InlineData(-128, -3, 1, -1000, 0xDEFF0001u)]
    [InlineData(32767, 32767, -32768, 2, 0xDEFF00FFu)]
    public void Position_color_D3_uses_signed_scalars_truncates_then_clamps_and_preserves_command(short factor, short x, short y, short z, uint expected)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0xF0D3, 0xFFFE);
        frame = frame with { Arena = frame.Arena.Write(Model + 0x8C, BitConverter.GetBytes(factor)).Write(Model + 0x94, BitConverter.GetBytes(x)).Write(Model + 0x96, BitConverter.GetBytes(y)).Write(Model + 0x98, BitConverter.GetBytes(z)).MarkUnknown(0x20CC, 3).Write(0x20CF, [0xDE]) };
        NativeBattleWaterExecutionFrame Run(NativeBattleWaterExecutionFrame current) => NativeBattleCinematicControlCommands.ScaleReferencedPositionColorD3(current, Program, (_, selector) =>
        {
            Assert.Equal((short)-2, selector);
            return NativeBattleEffectNodeMemory.ArenaAddress + Model;
        });
        NativeBattleWaterExecutionFrame replayFrame = NativeBattleTestSnapshots.RestoreJson<NativeBattleWaterExecutionFrame>(NativeBattleTestSnapshots.Json(frame))!;
        NativeBattleWaterExecutionFrame result = Run(frame);
        Assert.Equal(expected, BinaryPrimitives.ReadUInt32LittleEndian(result.Arena.Read(0x20CC, 4)));
        Assert.Equal(0x1004u, Pc(result));
        Assert.Same(frame.Random, result.Random);
        Assert.Equal(NativeBattleTestSnapshots.Json(result), NativeBattleTestSnapshots.Json(Run(replayFrame)));
    }

    [Theory]
    [InlineData(0, 0xFFFF, 0xFFFF)]
    [InlineData(255, 0xFFFF, 254)]
    [InlineData(128, 0xFF80, 0)]
    [InlineData(1, 0x8000, 0x8001)]
    public void Relative_actor_CE_zero_extends_the_actor_and_wraps_the_full_WORD_operand(byte actor, ushort offset, ushort expected)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0xF0CE, offset);
        frame = frame with { Arena = frame.Arena.Write(0x201B, [actor]).MarkUnknown(0x2012, 2).MarkUnknown(0x1004, 2) };
        NativeBattleWaterExecutionFrame replayFrame = NativeBattleTestSnapshots.RestoreJson<NativeBattleWaterExecutionFrame>(NativeBattleTestSnapshots.Json(frame))!;
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicControlCommands.SetRelativeActorIdCE(frame, Program);
        Assert.Equal(expected, BinaryPrimitives.ReadUInt16LittleEndian(result.Arena.Read(0x2012, 2)));
        Assert.Equal(0x1004u, Pc(result));
        Assert.Same(frame.Random, result.Random);
        Assert.Same(frame.TemporaryFrame, result.TemporaryFrame);
        Assert.Equal(NativeBattleTestSnapshots.Json(result), NativeBattleTestSnapshots.Json(NativeBattleCinematicControlCommands.SetRelativeActorIdCE(replayFrame, Program)));
    }

    [Theory]
    [InlineData(1, 0x0000F000u)]
    [InlineData(-1, 0xFFFEF000u)]
    [InlineData(32767, 0x7FFEF000u)]
    [InlineData(-32768, 0x7FFFF000u)]
    public void Resource_cursor120_uses_signed_page_and_native_DWORD_wrap_without_allocating_or_reading_old_cursor(short page, uint expected)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0xF120, unchecked((ushort)page));
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797690 + 0x70, 0xFFFFF000);
        frame = frame with { Core = frame.Core.MarkUnknown(0x2797690 + 0x74, 4), Arena = frame.Arena.MarkUnknown(0x1004, 2) };
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicControlCommands.SetResourceCursor120(frame, Program);
        Assert.Equal(expected, WaterSchedulerFixture.ReadCoreDword(result, 0x2797690 + 0x74));
        Assert.Equal(0x1004u, Pc(result));
        Assert.Same(frame.Arena, result.Arena);
        Assert.Same(frame.Random, result.Random);
    }

    [Theory]
    [InlineData(0x010D, 0x0001, 0xFFFF, true)]
    [InlineData(0x010D, 0xC000, 0xFFFF, false)]
    [InlineData(0x010D, 0x0002, 0x0001, false)]
    [InlineData(0x110D, 0x0001, 0xFFFF, false)]
    [InlineData(0x110D, 0xC000, 0xFFFF, true)]
    public void Wait10D_selects_signed_reference_masks_low14_bits_and_inverts_only_bit1000(ushort instruction, ushort flags, ushort mask, bool advances)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(instruction, 0xFFFE);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0xC8, 0x8765);
        frame = frame with { Arena = frame.Arena.Write(Model + 0x4A, BitConverter.GetBytes(flags)).Write(0x1004, BitConverter.GetBytes(mask)) };
        if (advances)
        {
            frame = frame with { Arena = frame.Arena.MarkUnknown(0x20C8, 2) };
        }
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicControlCommands.Wait10D(frame, Program, (_, selector) =>
        {
            Assert.Equal((short)-2, selector);
            return NativeBattleEffectNodeMemory.ArenaAddress + Model;
        });
        Assert.Equal(advances ? 0x1006u : 0x1000u, Pc(result));
        if (!advances)
        {
            Assert.Equal((ushort)0x8765, result.ReadTemporaryWord(0x13E));
        }
    }

    [Theory]
    [InlineData(0x0051, 0x81)]
    [InlineData(0x4051, 0xA1)]
    [InlineData(0xFE51, 0xFF)]
    public void Command51_uses_unsigned_full_instruction_shift_and_only_low_operand_word(ushort instruction, byte expectedFlags)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(instruction, 0xFEDC);
        frame = frame with { Arena = frame.Arena.Write(0x20DE, [0x81]).Write(0x20C2, [0xEF, 0xBE]).MarkUnknown(0x1004, 2) };
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicControlCommands.Execute51(frame, Program);
        Assert.Equal(expectedFlags, result.Arena.Read(0x20DE, 1)[0]);
        Assert.Equal(new byte[] { 0xDC, 0xFE, 0xEF, 0xBE }, result.Arena.Read(0x20C0, 4));
        Assert.Equal(0x1004u, Pc(result));
    }

    [Theory]
    [InlineData(31, 31, -12, 0xFF4u)]
    [InlineData(31, 31, 12, 0x100Cu)]
    [InlineData(31, 287, -12, 0x1006u)]
    [InlineData(31, 94, -12, 0x1006u)]
    public void D1_compares_unsigned_stage_byte_with_full_word_and_branches_from_same_PC(byte stage, ushort expected, short displacement, uint expectedPc)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0xD1, expected);
        frame = frame with { Arena = frame.Arena.Write(0x1004, BitConverter.GetBytes(displacement)) };
        if (stage != expected)
        {
            frame = frame with { Arena = frame.Arena.MarkUnknown(0x1004, 2) };
        }
        Assert.Equal(expectedPc, Pc(NativeBattleCinematicControlCommands.BranchD1(frame, Program, stage)));
    }

    [Theory]
    [InlineData(0x0041, 0x80FF, 0x0FF0, 0x800F)]
    [InlineData(0x8041, 0x800F, 0x0FF0, 0x8FFF)]
    public void Model41_word_modes_modify_only_actual_embedded_model_flags(ushort instruction, ushort initial, ushort operand, ushort expected)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(instruction, operand);
        frame = frame with { Arena = frame.Arena.Write(Model + 2, BitConverter.GetBytes(initial)).MarkUnknown(0x1004, 2).MarkUnknown(Model + 0x7C, 4) };
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicModel.Control41(frame, Program);
        Assert.Equal(expected, BinaryPrimitives.ReadUInt16LittleEndian(result.Arena.Read(Model + 2, 2)));
        Assert.Equal(0x1004u, Pc(result));
        Assert.Throws<NotSupportedException>(() => result.Arena.Read(Model + 0x7C, 4));
    }

    [Fact]
    public void Model41_script_mode_preserves_unknown_high_flag_byte_and_does_not_read_next_word()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x1041, 0xFFFE);
        frame = frame with { Arena = frame.Arena.Write(Model + 2, [0x80]).MarkUnknown(Model + 3, 1).MarkUnknown(0x1004, 2) };
        NativeBattleWaterExecutionFrame restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleWaterExecutionFrame>(NativeBattleTestSnapshots.Json(frame))!;
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicModel.Control41(frame, Program);
        Assert.Equal((byte)0x84, result.Arena.Read(Model + 2, 1)[0]);
        Assert.Throws<NotSupportedException>(() => result.Arena.Read(Model + 3, 1));
        Assert.Equal(0x1002u, BinaryPrimitives.ReadUInt32LittleEndian(result.Arena.Read(Model + 0x7C, 4)));
        Assert.Equal(0x1006u, Pc(result));
        Assert.Equal(NativeBattleTestSnapshots.Json(result), NativeBattleTestSnapshots.Json(NativeBattleCinematicModel.Control41(restored, Program)));
    }

    [Fact]
    public void Model41_unknown_modes_require_operand_but_never_dereference_model()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0xF041, 1);
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0xBC, 0xFFFFFFFF);
        Assert.Same(frame, NativeBattleCinematicModel.Control41(frame, Program));
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x1002, 2) };
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicModel.Control41(frame, Program));
    }

    private static NativeBattleWaterExecutionFrame Fixture(ushort instruction, ushort operand)
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create().WriteTemporaryWord(0x14A, instruction);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797450, 0x1000);
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0xBC, NativeBattleEffectNodeMemory.ArenaAddress + Model);
        return frame with { Arena = frame.Arena.Write(0x1002, BitConverter.GetBytes(operand)) };
    }

    private static uint Pc(NativeBattleWaterExecutionFrame frame) => WaterSchedulerFixture.ReadCoreDword(frame, 0x2797450);
}
