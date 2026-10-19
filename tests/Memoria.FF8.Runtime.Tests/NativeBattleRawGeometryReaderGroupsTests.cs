using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleRawGeometryReaderGroupsTests
{
    [Fact]
    public void Multiple_objects_retain_all_four_group_records_and_exact_source_ends()
    {
        byte[] bytes = new byte[160];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 2);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 12);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8), 108);
        foreach (int offset in new[] { 16, 18, 20, 22, 116 })
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset), 1);
        }
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(28), 0x24123456);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(44), 0x2C123456);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(64), 0x26ABCDEF);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(84), 0x2E010203);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(124), 0x26040506);
        List<(int Index, int Payload, int End)> observed = [];
        int consumed = 0;
        ImmutableArray<NativeBattleRawMesh> meshes = NativeBattleRawGeometryReader.Read((offset, length) =>
        {
            Assert.InRange(offset + length, 0, 144);
            consumed = Math.Max(consumed, offset + length);
            return bytes.AsSpan(offset, length).ToArray();
        }, 1, (index, counts, payload, end) => observed.Add((index, payload, end)));
        Assert.Equal(new[] { (0, 28, 108), (1, 124, 144) }, observed);
        Assert.Equal(144, consumed);
        Assert.Equal(new[] { 16, 20, 20, 24 }, meshes[0].Polygons.Select(polygon => polygon.Bytes.Length));
        Assert.Equal(new[] { false, true, false, true }, meshes[0].Polygons.Select(polygon => polygon.Quad));
        Assert.Equal(new uint[] { 0x24123456, 0x2C123456, 0x26ABCDEF, 0x2E010203 }, meshes[0].Polygons.Select(polygon => BinaryPrimitives.ReadUInt32LittleEndian(polygon.Bytes.AsSpan())));
        Assert.Single(meshes[1].Polygons);
        Assert.Equal(20, meshes[1].Polygons[0].Bytes.Length);
        Assert.Equal(0x26040506u, BinaryPrimitives.ReadUInt32LittleEndian(meshes[1].Polygons[0].Bytes.AsSpan()));
        Assert.All(meshes, mesh => mesh.Validate());
    }

    [Fact]
    public void High_bit_polygon_count_is_unsigned_and_requires_its_first_actual_record()
    {
        byte[] bytes = new byte[24];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 1);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 8);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(12), 0x8000);
        InvalidDataException error = Assert.Throws<InvalidDataException>(() => NativeBattleRawGeometryReader.Read((offset, length) =>
        {
            if (offset == 24 && length == 16)
            {
                throw new InvalidDataException("First unsigned polygon record required.");
            }
            return bytes.AsSpan(offset, length).ToArray();
        }, 1));
        Assert.Equal("First unsigned polygon record required.", error.Message);
    }
}
