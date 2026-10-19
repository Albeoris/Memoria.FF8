using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicPolar7CTests
{
    [Theory]
    [InlineData(0x007C)]
    [InlineData(0xF07C)]
    public void LiteralCrtAngleOffsetsWrapDwordsAndLeaveTheThirdChannelUnconsumed(ushort instruction)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(2, 0, 3, 0).WriteTemporaryWord(0x14A, instruction);
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0x64, 0xFFFFFFF0);
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0x5C, 0x80000000);
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x2060, 4).MarkUnknown(0x1000, 2).MarkUnknown(0x100A, 2) };
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicPolar7C.Execute(frame, Program());
        Assert.Equal(0x0001FFF0u, WaterSchedulerFixture.ReadSlotDword(result, 0, 0x64));
        Assert.Equal(0x7FFFFA30u, WaterSchedulerFixture.ReadSlotDword(result, 0, 0x5C));
        Assert.Equal(0x18BE873Au, result.Random.Seed);
        Assert.Equal(0x8000100Au, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
        Assert.Throws<NotSupportedException>(() => result.Arena.Read(0x2060, 1));
        Assert.Throws<NotSupportedException>(() => result.Arena.Read(0x1000, 1));
        Assert.Throws<NotSupportedException>(() => result.Arena.Read(0x100A, 1));
        Assert.Equal(frame.TemporaryFrame, result.TemporaryFrame);
    }

    [Fact]
    public void SignedRadiusOperandsAndSignedSamplesUseOneRetainedAngle()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(100, 100, -20, 100);
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicPolar7C.Execute(frame, Program());
        Assert.Equal(0x002C0000u, WaterSchedulerFixture.ReadSlotDword(result, 0, 0x64));
        Assert.Equal(0x000001F0u, WaterSchedulerFixture.ReadSlotDword(result, 0, 0x5C));
        Assert.Equal(0x18BE873Au, result.Random.Seed);
    }

    [Fact]
    public void MissingReachedSecondRandomWidthIsNotSubstituted()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(2, 0, 3, 0);
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x1008, 2) };
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicPolar7C.Execute(frame, Program()));
    }

    [Fact]
    public void SnapshotContinuesWithIdenticalRandomMotionAndUnknownPadding()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(short.MinValue, short.MaxValue, short.MaxValue, short.MinValue);
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x2060, 4), TemporaryFrame = frame.TemporaryFrame with { Known = frame.TemporaryFrame.Known.SetItem(0xEE, false) } };
        NativeBattleWaterExecutionFrame restored = JsonSerializer.Deserialize<NativeBattleWaterExecutionFrame>(JsonSerializer.Serialize(frame))!;
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicPolar7C.Execute(restored, Program());
        Assert.False(result.TemporaryFrame.Known[0xEE]);
        Assert.Throws<NotSupportedException>(() => result.Arena.Read(0x2060, 1));
        Assert.Equal(JsonSerializer.Serialize(NativeBattleCinematicPolar7C.Execute(frame, Program())), JsonSerializer.Serialize(result));
    }

    private static NativeBattleWaterScriptProgram Program() => new(0x1000, 12, 0x80001000);

    private static NativeBattleWaterExecutionFrame Fixture(short cosineBase, short cosineWidth, short sineBase, short sineWidth)
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create().WriteTemporaryWord(0x14A, 0x7C);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797450, 0x80001000);
        byte[] bytes = new byte[12];
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(2), cosineBase);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(4), cosineWidth);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(6), sineBase);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8), sineWidth);
        return frame with { Arena = frame.Arena.Write(0x1000, bytes) };
    }
}
