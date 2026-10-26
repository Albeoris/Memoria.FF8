using System.Buffers.Binary;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicNodeMeasurementsTests
{
    private const uint First = NativeBattleEffectNodeMemory.ArenaAddress + 0x2100;
    private const uint Second = NativeBattleEffectNodeMemory.ArenaAddress + 0x2200;

    [Theory]
    [InlineData(0x0103, 2, 0x01800000u)]
    [InlineData(0x0204, 3, 0x01555500u)]
    [InlineData(0x0405, -2, 0xFA000000u)]
    [InlineData(0x0700, 1, 0x0D000000u)]
    [InlineData(0x0002, 1, 0u)]
    [InlineData(0x0502, 12800, 0x00003D00u)]
    public void Distance112_selects_components_and_truncates_before_final_shift(int selector, int divisor, uint expected)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x8112, unchecked((short)selector), unchecked((short)divisor));
        List<short> resolved = [];
        int cached = 0;
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicNodeMeasurements.Execute(frame, new(0x1000, 10, 0x1000), new((current, id) => Resolve(current, id, resolved), current =>
        {
            cached++;
            Assert.Equal(0x1000u, WaterSchedulerFixture.ReadCoreDword(current, 0x2797450));
            Assert.Equal(expected, WaterSchedulerFixture.ReadSlotDword(current, 0, 0x50 + (selector & 7) * 4));
            return current;
        }, _ => throw new InvalidOperationException()));
        Assert.Equal(new short[] { 7, -3 }, resolved);
        Assert.Equal(1, cached);
        Assert.Equal(0x100Au, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
        Assert.Equal(frame.Random, result.Random);
    }

    [Fact]
    public void Distance112_preserves_unmasked_first_store_before_masked_store()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x0112, 0x0101, 2);
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicNodeMeasurements.Execute(frame, new(0x1000, 10, 0x1000), Services());
        Assert.Equal(0x06800000u, WaterSchedulerFixture.ReadSlotDword(result, 0, 0x454));
        Assert.Equal(0x01800000u, WaterSchedulerFixture.ReadSlotDword(result, 0, 0x54));
    }

    [Fact]
    public void Distance112_wraps_squared_sum_before_native_square_root()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x8112, 0x0300, 1);
        frame = Position(frame, 1, -32768, -32768, 0);
        frame = Position(frame, 2, 32767, 32767, 0);
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicNodeMeasurements.Execute(frame, new(0x1000, 10, 0x1000), Services());
        Assert.Equal(0u, WaterSchedulerFixture.ReadSlotDword(result, 0, 0x50));
    }

    [Fact]
    public void Distance112_keeps_native_divide_by_zero_failure()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x8112, 0x0100, 0);
        Assert.Throws<DivideByZeroException>(() => NativeBattleCinematicNodeMeasurements.Execute(frame, new(0x1000, 10, 0x1000), Services()));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    public void Direction113_publishes_angle_and_selects_cache_or_current_update(int lane)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x0113, unchecked((short)lane), 1);
        frame = Position(frame, 2, 100, 0, 0);
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x2196, 2).MarkUnknown(0x2296, 2) };
        List<short> resolved = [];
        List<string> published = [];
        NativeBattleWaterExecutionFrame Publish(NativeBattleWaterExecutionFrame current, string service)
        {
            published.Add(service);
            Assert.Equal(0x04000000u, WaterSchedulerFixture.ReadSlotDword(current, 0, 0x50 + lane * 4));
            Assert.Equal(100u, current.ReadTemporaryDword(0xFC));
            Assert.Equal(0x1000u, WaterSchedulerFixture.ReadCoreDword(current, 0x2797450));
            return current;
        }
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicNodeMeasurements.Execute(frame, new(0x1000, 8, 0x1000), new((current, id) => Resolve(current, id, resolved), current => Publish(current, "cache"), current => Publish(current, "update")));
        Assert.Equal(new short[] { 7, -3 }, resolved);
        Assert.Equal(new[] { lane < 3 ? "cache" : "update" }, published);
        Assert.Equal(0x1008u, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
    }

    [Fact]
    public void Truncated_distance_operand_fails_before_publication()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x8112, 0x0100, 1);
        Assert.Throws<InvalidDataException>(() => NativeBattleCinematicNodeMeasurements.Execute(frame, new(0x1000, 8, 0x1000), new((_, id) => id == 7 ? First : Second, _ => throw new InvalidOperationException(), _ => throw new InvalidOperationException())));
    }

    private static NativeBattleCinematicNodeMeasurementServices Services() => new((_, id) => id == 7 ? First : Second, current => current, _ => throw new InvalidOperationException());

    private static uint Resolve(NativeBattleWaterExecutionFrame frame, short id, List<short> resolved)
    {
        resolved.Add(id);
        Assert.Equal(0x1000u, WaterSchedulerFixture.ReadCoreDword(frame, 0x2797450));
        return id == 7 ? First : id == -3 ? Second : throw new InvalidOperationException();
    }

    private static NativeBattleWaterExecutionFrame Fixture(ushort opcode, short selector, short divisor)
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.CoreDword(WaterSchedulerFixture.Create(), 0x2797450, 0x1000).WriteTemporaryWord(0x14A, opcode);
        byte[] program = new byte[10];
        BinaryPrimitives.WriteUInt16LittleEndian(program, opcode);
        BinaryPrimitives.WriteInt16LittleEndian(program.AsSpan(2), selector);
        BinaryPrimitives.WriteInt16LittleEndian(program.AsSpan(4), 7);
        BinaryPrimitives.WriteInt16LittleEndian(program.AsSpan(6), -3);
        BinaryPrimitives.WriteInt16LittleEndian(program.AsSpan(8), divisor);
        frame = frame with { Arena = frame.Arena.Write(0x1000, program) };
        return Position(Position(frame, 1, 0, 0, 0), 2, 3, 4, 12);
    }

    private static NativeBattleWaterExecutionFrame Position(NativeBattleWaterExecutionFrame frame, int slot, short x, short y, short z)
    {
        frame = WaterSchedulerFixture.SlotWord(frame, slot, 0x94, unchecked((ushort)x));
        frame = WaterSchedulerFixture.SlotWord(frame, slot, 0x96, unchecked((ushort)y));
        return WaterSchedulerFixture.SlotWord(frame, slot, 0x98, unchecked((ushort)z));
    }
}
