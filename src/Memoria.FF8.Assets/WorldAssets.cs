using System.Numerics;
using System.Security.Cryptography;
using Albeoris.Games.FF8.FlArchives;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Albeoris.Games.FF8.Imaging;
using Albeoris.Games.FF8.Imaging.Tim;
using Memoria.FF8.Resources;

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
    private readonly Dictionary<(int, int), AssetHandle<TextureValue>> textureHandles = [];
    private readonly ResourceService? resources;
    private readonly AssetTextureScope textureScope;
    private AssetHandle<TextureValue>? roadHandle;
    private AssetHandle<TextureValue>? waterHandle;
    private bool roadsPrepared;
    private bool waterPrepared;
    private byte[]? texl;
    private PixelImage? roads;
    private PixelImage? water;
    private readonly Dictionary<byte, FieldModel> footModels = [];
    private WorldFieldEntryReader? fieldEntries;

    public WorldAssets(string gameDirectory, ResourceService? resources = null)
    {
        this.resources = resources;
        textureScope = new AssetTextureScope(resources);
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
    public string AllocationProgramSha256 => Convert.ToHexString(SHA256.HashData(wmset));

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
            model = model with { Textures = textureScope.RegisterList(new ResourceKey("world/chara.one"), model.Textures, "classic-model", $"model/{shape}/texture") };
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
            byte permission = entityType is 0 or 1 or 32 or 33 ? Memoria.FF8.Runtime.NativeWorldTerrain.PermissionMask(entityType) : throw new NotSupportedException("Original advisory world terrain selection for this entity is unrepresented.");
            if ((triangle.VertexFlags & permission) == 0 || !WorldAssetReader.TryNativeHeight(triangle, x, z, out int height) || previousHeight.HasValue && !triangle.Forest && previousGroundType is not <= 5 && Math.Abs(height - previousHeight.Value) >= 200)
            {
                continue;
            }
            WorldGround next = new(height, triangle, mapId);
            if (entityType is not (32 or 33))
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
            if (!roadsPrepared)
            {
                roads = CreateRoadAtlas();
                roadsPrepared = true;
            }
            return roadHandle?.Value.Image ?? roads!;
        }
        if (triangle.Water)
        {
            if (!waterPrepared)
            {
                water = CreateWaterAtlas();
                waterPrepared = true;
            }
            return waterHandle?.Value.Image ?? water!;
        }
        if (!textures.TryGetValue((triangle.TexturePage, triangle.Palette), out PixelImage? image))
        {
            texl ??= Read("texl.obj");
            TimImage tim = WorldAssetReader.ReadTim(texl, triangle.TexturePage * 0x12800).Image;
            image = tim.Decode(triangle.Palette);
            textures.Add((triangle.TexturePage, triangle.Palette), image);
            AssetHandle<TextureValue>? handle = textureScope.Register(new TextureKey(new ResourceKey("world/texl.obj"), $"texl/{triangle.TexturePage}", triangle.Palette), image, "classic-world-texl", new TextureSourceRegion(tim.X, tim.Y, tim.PixelMode == TimPixelMode.Indexed4 ? 4 : 8), palette => tim.Decode(palette), tim.Palettes.Count);
            if (handle is not null)
            {
                textureHandles.Add((triangle.TexturePage, triangle.Palette), handle);
            }
        }
        return textureHandles.TryGetValue((triangle.TexturePage, triangle.Palette), out AssetHandle<TextureValue>? current) ? current.Value.Image : image;
    }

    private PixelImage? CreateRoadAtlas()
    {
        return CreateSourceAtlas(38, null, "roads", "classic-world-road-region", 256, 256, out roadHandle);
    }

    private PixelImage? CreateWaterAtlas()
    {
        return CreateSourceAtlas(37, [23, 16, 21, 22, 17, 18, 19, 20], "water", "classic-world-water-region", 256, 128, out waterHandle);
    }

    private PixelImage? CreateSourceAtlas(int section, int[]? selected, string name, string layout, int width, int height, out AssetHandle<TextureValue>? handle)
    {
        int[] pointers = WorldAssetReader.InnerPointers(wmset, sections[section]);
        int[] members = selected ?? Enumerable.Range(0, pointers.Length).ToArray();
        List<TextureKey> dependencies = [];
        List<(int X, int Y)> positions = [];
        List<TextureValue> original = [];
        ResourceKey owner = new("world/wmsetus.obj");
        for (int index = 0; index < members.Length; index++)
        {
            int member = members[index];
            TimImage tim = WorldAssetReader.ReadTim(wmset, sections[section] + pointers[member]).Image;
            PixelImage decoded = tim.Decode();
            TextureKey key = new(owner, $"{name}/{member}");
            TextureSourceRegion region = new(tim.X, tim.Y, tim.PixelMode == TimPixelMode.Indexed4 ? 4 : 8);
            textureScope.Register(key, decoded, layout, region, palette => tim.Decode(palette), tim.Palettes.Count);
            dependencies.Add(key);
            positions.Add(selected is null ? ((tim.X - 832) * 4, tim.Y - 256) : (index % 4 * 64, index / 4 * 64));
            if (resources is null)
            {
                original.Add(new TextureValue(decoded, decoded.Width, decoded.Height, "classic"));
            }
        }
        handle = textureScope.RegisterAtlas(new TextureKey(owner, name + "-atlas"), dependencies, positions, width, height);
        return handle is null ? TextureAtlasBuilder.Build(original, positions, width, height).Image : null;
    }

    private IFlArchiveEntry Entry(string name)
    {
        return archive.Entries.Single(entry => entry.RelativePath.Replace('\\', '/').Split('/')[^1].Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    private byte[] Read(string name)
    {
        if (resources is not null)
        {
            return resources.Read(new ResourceKey("world/" + name));
        }
        IFlArchiveEntry entry = Entry(name);
        using Stream source = entry.OpenForRead();
        byte[] bytes = new byte[checked((int)entry.Size)];
        source.ReadExactly(bytes);
        return bytes;
    }

    public void Dispose()
    {
        textureScope.Dispose();
        archive.Dispose();
    }
}
