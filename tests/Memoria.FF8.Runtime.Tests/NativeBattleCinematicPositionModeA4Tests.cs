using System.Buffers.Binary;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicPositionModeA4Tests
{
    [Theory]
    [InlineData(0, 10, 0)]
    [InlineData(10, 0, 1024)]
    [InlineData(-10, 0, 3072)]
    [InlineData(0, -10, 2048)]
    public void Cylindrical_mode_preserves_world_position_with_signed_relative_height_and_native_yaw(short x, short z, int yaw)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(3);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x94, unchecked((ushort)x));
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x96, 7);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x98, unchecked((ushort)z));
        frame = WaterSchedulerFixture.SlotWord(frame, 1, 0x94, 0);
        frame = WaterSchedulerFixture.SlotWord(frame, 1, 0x96, unchecked((ushort)-3));
        frame = WaterSchedulerFixture.SlotWord(frame, 1, 0x98, 0);
        int updates = 0;
        NativeBattleWaterExecutionFrame Run(NativeBattleWaterExecutionFrame active) => NativeBattleCinematicPositionModeA4.Execute(active, new(0x1000, 6, 0x1000), (current, selector) =>
        {
            Assert.Equal(-1, selector);
            Assert.Equal(3, current.Arena.Read(0x2018, 1)[0]);
            Assert.Equal(0xFFFF, WaterSchedulerFixture.ReadSlotWord(current, 0, 0xB0));
            return Address(0x2100);
        }, current =>
        {
            updates++;
            Assert.Equal(10u << 16, WaterSchedulerFixture.ReadSlotDword(current, 0, 0x5C));
            Assert.Equal(10u << 16, WaterSchedulerFixture.ReadSlotDword(current, 0, 0x60));
            Assert.Equal(unchecked((uint)(yaw << 16)), WaterSchedulerFixture.ReadSlotDword(current, 0, 0x64));
            Assert.Equal(10u, current.ReadTemporaryDword(0xFC));
            return current;
        });
        NativeBattleWaterExecutionFrame result = Run(frame);
        Assert.Equal(1, updates);
        Assert.Equal(0x1006u, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
        Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(Run(JsonSerializer.Deserialize<NativeBattleWaterExecutionFrame>(JsonSerializer.Serialize(frame))!)));
    }

    [Theory]
    [InlineData((short)0)]
    [InlineData((short)0x103)]
    [InlineData((short)-253)]
    public void Other_full_signed_modes_lookup_and_update_without_reading_unknown_cached_coordinates(short mode)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(mode);
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x2094, 6).MarkUnknown(0x2194, 6).MarkUnknown(0x205C, 12) };
        int calls = 0;
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicPositionModeA4.Execute(frame, new(0x1000, 6, 0x1000), (_, _) => Address(0x2100), current =>
        {
            calls++;
            Assert.Equal(unchecked((byte)mode), current.Arena.Read(0x2018, 1)[0]);
            return current;
        });
        Assert.Equal(1, calls);
        Assert.Throws<NotSupportedException>(() => result.Arena.Read(0x205C, 4));
        Assert.Equal(frame.ReadTemporaryDword(0xFC), result.ReadTemporaryDword(0xFC));
        Assert.Equal(0x1006u, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
    }

    private static NativeBattleWaterExecutionFrame Fixture(short mode)
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create();
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797450, 0x1000);
        byte[] program = new byte[6];
        BinaryPrimitives.WriteUInt16LittleEndian(program, 0xA4);
        BinaryPrimitives.WriteInt16LittleEndian(program.AsSpan(2), mode);
        BinaryPrimitives.WriteInt16LittleEndian(program.AsSpan(4), -1);
        return frame with { Arena = frame.Arena.Write(0x1000, program) };
    }

    private static uint Address(int offset) => unchecked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)offset);
}
