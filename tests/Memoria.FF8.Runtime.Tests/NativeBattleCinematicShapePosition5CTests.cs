using System.Buffers.Binary;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicShapePosition5CTests
{
    private const uint Base = NativeBattleEffectNodeMemory.ArenaAddress;
    private const uint Pc = 0x80001000;

    [Theory]
    [InlineData(false, 35, 45, 44, 6)]
    [InlineData(true, 9, 21, 37, 8)]
    public void Both_shape_paths_restore_the_caller_before_update_and_preserve_native_signed_math_and_snapshot(bool transformed, int x, int y, int z, uint advance)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(transformed);
        List<uint> inputs = [];
        int updates = 0;
        NativeBattleCinematicShapePositionServices services = new((current, selector) =>
        {
            Assert.Contains(selector, new short[] { 1, 2, 3 });
            return Base + 0x2000u + (uint)(selector * 0x100);
        }, (_, selector) => selector switch
        {
            4 => Base + 0x4000,
            5 => Base + 0x5000,
            _ => throw new InvalidOperationException()
        }, (current, address) =>
        {
            inputs.Add(address);
            return current;
        }, (current, kind) =>
        {
            updates++;
            Assert.Equal((byte)0, kind);
            Assert.Equal(Base + 0x2000, WaterSchedulerFixture.ReadCoreDword(current, 0x27973E8));
            Assert.Equal(Pc + advance, WaterSchedulerFixture.ReadCoreDword(current, 0x2797450));
            return current;
        });
        NativeBattleWaterExecutionFrame Run(NativeBattleWaterExecutionFrame current) => NativeBattleCinematicShapePosition5C.Execute(current, new(0x1000, 8, Pc), services);
        NativeBattleWaterExecutionFrame result = Run(frame);
        Assert.Equal(unchecked((uint)(x << 16)), WaterSchedulerFixture.ReadSlotDword(result, 0, 0x5C));
        Assert.Equal(unchecked((uint)(y << 16)), WaterSchedulerFixture.ReadSlotDword(result, 0, 0x60));
        Assert.Equal(unchecked((uint)(z << 16)), WaterSchedulerFixture.ReadSlotDword(result, 0, 0x64));
        Assert.Equal(transformed ? new uint[] { Base + 0x2194, Base + 0x4080 } : [], inputs);
        Assert.Same(frame.Random, result.Random);
        Assert.Equal(frame.ReadTemporaryWord(0x13E), result.ReadTemporaryWord(0x13E));
        Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(Run(JsonSerializer.Deserialize<NativeBattleWaterExecutionFrame>(JsonSerializer.Serialize(frame))!)));
        Assert.Equal(2, updates);
    }

    private static NativeBattleWaterExecutionFrame Fixture(bool transformed)
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create();
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797450, Pc);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x27973B8, frame.TemporaryFrame.Address + 0x100);
        frame = frame.WriteTemporaryByte(0x14B, transformed ? (byte)0x80 : (byte)0);
        frame = frame with { Arena = frame.Arena.Write(0x1000, [0x5C, 0x80, 1, 0, 0, 0, 0, 0]).Write(0x2018, [0]) };
        frame = WaterSchedulerFixture.SlotWord(frame, 1, 0x9C, 2);
        frame = WaterSchedulerFixture.SlotDword(frame, 1, transformed ? 0xD8 : 0xBC, Base + (transformed ? 0x4000u : 0x3000u));
        frame = frame with { Arena = frame.Arena.Write(0x4014, BitConverter.GetBytes(0x80u)).Write(0x5014, BitConverter.GetBytes(0x80u)).Write(0x3000, [3, 0, 4, 0, 5, 0]) };
        short[] origin = [10, 20, 30];
        short[] local = [-2, 3, 4];
        short[] weights = [-128, 128, -129];
        short[] scales = transformed ? [256, 256, 256] : [128, -256, 512];
        short[] first = transformed ? [1, -2, 3] : [100, -100, 7];
        short[] second = [200, 50, 8];
        for (int lane = 0; lane < 3; lane++)
        {
            frame = WaterSchedulerFixture.SlotWord(frame, 1, 0x94 + lane * 2, unchecked((ushort)local[lane]));
            frame = WaterSchedulerFixture.SlotWord(frame, 1, 0x8C + lane * 2, unchecked((ushort)scales[lane]));
            frame = WaterSchedulerFixture.SlotWord(frame, 2, 0x94 + lane * 2, unchecked((ushort)origin[lane]));
            frame = WaterSchedulerFixture.SlotWord(frame, 2, 0x8C + lane * 2, 0);
            frame = WaterSchedulerFixture.SlotWord(frame, 3, 0x94 + lane * 2, unchecked((ushort)weights[lane]));
            frame = frame with { Arena = frame.Arena.Write(0x4080 + lane * 2, BitConverter.GetBytes(first[lane])).Write(0x5080 + lane * 2, BitConverter.GetBytes(second[lane])) };
        }
        return frame with { Arena = transformed ? frame.Arena.MarkUnknown(0x21BC, 4).MarkUnknown(0x3000, 6) : frame.Arena.MarkUnknown(0x1006, 2).MarkUnknown(0x21D8, 4) };
    }
}
