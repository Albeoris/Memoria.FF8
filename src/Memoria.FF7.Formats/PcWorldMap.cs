using System.Buffers.Binary;
using System.Numerics;

namespace Memoria.FF7.Formats;

public sealed record PcWorldTriangle(byte A, byte B, byte C, byte TerrainAndScript, Vector2 UvA, Vector2 UvB, Vector2 UvC, ushort PackedIds)
{
    public int TextureIndex => PackedIds & 511;
    public int Location => (PackedIds >> 9) & 31;
    public int Script => TerrainAndScript >> 5;
    public int Terrain => TerrainAndScript & 31;
    public bool Chocobo => (PackedIds & 0x8000) != 0;
}

public sealed record PcWorldMesh(int BlockIndex, int MeshIndex, IReadOnlyList<Vector3> Vertices, IReadOnlyList<Vector3> Normals, IReadOnlyList<PcWorldTriangle> Triangles);

public sealed class PcWorldMap
{
    public const int BlockSize = 0xB800;
    private readonly byte[] source;

    private PcWorldMap(byte[] source)
    {
        this.source = source;
    }

    public int BlockCount => source.Length / BlockSize;

    public static PcWorldMap Read(ReadOnlySpan<byte> source)
    {
        if (source.Length == 0 || source.Length % BlockSize != 0)
        {
            throw new InvalidDataException("FF7 world terrain requires complete fixed-size blocks.");
        }
        return new PcWorldMap(source.ToArray());
    }

    public PcWorldMesh ReadMesh(int blockIndex, int meshIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(blockIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(blockIndex, BlockCount);
        ArgumentOutOfRangeException.ThrowIfNegative(meshIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(meshIndex, 16);
        ReadOnlySpan<byte> block = source.AsSpan(blockIndex * BlockSize, BlockSize);
        uint rawOffset = BinaryPrimitives.ReadUInt32LittleEndian(block[(meshIndex * 4)..]);
        if (rawOffset < 64 || rawOffset > BlockSize - 4)
        {
            throw new InvalidDataException("FF7 world mesh offset lies outside its block.");
        }
        int offset = checked((int)rawOffset);
        uint compressedLength = BinaryPrimitives.ReadUInt32LittleEndian(block[offset..]);
        if (compressedLength > BlockSize - offset - 4)
        {
            throw new InvalidDataException("FF7 world compressed mesh lies outside its block.");
        }
        byte[] decoded = Lzss.DecodeLengthFramed(block.Slice(offset, checked((int)compressedLength + 4)), 1024 * 1024);
        if (decoded.Length < 4)
        {
            throw new InvalidDataException("FF7 world mesh has no counts.");
        }
        int triangleCount = BinaryPrimitives.ReadUInt16LittleEndian(decoded);
        int vertexCount = BinaryPrimitives.ReadUInt16LittleEndian(decoded.AsSpan(2));
        int required = checked(4 + triangleCount * 12 + vertexCount * 16);
        if (vertexCount > 256 || decoded.Length != required)
        {
            throw new InvalidDataException("FF7 world mesh counts do not match its decoded payload.");
        }
        PcWorldTriangle[] triangles = new PcWorldTriangle[triangleCount];
        for (int i = 0; i < triangles.Length; i++)
        {
            ReadOnlySpan<byte> row = decoded.AsSpan(4 + i * 12, 12);
            if (row[0] >= vertexCount || row[1] >= vertexCount || row[2] >= vertexCount)
            {
                throw new InvalidDataException("FF7 world triangle refers to an absent vertex.");
            }
            triangles[i] = new(row[0], row[1], row[2], row[3], new(row[4], row[5]), new(row[6], row[7]), new(row[8], row[9]), BinaryPrimitives.ReadUInt16LittleEndian(row[10..]));
        }
        Vector3[] vertices = new Vector3[vertexCount];
        Vector3[] normals = new Vector3[vertexCount];
        for (int i = 0; i < vertexCount; i++)
        {
            vertices[i] = ReadVector(decoded.AsSpan(4 + triangleCount * 12 + i * 8, 8));
            normals[i] = ReadVector(decoded.AsSpan(4 + triangleCount * 12 + vertexCount * 8 + i * 8, 8));
        }
        return new(blockIndex, meshIndex, Array.AsReadOnly(vertices), Array.AsReadOnly(normals), Array.AsReadOnly(triangles));
    }

    private static Vector3 ReadVector(ReadOnlySpan<byte> row)
    {
        return new(BinaryPrimitives.ReadInt16LittleEndian(row), BinaryPrimitives.ReadInt16LittleEndian(row[2..]), BinaryPrimitives.ReadInt16LittleEndian(row[4..]));
    }
}
