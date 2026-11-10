using System.Buffers.Binary;
using System.Numerics;
using Memoria.FF7.Formats;

namespace Memoria.FF7.Resources.Tests;

public sealed class PcWorldMapTests
{
    [Fact]
    public void FieldSelectorsUsePairedRowsAndRetainNativeSignedCoordinates()
    {
        byte[] bytes = new byte[24];
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(12), -777);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(14), 1212);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(16), 99);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(18), 345);
        bytes[20] = 254;
        PcWorldFieldTable table = PcWorldFieldTable.Read(bytes);
        Assert.Equal(new PcWorldFieldEntry(-777, 1212, 99, 345, 254), table.Select(0x101));
        Assert.Equal(table.Entries[0], table.Select(0x100));
        Assert.Throws<InvalidDataException>(() => table.Select(0x200));
        Assert.Throws<InvalidDataException>(() => PcWorldFieldTable.Read(new byte[13]));
    }

    [Fact]
    public void EventsKeepWordRelativeAliasesAndRejectInvalidDispatchEntries()
    {
        byte[] bytes = new byte[0x408];
        Array.Fill(bytes, (byte)255, 4, 0x3FC);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4), 0x4102);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(6), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), 0x8023);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(0x402), 0x203);
        PcWorldEvents parsed = PcWorldEvents.Read(bytes);
        Assert.Equal(2, parsed.Functions.Count);
        Assert.Equal(1, parsed.Functions[0].Kind);
        Assert.Equal(1, parsed.Functions[0].Model);
        Assert.Equal(2, parsed.Functions[0].Event);
        Assert.Equal(2, parsed.Functions[1].Kind);
        Assert.Equal(2, parsed.Functions[1].Mesh);
        Assert.Equal(3, parsed.Functions[1].Event);
        Assert.Equal(parsed.Functions[0].Offset, parsed.Functions[1].Offset);
        Assert.Equal(0x203, parsed.Code[1]);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(6), 4);
        Assert.Throws<InvalidDataException>(() => PcWorldEvents.Read(bytes));
        Assert.Throws<InvalidDataException>(() => PcWorldEvents.Read(new byte[0x403]));
    }

    [Fact]
    public void MeshPreservesSignedVectorsPackedMetadataAndTextureCoordinates()
    {
        byte[] native = new byte[64];
        BinaryPrimitives.WriteUInt16LittleEndian(native, 1);
        BinaryPrimitives.WriteUInt16LittleEndian(native.AsSpan(2), 3);
        native[5] = 1;
        native[6] = 2;
        native[7] = 0xA3;
        native[8] = 255;
        native[9] = 19;
        BinaryPrimitives.WriteUInt16LittleEndian(native.AsSpan(14), 0xBF19);
        BinaryPrimitives.WriteInt16LittleEndian(native.AsSpan(16), -32768);
        BinaryPrimitives.WriteInt16LittleEndian(native.AsSpan(18), 234);
        BinaryPrimitives.WriteInt16LittleEndian(native.AsSpan(20), 32767);
        BinaryPrimitives.WriteInt16LittleEndian(native.AsSpan(40), -4096);
        byte[] block = Block(native);
        PcWorldMesh mesh = PcWorldMap.Read(block).ReadMesh(0, 0);
        Assert.Equal(new Vector3(-32768, 234, 32767), mesh.Vertices[0]);
        Assert.Equal(new Vector3(-4096, 0, 0), mesh.Normals[0]);
        PcWorldTriangle triangle = Assert.Single(mesh.Triangles);
        Assert.Equal(281, triangle.TextureIndex);
        Assert.Equal(31, triangle.Location);
        Assert.Equal(5, triangle.Script);
        Assert.Equal(3, triangle.Terrain);
        Assert.True(triangle.Chocobo);
        Assert.Equal(new Vector2(255, 19), triangle.UvA);
        native[6] = 3;
        Assert.Throws<InvalidDataException>(() => PcWorldMap.Read(Block(native)).ReadMesh(0, 0));
        BinaryPrimitives.WriteUInt32LittleEndian(block, PcWorldMap.BlockSize - 2);
        Assert.Throws<InvalidDataException>(() => PcWorldMap.Read(block).ReadMesh(0, 0));
        Assert.Throws<InvalidDataException>(() => PcWorldMap.Read(new byte[10]));
    }

    private static byte[] Block(byte[] native)
    {
        List<byte> compressed = [];
        for (int offset = 0; offset < native.Length; offset += 8)
        {
            compressed.Add(255);
            compressed.AddRange(native.Skip(offset).Take(8));
        }
        byte[] block = new byte[PcWorldMap.BlockSize];
        BinaryPrimitives.WriteUInt32LittleEndian(block, 64);
        BinaryPrimitives.WriteUInt32LittleEndian(block.AsSpan(64), checked((uint)compressed.Count));
        compressed.ToArray().CopyTo(block, 68);
        return block;
    }
}
