using System.Buffers.Binary;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect231Color83Tests
{
    [Theory]
    [InlineData(-32768, 256, 127, 0xAB7FFF00u)]
    [InlineData(-1, 0, 32767, 0xABFF0000u)]
    [InlineData(255, 255, 255, 0xABFFFFFFu)]
    [InlineData(1, 2, 3, 0xAB030201u)]
    public void Signed_cached_channels_are_clamped_and_preserve_the_command_byte(short red, short green, short blue, uint expected)
    {
        NativeBattleWaterExecutionFrame source = Fixture(red, green, blue);
        NativeBattleWaterExecutionFrame restored = NativeBattleStorageSnapshots.Restore(NativeBattleStorageSnapshots.Capture(source));
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicControlCommands.StoreCachedColor83(source);
        Assert.Equal(expected, BinaryPrimitives.ReadUInt32LittleEndian(result.Arena.Read(0x20CC, 4)));
        Assert.Equal(0x80001002u, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
        Assert.Equal(source.Arena.Read(0x208C, 6), result.Arena.Read(0x208C, 6));
        Assert.Equal(source.Arena.Read(0x2050, 0x38), result.Arena.Read(0x2050, 0x38));
        Assert.Equal(source.TemporaryFrame, result.TemporaryFrame);
        Assert.Equal(source.Random, result.Random);
        Assert.Equal(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(result)), JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(NativeBattleCinematicControlCommands.StoreCachedColor83(restored))));
    }

    [Fact]
    public void Discarded_old_rgb_bytes_are_not_required_and_become_known()
    {
        NativeBattleWaterExecutionFrame source = Fixture(1, 2, 3);
        source = source with { Arena = source.Arena.MarkUnknown(0x20CC, 3) };
        NativeBattleWaterExecutionFrame before = NativeBattleStorageSnapshots.Capture(source);
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicControlCommands.StoreCachedColor83(source);
        Assert.Equal(0xAB030201u, BinaryPrimitives.ReadUInt32LittleEndian(result.Arena.Read(0x20CC, 4)));
        Assert.Throws<NotSupportedException>(() => before.Arena.Read(0x20CC, 3));
        Assert.Same(source.Arena, result.Arena);
    }

    [Theory]
    [InlineData(0x208E)]
    [InlineData(0x20CF)]
    public void Unknown_consumed_channel_or_retained_command_byte_fails_without_mutating_input(int unknownOffset)
    {
        NativeBattleWaterExecutionFrame source = Fixture(1, 2, 3);
        source = source with { Arena = source.Arena.MarkUnknown(unknownOffset, 1) };
        string before = JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(source));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicControlCommands.StoreCachedColor83(source));
        Assert.Equal(before, JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(source)));
        Assert.Equal(0x80001000u, WaterSchedulerFixture.ReadCoreDword(source, 0x2797450));
    }

    private static NativeBattleWaterExecutionFrame Fixture(short red, short green, short blue)
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create();
        byte[] channels = new byte[6];
        BinaryPrimitives.WriteInt16LittleEndian(channels, red);
        BinaryPrimitives.WriteInt16LittleEndian(channels.AsSpan(2), green);
        BinaryPrimitives.WriteInt16LittleEndian(channels.AsSpan(4), blue);
        frame = frame with { Arena = frame.Arena.Write(0x208C, channels).Write(0x20CC, [0x56, 0x34, 0x12, 0xAB]) };
        return WaterSchedulerFixture.CoreDword(frame, 0x2797450, 0x80001000);
    }
}
