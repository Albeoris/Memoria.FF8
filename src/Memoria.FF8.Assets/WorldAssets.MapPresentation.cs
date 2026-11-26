using System.Buffers.Binary;
using System.Numerics;

namespace Memoria.FF8.Assets;

public sealed record WorldGlobeVertex(Vector2 Position, Vector2 TextureOffset);
public sealed record WorldGlobeFace(IReadOnlyList<int> Vertices);
public sealed record WorldGlobeGeometry(IReadOnlyList<WorldGlobeVertex> Vertices, IReadOnlyList<WorldGlobeFace> Faces);

public sealed partial class WorldAssets
{
    private WorldGlobeGeometry? mapGlobeGeometry;
    private WorldMapLocationMarker[]? mapLocationMarkers;
    private (ushort Story, int XCell, int ZCell)? mapLocationSelectionKey;
    private IReadOnlyList<Vector2> visibleMapLocations = [];

    public IReadOnlyList<Vector2> ReadVisibleMapLocations(WorldFieldSelectionInput player)
    {
        var key = (player.StoryWord, player.X >> 11, player.RawZ >> 11);
        if (key == mapLocationSelectionKey)
        {
            return visibleMapLocations;
        }

        mapLocationMarkers ??= ReadMapLocationMarkers();
        List<Vector2> locations = [];
        foreach (WorldMapLocationMarker marker in mapLocationMarkers)
        {
            if (FieldEntries.SelectMapLocation(marker.X, marker.Y, player) == marker.Id)
            {
                locations.Add(new Vector2(marker.X, marker.Y));
            }
        }
        visibleMapLocations = locations.AsReadOnly();
        mapLocationSelectionKey = key;
        return visibleMapLocations;
    }

    private WorldMapLocationMarker[] ReadMapLocationMarkers()
    {
        const int recordSize = 12;
        ReadOnlySpan<byte> records = wmset.AsSpan(sections[30], sections[31] - sections[30]);
        ReadOnlySpan<byte> names = wmset.AsSpan(sections[31], sections[32] - sections[31]);
        int end = BinaryPrimitives.ReadInt32LittleEndian(records);
        if (end < 4 || end > records.Length || (end - 4) % recordSize != 0)
        {
            throw new InvalidDataException("Invalid WMSET map marker records.");
        }

        List<WorldMapLocationMarker> markers = [];
        for (int offset = 4; offset < end; offset += recordSize)
        {
            byte id = records[offset + 2];
            int namePointer = id * 4;
            if (namePointer > names.Length - 4)
            {
                throw new InvalidDataException("Invalid WMSET map marker name index.");
            }
            int nameOffset = BinaryPrimitives.ReadInt32LittleEndian(names[namePointer..]);
            if ((uint)nameOffset >= names.Length)
            {
                throw new InvalidDataException("Invalid WMSET map marker name pointer.");
            }
            if (names[nameOffset] != 0)
            {
                markers.Add(new WorldMapLocationMarker(records[offset], records[offset + 1], id));
            }
        }
        return markers.ToArray();
    }

    private readonly record struct WorldMapLocationMarker(byte X, byte Y, byte Id);

    public WorldGlobeGeometry ReadMapGlobeGeometry()
    {
        if (mapGlobeGeometry is not null)
        {
            return mapGlobeGeometry;
        }
        byte[] faces = NativeWorldMemory.Read(0xC76BE8, 148);
        byte[] positions = NativeWorldMemory.Read(0xC76C7C, 80);
        byte[] offsets = NativeWorldMemory.Read(0xC76CCC, 80);
        List<WorldGlobeVertex> vertices = [];
        List<WorldGlobeFace> polygons = [];
        for (int index = 0; index < 40; index++)
        {
            vertices.Add(new(new Vector2(unchecked((sbyte)positions[index * 2]), unchecked((sbyte)positions[index * 2 + 1])), new Vector2(unchecked((sbyte)offsets[index * 2]), unchecked((sbyte)offsets[index * 2 + 1]))));
        }
        for (int index = 0; index < 37; index++)
        {
            int count = index < 25 ? 4 : 3;
            int[] corners = faces.AsSpan(index * 4, count).ToArray().Select(value => (int)value).ToArray();
            if (corners.Any(corner => corner >= vertices.Count) || count == 3 && faces[index * 4 + 3] != 255)
            {
                throw new InvalidDataException("Native globe topology leaves its original vertex tables.");
            }
            polygons.Add(new(Array.AsReadOnly(corners)));
        }
        mapGlobeGeometry = new(vertices.AsReadOnly(), polygons.AsReadOnly());
        return mapGlobeGeometry;
    }
}
