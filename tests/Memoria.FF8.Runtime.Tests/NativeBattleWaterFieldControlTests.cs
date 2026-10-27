using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleWaterFieldControlTests
{
    private const uint Pc = 0xFFFFFFFE;
    private static readonly NativeBattleWaterSlotBank Bank = new(0x2000, 0x200, 2, 0x2000);

    [Theory]
    [InlineData(0, 0x019F, 0xA7123456u)]
    [InlineData(1, 0x01BF, 0xA7123456u)]
    [InlineData(2, 0x01DF, 0xA7123456u)]
    [InlineData(3, 0x01FF, 0xA7123456u)]
    [InlineData(4, 0xFFBF, 0xA5123456u)]
    [InlineData(127, 0xFFBF, 0xA5123456u)]
    public void Mode23PreservesExactMasksWithoutOperandReads(int kind, ushort expectedFlags, uint expectedColor)
    {
        NativeBattleWaterExecutionFrame frame = Fixture((ushort)((kind << 9) | 0x23));
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0xCC, 0xA7123456);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x92, 0xFFFF);
        NativeBattleWaterExecutionFrame result = Run(frame, 0);
        Assert.Equal(expectedFlags, WaterSchedulerFixture.ReadSlotWord(result, 0, 0x92));
        Assert.Equal(expectedColor, WaterSchedulerFixture.ReadSlotDword(result, 0, 0xCC));
        Assert.Equal(0u, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
    }

    [Theory]
    [InlineData(0, 0x80)]
    [InlineData(1, 1)]
    [InlineData(3, 3)]
    [InlineData(4, 0x84)]
    [InlineData(124, 0xFC)]
    [InlineData(127, 127)]
    public void Byte24ReplacementDefersOldByteReadButOrPreservesIt(int kind, byte expected)
    {
        NativeBattleWaterExecutionFrame frame = Fixture((ushort)((kind << 9) | 0x24));
        frame = frame with { Arena = frame.Arena.Write(0x20DE, new byte[] { 0x80 }) };
        if ((kind & 3) != 0)
        {
            frame = frame with { Arena = frame.Arena.MarkUnknown(0x20DE, 1) };
        }
        Assert.Equal(expected, Run(frame, 0).Arena.Read(0x20DE, 1)[0]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void StreamClear2AChangesOnlyActualDwordsIncludingWaitPairForThree(int stream)
    {
        NativeBattleWaterExecutionFrame frame = Fixture((ushort)((stream << 14) | 0x2A));
        frame = frame with { Arena = frame.Arena.Write(0x2000, Enumerable.Repeat((byte)0xA5, 0x100).ToArray()) };
        byte[] expected = Enumerable.Repeat((byte)0xA5, 0x100).ToArray();
        Array.Clear(expected, stream == 0 ? 4 : 4 * stream, stream == 0 ? 8 : 4);
        Assert.Equal(expected, Run(frame, 0).Arena.Read(0x2000, 0x100));
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x2000, 0x100) };
        NativeBattleWaterExecutionFrame result = Run(frame, 0);
        Assert.Equal(new byte[stream == 0 ? 8 : 4], result.Arena.Read(0x2000 + (stream == 0 ? 4 : 4 * stream), stream == 0 ? 8 : 4));
        Assert.Throws<NotSupportedException>(() => result.Arena.Read(0x2010, 2));
    }

    [Theory]
    [InlineData(0x8001, 0xFFFF, 0xFFFE)]
    [InlineData(0x7FFF, 0x8000, 0xFFFF)]
    [InlineData(0xFFFF, 0xFFFF, 0x8000)]
    public void CommandFlags3BPreserveDifferentActiveWord(ushort operand, ushort previous, ushort expected)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x3B, operand);
        frame = WaterSchedulerFixture.CoreWord(frame, 0x2797630, previous);
        frame = WaterSchedulerFixture.CoreWord(frame, 0x2797632, 0xCDEF);
        NativeBattleWaterExecutionFrame result = Run(frame);
        Assert.Equal(expected, BinaryPrimitives.ReadUInt16LittleEndian(result.Core.Read(0x2797630, 2)));
        Assert.Equal((ushort)0xCDEF, BinaryPrimitives.ReadUInt16LittleEndian(result.Core.Read(0x2797632, 2)));
        Assert.Equal(2u, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
    }

    [Fact]
    public void MorphPointer3EStoresWrappedPointerWithoutReadingInlinePayload()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x3E);
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x20BC, 4).MarkUnknown(0x1002, 6) };
        NativeBattleWaterExecutionFrame result = Run(frame, 0);
        Assert.Equal(0u, WaterSchedulerFixture.ReadSlotDword(result, 0, 0xBC));
        Assert.Equal(6u, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
    }

    [Theory]
    [InlineData(42)]
    [InlineData(0xFFFF)]
    public void ReferencedColor40UsesActualIdLookupClampsSignedCoordinatesAndRetainsCommand(ushort selector)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x40, selector);
        frame = frame with { Core = frame.Core.Write(0x2797454, new byte[] { 0x81, 0xFF }).MarkUnknown(0x2797456, 126), Arena = frame.Arena.MarkUnknown(0x2100, 0x100) };
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x14, 42);
        frame = WaterSchedulerFixture.SlotWord(frame, 1, 0x12, 42);
        frame = WaterSchedulerFixture.SlotWord(frame, 1, 0x94, 0xFFFF);
        frame = WaterSchedulerFixture.SlotWord(frame, 1, 0x96, 300);
        frame = WaterSchedulerFixture.SlotWord(frame, 1, 0x98, 128);
        frame = frame with { Arena = frame.Arena.Write(0x20CF, new byte[] { 0xAB }).MarkUnknown(0x20CC, 3) };
        NativeBattleWaterExecutionFrame result = Run(frame);
        Assert.Equal(0xAB80FF00u, WaterSchedulerFixture.ReadSlotDword(result, 0, 0xCC));
        Assert.Throws<NotSupportedException>(() => result.Core.Read(0x2797456, 1));
        Assert.Throws<InvalidDataException>(() => NativeBattleWaterFieldControl.Execute(frame, new(0x1000, 4, Pc), Bank, 1));
    }

    [Theory]
    [InlineData(0, 0x5A)]
    [InlineData(0x8000, 0xFF)]
    [InlineData(0x4000, 0)]
    [InlineData(0xC000, 0)]
    public void Filter47BothBitsPreferClearAndOnlySelectedOrderEntryIsMarked(int bits, byte expected)
    {
        NativeBattleWaterExecutionFrame frame = Fixture((ushort)(bits | 0x47)).WriteTemporaryByte(0x145, 0x5A).WriteTemporaryByte(0x144, 1);
        frame = frame with { Core = frame.Core.Write(0x2797454, new byte[] { 0x17, 0x23, 0xFF }), Arena = frame.Arena.MarkUnknown(0x2000, 0x100) };
        NativeBattleWaterExecutionFrame result = Run(frame, 0);
        Assert.Equal(expected, result.ReadTemporaryByte(0x145));
        Assert.Equal(new byte[] { 0x17, 0xA3, 0xFF }, result.Core.Read(0x2797454, 3));
        Assert.Throws<NotSupportedException>(() => result.Arena.Read(0x2000, 1));
    }

    [Fact]
    public void Filter47NoFlagsPreservesUnknownFilterAndJsonRetainsIntentionalWrittenValue()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x47);
        bool[] known = NativeBattleStorageSnapshots.Capture(frame.TemporaryFrame).Known.ToArray();
        known[0x145] = false;
        frame = frame with { TemporaryFrame = frame.TemporaryFrame.SetKnownness(0, known) };
        Assert.False(NativeBattleStorageSnapshots.Capture(Run(frame, 0).TemporaryFrame).Known[0x145]);
        frame = Run(Fixture(0x8047), 0);
        NativeBattleWaterExecutionFrame restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleWaterExecutionFrame>(NativeBattleTestSnapshots.Json(frame))!;
        Assert.Equal((byte)0xFF, restored.ReadTemporaryByte(0x145));
        restored = restored.WriteTemporaryWord(0x14A, 0x47);
        frame = frame.WriteTemporaryWord(0x14A, 0x47);
        Assert.Equal(NativeBattleTestSnapshots.Json(Run(frame, 0)), NativeBattleTestSnapshots.Json(Run(restored, 0)));
    }

    [Fact]
    public void RawCopies68AndB1DoNotReadPriorTargetOrBytePadding()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x68, 0xFFFF);
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x209C, 2) };
        Assert.Equal((ushort)0xFFFF, WaterSchedulerFixture.ReadSlotWord(Run(frame), 0, 0x9C));
        frame = Fixture(0xB1, 0xABFE);
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x1003, 1), Core = frame.Core.MarkUnknown(0x279762E, 1) };
        Assert.Equal((byte)0xFE, Run(frame).Core.Read(0x279762E, 1)[0]);
        Assert.Throws<NotSupportedException>(() => Run(Fixture(0x25), 0));
    }

    [Theory]
    [InlineData(0x001B)]
    [InlineData(0xFE1B)]
    [InlineData(0x00A2)]
    [InlineData(0xFEA2)]
    public void Advance4_leaves_unused_operand_unknown_and_changes_only_current_PC(ushort instruction)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(instruction);
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x1000, 4) };
        NativeBattleWaterExecutionFrame result = Run(frame, 0);
        Assert.Same(frame.Arena, result.Arena);
        Assert.Equal(2u, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
    }

    [Theory]
    [InlineData(0x00CD)]
    [InlineData(0xFECD)]
    public void Stored_word_CD_preserves_high_bits_and_does_not_read_previous_field(ushort instruction)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(instruction, 0xFEDC);
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x2048, 2) };
        NativeBattleWaterExecutionFrame result = Run(frame);
        Assert.Equal((ushort)0xFEDC, WaterSchedulerFixture.ReadSlotWord(result, 0, 0x48));
        Assert.Equal(2u, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
    }

    private static NativeBattleWaterExecutionFrame Fixture(ushort instruction, ushort operand = 0)
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create().WriteTemporaryWord(0x14A, instruction);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797450, Pc);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x27973C0, 0x2797628);
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, instruction);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), operand);
        return frame with { Arena = frame.Arena.Write(0x1000, bytes) };
    }

    private static NativeBattleWaterExecutionFrame Run(NativeBattleWaterExecutionFrame frame, int length = 4) => NativeBattleWaterFieldControl.Execute(frame, new(0x1000, length, Pc), Bank, 0);
}
