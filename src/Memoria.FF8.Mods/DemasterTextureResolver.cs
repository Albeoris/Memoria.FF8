using System.Collections.Frozen;
using System.Globalization;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Mods;

public sealed record DemasterInspection(IReadOnlySet<string> UsablePackageIds, IReadOnlyList<ModDiagnostic> Diagnostics);

/// <summary>Semantic addressing only; the selected source must explicitly publish a compatible layout.</summary>
public sealed partial class DemasterTextureResolver : ITextureReplacementResolver
{
    private readonly ModCatalogSnapshot catalog;

    public DemasterTextureResolver(ModCatalogSnapshot catalog)
    {
        this.catalog = catalog;
    }

    /// <summary>Inspects target names against source-published layouts without opening image content.</summary>
    public static DemasterInspection Inspect(ResourceSourceDescriptor source, ModCatalogSnapshot catalog)
    {
        HashSet<string> usable = new(StringComparer.OrdinalIgnoreCase);
        List<ModDiagnostic> diagnostics = [];
        foreach (ModFile file in catalog.Files.Where(file => file.Format == ModFormat.Demaster))
        {
            string path = file.RelativePath;
            string extension = Path.GetExtension(path);
            if (!path.StartsWith("textures/", StringComparison.OrdinalIgnoreCase) || !extension.Equals(".png", StringComparison.OrdinalIgnoreCase) && !extension.Equals(".dds", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            string stem = path[..^extension.Length];
            bool supported = TryWorldTarget(stem, "textures/world/dat/texl/texl_", out int texlId) && texlId >= 1 && HasLayout(source, "classic-world-texl");
            supported |= HasLayout(source, "demaster-card-artwork") && IsCardArtworkTarget(stem);
            if (TryWorldTarget(stem, "textures/world/dat/wmset/wmset_", out int page))
            {
                supported |= page == 29 && HasLayout(source, "classic-world-road-region");
                supported |= source.Edition == GameDataEdition.Remastered && HasLayout(source, "demaster-world-wmset");
            }
            if (TryWorldTarget(stem, "textures/world/dat/wmset/wmset0_", out _))
            {
                supported |= source.Edition == GameDataEdition.Remastered && HasLayout(source, "demaster-world-wmset0");
            }
            bool battleTarget = stem.StartsWith(BattlePagePrefix, StringComparison.OrdinalIgnoreCase);
            supported |= battleTarget && HasLayout(source, BattlePageLayout) && IsBattlePageTarget(stem);
            bool fieldTarget = stem.StartsWith(FieldPagePrefix, StringComparison.OrdinalIgnoreCase);
            supported |= fieldTarget && HasLayout(source, FieldPageLayout) && IsFieldPageTarget(stem);
            bool modelTarget = IsModelSheetFamily(stem);
            supported |= modelTarget && HasLayout(source, ModelSheetLayout) && IsModelSheetTarget(source, stem);
            bool fieldModelTarget = IsFieldModelSheetFamily(stem);
            supported |= fieldModelTarget && HasLayout(source, FieldModelSheetLayout) && IsFieldModelSheetTarget(stem);
            supported |= !battleTarget && !fieldTarget && !modelTarget && !fieldModelTarget && source.Edition == GameDataEdition.Remastered && HasLayout(source, "demaster-direct");
            if (supported)
            {
                usable.Add(file.PackageId);
            }
            else
            {
                diagnostics.Add(new ModDiagnostic("demaster-unsupported-target", $"Texture target '{path}' has no compatible layout published by source '{source.Id}' ({source.Edition}).", file.PackageId, path));
            }
        }
        return new(usable.ToFrozenSet(StringComparer.OrdinalIgnoreCase), diagnostics.AsReadOnly());
    }

    public TextureReplacement? Resolve(TextureBuildContext context)
    {
        if (context.Layout == "classic-field-model")
        {
            return ResolveFieldModelSheet(context);
        }
        if (context.Layout == "classic-battle")
        {
            return ResolveModelSheet(context);
        }
        if (context.Layout == "classic-field-tile")
        {
            return ResolveFieldPage(context);
        }
        if (context.Layout == BattlePageLayout)
        {
            return ResolveBattlePage(context);
        }
        if (context.Layout == "native-artwork" && HasLayout(context.Source, "demaster-card-artwork"))
        {
            return ResolveCardArtwork(context);
        }
        foreach (string path in CandidatePaths(context))
        {
            ModFile? file = catalog.FindAll(ModFormat.Demaster, path).FirstOrDefault();
            if (file is null)
            {
                continue;
            }
            DecodedModImage decoded = ModImageDecoder.Decode(file);
            bool roadRegion = context.Layout == "classic-world-road-region";
            int canvasWidth = roadRegion ? context.Region.CanvasWidth : context.LogicalWidth;
            int canvasHeight = roadRegion ? context.Region.CanvasHeight : context.LogicalHeight;
            if (decoded.Image.Width % canvasWidth != 0 || decoded.Image.Height % canvasHeight != 0 || decoded.Image.Width / canvasWidth != decoded.Image.Height / canvasHeight)
            {
                throw new ModTextureLoadException(file.PackageId, file.RelativePath, new InvalidDataException($"Demaster texture '{file.RelativePath}' does not fit published layout '{context.Layout}' ({canvasWidth}x{canvasHeight})."));
            }
            if (roadRegion)
            {
                decoded = CropRoadRegion(decoded, context);
            }
            return new TextureReplacement(decoded.Image, $"{file.PackageId}/{file.RelativePath}@{file.Revision}", context.LogicalWidth, context.LogicalHeight, decoded.MipLevels);
        }
        return null;
    }

    public static IReadOnlyList<string> CandidatePaths(TextureBuildContext context)
    {
        if (context.Layout == "classic-field-model")
        {
            return TryFieldModelSheetAddress(context, out string fieldModelName, out int fieldModelRow, out string fieldModelVariant, out _) ? FieldModelSheetPaths(fieldModelName, fieldModelRow, fieldModelVariant) : Array.Empty<string>();
        }
        if (context.Layout == "classic-battle")
        {
            return TryModelSheetAddress(context, out string modelStem, out _) ? WithExtensions(modelStem) : Array.Empty<string>();
        }
        if (context.Layout == "classic-field-tile")
        {
            return TryFieldPageAddress(context, out string map, out int nativePage, out _) ? FieldBackgroundCandidates(map, nativePage, context.Key.Palette) : Array.Empty<string>();
        }
        if (context.Layout == BattlePageLayout)
        {
            return TryBattlePageStem(context, out string battleStem) ? WithExtensions($"{battleStem}_0").Concat(WithExtensions(battleStem)).ToArray() : Array.Empty<string>();
        }
        string? stem = null;
        bool paletteFallback = true;
        string owner = context.Key.Owner.Path;
        string subresource = context.Key.Subresource;
        if (owner == "world/texl.obj" && context.Layout == "classic-world-texl" && subresource.StartsWith("texl/", StringComparison.Ordinal) && TryNonnegative(subresource[5..], out int page) && page < int.MaxValue && context.LogicalWidth == 256 && context.LogicalHeight == 256)
        {
            // GetTextureIndex adds one to the native loaded texl index, independently of VRAM page.
            stem = $"textures/world/dat/texl/texl_{page + 1:D3}";
        }
        else if (IsWorldSet(owner) && context.Layout == "classic-world-road-region" && subresource.StartsWith("roads/", StringComparison.Ordinal) && TryNonnegative(subresource[6..], out _) && context.Key.Palette == 0 && IsRoadRegion(context))
        {
            // Match the unmodified section-38 TIM before the engine packs its road atlas.
            stem = "textures/world/dat/wmset/wmset_029";
        }
        else if (IsWorldSet(owner) && context.Layout == "demaster-world-wmset" && TryNonnegative(subresource, out int worldPage))
        {
            stem = $"textures/world/dat/wmset/wmset_{worldPage:D3}";
        }
        else if (IsWorldSet(owner) && context.Layout == "demaster-world-wmset0" && TryNonnegative(subresource, out int worldPage0))
        {
            stem = $"textures/world/dat/wmset/wmset0_{worldPage0:D3}";
        }
        else if (context.Layout == "demaster-direct" && owner.StartsWith("textures/", StringComparison.Ordinal) && Path.GetExtension(owner).Length == 0 && subresource.Length == 0)
        {
            // This full identity is supplied by the source, never inferred from a basename.
            stem = owner;
            paletteFallback = false;
        }
        if (stem is null || context.Key.Palette < 0)
        {
            return Array.Empty<string>();
        }
        return paletteFallback ? PaletteCandidates(stem, context.Key.Palette) : WithExtensions(stem);
    }

    public static IReadOnlyList<string> FieldBackgroundCandidates(string map, int nativePage, int palette)
    {
        ValidateName(map);
        if (map.Length < 2 || nativePage is < 16 or > 31 || palette is < 0 or > 15)
        {
            throw new ArgumentOutOfRangeException(nameof(nativePage));
        }
        string stem = $"textures/field_bg/{map[..2]}/{map}/{map}_{nativePage - 16}";
        return WithExtensions($"{stem}_{palette}").Concat(WithExtensions(stem)).ToArray();
    }

    public static IReadOnlyList<string> BattleSceneryCandidates(int stage, int nativePage, int? frame = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(stage);
        ArgumentOutOfRangeException.ThrowIfLessThan(nativePage, 16);
        if (frame < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(frame));
        }
        string stem = $"textures/battle.fs/hd_new/a0stg{stage:D3}_{nativePage}";
        if (frame is int animationFrame)
        {
            IEnumerable<string> candidates = WithExtensions($"{stem}_{animationFrame}");
            if (animationFrame != 0)
            {
                candidates = candidates.Concat(WithExtensions($"{stem}_0"));
            }
            return candidates.Concat(WithExtensions(stem)).ToArray();
        }
        return WithExtensions(stem).Concat(WithExtensions($"{stem}_0")).ToArray();
    }

    public static IReadOnlyList<string> FieldModelCandidates(int objectIndex, int row)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(row);
        if (objectIndex < 97)
        {
            throw new ArgumentOutOfRangeException(nameof(objectIndex));
        }
        string model = objectIndex switch
        {
            >= 0xC19 => $"p{objectIndex - 3097:D3}",
            >= 0x831 => $"o{objectIndex - 2097:D3}",
            >= 0x449 => $"c{objectIndex - 1097:D3}",
            _ => $"d{objectIndex - 97:D3}"
        };
        return WithExtensions($"textures/field.fs/field_hd/{model}_{row}").Concat(WithExtensions($"textures/field.fs/field_hd_new/{model}_{row}")).ToArray();
    }

    public static IReadOnlyList<string> BattleCharacterCandidates(int objectIndex, int row)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(objectIndex, 4097);
        ArgumentOutOfRangeException.ThrowIfNegative(row);
        int id = objectIndex - 4097;
        return WithExtensions($"textures/battle.fs/hd_new/d{id / 100:x}c{id % 100:D3}_{row}");
    }

    private static IReadOnlyList<string> PaletteCandidates(string stem, int palette)
    {
        return palette == 0 ? WithExtensions($"{stem}_0") : WithExtensions($"{stem}_{palette}").Concat(WithExtensions($"{stem}_0")).ToArray();
    }

    private static string[] WithExtensions(string stem) => [$"{stem}.dds", $"{stem}.png"];

    private static bool IsRoadRegion(TextureBuildContext context)
    {
        TextureSourceRegion region = context.Region;
        int x = region.X % 64 * 4;
        int y = region.Y % 256;
        return region.BitsPerPixel == 4 && region.CanvasWidth == 256 && region.CanvasHeight == 256 && region.X is >= 832 and < 896 && region.Y is >= 256 and < 512 && context.LogicalWidth > 0 && context.LogicalHeight > 0 && context.LogicalWidth <= 256 - x && context.LogicalHeight <= 256 - y;
    }

    private static DecodedModImage CropRoadRegion(DecodedModImage decoded, TextureBuildContext context)
    {
        int x = context.Region.X % 64 * 4;
        int y = context.Region.Y % 256;
        List<PixelImage> levels = [];
        foreach (PixelImage mip in decoded.MipLevels)
        {
            if ((long)x * mip.Width % 256 != 0 || (long)y * mip.Height % 256 != 0 || (long)context.LogicalWidth * mip.Width % 256 != 0 || (long)context.LogicalHeight * mip.Height % 256 != 0)
            {
                break;
            }
            int cropX = x * mip.Width / 256;
            int cropY = y * mip.Height / 256;
            int width = context.LogicalWidth * mip.Width / 256;
            int height = context.LogicalHeight * mip.Height / 256;
            if (width == 0 || height == 0)
            {
                break;
            }
            PixelImage image = new(width, height);
            for (int row = 0; row < height; row++)
            {
                mip.Pixels.AsSpan((cropY + row) * mip.Width + cropX, width).CopyTo(image.Pixels.AsSpan(row * width, width));
            }
            levels.Add(image);
        }
        return new(levels[0], levels.AsReadOnly());
    }

    private static bool IsWorldSet(string owner) => owner.StartsWith("world/wmset", StringComparison.Ordinal) && owner.EndsWith(".obj", StringComparison.Ordinal);

    private static bool TryNonnegative(string value, out int result) => int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out result) && result >= 0;

    private static bool HasLayout(ResourceSourceDescriptor source, string layout) => source.Layouts.Contains(layout, StringComparer.Ordinal);

    private static bool TryWorldTarget(string stem, string prefix, out int id)
    {
        id = 0;
        if (!stem.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        string[] parts = stem[prefix.Length..].Split('_');
        return parts.Length == 2 && TryNonnegative(parts[0], out id) && TryNonnegative(parts[1], out int palette) && stem.Equals($"{prefix}{id:D3}_{palette}", StringComparison.OrdinalIgnoreCase);
    }

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '_'))
        {
            throw new ArgumentException("A semantic field name must contain ASCII letters, digits, or underscores.", nameof(name));
        }
    }
}
