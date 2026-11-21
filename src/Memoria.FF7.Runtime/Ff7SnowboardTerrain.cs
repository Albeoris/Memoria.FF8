using System.Buffers.Binary;

namespace Memoria.FF7.Runtime;

public static class Ff7SnowboardTerrain
{
    public const int TileSize = 0x374;
    public const int TriangleCount = 72;
    private const int VertexStart = 8;
    private const int TriangleStart = 0x254;

    public static int FindTriangle(ReadOnlySpan<byte> tile, int localX, int localZ)
    {
        Validate(tile);
        int x = unchecked((short)localX);
        int z = unchecked((short)localZ);
        int bound = BinaryPrimitives.ReadInt16LittleEndian(tile[0x3E..]);
        for (int triangle = TriangleCount - 1; triangle >= 0; triangle--)
        {
            int record = TriangleStart + triangle * 4;
            var a = Vertex(tile, tile[record]);
            var b = Vertex(tile, tile[record + 1]);
            var c = Vertex(tile, tile[record + 2]);
            if (!Within(a, x, z, bound) || !Within(b, x, z, bound) || !Within(c, x, z, bound))
            {
                continue;
            }
            if (Side(a, b, x, z) >= 0 && Side(b, c, x, z) >= 0 && Side(c, a, x, z) >= 0)
            {
                return triangle;
            }
        }
        return -1;
    }

    public static byte SurfaceFlag(ReadOnlySpan<byte> tile, int triangle)
    {
        Validate(tile);
        if ((uint)triangle >= TriangleCount)
        {
            throw new ArgumentOutOfRangeException(nameof(triangle));
        }
        return tile[TriangleStart + triangle * 4 + 3];
    }

    private static bool Within((short X, short Z) vertex, int x, int z, int bound) => Math.Abs(vertex.X - x) <= bound && Math.Abs(vertex.Z - z) <= bound;

    private static int Side((short X, short Z) a, (short X, short Z) b, int x, int z)
    {
        return unchecked((short)(b.Z - a.Z) * (short)(x - a.X) - (short)(b.X - a.X) * (short)(z - a.Z));
    }

    private static (short X, short Z) Vertex(ReadOnlySpan<byte> tile, byte index)
    {
        int offset = VertexStart + index * 8;
        return (BinaryPrimitives.ReadInt16LittleEndian(tile[offset..]), BinaryPrimitives.ReadInt16LittleEndian(tile[(offset + 4)..]));
    }

    private static void Validate(ReadOnlySpan<byte> tile)
    {
        if (tile.Length < TileSize)
        {
            throw new InvalidDataException("The native snowboard terrain tile is truncated.");
        }
        for (int triangle = 0; triangle < TriangleCount; triangle++)
        {
            for (int corner = 0; corner < 3; corner++)
            {
                if (VertexStart + tile[TriangleStart + triangle * 4 + corner] * 8 + 8 > TriangleStart)
                {
                    throw new InvalidDataException("A native snowboard triangle references a vertex outside its tile vector area.");
                }
            }
        }
    }
}
