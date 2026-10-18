using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleWaterSavedMatrixCommandsTests
{
    private const uint Saved = 0x2798B68;
    private const uint FirstRow = 0x2797A08;
    private static readonly NativeBattleWaterSlotBank Bank = new(0x2000, 0x400, 4, 0x2000);

    [Fact]
    public void SaveUsesSeparateModuleBackupAndPublishesOneAliasedCoreMatrix()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x104, 0);
        NativeBattleActionMatrix module = NativeBattleGfMatrices.Euler56CD50(new(512, -256, 768)) with { X = int.MaxValue - 10, Y = -40000, Z = 50000 };
        frame = frame with { ModuleViewBackup = Bytes(module).ToImmutableArray(), Core = frame.Core.MarkUnknown(0x27971E4, 32).MarkUnknown(0x27979E8, 32) };
        frame = Position(frame, new(100, -200, 300));
        frame = Angles(frame, new(300, 500, -700));
        frame = frame.WriteTemporaryDword(0x34, 0x12345678).WriteTemporaryDword(0x38, 0x23456789).WriteTemporaryDword(0x3C, 0x3456789A).WriteTemporaryDword(0xF4, 0x456789AB);
        NativeBattleActionMatrix euler = NativeBattleGfMatrices.Euler56CD50(new(300, 500, -700));
        NativeBattleActionMatrix rotation = NativeBattleGfMatrices.Multiply56C090(module, euler);
        NativeBattleFullMacVertex translated = NativeBattleGfMatrices.TransformMac460840(module, new(100, -200, 300));
        NativeBattleWaterExecutionFrame result = Execute(frame);
        Assert.Equal(Bytes(rotation with { X = translated.X, Y = translated.Y, Z = translated.Z }), result.Core.Read(Saved, 32));
        for (int i = 0; i < 8; i++)
        {
            Assert.Equal(BinaryPrimitives.ReadUInt32LittleEndian(result.Core.Read(Saved + (uint)(4 * i), 4)), WaterSchedulerFixture.ReadCoreDword(result, 0x2798A68 + (uint)(4 * (64 + i))));
        }
        Assert.Equal(translated.Flags, result.ReadTemporaryDword(0xF0));
        Assert.Equal(0x456789ABu, result.ReadTemporaryDword(0xF4));
        Assert.Throws<NotSupportedException>(() => result.ReadTemporaryWord(0x32));
        Assert.Equal(0x12345678u, result.ReadTemporaryDword(0x34));
        Assert.Equal(0x23456789u, result.ReadTemporaryDword(0x38));
        Assert.Equal(0x3456789Au, result.ReadTemporaryDword(0x3C));
        Assert.Equal(0x1004u, Pc(result));
        Assert.Equal(frame.Core.Read(0x2797204, 128), result.Core.Read(0x2797204, 128));
    }

    [Fact]
    public void SaveRequiresExplicitBackupAndIgnoresItsUnconsumedPaddingBytes()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x104, 0);
        Assert.Throws<NotSupportedException>(() => Execute(frame));
        frame = frame with { ModuleViewBackup = new byte[31].ToImmutableArray() };
        Assert.Throws<NotSupportedException>(() => Execute(frame));
        byte[] first = Bytes(NativeBattleGfMatrices.Euler56CD50(new(0, 0, 0)));
        byte[] second = (byte[])first.Clone();
        first[18] = 0xA5;
        first[19] = 0x5A;
        second[18] = 0xFF;
        second[19] = 0xEE;
        Assert.Equal(Execute(frame with { ModuleViewBackup = first.ToImmutableArray() }).Core.Read(Saved, 32), Execute(frame with { ModuleViewBackup = second.ToImmutableArray() }).Core.Read(Saved, 32));
    }

    [Fact]
    public void LoadAllocatesCurrentIdAndUsesSavedMatrixWithoutReadingUnusedOperandOrPublishingFocal()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x105, 0, 0xFFFF);
        NativeBattleActionMatrix saved = NativeBattleGfMatrices.Euler56CD50(new(0, 1024, 0)) with { X = 40000, Y = -50000, Z = int.MaxValue };
        frame = frame with { Core = frame.Core.Write(Saved, Bytes(saved)).MarkUnknown(Saved + 18, 2).MarkUnknown(0x27979E8, 32), Arena = frame.Arena.MarkUnknown(0x1004, 2) };
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x12, 0xBEEF);
        frame = Position(frame, new(50, -60, 70));
        frame = Angles(frame, new(-300, 700, 900));
        NativeBattleFullMacVertex translated = NativeBattleGfMatrices.TransformMac460840(saved, new(50, -60, 70));
        NativeBattleActionMatrix rotation = NativeBattleGfMatrices.Multiply56C090(saved, NativeBattleGfMatrices.Euler56CD50(new(-300, 700, 900)));
        NativeBattleWaterExecutionFrame result = Execute(frame);
        Assert.Equal((ushort)0xBEEF, BinaryPrimitives.ReadUInt16LittleEndian(result.Core.Read(0x2797206, 2)));
        Assert.Equal(Bytes(rotation with { X = translated.X, Y = translated.Y, Z = translated.Z }), result.Core.Read(FirstRow, 32));
        Assert.Throws<NotSupportedException>(() => result.ReadTemporaryWord(0xF2));
        Assert.Equal((ushort)rotation.Rotation[0], BinaryPrimitives.ReadUInt16LittleEndian(result.Core.Read(FirstRow, 2)));
        Assert.Equal(0x1006u, Pc(result));
        Assert.Throws<NotSupportedException>(() => result.Arena.Read(0x1004, 2));
        Assert.Equal(frame.Core.Read(0x27972C4, 16), result.Core.Read(0x27972C4, 16));
        Assert.Equal(frame.ModuleViewBackup, result.ModuleViewBackup);
    }

    [Fact]
    public void SignedSavedIndexCanAliasAllocatedDestinationAndJsonNextUsesTheCurrentCoreBytes()
    {
        short index = -139;
        Assert.Equal(FirstRow, unchecked(Saved + (uint)(32 * index)));
        NativeBattleWaterExecutionFrame frame = Fixture(0x105, unchecked((ushort)index), 0xFFFF);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x12, 99);
        frame = WaterSchedulerFixture.CoreWord(frame, 0x2797206, 99);
        NativeBattleActionMatrix saved = NativeBattleGfMatrices.Euler56CD50(new(256, 512, 768)) with { X = 100, Y = 200, Z = 300 };
        frame = frame with { Core = frame.Core.Write(FirstRow, Bytes(saved)) };
        frame = Position(frame, new(10, 20, 30));
        frame = Angles(frame, new(128, 256, 384));
        NativeBattleFullMacVertex translated = NativeBattleGfMatrices.TransformMac460840(saved, new(10, 20, 30));
        NativeBattleActionMatrix rotation = NativeBattleGfMatrices.Multiply56C090(saved, NativeBattleGfMatrices.Euler56CD50(new(128, 256, 384)));
        NativeBattleWaterExecutionFrame result = Execute(frame);
        Assert.Equal(Bytes(rotation with { X = translated.X, Y = translated.Y, Z = translated.Z }), result.Core.Read(FirstRow, 32));
        NativeBattleWaterExecutionFrame restored = JsonSerializer.Deserialize<NativeBattleWaterExecutionFrame>(JsonSerializer.Serialize(frame))!;
        Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(Execute(restored)));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-32768)]
    [InlineData(0)]
    [InlineData(256)]
    [InlineData(32767)]
    public void UniformColorUsesSignedOrReplicationWithoutClampingOrMaskingCommand(int channel)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x80D4, unchecked((ushort)channel));
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0xCC, 0x1200AA55);
        uint replication = unchecked((uint)(channel | (channel << 8) | (channel << 16)));
        NativeBattleWaterExecutionFrame result = Execute(frame);
        Assert.Equal(0x12000000u | replication, WaterSchedulerFixture.ReadSlotDword(result, 0, 0xCC));
        Assert.Equal(0x1004u, Pc(result));
        Assert.Equal(frame.Random, result.Random);
        Assert.Equal(frame.Core.Read(0x27979E8, 32), result.Core.Read(0x27979E8, 32));
        Assert.Equal(frame.ReadTemporary(0xE0, 32), result.ReadTemporary(0xE0, 32));
    }

    [Fact]
    public void UnsupportedRotationAndColorVariantsFailWithoutReadingUnselectedOperands()
    {
        NativeBattleWaterExecutionFrame load = Fixture(0x1105);
        load = load with { Arena = load.Arena.MarkUnknown(0x1002, 4) };
        Assert.Throws<NotSupportedException>(() => Execute(load));
        NativeBattleWaterExecutionFrame color = Fixture(0xD4);
        color = color with { Arena = color.Arena.MarkUnknown(0x1002, 6) };
        Assert.Throws<NotSupportedException>(() => Execute(color));
        Assert.Throws<InvalidDataException>(() => Execute(Fixture(0x104, 0x7FFF) with { ModuleViewBackup = Bytes(NativeBattleGfMatrices.Euler56CD50(new(0, 0, 0))).ToImmutableArray() }));
    }

    private static NativeBattleWaterExecutionFrame Fixture(params ushort[] words)
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create().WriteTemporaryWord(0x14A, words[0]);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797450, 0x1000);
        for (int i = 0; i < words.Length; i++)
        {
            byte[] bytes = new byte[2];
            BinaryPrimitives.WriteUInt16LittleEndian(bytes, words[i]);
            frame = frame with { Arena = frame.Arena.Write(0x1000 + 2 * i, bytes) };
        }
        return frame;
    }
    private static NativeBattleWaterExecutionFrame Execute(NativeBattleWaterExecutionFrame frame) => NativeBattleWaterMatrixCommands.Execute(frame, new(0x1000, 0x100, 0x1000), Bank);
    private static uint Pc(NativeBattleWaterExecutionFrame frame) => WaterSchedulerFixture.ReadCoreDword(frame, 0x2797450);
    private static NativeBattleWaterExecutionFrame Position(NativeBattleWaterExecutionFrame frame, NativeBattleVector position)
    {
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x94, unchecked((ushort)position.X));
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x96, unchecked((ushort)position.Y));
        return WaterSchedulerFixture.SlotWord(frame, 0, 0x98, unchecked((ushort)position.Z));
    }
    private static NativeBattleWaterExecutionFrame Angles(NativeBattleWaterExecutionFrame frame, NativeBattleVector angles)
    {
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x8C, unchecked((ushort)angles.X));
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x8E, unchecked((ushort)angles.Y));
        return WaterSchedulerFixture.SlotWord(frame, 0, 0x90, unchecked((ushort)angles.Z));
    }
    private static byte[] Bytes(NativeBattleActionMatrix matrix)
    {
        byte[] bytes = new byte[32];
        for (int i = 0; i < 9; i++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(2 * i), matrix.Rotation[i]);
        }
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(20), matrix.X);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(24), matrix.Y);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(28), matrix.Z);
        return bytes;
    }
}
