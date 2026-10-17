using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeEventMemoryTests
{
    [Fact]
    public void MixedCellReadsMatchAnIndependentByteImageAcrossGapsAndPhysicalBoundaries()
    {
        Random random = new(19);
        byte[] bytes = new byte[ushort.MaxValue + 1];
        bool[] represented = new bool[bytes.Length];
        Dictionary<int, NativeMemoryValue> cells = [];
        for (int address = 0; address < 1024;)
        {
            int width = 1 << random.Next(3);
            uint value = unchecked((uint)random.NextInt64(1L << 32));
            int projected = width == 1 ? (byte)value : width == 2 ? (ushort)value : unchecked((int)value);
            cells.Add(address, new(width, NativeScalarValue.Exact(projected)));
            for (int offset = 0; offset < width; offset++)
            {
                bytes[address + offset] = (byte)(value >> (offset * 8));
                represented[address + offset] = true;
            }
            address += width + random.Next(3);
        }
        cells.Add(NativeEventMemory.MaximumAddress, new(4, NativeScalarValue.Exact(unchecked((int)0x89ABCDEF))));
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(NativeEventMemory.MaximumAddress), 0x89ABCDEF);
        Array.Fill(represented, true, NativeEventMemory.MaximumAddress, 4);
        NativeEventMemory memory = new(1, cells);

        foreach (int address in Enumerable.Range(0, 1030).Concat(Enumerable.Range(NativeEventMemory.MaximumAddress - 7, 8)))
        {
            Assert.Equal(bytes[address], memory.Read(address, 1).RequireExact());
            Assert.Equal(unchecked((sbyte)bytes[address]), memory.Read(address, 1, signed: true).RequireExact());
            Assert.Equal(System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(address)), memory.Read(address, 2).RequireExact());
            Assert.Equal(System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(address)), memory.Read(address, 2, signed: true).RequireExact());
            Assert.Equal(System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(address)), memory.Read(address, 4).RequireExact());
            foreach (int width in new[] { 1, 2, 4 })
            {
                Assert.Equal(Enumerable.Range(address, width).All(index => represented[index]), memory.HasRepresentedExactBytes(address, width));
            }
        }
    }

    [Fact]
    public void Byte201AndByte204AreIndependentCellsOverAZeroBackground()
    {
        NativeEventMemory memory = NativeEventMemory.Empty.Write(201, 1, 0xAB).Write(204, 1, 0xCD);

        Assert.Equal(0, NativeEventMemory.Empty.Read(201, 4).RequireExact());
        Assert.Equal(0xAB, memory.Read(201, 1).RequireExact());
        Assert.Equal(0xCD, memory.Read(204, 1).RequireExact());
        Assert.Equal(unchecked((int)0xCD0000AB), memory.Read(201, 4).RequireExact());
        Assert.Equal(new[] { 201, 204 }, memory.KnownValues.Keys.Order());
        Assert.All(memory.Cells.Values, cell => Assert.Equal(1, cell.Width));
    }

    [Fact]
    public void ReadsCombineLittleEndianCellsAndApplySignednessAtTheRequestedWidth()
    {
        NativeEventMemory memory = NativeEventMemory.Empty.Write(100, 1, 0x80).Write(101, 1, 0xFF).Write(102, 2, 0xABCD);

        Assert.Equal(-128, memory.Read(100, 1, true).RequireExact());
        Assert.Equal(128, memory.Read(100, 1).RequireExact());
        Assert.Equal(-128, memory.Read(100, 2, true).RequireExact());
        Assert.Equal(65408, memory.Read(100, 2).RequireExact());
        Assert.Equal(unchecked((int)0xABCDFF80), memory.Read(100, 4).RequireExact());
        Assert.Equal(0xCDFF, memory.Read(101, 2).RequireExact());
    }

    [Fact]
    public void ByteAndWordWritesRetainUntouchedDwordBytesAndEarlierInstances()
    {
        NativeEventMemory original = NativeEventMemory.FromDWords(new Dictionary<int, int> { [100] = 0x12345678 });
        NativeEventMemory changed = original.Write(101, 1, 0xAB).Write(102, 2, 0xCDEF);

        Assert.Equal(0x12345678, original.Read(100, 4).RequireExact());
        Assert.Equal(unchecked((int)0xCDEFAB78), changed.Read(100, 4).RequireExact());
        Assert.Equal(0x78, changed.Read(100, 1).RequireExact());
        Assert.Equal(0xAB, changed.Read(101, 1).RequireExact());
        Assert.Throws<InvalidDataException>(() => NativeEventMemory.FromDWords(new Dictionary<int, int> { [201] = 1, [204] = 2 }));
    }

    [Fact]
    public void AWriteCanSpanMultipleExactCellsAndMissingBytes()
    {
        NativeEventMemory memory = NativeEventMemory.Empty.Write(100, 2, 0x1234).Write(104, 2, 0x5678).Write(101, 4, unchecked((int)0xAABBCCDD));

        Assert.Equal(0xDD34, memory.Read(100, 2).RequireExact());
        Assert.Equal(unchecked((int)0xAABBCCDD), memory.Read(101, 4).RequireExact());
        Assert.Equal(0x56AA, memory.Read(104, 2).RequireExact());
        memory.Validate();
    }

    [Fact]
    public void UncertainCellsKeepExecutedProvenanceAndRefusePartialOrMixedAccessAtomically()
    {
        NativeScalarValue scalar = TimerValue(32767, 32768);
        NativeEventMemory memory = NativeEventMemory.Empty.Write(100, 2, scalar).Write(104, 1, 7);
        string before = JsonSerializer.Serialize(memory);
        NativeScalarValue read = memory.Read(100, 2, true);

        Assert.Equal(new[] { -32768, 32767 }, read.Values);
        Assert.Equal(scalar.Origin!.ProgramHash, read.Origin!.ProgramHash);
        Assert.Equal(scalar.Origin.Timer.Alternatives, read.Origin.Timer.Alternatives);
        Assert.Throws<NotSupportedException>(() => memory.Read(100, 1));
        Assert.Throws<NotSupportedException>(() => memory.Read(99, 4));
        Assert.False(memory.HasRepresentedExactBytes(100, 2));
        Assert.True(memory.HasRepresentedExactBytes(104, 1));
        Assert.False(memory.HasRepresentedExactBytes(103, 2));
        Assert.Throws<NotSupportedException>(() => memory.Write(101, 1, 0));
        Assert.Throws<NotSupportedException>(() => memory.Write(99, 4, 0));
        Assert.Throws<NotSupportedException>(() => memory.Clear(101, 102));
        Assert.Equal(before, JsonSerializer.Serialize(memory));
        Assert.Equal(7, memory.KnownValues[104]);
        Assert.Equal(new[] { 100 }, memory.UncertainValues.Keys);
        Assert.Equal(1234, memory.Write(100, 2, 1234).Read(100, 2).RequireExact());
        Assert.Equal(0, memory.Clear(100, 102).Read(100, 2).RequireExact());
    }

    [Fact]
    public void IncomingUncertaintyCanReplaceExactBytesWithoutInventingOtherBytes()
    {
        NativeEventMemory memory = NativeEventMemory.FromDWords(new Dictionary<int, int> { [100] = 0x12345678 }).Write(101, 2, TimerValue(599, 600));

        Assert.Equal(0x78, memory.Read(100, 1).RequireExact());
        Assert.Equal(0x12, memory.Read(103, 1).RequireExact());
        Assert.Equal(new[] { 599, 600 }, memory.Read(101, 2).Values);
        Assert.Throws<NotSupportedException>(() => memory.Read(100, 4));
    }

    [Fact]
    public void ClearZerosPartialExactCellsAndDropsWholeCells()
    {
        NativeEventMemory memory = NativeEventMemory.FromDWords(new Dictionary<int, int> { [100] = 0x12345678, [104] = 99 });
        NativeEventMemory cleared = memory.Clear(101, 108);

        Assert.Equal(0x78, cleared.Read(100, 4).RequireExact());
        Assert.False(cleared.Cells.ContainsKey(104));
        Assert.Equal(99, memory.Read(104, 4).RequireExact());
        Assert.Empty(cleared.Remove(100).Cells);
    }

    [Fact]
    public void InputAndOutputArraysCannotMutateTheMemoryOwner()
    {
        NativeScalarValue source = TimerValue(599, 600);
        Dictionary<int, NativeMemoryValue> input = new() { [100] = new NativeMemoryValue(2, source) };
        NativeEventMemory memory = new(1, input);
        input.Clear();
        source.Values[0] = -1;
        source.Origin!.Timer.Alternatives[0] = new NativeTimerAlternative(0, 0, -1);
        NativeScalarValue cell = memory.Cells[100].Value;
        cell.Values[0] = -2;
        cell.Origin!.Timer.Alternatives[0] = new NativeTimerAlternative(0, 0, -2);
        NativeScalarValue read = memory.Read(100, 2);
        read.Values[0] = -3;
        read.Origin!.Timer.Alternatives[0] = new NativeTimerAlternative(0, 0, -3);

        Assert.Equal(new[] { 599, 600 }, memory.Read(100, 2).Values);
        memory.Validate();
        Assert.Throws<NotSupportedException>(() => ((IDictionary<int, NativeMemoryValue>)memory.Cells).Clear());
    }

    [Fact]
    public void JsonRoundTripRetainsTypedCellsAndProvenance()
    {
        NativeEventMemory memory = NativeEventMemory.Empty.Write(201, 1, 1).Write(204, 2, TimerValue(599, 600));
        string json = JsonSerializer.Serialize(memory);
        NativeEventMemory restored = JsonSerializer.Deserialize<NativeEventMemory>(json)!;

        Assert.Equal(1, restored.Version);
        Assert.Equal(1, restored.Cells[201].Width);
        Assert.Equal(2, restored.Cells[204].Width);
        Assert.Equal(new[] { 599, 600 }, restored.Read(204, 2).Values);
        Assert.Equal(memory.Read(204, 2).Origin!.ProgramHash, restored.Read(204, 2).Origin!.ProgramHash);
        Assert.DoesNotContain("KnownValues", json);
        Assert.DoesNotContain("UncertainValues", json);
    }

    [Theory]
    [InlineData("{\"Version\":2,\"Cells\":{}}")]
    [InlineData("{\"Cells\":{}}")]
    [InlineData("{\"Version\":1}")]
    [InlineData("{\"Version\":1,\"Version\":1,\"Cells\":{}}")]
    [InlineData("{\"Version\":1,\"Cells\":{\"201\":{\"Width\":1,\"Value\":1},\"201\":{\"Width\":1,\"Value\":2}}}")]
    [InlineData("{\"Version\":1,\"Cells\":{\"201\":{\"Width\":4,\"Value\":1},\"204\":{\"Width\":1,\"Value\":2}}}")]
    [InlineData("{\"Version\":1,\"Cells\":{\"201\":{\"Width\":3,\"Value\":1}}}")]
    [InlineData("{\"Version\":1,\"Cells\":{\"201\":{\"Width\":1,\"Value\":256}}}")]
    [InlineData("{\"Version\":1,\"Cells\":{\"65533\":{\"Width\":1,\"Value\":1}}}")]
    public void MalformedSnapshotsFailClosed(string json)
    {
        Assert.ThrowsAny<Exception>(() => JsonSerializer.Deserialize<NativeEventMemory>(json));
    }

    [Fact]
    public void HighestSupportedAddressRetainsItsCompleteDwordWhenUpdated()
    {
        NativeEventMemory memory = NativeEventMemory.Empty.Write(NativeEventMemory.MaximumAddress - 1, 2, 0x1234).Write(NativeEventMemory.MaximumAddress, 4, unchecked((int)0xAABBCCDD));

        Assert.Equal(unchecked((int)0xAABBCCDD), memory.Read(NativeEventMemory.MaximumAddress, 4).RequireExact());
        Assert.Equal(0xDD34, memory.Read(NativeEventMemory.MaximumAddress - 1, 2).RequireExact());
        Assert.Throws<ArgumentOutOfRangeException>(() => memory.Read(NativeEventMemory.MaximumAddress + 1, 1));
    }

    private static NativeScalarValue TimerValue(int first, int second)
    {
        NativeExamTimerState timer = NativeExamTimerState.FromReachedSet(first) with { Alternatives = [new NativeTimerAlternative(0, 0, first), new NativeTimerAlternative(1, 1, second)] };
        return NativeScalarValue.FromTimer(new NativeTimerReadOrigin(1, 999, new string('A', 64), 0, 0, 1, 2, timer));
    }
}
