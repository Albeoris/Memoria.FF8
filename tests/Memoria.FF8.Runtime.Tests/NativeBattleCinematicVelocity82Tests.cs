using System.Buffers.Binary;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicVelocity82Tests
{
    [Theory]
    [InlineData((short)3, 2, -2)]
    [InlineData((short)-3, -2, 2)]
    public void Divides_wrapping_signed_fixed_deltas_without_refreshing_cached_position(short duration, int x, int y)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(duration);
        int[] positions = [-2, 5, int.MaxValue];
        int[] targets = [5, -2, int.MinValue];
        for (int lane = 0; lane < 3; lane++)
        {
            frame = WaterSchedulerFixture.SlotDword(frame, 0, 0x5C + lane * 4, unchecked((uint)positions[lane]));
            frame = WaterSchedulerFixture.SlotDword(frame, 1, 0x5C + lane * 4, unchecked((uint)targets[lane]));
        }
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x2094, 6).MarkUnknown(0x2018, 1).MarkUnknown(0x2168, 4) };
        NativeBattleWaterExecutionFrame Run(NativeBattleWaterExecutionFrame active) => NativeBattleCinematicVelocity82.Execute(active, new(0x1000, 6, 0x1000), (_, selector) =>
        {
            Assert.Equal(-1, selector);
            return Address(0x2100);
        });
        NativeBattleWaterExecutionFrame result = Run(frame);
        Assert.Equal(unchecked((uint)x), WaterSchedulerFixture.ReadSlotDword(result, 0, 0x74));
        Assert.Equal(unchecked((uint)y), WaterSchedulerFixture.ReadSlotDword(result, 0, 0x78));
        Assert.Equal(0u, WaterSchedulerFixture.ReadSlotDword(result, 0, 0x7C));
        Assert.Equal(0x1006u, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
        Assert.Same(frame.TemporaryFrame, result.TemporaryFrame);
        Assert.Same(frame.Random, result.Random);
        Assert.Throws<NotSupportedException>(() => result.Arena.Read(0x2094, 6));
        Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(Run(JsonSerializer.Deserialize<NativeBattleWaterExecutionFrame>(JsonSerializer.Serialize(frame))!)));
    }

    [Fact]
    public void Zero_and_signed_overflow_divisors_preserve_original_integer_faults()
    {
        NativeBattleWaterExecutionFrame zero = Fixture(0);
        Assert.Throws<DivideByZeroException>(() => NativeBattleCinematicVelocity82.Execute(zero, new(0x1000, 6, 0x1000), (_, _) => Address(0x2100)));
        NativeBattleWaterExecutionFrame overflow = Fixture(-1);
        overflow = WaterSchedulerFixture.SlotDword(overflow, 0, 0x5C, 0);
        overflow = WaterSchedulerFixture.SlotDword(overflow, 1, 0x5C, 0x80000000);
        Assert.Throws<OverflowException>(() => NativeBattleCinematicVelocity82.Execute(overflow, new(0x1000, 6, 0x1000), (_, _) => Address(0x2100)));
    }

    private static NativeBattleWaterExecutionFrame Fixture(short duration)
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create();
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797450, 0x1000);
        byte[] program = new byte[6];
        BinaryPrimitives.WriteUInt16LittleEndian(program, 0x82);
        BinaryPrimitives.WriteInt16LittleEndian(program.AsSpan(2), -1);
        BinaryPrimitives.WriteInt16LittleEndian(program.AsSpan(4), duration);
        return frame with { Arena = frame.Arena.Write(0x1000, program) };
    }

    private static uint Address(int offset) => unchecked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)offset);
}
