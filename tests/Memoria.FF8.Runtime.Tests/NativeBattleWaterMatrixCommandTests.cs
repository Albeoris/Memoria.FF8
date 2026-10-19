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
    public void Matrix69_postmultiplies_both_selected_rotations_in_order_without_reading_the_unused_WORD_or_translations()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x69);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x12, 7);
        frame = WaterSchedulerFixture.SlotWord(frame, 1, 0x12, 8);
        frame = WaterSchedulerFixture.CoreWord(frame, 0x2797206, 7);
        frame = WaterSchedulerFixture.CoreWord(frame, 0x2797208, 8);
        frame = frame with { Core = frame.Core.Write(0x2797454, new byte[] { 0, 1, 0xFF }), Arena = frame.Arena.Write(0x1004, BitConverter.GetBytes((ushort)8)).MarkUnknown(0x1002, 2) };
        frame = WriteMatrix(frame, MatrixBase, new([2048, 0, 0, 0, 4096, 0, 0, 0, 8192], 123, 456, 789, 4096));
        frame = WriteMatrix(frame, MatrixBase + 64, new([4096, 0, 0, 0, 0, 4096, 0, -4096, 0], -12, -34, -56, 4096));
        frame = Vector(frame, 0, 0x8C, new(0, 0, 1024));
        frame = Vector(frame, 0, 0x94, new(-32768, -1, 32767));
        frame = frame with { Core = frame.Core.MarkUnknown(MatrixBase + 18, 14).MarkUnknown(MatrixBase + 64 + 18, 14).MarkUnknown(MatrixBase + 32, 32) };
        NativeBattleWaterExecutionFrame result = Run(frame);
        // A positive quarter-turn around Z maps +X to +Y before either postmultiply.
        short[] expected = [0, 0, -4096, 2048, 0, 0, 0, -8192, 0];
        for (int lane = 0; lane < expected.Length; lane++)
        {
            Assert.Equal(unchecked((ushort)expected[lane]), ReadWord(result, MatrixBase + 32 + (uint)(lane * 2)));
        }
        Assert.Equal(unchecked((uint)-32768), WaterSchedulerFixture.ReadCoreDword(result, MatrixBase + 32 + 20));
        Assert.Equal(uint.MaxValue, WaterSchedulerFixture.ReadCoreDword(result, MatrixBase + 32 + 24));
        Assert.Equal(32767u, WaterSchedulerFixture.ReadCoreDword(result, MatrixBase + 32 + 28));
        Assert.Equal((ushort)0, ReadWord(result, MatrixBase + 32 + 18));
        Assert.Equal(Pc + 6, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
        Assert.Same(frame.TemporaryFrame, result.TemporaryFrame);
        Assert.Same(frame.Random, result.Random);
        Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(Run(JsonSerializer.Deserialize<NativeBattleWaterExecutionFrame>(JsonSerializer.Serialize(frame))!)));
    }

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
    public void NonzeroOrderConsumesItsBasisEulerAndNextCommandSurvivesJson()
    {
        NativeBattleWaterExecutionFrame frame = Nested();
        frame = frame with { Arena = frame.Arena.Write(0x1004, new byte[] { 1, 0 }).MarkUnknown(0x228C, 6) };
        Assert.Throws<NotSupportedException>(() => Run(frame));
        frame = Nested();
        frame = frame with { Arena = frame.Arena.Write(0x1006, new byte[] { 0x63, 0, 0xFF, 0xFF, 0, 0 }) };
        frame = Run(frame, 12);
        NativeBattleWaterExecutionFrame restored = JsonSerializer.Deserialize<NativeBattleWaterExecutionFrame>(JsonSerializer.Serialize(frame))!;
        Assert.Equal(JsonSerializer.Serialize(Run(frame, 12)), JsonSerializer.Serialize(Run(restored, 12)));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(15)]
    [InlineData(-1)]
    public void NonzeroRotationModesPreserveSeedTailAndNativeAxisScratch(int mode)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(unchecked((ushort)((mode << 12) | 0x67)));
        NativeBattleActionMatrix seed = NativeBattleGfMatrices.Euler56CD50(new(0, 0, 0));
        frame = WriteMatrix(frame, 0x2797968, seed with { X = 0x12345678 });
        frame = frame with { Core = frame.Core.MarkUnknown(0x279797A, 2) };
        NativeBattleVector angles = new(256, -513, 777);
        NativeBattleActionMatrix x = NativeBattleGfMatrices.RotateX56CFB0(seed, angles.X);
        NativeBattleActionMatrix y = NativeBattleGfMatrices.RotateY56D020(seed, angles.Y);
        NativeBattleActionMatrix z = NativeBattleGfMatrices.RotateZ56D090(seed, angles.Z);
        NativeBattleActionMatrix expected = mode switch
        {
            1 => NativeBattleGfMatrices.Multiply56C090(NativeBattleGfMatrices.Multiply56C090(z, x), y),
            2 => NativeBattleGfMatrices.Multiply56C090(NativeBattleGfMatrices.Multiply56C090(x, z), y),
            3 => NativeBattleGfMatrices.Euler56CE30(angles),
            _ => z
        };
        var result = NativeBattleWaterMatrixCommands.BuildRotationB65590(frame, angles, mode);
        Assert.Equal(expected.Rotation, result.Matrix.Rotation);
        foreach (int offset in new[] { 0xA0, 0xC0, 0xE0 })
        {
            Assert.False(result.Frame.TemporaryFrame.Known[offset + 18]);
            Assert.False(result.Frame.TemporaryFrame.Known[offset + 19]);
            Assert.Equal(0x12345678u, result.Frame.ReadTemporaryDword(offset + 20));
            Assert.Equal(frame.TemporaryFrame.Bytes.Skip(offset + 24).Take(8), result.Frame.TemporaryFrame.Bytes.Skip(offset + 24).Take(8));
        }
        NativeBattleActionMatrix expectedX = mode == 1 ? NativeBattleGfMatrices.Multiply56C090(z, x) : x;
        for (int lane = 0; lane < 9; lane++)
        {
            Assert.Equal(unchecked((ushort)expectedX.Rotation[lane]), result.Frame.ReadTemporaryWord(0xA0 + lane * 2));
            Assert.Equal(unchecked((ushort)y.Rotation[lane]), result.Frame.ReadTemporaryWord(0xC0 + lane * 2));
            Assert.Equal(unchecked((ushort)expected.Rotation[lane]), result.Frame.ReadTemporaryWord(0xE0 + lane * 2));
        }
        Assert.Throws<NotSupportedException>(() => frame.Core.Read(0x279797A, 2));
    }

    [Theory]
    [InlineData(0x00D4, 0x12, 0x34, 0x56, 0xA5563412u, 8)]
    [InlineData(0x7ED4, 0x1234, -2, 0x5678, 0xFFFFFE34u, 8)]
    [InlineData(0x80D4, 0x12, 0, 0, 0xA5121212u, 4)]
    [InlineData(0xFED4, -2, 0, 0, 0xFFFFFFFEu, 4)]
    public void ColorD4UsesOnlyUniformBitAndSignedUnmaskedChannels(ushort instruction, short red, short green, short blue, uint expected, int width)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(instruction);
        byte[] operands = new byte[6];
        BinaryPrimitives.WriteInt16LittleEndian(operands, red);
        BinaryPrimitives.WriteInt16LittleEndian(operands.AsSpan(2), green);
        BinaryPrimitives.WriteInt16LittleEndian(operands.AsSpan(4), blue);
        frame = frame with { Arena = frame.Arena.Write(0x1002, operands) };
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0xCC, 0xA5C0B0A0);
        if (width == 4)
        {
            frame = frame with { Arena = frame.Arena.MarkUnknown(0x1004, 4) };
        }
        NativeBattleWaterExecutionFrame result = Run(frame, width);
        Assert.Equal(Pc + (uint)width, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
        Assert.Equal(expected, BinaryPrimitives.ReadUInt32LittleEndian(result.Arena.Read(Bank.SlotOffset(result.Arena, 0) + 0xCC, 4)));
        string json = JsonSerializer.Serialize(result);
        Assert.Equal(json, JsonSerializer.Serialize(JsonSerializer.Deserialize<NativeBattleWaterExecutionFrame>(json)!));
    }

    [Theory]
    [InlineData(0x006A)]
    [InlineData(0x306A)]
    [InlineData(0x00C4)]
    [InlineData(0x30C4)]
    public void Matrix6A_and_C4_use_direct_cached_translation_and_current_manager_word_without_reading_an_operand(ushort instruction)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(instruction);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x12, 7);
        frame = WaterSchedulerFixture.CoreWord(frame, 0x2797206, 7);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x27973EC, 0x2797690);
        frame = WaterSchedulerFixture.CoreWord(frame, 0x2797692, 0xFEDC);
        frame = WriteMatrix(frame, MatrixBase, new(new short[] { 0, -4096, 0, 4096, 0, 0, 0, 0, 4096 }.ToImmutableArray(), 50000, -50000, 60000, 4096));
        frame = Vector(frame, 0, 0x8C, new(0, 0, 0));
        frame = Vector(frame, 0, 0x94, new(-123, 456, -789));
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x1002, 2), Core = frame.Core.MarkUnknown(MatrixBase + 18, 14) };
        if ((instruction & 0x1FF) == 0xC4)
        {
            frame = frame with { Core = frame.Core.MarkUnknown(MatrixBase, 32) };
        }
        NativeBattleWaterExecutionFrame result = Run(frame, 2);
        Assert.Equal((ushort)((instruction & 0x1FF) == 0xC4 ? 4096 : 0), ReadWord(result, MatrixBase + 32));
        Assert.Equal((ushort)0xFEDC, ReadWord(result, MatrixBase + 32 + 18));
        Assert.Equal(unchecked((uint)-123), WaterSchedulerFixture.ReadCoreDword(result, MatrixBase + 32 + 20));
        Assert.Equal(456u, WaterSchedulerFixture.ReadCoreDword(result, MatrixBase + 32 + 24));
        Assert.Equal(unchecked((uint)-789), WaterSchedulerFixture.ReadCoreDword(result, MatrixBase + 32 + 28));
        Assert.Equal(Pc + 4, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
    }

    [Theory]
    [InlineData(0x0116)]
    [InlineData(0xF116)]
    public void Matrix116_transforms_full_translation_but_publishes_plain_Euler_without_reading_mode_seed_or_operand(ushort instruction)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(instruction);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x12, 7);
        frame = WaterSchedulerFixture.CoreWord(frame, 0x2797206, 7);
        NativeBattleActionMatrix view = new(new short[] { 0, -4096, 0, 4096, 0, 0, 0, 0, 4096 }.ToImmutableArray(), 50000, -50000, 70000, 4096);
        frame = WriteMatrix(frame, MatrixBase, view);
        frame = Vector(frame, 0, 0x94, new(30000, 10000, -10000));
        NativeBattleVector angles = new(-12, 256, -513);
        frame = Vector(frame, 0, 0x8C, angles);
        frame = frame with { Core = frame.Core.MarkUnknown(0x2797968, 24).MarkUnknown(MatrixBase + 18, 2), Arena = frame.Arena.MarkUnknown(0x1002, 4) };
        NativeBattleWaterExecutionFrame result = Run(frame, 2);
        uint destination = MatrixBase + 32;
        Assert.Equal(40000u, WaterSchedulerFixture.ReadCoreDword(result, destination + 20));
        Assert.Equal(unchecked((uint)-20000), WaterSchedulerFixture.ReadCoreDword(result, destination + 24));
        Assert.Equal(60000u, WaterSchedulerFixture.ReadCoreDword(result, destination + 28));
        NativeBattleActionMatrix euler = NativeBattleGfMatrices.Euler56CD50(angles);
        for (int lane = 0; lane < 9; lane++)
        {
            Assert.Equal(unchecked((ushort)euler.Rotation[lane]), ReadWord(result, destination + (uint)(lane * 2)));
        }
        Assert.Equal((ushort)0, ReadWord(result, destination + 18));
        Assert.Equal(NativeBattleGfMatrices.TransformMac460840(view, new(30000, 10000, -10000)).Flags, result.ReadTemporaryDword(0xF0));
        Assert.Equal(frame.ReadTemporaryDword(0xF4), result.ReadTemporaryDword(0xF4));
        Assert.Equal(frame.ReadTemporaryDword(0xF8), result.ReadTemporaryDword(0xF8));
        Assert.Equal(frame.ReadTemporaryDword(0xFC), result.ReadTemporaryDword(0xFC));
        Assert.Equal(Pc + 4, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
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
