using System.Numerics;
using Albeoris.Games.FF8.FlArchives;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Albeoris.Games.FF8.Imaging;
using Albeoris.Games.FF8.Imaging.Tim;

namespace Memoria.FF8.Assets;

public readonly record struct WorldSpawn(int X, int Z, short NativeHeight, ushort Unknown);
public readonly record struct WorldGround(float Height, WorldTriangle Triangle, int Segment);
public readonly record struct WorldSky(Vector3 Position, Rgba32 Top, Rgba32 Center, Rgba32 Bottom, int Radius = 0);
public sealed record WorldTriangle(Vector3 A, Vector3 B, Vector3 C, Vector2 UvA, Vector2 UvB, Vector2 UvC, byte TexturePage, byte Palette, byte GroundType, byte TextureFlags, byte VertexFlags)
{
    public int NativeBlockIndex { get; init; }
    public int NativePolygonIndex { get; init; }
    public bool Walkable => (VertexFlags & 0x80) != 0;
    public bool Forest => GroundType <= 5;
    public bool Road => (TextureFlags & 0x20) != 0;
    public bool Water => (TextureFlags & 0x40) != 0;
    public bool Transparent => (TextureFlags & 0x10) != 0;
    public bool FieldEntrance => (TextureFlags & 8) != 0;
}

public sealed record WorldSegment(int MapId, int SourceId, uint GroupId, IReadOnlyList<WorldTriangle> Triangles);

/// <summary>Reads selected original world segments. Coordinates use centered native X/Z and upward elevation.</summary>
public sealed partial class WorldAssets : IDisposable
{
    public const int SegmentBytes = 0x9000;
    public const int SegmentUnits = 8192;
    public const int Columns = 32;
    public const int Rows = 24;
    private readonly IFlArchive archive;
    private readonly string gameDirectory;
    private readonly IFlArchiveEntry terrain;
    private readonly byte[] wmset;
    private readonly int[] sections;
    private readonly Dictionary<(int, int), WorldSegment> segments = [];
    private readonly Dictionary<(int, int), PixelImage> textures = [];
    private byte[]? texl;
    private PixelImage? roads;
    private PixelImage? water;
    private readonly Dictionary<byte, FieldModel> footModels = [];
    private WorldFieldEntryReader? fieldEntries;

    public WorldAssets(string gameDirectory)
    {
        this.gameDirectory = gameDirectory;
        archive = FlArchive.OpenForRead(Path.Combine(gameDirectory, "Data", "lang-en", "world.fs"), FlArchiveRepresentation.Files);
        terrain = Entry("wmx.obj");
        if (terrain.Size != 835 * SegmentBytes)
        {
            throw new InvalidDataException("Unexpected WMX segment count.");
        }
        wmset = Read("wmsetus.obj");
        sections = WorldAssetReader.ReadSections(wmset);
        Spawns = WorldAssetReader.ReadSpawns(wmset, sections);
        Skies = WorldAssetReader.InnerPointers(wmset, sections[32]).Select(pointer => WorldAssetReader.ReadSky(wmset, sections[32] + pointer)).ToArray();
    }

    public IReadOnlyList<WorldSpawn> Spawns { get; }
    public IReadOnlyList<WorldSky> Skies { get; }
    public int LoadedSegmentCount => segments.Count;
    public int LoadedTextureCount => textures.Count;
    public FieldModel Squall => GetFootModel(0);
    public WorldFieldEntryReader FieldEntries => fieldEntries ??= ReadFieldEntries();

    public FieldModel GetFootModel(byte entityType)
    {
        // Native546E10 selects stored shape4 for uniform Squall type1.
        int shape = entityType switch
        {
            0 => 0,
            1 => 4,
            _ => throw new NotSupportedException($"Native foot entity type {entityType} has no implemented original model path.")
        };
        if (!footModels.TryGetValue(entityType, out FieldModel? model))
        {
            model = WorldAssetReader.ReadWorldCharacter(Read("chara.one"), shape);
            footModels.Add(entityType, model);
        }
        return model;
    }

    public short GetNativeMapLocationId(int x, int rawZ)
    {
        if (x is < -131072 or >= 131072 || rawZ is < -98304 or >= 98304 || sections[19] - sections[18] != Columns * Rows + 4)
        {
            throw new InvalidDataException("Invalid native world map-location table or centered coordinates.");
        }
        // Native542DA0 binds dword20400BC to zero-based section18 (the nineteenth section).
        int segment = 32 * ((rawZ + 294912) % 196608 / 8192) + (x + 393216) % 262144 / 8192;
        return unchecked((sbyte)wmset[sections[18] + segment]);
    }

    private WorldFieldEntryReader ReadFieldEntries()
    {
        using IFlArchive main = FlArchive.OpenForRead(Path.Combine(gameDirectory, "Data", "lang-en", "main.fs"), FlArchiveRepresentation.Files);
        IFlArchiveEntry entry = main.Entries.Single(item => item.RelativePath.Replace('\\', '/').Split('/')[^1].Equals("wm2field.tbl", StringComparison.OrdinalIgnoreCase));
        using Stream source = entry.OpenForRead();
        byte[] table = new byte[checked((int)entry.Size)];
        source.ReadExactly(table);
        return WorldFieldEntryReader.Read(wmset.AsSpan(sections[7], sections[8] - sections[7]), table);
    }

    public static int SegmentAt(float x, float z)
    {
        int column = (int)MathF.Floor((Wrap(x, Columns * SegmentUnits) + 131072) / SegmentUnits);
        int row = (int)MathF.Floor((Wrap(z, Rows * SegmentUnits) + 98304) / SegmentUnits);
        return row * Columns + column;
    }

    public static float Wrap(float coordinate, int span)
    {
        return coordinate - MathF.Floor((coordinate + span / 2f) / span) * span;
    }

    public WorldSegment GetSegment(int mapId, IReadOnlyDictionary<int, int>? replacements = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(mapId);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(mapId, Columns * Rows);
        int sourceId = replacements?.GetValueOrDefault(mapId, mapId) ?? mapId;
        if (!segments.TryGetValue((mapId, sourceId), out WorldSegment? segment))
        {
            ArgumentOutOfRangeException.ThrowIfNegative(sourceId);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(sourceId, 835);
            using Stream source = terrain.OpenForRead();
            source.Seek((long)sourceId * SegmentBytes, SeekOrigin.Begin);
            byte[] bytes = new byte[SegmentBytes];
            source.ReadExactly(bytes);
            segment = WorldAssetReader.ReadSegment(bytes, mapId, sourceId);
            segments.Add((mapId, sourceId), segment);
        }
        return segment;
    }

    public WorldGround? FindGround(float x, float z, IReadOnlyDictionary<int, int>? replacements = null, float? nearHeight = null)
    {
        x = Wrap(x, Columns * SegmentUnits);
        z = Wrap(z, Rows * SegmentUnits);
        int mapId = SegmentAt(x, z);
        WorldGround? result = null;
        foreach (WorldTriangle triangle in GetSegment(mapId, replacements).Triangles)
        {
            if (!WorldAssetReader.TryHeight(triangle, x, z, out float height))
            {
                continue;
            }
            if (result is null || (nearHeight.HasValue ? MathF.Abs(height - nearHeight.Value) < MathF.Abs(result.Value.Height - nearHeight.Value) : height > result.Value.Height))
            {
                result = new WorldGround(height, triangle, mapId);
            }
        }
        return result;
    }

    public WorldGround? FindNativeGround(int x, int z, IReadOnlyDictionary<int, int>? replacements = null, int? previousHeight = null, int entityType = 0, byte? previousGroundType = null)
    {
        x = (int)Wrap(x, Columns * SegmentUnits);
        z = (int)Wrap(z, Rows * SegmentUnits);
        int mapId = SegmentAt(x, z);
        WorldGround? selected = null;
        foreach (WorldTriangle triangle in GetSegment(mapId, replacements).Triangles)
        {
            byte permission = entityType is 0 or 1 or 33 ? Memoria.FF8.Runtime.NativeWorldTerrain.PermissionMask(entityType) : throw new NotSupportedException("Original advisory world terrain selection for this entity is unrepresented.");
            if ((triangle.VertexFlags & permission) == 0 || !WorldAssetReader.TryNativeHeight(triangle, x, z, out int height) || previousHeight.HasValue && !triangle.Forest && previousGroundType is not <= 5 && Math.Abs(height - previousHeight.Value) >= 200)
            {
                continue;
            }
            WorldGround next = new(height, triangle, mapId);
            if (entityType != 33)
            {
                return next;
            }
            if (selected is WorldGround prior && (prior.Height != height || prior.Triangle.GroundType != triangle.GroundType || prior.Triangle.FieldEntrance != triangle.FieldEntrance))
            {
                throw new NotSupportedException("Native car ground selection requires unrepresented polygon-cache precedence at overlapping accepted surfaces.");
            }
            selected ??= next;
        }
        return selected;
    }

    public PixelImage GetTexture(WorldTriangle triangle)
    {
        if (triangle.Road)
        {
            return roads ??= CreateRoadAtlas();
        }
        if (triangle.Water)
        {
            return water ??= CreateWaterAtlas();
        }
        if (!textures.TryGetValue((triangle.TexturePage, triangle.Palette), out PixelImage? image))
        {
            texl ??= Read("texl.obj");
            TimImage tim = WorldAssetReader.ReadTim(texl, triangle.TexturePage * 0x12800).Image;
            image = tim.Decode(triangle.Palette);
            textures.Add((triangle.TexturePage, triangle.Palette), image);
        }
        return image;
    }

    private PixelImage CreateRoadAtlas()
    {
        PixelImage atlas = new(256, 256);
        foreach (int pointer in WorldAssetReader.InnerPointers(wmset, sections[38]))
        {
            TimImage tim = WorldAssetReader.ReadTim(wmset, sections[38] + pointer).Image;
            Copy(tim.Decode(), atlas, (tim.X - 832) * 4, tim.Y - 256);
        }
        return atlas;
    }

    private PixelImage CreateWaterAtlas()
    {
        PixelImage atlas = new(256, 128);
        int[] pointers = WorldAssetReader.InnerPointers(wmset, sections[37]);
        int[] images = [23, 16, 21, 22, 17, 18, 19, 20];
        for (int i = 0; i < images.Length; i++)
        {
            TimImage tim = WorldAssetReader.ReadTim(wmset, sections[37] + pointers[images[i]]).Image;
            Copy(tim.Decode(), atlas, i % 4 * 64, i / 4 * 64);
        }
        return atlas;
    }

    private static void Copy(PixelImage source, PixelImage target, int x, int y)
    {
        if (x < 0 || y < 0 || x + source.Width > target.Width || y + source.Height > target.Height)
        {
            throw new InvalidDataException("World texture lies outside its VRAM atlas.");
        }
        for (int row = 0; row < source.Height; row++)
        {
            source.Pixels.AsSpan(row * source.Width, source.Width).CopyTo(target.Pixels.AsSpan((y + row) * target.Width + x, source.Width));
        }
    }

    private IFlArchiveEntry Entry(string name)
    {
        return archive.Entries.Single(entry => entry.RelativePath.Replace('\\', '/').Split('/')[^1].Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    private byte[] Read(string name)
    {
        IFlArchiveEntry entry = Entry(name);
        using Stream source = entry.OpenForRead();
        byte[] bytes = new byte[checked((int)entry.Size)];
        source.ReadExactly(bytes);
        return bytes;
    }

    public void Dispose()
    {
        archive.Dispose();
    }
}
