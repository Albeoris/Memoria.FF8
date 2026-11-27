using System.Globalization;
using System.Text;
using Albeoris.Games.FF8.Imaging;
using Albeoris.Games.FF8.Imaging.Map;
using Memoria.FF8.Assets;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Mods;

/// <summary>Resolves painted field layers against the active MAP and returns native-size tile replacements.</summary>
public static class DemasterFieldLayers
{
    private const string Prefix = "textures/field_bg/";
    private const int TileSize = 16;
    private static readonly string[] CompanionSuffixes = ["_nt_A", "_A", "_LF", "_ND", "_ORME", "_EC"];

    public static bool IsInput(string relativePath)
    {
        if (!relativePath.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        string[] parts = relativePath[Prefix.Length..].Split('/');
        if (parts.Length != 3 || parts[0].Length != 2 || parts[1].Length < 2 || !parts[1].StartsWith(parts[0], StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        string field = parts[1];
        string file = parts[2];
        string extension = Path.GetExtension(file).ToLowerInvariant();
        if (extension is not (".dds" or ".png") || !file.StartsWith(field + "_", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        ReadOnlySpan<char> token = file.AsSpan(field.Length + 1, file.Length - field.Length - extension.Length - 1);
        if (token.Length == 11 && token[2] == '_' && IsLanguage(token[..2]))
        {
            token = token[3..];
        }
        return token.Length == 8 && uint.TryParse(token, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out _);
    }

    public static IReadOnlyDictionary<int, TextureReplacement> Resolve(ModCatalogSnapshot catalog, FieldAssets field, string? language, Action<ModDiagnostic>? diagnose = null)
    {
        return Resolve(catalog, field.Name, field.Background, language, diagnose);
    }

    internal static IReadOnlyDictionary<int, TextureReplacement> Resolve(ModCatalogSnapshot catalog, string field, IReadOnlyList<BackgroundTile> tiles, string? language, Action<ModDiagnostic>? diagnose = null)
    {
        Dictionary<int, TextureReplacement> replacements = [];
        if (field.Length < 2 || tiles.Count == 0)
        {
            return replacements;
        }
        string folder = $"{Prefix}{field[..2]}/{field}/";
        var (files, companions) = SelectFiles(catalog, folder);
        if (files.Count == 0 && companions.Count == 0)
        {
            return replacements;
        }
        (uint[] pupu, Frame[] frames) = PupuLayout(tiles);
        (uint[] fme, Frame canvas) = FmeLayout(tiles);
        Dictionary<uint, List<Frame>> candidates = [];
        for (int i = 0; i < tiles.Count; i++)
        {
            AddCandidate(candidates, pupu[i], frames[i]);
        }
        foreach (uint id in fme)
        {
            AddCandidate(candidates, id, canvas);
        }
        Dictionary<uint, Painting> paintings = [];
        ILookup<string, ModFile> fieldFiles = catalog.Files.Where(file => file.Format == ModFormat.Demaster && file.RelativePath.StartsWith(folder, StringComparison.OrdinalIgnoreCase)).ToLookup(file => file.RelativePath, StringComparer.OrdinalIgnoreCase);
        foreach ((uint id, List<Frame> choices) in candidates)
        {
            string suffix = $"{id:X8}";
            ModFile? file = language is null ? null : files.GetValueOrDefault($"{field}_{language}_{suffix}");
            file ??= files.GetValueOrDefault($"{field}_{suffix}");
            if (file is null)
            {
                ModFile? companion = language is null ? null : companions.GetValueOrDefault($"{field}_{language}_{suffix}");
                companion ??= companions.GetValueOrDefault($"{field}_{suffix}");
                if (companion is not null)
                {
                    diagnose?.Invoke(new("Demaster.FieldLayerPaintingMissing", $"Prepared layer {suffix} requires its base painting to bind to the selected MAP.", companion.PackageId, companion.RelativePath));
                }
                continue;
            }
            try
            {
                DecodedModImage decoded = ModImageDecoder.Decode(file);
                foreach (Frame frame in choices)
                {
                    if (frame.ScaleOf(decoded.Image) > 0)
                    {
                        paintings.Add(id, new(file, frame, decoded, PrepareBakedLayer(fieldFiles, field, file, id, decoded.Image, diagnose)));
                        break;
                    }
                }
                if (!paintings.ContainsKey(id))
                {
                    diagnose?.Invoke(new("Demaster.FieldLayerDimensions", $"The {decoded.Image.Width}x{decoded.Image.Height} image does not match the selected MAP layer or field canvas.", file.PackageId, file.RelativePath));
                }
            }
            catch (ModTextureLoadException error)
            {
                diagnose?.Invoke(new("Demaster.FieldLayerImage", error.Message, file.PackageId, file.RelativePath));
            }
        }
        for (int i = 0; i < tiles.Count; i++)
        {
            BackgroundTile tile = tiles[i];
            if (!tile.Draw)
            {
                continue;
            }
            if (!paintings.TryGetValue(pupu[i], out Painting? painting) || painting.Frame != frames[i])
            {
                if (!paintings.TryGetValue(fme[i], out painting) || painting.Frame != canvas)
                {
                    continue;
                }
            }
            if (Crop(tile, painting) is { } replacement)
            {
                replacements.Add(i, replacement);
            }
        }
        return replacements;
    }

    private static (Dictionary<string, ModFile> Paintings, Dictionary<string, ModFile> Companions) SelectFiles(ModCatalogSnapshot catalog, string folder)
    {
        Dictionary<string, ModFile> selected = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, ModFile> companions = new(StringComparer.OrdinalIgnoreCase);
        foreach (ModFile file in catalog.Files.Where(file => file.Format == ModFormat.Demaster && file.RelativePath.StartsWith(folder, StringComparison.OrdinalIgnoreCase)))
        {
            if (IsInput(file.RelativePath))
            {
                string name = Path.GetFileNameWithoutExtension(file.RelativePath);
                if (!selected.TryGetValue(name, out ModFile? current) || file.PackageId == current.PackageId && PreferImage(file, current))
                {
                    selected[name] = file;
                }
            }
            else if (CompanionPaintingPath(file.RelativePath) is { } painting)
            {
                companions.TryAdd(Path.GetFileNameWithoutExtension(painting), file);
            }
        }
        return (selected, companions);
    }

    private static string? CompanionPaintingPath(string path)
    {
        string extension = Path.GetExtension(path);
        string stem = path[..^extension.Length];
        foreach (string suffix in CompanionSuffixes)
        {
            if (stem.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                string painting = stem[..^suffix.Length] + extension;
                if (IsInput(painting))
                {
                    return painting;
                }
            }
        }
        return null;
    }

    private static bool PreferImage(ModFile candidate, ModFile current)
    {
        long candidateTime = candidate.ContentStamp?.Modified ?? 0;
        long currentTime = current.ContentStamp?.Modified ?? 0;
        return candidateTime > currentTime || candidateTime == currentTime && candidate.RelativePath.EndsWith(".dds", StringComparison.OrdinalIgnoreCase);
    }

    private static BakedLayer? PrepareBakedLayer(ILookup<string, ModFile> files, string field, ModFile painting, uint id, PixelImage baseImage, Action<ModDiagnostic>? diagnose)
    {
        string stem = painting.RelativePath[..^Path.GetExtension(painting.RelativePath).Length];
        ModFile? albedo = FindImage("_A");
        ModFile? lightmap = FindImage("_LF");
        if (albedo is null || lightmap is null)
        {
            return null;
        }
        string metadataPath = stem[..(stem.LastIndexOf('/') + 1)] + field + "_pbr.txt";
        ModFile? metadata = files[metadataPath].FirstOrDefault(candidate => candidate.PackageId == painting.PackageId);
        try
        {
            float scale = metadata is null ? 1 : DemasterMaterialChannels.ReadLayerLightmapScale(Encoding.UTF8.GetString(metadata.ReadBytes()), id);
            PreparedFieldMaterial material = DemasterMaterialChannels.LoadField(albedo, lightmap, scale);
            if (material.Albedo.Width != baseImage.Width || material.Albedo.Height != baseImage.Height)
            {
                throw new InvalidDataException("Prepared material dimensions differ from the selected painting.");
            }
            string origin = $"{albedo.RelativePath}@{albedo.Revision}/{lightmap.RelativePath}@{lightmap.Revision}/{metadata?.Revision ?? 0}";
            return new(material.BakePaintedLight(), origin);
        }
        catch (Exception error) when (error is InvalidDataException or IOException or ModTextureLoadException)
        {
            diagnose?.Invoke(new("Demaster.FieldMaterialImage", error.Message, painting.PackageId, albedo.RelativePath));
            return null;
        }

        ModFile? FindImage(string suffix)
        {
            ModFile? best = null;
            foreach (ModFile candidate in files[stem + suffix + ".png"].Concat(files[stem + suffix + ".dds"]))
            {
                if (candidate.PackageId == painting.PackageId && (best is null || PreferImage(candidate, best)))
                {
                    best = candidate;
                }
            }
            return best;
        }
    }

    private static bool IsLanguage(ReadOnlySpan<char> token)
    {
        return token.ToString().ToLowerInvariant() is "en" or "fr" or "de" or "it" or "es" or "jp";
    }

    private static void AddCandidate(Dictionary<uint, List<Frame>> candidates, uint id, Frame frame)
    {
        if (!candidates.TryGetValue(id, out List<Frame>? frames))
        {
            frames = [];
            candidates.Add(id, frames);
        }
        if (!frames.Contains(frame))
        {
            frames.Add(frame);
        }
    }

    private static (uint[] Names, Frame[] Frames) PupuLayout(IReadOnlyList<BackgroundTile> tiles)
    {
        uint[] names = new uint[tiles.Count];
        var positions = new Dictionary<(int X, int Y), List<int>>();
        for (int i = 0; i < tiles.Count; i++)
        {
            BackgroundTile tile = tiles[i];
            names[i] = Name(tile, 0xF);
            var position = (tile.X, tile.Y);
            if (!positions.TryGetValue(position, out List<int>? duplicates))
            {
                duplicates = [];
                positions.Add(position, duplicates);
            }
            duplicates.Add(i);
        }
        foreach (List<int> coincident in positions.Values)
        {
            for (int i = 0; i < coincident.Count; i++)
            {
                for (int j = i + 1; j < coincident.Count; j++)
                {
                    uint earlier = names[coincident[i]];
                    if (((earlier ^ names[coincident[j]]) & 0xFFFFFFF0u) == 0)
                    {
                        names[coincident[j]] = earlier + 1;
                    }
                }
            }
        }
        Dictionary<uint, Frame> layerFrames = [];
        for (int i = 0; i < tiles.Count; i++)
        {
            uint layer = names[i] >> 24;
            Frame frame = layerFrames.GetValueOrDefault(layer, new(0, 0, TileSize, TileSize));
            layerFrames[layer] = frame.Include(tiles[i]);
        }
        return (names, names.Select(id => layerFrames[id >> 24]).ToArray());
    }

    private static (uint[] Names, Frame Canvas) FmeLayout(IReadOnlyList<BackgroundTile> tiles)
    {
        int minX = tiles.Min(tile => tile.X);
        int minY = tiles.Min(tile => tile.Y);
        int shiftX = minX < 0 || minY < 0 ? Math.Abs(minX) : 0;
        int shiftY = minX < 0 || minY < 0 ? Math.Abs(minY) : 0;
        uint[] names = new uint[tiles.Count];
        int left = int.MaxValue;
        int top = int.MaxValue;
        int right = int.MinValue;
        int bottom = int.MinValue;
        Dictionary<(uint Name, int X, int Y), byte> occurrences = [];
        for (int i = 0; i < tiles.Count; i++)
        {
            BackgroundTile tile = tiles[i];
            int x = unchecked((short)(tile.X + shiftX));
            int y = unchecked((short)(tile.Y + shiftY));
            left = Math.Min(left, x);
            top = Math.Min(top, y);
            right = Math.Max(right, x);
            bottom = Math.Max(bottom, y);
            uint name = Name(tile, 0x7);
            if (x % TileSize != 0)
            {
                name |= 0x80000000u;
            }
            if (y % TileSize != 0)
            {
                name |= 0x00800000u;
            }
            var cell = (name, x / TileSize, y / TileSize);
            byte occurrence = occurrences.TryGetValue(cell, out byte previous) ? unchecked((byte)(previous + 1)) : (byte)0;
            occurrences[cell] = occurrence;
            names[i] = name + occurrence;
        }
        return (names, new(-shiftX, -shiftY, Math.Abs(left) + Math.Abs(right) + TileSize, Math.Abs(top) + Math.Abs(bottom) + TileSize));
    }

    private static uint Name(BackgroundTile tile, uint blendMask)
    {
        return ((uint)tile.Layer & 0x7Fu) << 24 | ((uint)tile.BlendMode & blendMask) << 20 | (uint)(byte)tile.AnimationId << 12 | (uint)(byte)tile.AnimationState << 4;
    }

    private static TextureReplacement? Crop(BackgroundTile tile, Painting painting)
    {
        int x = tile.X - painting.Frame.Left;
        int y = tile.Y - painting.Frame.Top;
        if (x < 0 || y < 0 || x + TileSize > painting.Frame.Width || y + TileSize > painting.Frame.Height)
        {
            return null;
        }
        List<PixelImage> levels = [];
        BakedLayer? material = tile.BlendMode == MapBlendMode.None ? painting.Material : null;
        IReadOnlyList<PixelImage> mipLevels = material is null ? painting.Image.MipLevels : [material.Paint];
        foreach (PixelImage mip in mipLevels)
        {
            int scale = painting.Frame.ScaleOf(mip);
            if (scale == 0)
            {
                break;
            }
            int size = TileSize * scale;
            PixelImage crop = new(size, size);
            for (int row = 0; row < size; row++)
            {
                mip.Pixels.AsSpan((y * scale + row) * mip.Width + x * scale, size).CopyTo(crop.Pixels.AsSpan(row * size, size));
            }
            if (tile.BlendMode < MapBlendMode.None && tile.Image.Width == TileSize && tile.Image.Height == TileSize)
            {
                ApplyNativeCoverage(crop, tile.Image, scale);
            }
            levels.Add(crop);
        }
        string origin = $"{painting.File.PackageId}/{painting.File.RelativePath}@{painting.File.Revision}";
        if (material is not null)
        {
            origin += "/" + material.Origin;
        }
        return new(levels[0], origin, TileSize, TileSize, levels.AsReadOnly());
    }

    private static void ApplyNativeCoverage(PixelImage crop, PixelImage native, int scale)
    {
        for (int y = 0; y < crop.Height; y++)
        {
            for (int x = 0; x < crop.Width; x++)
            {
                byte alpha = native[x / scale, y / scale].A;
                Rgba32 color = crop[x, y];
                if (alpha is > 0 and < 255 && color.A > 0)
                {
                    crop[x, y] = color with { A = (byte)((color.A * alpha + 127) / 255) };
                }
            }
        }
    }

    private sealed record Painting(ModFile File, Frame Frame, DecodedModImage Image, BakedLayer? Material);

    private sealed record BakedLayer(PixelImage Paint, string Origin);

    private readonly record struct Frame(int Left, int Top, int Width, int Height)
    {
        public Frame Include(BackgroundTile tile)
        {
            int left = Math.Min(Left, tile.X);
            int top = Math.Min(Top, tile.Y);
            return new(left, top, Math.Max(Left + Width, tile.X + TileSize) - left, Math.Max(Top + Height, tile.Y + TileSize) - top);
        }

        public int ScaleOf(PixelImage image)
        {
            int scale = image.Width / Width;
            return scale is >= 1 and <= 16 && image.Width == Width * scale && image.Height == Height * scale ? scale : 0;
        }
    }
}
