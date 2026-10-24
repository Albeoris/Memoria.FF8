using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleRawGeometryReaderAllocationTests
{
    [Fact]
    public void ArrayAndAddressedReadersRetainIndependentPackedGroupsAndPolygonBytes()
    {
        byte[] bytes = Geometry(1);
        ImmutableArray<NativeBattleRawMesh> direct = NativeBattleRawGeometryReader.Read(bytes, 1);
        List<(int Offset, int Length)> reads = [];
        List<byte[]> borrowed = [];
        ImmutableArray<NativeBattleRawMesh> addressed = NativeBattleRawGeometryReader.Read((offset, length) =>
        {
            reads.Add((offset, length));
            byte[] slice = bytes.AsSpan(offset, length).ToArray();
            borrowed.Add(slice);
            return slice;
        }, 1);
        Assert.Equal(new (int, int)[] { (0, 4), (4, 4), (8, 2), (10, 2), (12, 2), (14, 12), (14, 2), (16, 2), (18, 2), (20, 2), (22, 2), (24, 2), (26, 2), (32, 16) }, reads);
        string expected = JsonSerializer.Serialize(direct);
        Assert.Equal(expected, JsonSerializer.Serialize(addressed));
        Array.Fill(bytes, (byte)0xCC);
        foreach (byte[] slice in borrowed)
        {
            Array.Fill(slice, (byte)0xDD);
        }
        Assert.Equal(expected, JsonSerializer.Serialize(direct));
        Assert.Equal(expected, JsonSerializer.Serialize(addressed));
        Assert.Throws<InvalidDataException>(() => NativeBattleRawGeometryReader.Read((_, length) => new byte[length + 1], 1));
        Assert.Throws<InvalidDataException>(() => NativeBattleRawGeometryReader.Read((_, _) => null!, 1));
    }

    [Fact]
    public void ArrayReaderAvoidsPerScalarAddressedReadBuffers()
    {
        byte[] bytes = Geometry(16);
        Func<int, int, byte[]> copiedRead = (offset, length) => bytes.AsSpan(offset, length).ToArray();
        _ = NativeBattleRawGeometryReader.Read(bytes, 1);
        _ = NativeBattleRawGeometryReader.Read(copiedRead, 1);
        long before = GC.GetAllocatedBytesForCurrentThread();
        int total = 0;
        for (int iteration = 0; iteration < 100; iteration++)
        {
            total += NativeBattleRawGeometryReader.Read(bytes, 1)[0].Vertices.Length;
        }
        long direct = GC.GetAllocatedBytesForCurrentThread() - before;
        before = GC.GetAllocatedBytesForCurrentThread();
        for (int iteration = 0; iteration < 100; iteration++)
        {
            total += NativeBattleRawGeometryReader.Read(copiedRead, 1)[0].Vertices.Length;
        }
        long addressed = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(3200, total);
        Assert.True(direct < addressed * 3 / 4, $"Array geometry reads allocated {direct} bytes versus {addressed} bytes with copied scalar buffers.");
    }

    private static byte[] Geometry(int vertices)
    {
        int counts = (14 + vertices * 6 + 3) & ~3;
        byte[] bytes = new byte[counts + 28];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 1);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 8);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(12), (ushort)vertices);
        for (int vertex = 0; vertex < vertices; vertex++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(14 + vertex * 6), (short)(vertex - 1));
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(16 + vertex * 6), 2);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(18 + vertex * 6), 3);
        }
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(counts), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(counts + 12), 0x8000);
        return bytes;
    }
}
