using System.Buffers.Binary;
using Fields = Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleWaterScalarMemoryTests
{
    private const uint DwordAddress = NativeBattleScriptedEffectMemory.Address + 0x100;
    private const uint WordAddress = DwordAddress + 4;

    [Fact]
    public void Scalar_stores_retain_existing_frame_owners_and_match_previous_bytes_knownness_and_versions()
    {
        NativeBattleWaterExecutionFrame previous = WaterSchedulerFixture.Create();
        NativeBattleWaterExecutionFrame current = WaterSchedulerFixture.Create();
        NativeBattleWaterExecutionFrame original = current;
        previous = PreviousBatch(previous, 0xF1234567, out uint expected);
        current = CurrentBatch(current, 0xF1234567, out uint actual);
        Assert.Same(original, current);
        Assert.Same(original.Core, current.Core);
        Assert.Same(original.Arena, current.Arena);
        Assert.Same(original.TemporaryFrame, current.TemporaryFrame);
        Assert.Same(original.Random, current.Random);
        Assert.Equal(expected, actual);
        AssertOwners(previous, current);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Scalar_stores_preserve_private_segment_rollback_and_explicit_publication(bool publish)
    {
        NativeBattleWaterExecutionFrame previous = WaterSchedulerFixture.Create();
        NativeBattleWaterExecutionFrame current = WaterSchedulerFixture.Create();
        uint expected;
        using (NativeBattleWaterFrameBatch oldBatch = new())
        {
            NativeBattleWaterExecutionFrame oldSegment = PreviousBatch(oldBatch.Begin(previous), 123, out expected);
            if (publish)
            {
                previous = oldBatch.Publish(oldSegment);
            }
        }
        using (NativeBattleWaterFrameBatch newBatch = new())
        {
            NativeBattleWaterExecutionFrame newSegment = newBatch.Begin(current);
            Assert.Same(newSegment, CurrentBatch(newSegment, 123, out uint actual));
            Assert.Equal(expected, actual);
            if (publish)
            {
                current = newBatch.Publish(newSegment);
            }
        }
        AssertOwners(previous, current);
        Assert.Equal(publish ? 123u : 0u, Fields.CoreDword(current, DwordAddress));
        Assert.Equal(publish ? (ushort)123 : (ushort)0, Fields.SlotWord(current, 0x40));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void Unknown_invalid_range_and_frozen_store_errors_keep_the_previous_order_and_message(int kind)
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create();
        if (kind == 0)
        {
            frame.Core.MarkUnknown(DwordAddress, 1);
        }
        else if (kind == 1)
        {
            frame.Arena.MarkUnknown(0x2040, 1);
        }
        else if (kind == 4)
        {
            frame = frame with { Core = new(frame.Core.Core.Capture()) };
        }
        else if (kind == 5)
        {
            frame = frame with { Arena = frame.Arena.Capture() };
        }
        Exception previous = Assert.IsAssignableFrom<Exception>(Record.Exception(() => Access(frame, kind, false)));
        Exception current = Assert.IsAssignableFrom<Exception>(Record.Exception(() => Access(frame, kind, true)));
        Assert.Equal(previous.GetType(), current.GetType());
        Assert.Equal(previous.Message, current.Message);
    }

    [Fact]
    public void Warm_scalar_microbatch_removes_small_read_arrays_and_redundant_frame_clones()
    {
        NativeBattleWaterExecutionFrame previous = WaterSchedulerFixture.Create();
        NativeBattleWaterExecutionFrame current = WaterSchedulerFixture.Create();
        for (uint index = 0; index < 32; index++)
        {
            previous = PreviousBatch(previous, index, out _);
            current = CurrentBatch(current, index, out _);
        }
        const uint iterations = 256;
        long start = GC.GetAllocatedBytesForCurrentThread();
        for (uint index = 0; index < iterations; index++)
        {
            previous = PreviousBatch(previous, index, out _);
        }
        long previousBytes = GC.GetAllocatedBytesForCurrentThread() - start;
        start = GC.GetAllocatedBytesForCurrentThread();
        for (uint index = 0; index < iterations; index++)
        {
            current = CurrentBatch(current, index, out _);
        }
        long currentBytes = GC.GetAllocatedBytesForCurrentThread() - start;
        AssertOwners(previous, current);
        Assert.True(previousBytes - currentBytes >= iterations * 64, $"Previous scalar batches allocated {previousBytes} bytes; current batches allocated {currentBytes} bytes.");
    }

    private static void AssertOwners(NativeBattleWaterExecutionFrame expected, NativeBattleWaterExecutionFrame actual)
    {
        Assert.Equal(NativeBattleTestSnapshots.Json(expected), NativeBattleTestSnapshots.Json(actual));
        Assert.Equal(expected.Core.Core.Version, actual.Core.Core.Version);
        Assert.Equal(expected.Arena.Version, actual.Arena.Version);
    }

    private static NativeBattleWaterExecutionFrame CurrentBatch(NativeBattleWaterExecutionFrame frame, uint value, out uint sum)
    {
        frame = Fields.WriteCoreDword(frame, DwordAddress, value);
        frame = Fields.WriteCoreWord(frame, WordAddress, unchecked((ushort)value));
        frame = Fields.WriteSlotWord(frame, 0x40, unchecked((ushort)value));
        frame = Fields.WriteSlotDword(frame, 0x44, value);
        sum = unchecked(Fields.CoreDword(frame, DwordAddress) + Fields.CoreWord(frame, WordAddress) + Fields.SlotWord(frame, 0x40));
        return frame;
    }

    private static NativeBattleWaterExecutionFrame PreviousBatch(NativeBattleWaterExecutionFrame frame, uint value, out uint sum)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        frame = frame with { Core = frame.Core.Write(DwordAddress, bytes) };
        frame = frame with { Core = frame.Core.Write(WordAddress, bytes[..2]) };
        frame = frame with { Arena = frame.Arena.Write(PreviousSlotOffset(frame, 0x40, 2), bytes[..2]) };
        frame = frame with { Arena = frame.Arena.Write(PreviousSlotOffset(frame, 0x44, 4), bytes) };
        sum = unchecked(BinaryPrimitives.ReadUInt32LittleEndian(frame.Core.Read(DwordAddress, 4)) + BinaryPrimitives.ReadUInt16LittleEndian(frame.Core.Read(WordAddress, 2)) + BinaryPrimitives.ReadUInt16LittleEndian(frame.Arena.Read(PreviousSlotOffset(frame, 0x40, 2), 2)));
        return frame;
    }

    private static int PreviousSlotOffset(NativeBattleWaterExecutionFrame frame, int offset, int length)
    {
        if (offset < 0 || length < 0 || offset > 0x100 - length)
        {
            throw new NotSupportedException("Native current-slot access leaves its physical 256-byte owner.");
        }
        return checked(Fields.ArenaOffset(BinaryPrimitives.ReadUInt32LittleEndian(frame.Core.Read(0x27973E8, 4)), 0x100) + offset);
    }

    private static void Access(NativeBattleWaterExecutionFrame frame, int kind, bool current)
    {
        if (kind is 0 or 2)
        {
            uint address = kind == 2 ? NativeBattleScriptedEffectMemory.Address - 1 : DwordAddress;
            _ = current ? Fields.CoreDword(frame, address) : BinaryPrimitives.ReadUInt32LittleEndian(frame.Core.Read(address, 4));
        }
        else if (kind is 1 or 3)
        {
            int offset = kind == 3 ? 255 : 0x40;
            _ = current ? Fields.SlotWord(frame, offset) : BinaryPrimitives.ReadUInt16LittleEndian(frame.Arena.Read(PreviousSlotOffset(frame, offset, 2), 2));
        }
        else if (kind == 4)
        {
            if (current)
            {
                Fields.WriteCoreWord(frame, WordAddress, 1);
            }
            else
            {
                frame.Core.Write(WordAddress, [1, 0]);
            }
        }
        else if (current)
        {
            Fields.WriteSlotDword(frame, 0x44, 1);
        }
        else
        {
            frame.Arena.Write(PreviousSlotOffset(frame, 0x44, 4), [1, 0, 0, 0]);
        }
    }
}
