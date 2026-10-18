using System.Buffers.Binary;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleWaterStopCommandTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void SecondaryStreamStopsOnlyPcAndYieldWithoutReadingSlotOrLists(byte stream)
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create().WriteTemporaryByte(0x143, stream);
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x2000, 0x100), Core = frame.Core.MarkUnknown(0x2797454, 128).MarkUnknown(0x2797554, 128) };
        NativeBattleWaterExecutionFrame result = NativeBattleWaterStopCommand.StopRemoveAA5120(frame);
        Assert.Equal((ushort)0x8000, result.ReadTemporaryWord(0x13E));
        Assert.Equal(0u, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
        Assert.Same(frame.Arena, result.Arena);
        Assert.Throws<NotSupportedException>(() => result.Core.Read(0x2797454, 1));
    }

    [Theory]
    [InlineData(123)]
    [InlineData(0x8001)]
    public void StreamZeroRemovesFirstEntriesClearsCorrectIdAndPreservesOtherPayload(ushort id)
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create();
        frame = frame with { Core = frame.Core.Write(0x2797454, new byte[] { 0x80, 1, 0xFF, 0xBB }).Write(0x2797554, new byte[] { 0x80, 1, 0xFF, 0xCC }) };
        frame = frame with { Arena = frame.Arena.Write(0x2000, Enumerable.Repeat((byte)0xA5, 0x100).ToArray()) };
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x12, id);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0xC8, 7);
        frame = WaterSchedulerFixture.CoreWord(frame, 0x2797204, id);
        frame = WaterSchedulerFixture.CoreWord(frame, 0x2797206, id);
        NativeBattleWaterExecutionFrame result = NativeBattleWaterStopCommand.StopRemoveAA5120(frame);
        Assert.Equal(new byte[16], result.Arena.Read(0x2000, 16));
        Assert.Equal(new byte[] { 0xA5, 0xA5 }, result.Arena.Read(0x2010, 2));
        Assert.Equal(id, WaterSchedulerFixture.ReadSlotWord(result, 0, 0x12));
        Assert.Equal(new byte[] { 1, 0xFF, 0xFF, 0xBB }, result.Core.Read(0x2797454, 4));
        Assert.Equal(new byte[] { 1, 0xFF, 0xFF, 0xCC }, result.Core.Read(0x2797554, 4));
        Assert.Equal((ushort)0, BinaryPrimitives.ReadUInt16LittleEndian(result.Core.Read(0x2797204, 2)));
        Assert.Equal(id >= 0x8000 ? id : (ushort)0, BinaryPrimitives.ReadUInt16LittleEndian(result.Core.Read(0x2797206, 2)));
        Assert.Equal((ushort)7, result.ReadTemporaryWord(0x13E));
        Assert.Equal((byte)0xFF, result.ReadTemporaryByte(0x144));
        Assert.Equal(0u, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
        Assert.Equal((ushort)0, BinaryPrimitives.ReadUInt16LittleEndian(result.Core.Read(0x2797080, 2)));
    }

    [Fact]
    public void ActiveSearchDoesNotTreatFirstTerminatorAsRequestedSlotAndMissingOwnerFails()
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create().WriteTemporaryByte(0x142, 1);
        frame = frame with { Core = frame.Core.Write(0x2797454, new byte[] { 0xFF, 1, 2, 0xFF, 0xA5 }) };
        frame = WaterSchedulerFixture.CoreDword(frame, 0x27973E8, NativeBattleEffectNodeMemory.ArenaAddress + 0x2100);
        NativeBattleWaterExecutionFrame result = NativeBattleWaterStopCommand.StopRemoveAA5120(frame);
        Assert.Equal(new byte[] { 0xFF, 2, 0xFF, 0xFF, 0xA5 }, result.Core.Read(0x2797454, 5));
        NativeBattleWaterExecutionFrame missing = WaterSchedulerFixture.Create().WriteTemporaryByte(0x142, 2);
        Assert.Throws<NotSupportedException>(() => NativeBattleWaterStopCommand.StopRemoveAA5120(missing));
    }

    [Fact]
    public void ActiveMaskDoesNotMaskDrawRequestedIndexAndCountWraps()
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create().WriteTemporaryByte(0x142, 0x81);
        frame = frame with { Core = frame.Core.Write(0x2797454, new byte[] { 1, 0xFF }).Write(0x2797554, new byte[] { 1, 0xFF }) };
        frame = WaterSchedulerFixture.CoreDword(frame, 0x27973E8, NativeBattleEffectNodeMemory.ArenaAddress + 0x2100);
        frame = WaterSchedulerFixture.CoreWord(frame, 0x2797080, 0);
        NativeBattleWaterExecutionFrame result = NativeBattleWaterStopCommand.StopRemoveAA5120(frame);
        Assert.Equal((byte)0xFF, result.Core.Read(0x2797454, 1)[0]);
        Assert.Equal(new byte[] { 1, 0xFF }, result.Core.Read(0x2797554, 2));
        Assert.Equal((ushort)0xFFFF, BinaryPrimitives.ReadUInt16LittleEndian(result.Core.Read(0x2797080, 2)));
    }

    [Fact]
    public void ZeroTickDoesNotInventYieldAndSchedulerAttemptsPcZeroFetch()
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.SlotDword(WaterSchedulerFixture.Create(), 0, 0, 0x1000);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0xC8, 0);
        List<uint> reads = [];
        Assert.Throws<InvalidDataException>(() => NativeBattleWaterScriptScheduler.RunScriptPass(false, frame, (_, pc) =>
        {
            reads.Add(pc);
            if (pc == 0)
            {
                throw new InvalidDataException("No program owner for zero pointer.");
            }
            return 0;
        }, (current, _) => NativeBattleWaterStopCommand.StopRemoveAA5120(current)));
        Assert.Equal(new uint[] { 0x1000, 0 }, reads);
    }
}
