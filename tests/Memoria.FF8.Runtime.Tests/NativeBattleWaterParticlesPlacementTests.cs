using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed partial class NativeBattleWaterParticlesTests
{
    [Theory]
    [InlineData(0x0009, 1, 100, 0xFFFE, 300)]
    [InlineData(0xAB09, 1, 100, 0xFFFE, 300)]
    [InlineData(0x0009, 2, 4096, 0xFFFE, 0)]
    public void Recenter_zeroes_all_offsets_before_selected_placement_and_preserves_wrapped_current_origin(ushort instruction, byte mode, ushort expectedX, ushort expectedY, ushort expectedZ)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(instruction).WriteTemporaryWord(0x38, 0x8123).WriteTemporaryWord(0x3C, 0x8456).WriteTemporaryWord(0x13E, 0);
        frame = frame with { Arena = frame.Arena.Write(Particle + 0x37, [mode]).MarkUnknown(Particle + 0x42, 6) };
        frame = PutDword(frame, 0x14, mode == 1 ? 100u << 8 : 0x100000);
        frame = PutDword(frame, 0x18, 0xFFFFFE00);
        frame = PutDword(frame, 0x1C, mode == 1 ? 300u << 8 : 0x1400u << 4);
        List<string> handlers = [];
        NativeBattleWaterParticleServices services = Services() with { RequireHandler = (table, index) => handlers.Add(table + ":" + index) };
        NativeBattleWaterExecutionFrame replayFrame = NativeBattleTestSnapshots.RestoreJson<NativeBattleWaterExecutionFrame>(NativeBattleTestSnapshots.Json(frame))!;
        NativeBattleWaterExecutionFrame result = One(frame, services);
        Assert.Equal(expectedX, Word(result, 0x42));
        Assert.Equal(expectedY, Word(result, 0x44));
        Assert.Equal(expectedZ, Word(result, 0x46));
        Assert.Equal((ushort)0, result.ReadTemporaryWord(0x13E));
        Assert.Equal(0x1002u, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
        Assert.Equal(new[] { "pop_table:9", "ptype_table:" + mode }, handlers);
        Assert.Equal(frame.Random, result.Random);
        Assert.Equal(NativeBattleTestSnapshots.Json(result), NativeBattleTestSnapshots.Json(One(replayFrame)));
    }

    [Fact]
    public void Polar_mode_instruction_writes_only_its_byte_without_reading_operands_or_old_mode()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0xFF0A).WriteTemporaryWord(0x13E, 0);
        frame = frame with { Arena = frame.Arena.MarkUnknown(Particle, 0x50).MarkUnknown(0x1002, 2) };
        NativeBattleWaterExecutionFrame result = One(frame);
        Assert.Equal((byte)2, result.Arena.Read(Particle + 0x37, 1)[0]);
        Assert.Throws<NotSupportedException>(() => result.Arena.Read(Particle + 0x36, 1));
        Assert.Throws<NotSupportedException>(() => result.Arena.Read(Particle + 0x38, 1));
        Assert.Equal(0x1002u, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
        Assert.Equal((ushort)0, result.ReadTemporaryWord(0x13E));
    }

    [Fact]
    public void Placement_selector_is_signed_and_keeps_the_selected_negative_index_in_diagnostics()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(9);
        frame = frame with { Arena = frame.Arena.Write(Particle + 0x37, [0xFF]) };
        NotSupportedException error = Assert.Throws<NotSupportedException>(() => One(frame));
        Assert.Contains("placement mode -1", error.Message);
    }
}
