using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect232Between10Tests
{
    [Theory]
    [InlineData(-100, 300, 50, 128, 128, 128, 32, 187, 309)]
    [InlineData(-100, 300, 50, 1, 1, 1, 107, 197, 309)]
    [InlineData(-100, 300, 50, -128, -128, -128, 182, 207, 309)]
    [InlineData(100, 100, 50, 128, 128, 128, 107, 187, 234)]
    [InlineData(100, 100, -50, 256, 256, 256, 107, 177, 59)]
    [InlineData(100, 100, 50, 128, -128, -128, 107, 207, 384)]
    public void Selected_update_uses_two_signed_selectors_planar_extension_and_truncating_per_axis_fractions(short secondX, short secondZ, short extension, short xFraction, short yFraction, short zFraction, ushort expectedX, ushort expectedY, ushort expectedZ)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(secondX, secondZ, extension, xFraction, yFraction, zFraction);
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x3006, 2) };
        string before = JsonSerializer.Serialize(frame.Arena);
        List<short> selectors = [];
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicBetween10.Update(frame, (owner, selector) =>
        {
            Assert.Same(frame, owner);
            selectors.Add(selector);
            return NativeBattleEffectNodeMemory.ArenaAddress + (selector == -5 ? 0x2400u : 0x2500u);
        });
        Assert.Equal(new short[] { -5, 7 }, selectors);
        Assert.Equal(expectedX, WaterSchedulerFixture.ReadSlotWord(result, 0, 0x94));
        Assert.Equal(expectedY, WaterSchedulerFixture.ReadSlotWord(result, 0, 0x96));
        Assert.Equal(expectedZ, WaterSchedulerFixture.ReadSlotWord(result, 0, 0x98));
        Assert.Equal(200u, result.ReadTemporaryDword(0xFC));
        Assert.Equal(frame.Core, result.Core);
        Assert.Equal(frame.Random, result.Random);
        Assert.Equal(before, JsonSerializer.Serialize(frame.Arena));
        Assert.Throws<NotSupportedException>(() => result.Arena.Read(0x3006, 2));
    }

    [Fact]
    public void Unknown_extension_is_rejected_without_publishing_any_position()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(-100, 300, 50, 128, 128, 128);
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x3004, 2) };
        byte[] position = frame.Arena.Read(0x2094, 6);
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicBetween10.Update(frame, (_, selector) => NativeBattleEffectNodeMemory.ArenaAddress + (selector == -5 ? 0x2400u : 0x2500u)));
        Assert.Equal(position, frame.Arena.Read(0x2094, 6));
    }

    private static NativeBattleWaterExecutionFrame Fixture(short secondX, short secondZ, short extension, short xFraction, short yFraction, short zFraction)
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create();
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0xA0, NativeBattleEffectNodeMemory.ArenaAddress + 0x3000);
        short[] block = [-5, 7, extension];
        for (int index = 0; index < block.Length; index++)
        {
            frame = frame with { Arena = frame.Arena.Write(0x3000 + index * 2, BitConverter.GetBytes(block[index])) };
        }
        short[] first = [100, 200, 300];
        short[] second = [secondX, 180, secondZ];
        short[] fractions = [xFraction, yFraction, zFraction];
        short[] offsets = [7, -3, 9];
        for (int lane = 0; lane < 3; lane++)
        {
            frame = frame with { Arena = frame.Arena.Write(0x2494 + lane * 2, BitConverter.GetBytes(first[lane])).Write(0x2594 + lane * 2, BitConverter.GetBytes(second[lane])) };
            frame = WaterSchedulerFixture.SlotDword(frame, 0, 0x50 + lane * 4, unchecked((uint)(fractions[lane] << 16)));
            frame = WaterSchedulerFixture.SlotDword(frame, 0, 0x5C + lane * 4, unchecked((uint)(offsets[lane] << 16)));
        }
        return frame;
    }
}
