using System.Buffers.Binary;

namespace Albeoris.Games.FF8.Imaging.Map;

/// <summary>An FF8 field tile map terminated by the X coordinate 0x7FFF.</summary>
public sealed class MapFile
{
    private const UInt16 Terminator = 0x7FFF;

    public MapFile(IEnumerable<MapTile> tiles, Byte[]? terminatorData = null)
    {
        ArgumentNullException.ThrowIfNull(tiles);
        Tiles.AddRange(tiles);
        TerminatorData = terminatorData ?? [0xFF, 0x7F];
    }

    public List<MapTile> Tiles { get; } = [];
    public Byte[] TerminatorData { get; }
    public Boolean HasTerminator => TerminatorData.Length >= sizeof(UInt16)
        && BinaryPrimitives.ReadUInt16LittleEndian(TerminatorData) == Terminator;
    public Int32 Width => Tiles.Count == 0 ? 0 : Tiles.Max(tile => (Int32)tile.X) - Tiles.Min(tile => (Int32)tile.X) + 16;
    public Int32 Height => Tiles.Count == 0 ? 0 : Tiles.Max(tile => (Int32)tile.Y) - Tiles.Min(tile => (Int32)tile.Y) + 16;
    public Int32 TextureCount => Tiles.Count == 0 ? 0 : Tiles.Max(tile => tile.TextureId) + 1;

    public static MapFile Read(ReadOnlySpan<Byte> content)
    {
        List<MapTile> tiles = [];
        Int32 position = 0;
        while (content.Length - position >= sizeof(UInt16))
        {
            if (BinaryPrimitives.ReadUInt16LittleEndian(content[position..]) == Terminator)
                return new MapFile(tiles, content[position..].ToArray());
            if (content.Length - position < MapTile.Size)
                return new MapFile(tiles, content[position..].ToArray());
            tiles.Add(MapTile.Read(content[position..]));
            position += MapTile.Size;
        }

        return new MapFile(tiles, content[position..].ToArray());
    }

    public Byte[] Write()
    {
        Byte[] result = new Byte[checked(Tiles.Count * MapTile.Size + TerminatorData.Length)];
        Span<Byte> remaining = result;
        foreach (MapTile tile in Tiles)
        {
            tile.CopyTo(remaining);
            remaining = remaining[MapTile.Size..];
        }
        TerminatorData.CopyTo(remaining);
        return result;
    }
}
