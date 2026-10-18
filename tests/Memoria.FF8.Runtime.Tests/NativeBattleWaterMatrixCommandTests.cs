using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleWaterMatrixCommandTests
{
    private const uint MatrixBase = 0x27979E8;
    private const uint Pc = 0x80001000;
    private static readonly NativeBattleWaterSlotBank Bank = new(0x2000, 0x400, 4, 0x2000);

    [Fact]
    public void AllocationExcludesRowZeroAndFindsFirstExistingBeforeEarlierFreeRow()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x67);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x12, 0x8001);
        frame = WaterSchedulerFixture.CoreWord(frame, 0x2797204, 0x8001);
        frame = WaterSchedulerFixture.CoreWord(frame, 0x2797206, 7);
        frame = WaterSchedulerFixture.CoreWord(frame, 0x279720A, 0x8001);
        frame = WaterSchedulerFixture.CoreWord(frame, 0x279720E, 0x8001);
        frame = frame with { Core = frame.Core.Write(MatrixBase, Enumerable.Repeat((byte)0xA5, 64 * 32).ToArray()) };
        var allocated = NativeBattleWaterMatrixCommands.AllocateB65480(frame);
        Assert.Equal(MatrixBase + 3 * 32u, allocated.MatrixAddress);
        Assert.Same(frame, allocated.Frame);
        Assert.All(allocated.Frame.Core.Read(MatrixBase, 64 * 32), value => Assert.Equal(0xA5, value));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(0xFFFF)]
    public void AllocationUsesUnsignedIdAndNeverInitializesPayload(ushort id)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x67);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x12, id);
        frame = WaterSchedulerFixture.CoreWord(frame, 0x2797204, id);
        frame = frame with { Core = frame.Core.MarkUnknown(MatrixBase + 32, 32) };
        var allocated = NativeBattleWaterMatrixCommands.AllocateB65480(frame);
        Assert.Equal(MatrixBase + 32, allocated.MatrixAddress);
        Assert.Equal(id, ReadWord(allocated.Frame, 0x2797206));
        Assert.Throws<NotSupportedException>(() => allocated.Frame.Core.Read(MatrixBase + 32, 1));
    }

    [Fact]
    public void FullIdTableOverwritesOnlyLastHandleAndRetainsMatrix63()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x67);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x12, 0xFFFF);
        for (int row = 1; row < 64; row++)
        {
            frame = WaterSchedulerFixture.CoreWord(frame, 0x2797204 + (uint)(2 * row), (ushort)row);
        }
        frame = frame with { Core = frame.Core.Write(MatrixBase + 63 * 32u, Enumerable.Repeat((byte)0x91, 32).ToArray()) };
        var allocated = NativeBattleWaterMatrixCommands.AllocateB65480(frame);
        Assert.Equal(MatrixBase + 63 * 32u, allocated.MatrixAddress);
        Assert.Equal((ushort)0xFFFF, ReadWord(allocated.Frame, 0x2797204 + 126));
        Assert.Equal((ushort)62, ReadWord(allocated.Frame, 0x2797204 + 124));
        Assert.All(allocated.Frame.Core.Read(allocated.MatrixAddress, 32), value => Assert.Equal(0x91, value));
    }

    [Fact]
    public void Matrix67UsesViewTimesEulerAndFullMacTranslationWithKnownZeroFocal()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x67);
        NativeBattleActionMatrix view = new(new short[] { 0, -4096, 0, 4096, 0, 0, 0, 0, 4096 }.ToImmutableArray(), 50000, -50000, 70000, 4096);
        frame = WriteMatrix(frame, MatrixBase, view);
        frame = frame with { Core = frame.Core.MarkUnknown(MatrixBase + 18, 2), Arena = frame.Arena.MarkUnknown(0x1002, 2) };
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x12, 7);
        frame = WaterSchedulerFixture.CoreWord(frame, 0x2797206, 7);
        frame = Vector(frame, 0, 0x94, new(30000, 10000, -10000));
        NativeBattleVector angles = new(-12, 256, -513);
        frame = Vector(frame, 0, 0x8C, angles);
        NativeBattleWaterExecutionFrame result = Run(frame, 2);
        uint destination = MatrixBase + 32;
        Assert.Equal(40000u, WaterSchedulerFixture.ReadCoreDword(result, destination + 20));
        Assert.Equal(unchecked((uint)-20000), WaterSchedulerFixture.ReadCoreDword(result, destination + 24));
        Assert.Equal(60000u, WaterSchedulerFixture.ReadCoreDword(result, destination + 28));
        NativeBattleActionMatrix euler = NativeBattleGfMatrices.Euler56CD50(angles);
        NativeBattleActionMatrix expected = NativeBattleGfMatrices.Multiply56C090(view, euler);
        for (int lane = 0; lane < 9; lane++)
        {
            Assert.Equal(unchecked((ushort)expected.Rotation[lane]), ReadWord(result, destination + (uint)(lane * 2)));
        }
        Assert.Equal((ushort)0, ReadWord(result, destination + 18));
        Assert.False(result.TemporaryFrame.Known[0xF2]);
        Assert.False(result.TemporaryFrame.Known[0xF3]);
        Assert.Equal(unchecked((ushort)euler.Rotation[8]), result.ReadTemporaryWord(0xF0));
        Assert.Equal(0xA5A5A5A5u, result.ReadTemporaryDword(0xF4));
        Assert.Equal(0xA5A5A5A5u, result.ReadTemporaryDword(0xF8));
        Assert.Equal(0xA5A5A5A5u, result.ReadTemporaryDword(0xFC));
        Assert.Equal(Pc + 4, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
        Assert.Throws<NotSupportedException>(() => result.Core.Read(MatrixBase + 18, 2));
    }

    [Theory]
    [InlineData(40000, 0, 0, 0x81000000u)]
    [InlineData(0, -40000, 0, 0x80800000u)]
    [InlineData(0, 0, 40000, 0x00400000u)]
    [InlineData(40000, -40000, 40000, 0x81C00000u)]
    public void SharedFullMacReportsOnlyIrSaturationAndRetainsFullDwords(int x, int y, int z, uint flags)
    {
        NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Euler56CD50(new(0, 0, 0)) with { X = x, Y = y, Z = z };
        NativeBattleFullMacVertex mac = NativeBattleGfMatrices.TransformMac460840(matrix, new(0, 0, 0));
        Assert.Equal(new NativeBattleFullMacVertex(x, y, z, flags), mac);
        Assert.Equal(new NativeBattleVector((short)Math.Clamp(x, short.MinValue, short.MaxValue), (short)Math.Clamp(y, short.MinValue, short.MaxValue), (short)Math.Clamp(z, short.MinValue, short.MaxValue)), NativeBattleGfMatrices.Transform460840(matrix, new(0, 0, 0)));
    }

    [Fact]
    public void Matrix63UsesNestedSlotContextRestoresOriginalAndUpdatesWithoutPositionCache()
    {
        NativeBattleWaterExecutionFrame frame = Nested();
        byte[] referenceBefore = frame.Arena.Read(0x2100, 0x200);
        frame = frame with { Core = frame.Core.MarkUnknown(MatrixBase, 32).MarkUnknown(0x2797204, 128), Arena = frame.Arena.MarkUnknown(0x208C, 6) };
        NativeBattleWaterExecutionFrame result = Run(frame);
        Assert.Equal(NativeBattleEffectNodeMemory.ArenaAddress + 0x2000, WaterSchedulerFixture.ReadCoreDword(result, 0x27973E8));
        Assert.Equal(50000u, result.ReadTemporaryDword(0xF0));
        Assert.Equal(unchecked((uint)-20000), result.ReadTemporaryDword(0xF4));
        Assert.Equal(52767u, result.ReadTemporaryDword(0xF8));
        Assert.Equal(0x81400000u, result.ReadTemporaryDword(0xFC));
        Assert.All(result.TemporaryFrame.Known.Skip(0xF0).Take(16), value => Assert.True(value));
        Assert.Equal(unchecked(50000u << 16), WaterSchedulerFixture.ReadSlotDword(result, 0, 0x5C));
        Assert.Equal(unchecked((uint)-20000 << 16), WaterSchedulerFixture.ReadSlotDword(result, 0, 0x60));
        Assert.Equal(unchecked(52767u << 16), WaterSchedulerFixture.ReadSlotDword(result, 0, 0x64));
        Assert.Equal((ushort)50000, WaterSchedulerFixture.ReadSlotWord(result, 0, 0x94));
        Assert.Equal(unchecked((ushort)-20000), WaterSchedulerFixture.ReadSlotWord(result, 0, 0x96));
        Assert.Equal((ushort)52767, WaterSchedulerFixture.ReadSlotWord(result, 0, 0x98));
        Assert.Equal(frame.Arena.Read(0x2050, 12), result.Arena.Read(0x2050, 12));
        Assert.Throws<NotSupportedException>(() => result.Arena.Read(0x208C, 6));
        Assert.Equal(referenceBefore, result.Arena.Read(0x2100, 0x200));
        Assert.Equal(Pc + 6, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
    }

    [Fact]
    public void UnsupportedOrderFailsBeforeReadingUnknownEulerAndNextCommandSurvivesJson()
    {
        NativeBattleWaterExecutionFrame frame = Nested();
        frame = frame with { Arena = frame.Arena.Write(0x1004, new byte[] { 1, 0 }).MarkUnknown(0x228C, 6) };
        NotSupportedException error = Assert.Throws<NotSupportedException>(() => Run(frame));
        Assert.Contains("rotation order zero", error.Message);
        Assert.Throws<NotSupportedException>(() => Run(Fixture(0x1067), 2));
        frame = Nested();
        frame = frame with { Arena = frame.Arena.Write(0x1006, new byte[] { 0x63, 0, 0xFF, 0xFF, 0, 0 }) };
        frame = Run(frame, 12);
        NativeBattleWaterExecutionFrame restored = JsonSerializer.Deserialize<NativeBattleWaterExecutionFrame>(JsonSerializer.Serialize(frame))!;
        Assert.Equal(JsonSerializer.Serialize(Run(frame, 12)), JsonSerializer.Serialize(Run(restored, 12)));
    }

    private static NativeBattleWaterExecutionFrame Nested()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x63);
        frame = frame with { Core = frame.Core.Write(0x2797454, new byte[] { 0, 1, 2, 0xFF }) };
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x12, 300);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x14, 100);
        frame = WaterSchedulerFixture.SlotWord(frame, 1, 0x12, 100);
        frame = WaterSchedulerFixture.SlotWord(frame, 1, 0x14, 200);
        frame = WaterSchedulerFixture.SlotWord(frame, 1, 0x9C, 0xFFFF);
        frame = WaterSchedulerFixture.SlotWord(frame, 2, 0x12, 200);
        frame = Vector(frame, 1, 0x94, new(30000, 10000, 32767));
        return Vector(frame, 2, 0x94, new(20000, -30000, 20000));
    }

    private static NativeBattleWaterExecutionFrame Fixture(ushort instruction)
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create().WriteTemporaryWord(0x14A, instruction);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797450, Pc);
        return frame with { Arena = frame.Arena.Write(0x1000, new byte[] { (byte)instruction, (byte)(instruction >> 8), 0xFF, 0xFF, 0, 0 }) };
    }

    private static NativeBattleWaterExecutionFrame Vector(NativeBattleWaterExecutionFrame frame, int index, int offset, NativeBattleVector vector)
    {
        frame = WaterSchedulerFixture.SlotWord(frame, index, offset, unchecked((ushort)vector.X));
        frame = WaterSchedulerFixture.SlotWord(frame, index, offset + 2, unchecked((ushort)vector.Y));
        return WaterSchedulerFixture.SlotWord(frame, index, offset + 4, unchecked((ushort)vector.Z));
    }

    private static NativeBattleWaterExecutionFrame WriteMatrix(NativeBattleWaterExecutionFrame frame, uint address, NativeBattleActionMatrix matrix)
    {
        for (int lane = 0; lane < 9; lane++)
        {
            frame = WaterSchedulerFixture.CoreWord(frame, address + (uint)(2 * lane), unchecked((ushort)matrix.Rotation[lane]));
        }
        frame = WaterSchedulerFixture.CoreDword(frame, address + 20, unchecked((uint)matrix.X));
        frame = WaterSchedulerFixture.CoreDword(frame, address + 24, unchecked((uint)matrix.Y));
        return WaterSchedulerFixture.CoreDword(frame, address + 28, unchecked((uint)matrix.Z));
    }

    private static ushort ReadWord(NativeBattleWaterExecutionFrame frame, uint address) => BinaryPrimitives.ReadUInt16LittleEndian(frame.Core.Read(address, 2));
    private static NativeBattleWaterExecutionFrame Run(NativeBattleWaterExecutionFrame frame, int length = 6) => NativeBattleWaterMatrixCommands.Execute(frame, new(0x1000, length, Pc), Bank);
}
