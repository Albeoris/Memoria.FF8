using System.Buffers.Binary;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicPresentationCommandsTests
{
    [Fact]
    public void Flag2D_changes_only_the_high_bit_of_the_second_graphics_byte_without_reading_an_operand()
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.CoreDword(WaterSchedulerFixture.Create(), 0x2797450, 0x1000);
        uint published = 0;
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicPresentationCommands.SetGraphicsFlag2D(frame, 0x12345678, value => published = value);
        Assert.Equal(0x1234D678u, published);
        Assert.Equal(0x1002u, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
        Assert.Same(frame.Arena, result.Arena);
    }

    [Theory]
    [InlineData((byte)0x7F, false)]
    [InlineData((byte)0x80, true)]
    public void Fade53_reads_signed_word_and_only_the_runtime_selector_high_bit(byte selector, bool expectedDirect)
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.CoreDword(WaterSchedulerFixture.Create(), 0x2797450, 0x1000);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x27973B8, frame.TemporaryFrame.Address + 0x100);
        frame = frame.WriteTemporaryByte(0x14B, selector);
        frame = frame with { Arena = frame.Arena.Write(0x1002, BitConverter.GetBytes((short)-2)) };
        short duration = 0;
        bool direct = false;
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicPresentationCommands.FadeMusic53(frame, new(0x1000, 4, 0x1000), (value, selected) =>
        {
            duration = value;
            direct = selected;
        });
        Assert.Equal((short)-2, duration);
        Assert.Equal(expectedDirect, direct);
        Assert.Equal(0x1004u, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
        Assert.Same(frame.Arena, result.Arena);
    }
}
