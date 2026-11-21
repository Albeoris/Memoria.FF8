using System.Buffers.Binary;
using Memoria.FF7.Runtime;

internal static class NativeSnowboardTerrainTests
{
    internal static void Run()
    {
        byte[] tile = Tile();
        byte[] before = tile.ToArray();
        Check(Ff7SnowboardTerrain.FindTriangle(tile, 10, 10) == 0, "Native negative-XZ winding did not admit an interior point.");
        Check(Ff7SnowboardTerrain.FindTriangle(tile, 0, 0) == 0 && Ff7SnowboardTerrain.FindTriangle(tile, 50, 50) == 0, "Native zero edge determinants rejected an endpoint or edge.");
        Check(Ff7SnowboardTerrain.FindTriangle(tile, 90, 90) == -1, "A point outside the native triangle was admitted.");
        Check(Ff7SnowboardTerrain.FindTriangle(tile, 65546, 65546) == 0, "Native local coordinates were not reduced to signed WORDs.");
        Check(Ff7SnowboardTerrain.SurfaceFlag(tile, 0) == 0xA7 && before.SequenceEqual(tile), "Picking changed native data or lost the raw surface flag.");
        tile[0x254 + 71 * 4] = 0;
        tile[0x254 + 71 * 4 + 1] = 1;
        tile[0x254 + 71 * 4 + 2] = 2;
        Check(Ff7SnowboardTerrain.FindTriangle(tile, 10, 10) == 71, "Overlapping native triangles did not select the highest index.");
        tile = Tile();
        BinaryPrimitives.WriteInt16LittleEndian(tile.AsSpan(0x3E), 50);
        Check(Ff7SnowboardTerrain.FindTriangle(tile, 10, 10) == -1, "The native per-vertex signed bounding check was omitted.");
        BinaryPrimitives.WriteInt16LittleEndian(tile.AsSpan(0x3E), -1);
        Check(Ff7SnowboardTerrain.FindTriangle(tile, 10, 10) == -1, "A negative native bound was treated as unsigned.");
        tile = Tile();
        Vertex(tile, 0, -32767, -32767);
        Vertex(tile, 1, 32767, -32767);
        Vertex(tile, 2, -32767, 32767);
        BinaryPrimitives.WriteInt16LittleEndian(tile.AsSpan(0x3E), short.MaxValue);
        Check(Ff7SnowboardTerrain.FindTriangle(tile, 0, 0) == 0, "Native wrapped WORD edge differences were replaced with wide geometric determinants.");
        Check(Ff7SnowboardTerrain.FindTriangle(tile, 1, 0) == -1, "The cheap bounding difference incorrectly wrapped at WORD width.");
        byte[] malformed = Tile();
        malformed[0x254] = 255;
        Reject(malformed);
        Reject(before.AsSpan(0, before.Length - 1));
        Console.WriteLine("Native snowboard tile winding, reverse selection, signed arithmetic and bounds passed.");
    }

    private static byte[] Tile()
    {
        byte[] tile = new byte[Ff7SnowboardTerrain.TileSize];
        Vertex(tile, 0, 0, 0);
        Vertex(tile, 1, 0, 100);
        Vertex(tile, 2, 100, 0);
        BinaryPrimitives.WriteInt16LittleEndian(tile.AsSpan(0x3E), 1000);
        for (int triangle = 0; triangle < 72; triangle++)
        {
            int offset = 0x254 + triangle * 4;
            tile[offset] = 0;
            tile[offset + 1] = triangle == 0 ? (byte)1 : (byte)2;
            tile[offset + 2] = triangle == 0 ? (byte)2 : (byte)1;
        }
        tile[0x257] = 0xA7;
        return tile;
    }

    private static void Vertex(byte[] tile, int index, short x, short z)
    {
        BinaryPrimitives.WriteInt16LittleEndian(tile.AsSpan(8 + index * 8), x);
        BinaryPrimitives.WriteInt16LittleEndian(tile.AsSpan(12 + index * 8), z);
    }

    private static void Reject(ReadOnlySpan<byte> tile)
    {
        try
        {
            Ff7SnowboardTerrain.FindTriangle(tile, 0, 0);
        }
        catch (InvalidDataException)
        {
            return;
        }
        throw new InvalidOperationException("Malformed native snowboard terrain was accepted.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
