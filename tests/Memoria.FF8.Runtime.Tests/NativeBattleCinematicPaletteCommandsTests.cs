using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.Tests.NativeBattleCinematicTextureMetadataTests;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicPaletteCommandsTests
{
    private const uint Palette = 0x279822C;
    private static readonly NativeBattleWaterScriptProgram Program = new(0x1000, 10, 0x1000);
    private static readonly NativeBattleWaterSlotBank Bank = new(0x2000, 0x400, 4, 0x2000);

    [Fact]
    public void Transform_clamps_signed_additions_preserves_bit15_and_queues_the_live_core_pointer()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x204B, 22, 0x0301, 2);
        frame = frame with { Core = frame.Core.Write(Palette, [0x34, 0x12]).MarkUnknown(Palette + 6, 2).Write(NativeBattleScriptedEffectMemory.Address + 0x43, [0xFF]).MarkUnknown(0x2798219, 1).MarkUnknown(0x2797762, 1) };
        NativeBattleEffectTextureState textures = NativeBattleEffectTextureState.FromColdProcess();
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicPaletteCommands.Execute(frame, Program, Resources(), Bank, 0, (current, rectangle, source) =>
        {
            Assert.Equal(0x1008u, WaterSchedulerFixture.ReadCoreDword(current, 0x2797450));
            Assert.Equal(new NativeBattleEffectTextureRectangle(33, 259, 2, 1), rectangle);
            Assert.Equal(Palette + 2, source);
            textures = textures.EnqueueScriptedRectangle505DF0(rectangle, source);
            return current;
        });
        Assert.Equal((ushort)(0x8000 | 31 | (5 << 5) | (31 << 10)), Word(result, Palette + 2));
        Assert.Equal((ushort)(31 | (11 << 5) | (31 << 10)), Word(result, Palette + 4));
        Assert.Equal((ushort)0x1234, Word(result, Palette));
        Assert.Throws<NotSupportedException>(() => result.Core.Read(Palette + 6, 2));
        Assert.Equal((byte)0, result.Core.Read(NativeBattleScriptedEffectMemory.Address + 0x43, 1)[0]);
        Assert.Equal(new byte[] { 33, 0, 3, 1, 2, 0, 1, 0 }, result.Core.Read(0x27974D4 + 15 * 8, 8));
        Assert.Equal(NativeBattleEffectNodeMemory.ArenaAddress + 0x5182, result.ReadTemporaryDword(0xF0));
        Assert.Equal(2u, result.ReadTemporaryDword(0xF4));
        Assert.Equal(Palette + 2, result.ReadTemporaryDword(0xF8));
        Assert.Equal(0x301u, result.ReadTemporaryDword(0xFC));
        NativeBattleEffectTextureState restored = JsonSerializer.Deserialize<NativeBattleEffectTextureState>(JsonSerializer.Serialize(textures))!;
        Assert.Throws<NotSupportedException>(() => restored.Drain505D20());
        NativeBattleScriptedEffectMemory later = result.Core.Write(Palette + 2, [1, 2, 3, 4]);
        NativeBattleEffectTextureDrain drained = restored.Drain505D20(scriptedSource: later);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, Assert.Single(drained.Uploads).Bytes);
    }

    [Theory]
    [InlineData(0x004B)]
    [InlineData(0x304B)]
    [InlineData(0xF04B)]
    public void Other_modes_transform_256_colors_without_reading_unselected_count_or_reference_operands(ushort raw)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(raw, 22, 0, 1);
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x1006, 4) };
        int queued = 0;
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicPaletteCommands.Execute(frame, Program, Resources(), Bank, 0, (current, rectangle, source) =>
        {
            queued++;
            Assert.Equal((short)256, rectangle.Width);
            Assert.Equal(Palette, source);
            return current;
        });
        Assert.Equal(1, queued);
        Assert.Equal(256u, result.ReadTemporaryDword(0xF4));
        Assert.Equal(0x1006u, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
        Assert.Equal(512, result.Core.Read(Palette, 512).Length);
    }

    [Fact]
    public void Reference_mode_uses_the_selected_bone_colors_and_signed_bank_arithmetic()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x104B, unchecked((short)0xFF16), 0, 1, 42);
        frame = frame with { Core = frame.Core.Write(0x2797454, Enumerable.Repeat((byte)0xFF, 128).ToArray()).Write(0x2797454, [0, 1, 0xFF]) };
        frame = WaterSchedulerFixture.SlotWord(frame, 1, 0x12, 42);
        frame = WaterSchedulerFixture.SlotWord(frame, 1, 0x8C, unchecked((ushort)-9));
        frame = WaterSchedulerFixture.SlotWord(frame, 1, 0x8E, 99);
        frame = WaterSchedulerFixture.SlotWord(frame, 1, 0x90, 0);
        uint destination = Palette - 512;
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicPaletteCommands.Execute(frame, Program, Resources(), Bank, 0, (current, _, source) =>
        {
            Assert.Equal(destination, source);
            return current;
        });
        Assert.Equal((ushort)(22 | (31 << 5) | (31 << 10)), Word(result, destination));
        Assert.Equal(0x100Au, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
    }

    [Fact]
    public void Zero_count_retains_unknown_palette_bytes_but_still_consumes_a_rectangle_ring_entry()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x204B, 22, 0, 0);
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x5180, 512), Core = frame.Core.MarkUnknown(Palette, 512) };
        int queued = 0;
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicPaletteCommands.Execute(frame, Program, Resources(), Bank, 0, (current, rectangle, source) =>
        {
            queued++;
            Assert.Equal((short)0, rectangle.Width);
            Assert.Equal(Palette, source);
            return current;
        });
        Assert.Equal(1, queued);
        Assert.Equal((byte)1, result.Core.Read(NativeBattleScriptedEffectMemory.Address + 0x43, 1)[0]);
        Assert.Throws<NotSupportedException>(() => result.Core.Read(Palette, 1));
    }

    private static NativeBattleWaterExecutionFrame Fixture(ushort raw, short selection, short packed, short count, short reference = -1)
    {
        NativeBattleWaterExecutionFrame frame = Frame().WriteTemporaryWord(0x14A, raw);
        frame = WriteCore(frame, 0x2797450, 0x1000);
        frame = WriteCore(frame, 0x279744C, NativeBattleScriptedEffectMemory.Address);
        frame = WaterSchedulerFixture.CoreWord(frame, NativeBattleScriptedEffectMemory.Address + 0x1C, 0x4002);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x8C, 40);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x8E, unchecked((ushort)-20));
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x90, 7);
        byte[] program = new byte[10];
        BinaryPrimitives.WriteUInt16LittleEndian(program, raw);
        BinaryPrimitives.WriteInt16LittleEndian(program.AsSpan(2), selection);
        BinaryPrimitives.WriteInt16LittleEndian(program.AsSpan(4), packed);
        BinaryPrimitives.WriteInt16LittleEndian(program.AsSpan(6), count);
        BinaryPrimitives.WriteInt16LittleEndian(program.AsSpan(8), reference);
        byte[] colors = new byte[512];
        BinaryPrimitives.WriteUInt16LittleEndian(colors, 0x7FFF);
        BinaryPrimitives.WriteUInt16LittleEndian(colors.AsSpan(2), 0x8000 | 3 | (25 << 5) | (26 << 10));
        BinaryPrimitives.WriteUInt16LittleEndian(colors.AsSpan(4), 0x7FFF);
        return frame with { Arena = frame.Arena.Write(0x1000, program).Write(0x5180, colors) };
    }

    private static ushort Word(NativeBattleWaterExecutionFrame frame, uint address) => BinaryPrimitives.ReadUInt16LittleEndian(frame.Core.Read(address, 2));
}
