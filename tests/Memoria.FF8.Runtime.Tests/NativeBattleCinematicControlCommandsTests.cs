using System.Buffers.Binary;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicControlCommandsTests
{
    private static readonly NativeBattleWaterScriptProgram Program = new(0x1000, 6, 0x1000);
    private const int Model = 0x18000;

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
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicModel.Control41(frame, Program);
        Assert.Equal((byte)0x84, result.Arena.Read(Model + 2, 1)[0]);
        Assert.Throws<NotSupportedException>(() => result.Arena.Read(Model + 3, 1));
        Assert.Equal(0x1002u, BinaryPrimitives.ReadUInt32LittleEndian(result.Arena.Read(Model + 0x7C, 4)));
        Assert.Equal(0x1006u, Pc(result));
        NativeBattleWaterExecutionFrame restored = JsonSerializer.Deserialize<NativeBattleWaterExecutionFrame>(JsonSerializer.Serialize(frame))!;
        Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(NativeBattleCinematicModel.Control41(restored, Program)));
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
