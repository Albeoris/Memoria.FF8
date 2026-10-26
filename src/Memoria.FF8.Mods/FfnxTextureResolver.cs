using System.Globalization;
using System.Collections.Frozen;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Mods;

/// <summary>Adapts native resource identities to the FFNx external texture namespace.</summary>
public sealed partial class FfnxTextureResolver(ModCatalogSnapshot catalog) : ITextureReplacementResolver
{
    public TextureReplacement? Resolve(TextureBuildContext context)
    {
        if (!context.Source.Layouts.Contains("classic-ffnx", StringComparer.Ordinal))
        {
            return null;
        }
        if (context.Layout == "native-artwork")
        {
            return ResolveArtwork(context);
        }
        if (context.Layout == "classic-battle")
        {
            return ResolveBattle(context);
        }
        if (context.Layout == "classic-field-model" && ResolveFieldPage(context) is { } fieldPage)
        {
            return fieldPage;
        }
        string? name = NativeName(context);
        int palette = context.Layout == "classic-world-texl" ? 0 : context.Key.Palette;
        if (name is not null && context.Layout == "classic-world-section" && ReadVramPages(context, name) is { } pages)
        {
            return pages;
        }
        if (name is not null && ReadRegion(context, name, palette, 0, 0, context.LogicalWidth, context.LogicalHeight, context.LogicalWidth, context.LogicalHeight) is { } direct)
        {
            return direct;
        }
        if (context.NativeAlias is { } alias && alias.Owner.Path.StartsWith("world/wmset", StringComparison.Ordinal))
        {
            string[] address = alias.Subresource.Split('/');
            if (address.Length == 4 && address[0] == "section" && address[2] == "texture" && Number(address[1], out int section) && Number(address[3], out int member))
            {
                return ReadRegion(context, $"world/dat/wmset/section{section}/texture{member}", alias.Palette, alias.X, alias.Y, alias.Width, alias.Height, alias.CanvasWidth, alias.CanvasHeight);
            }
        }
        return null;
    }

    public static DemasterInspection Inspect(ResourceSourceDescriptor source, ModCatalogSnapshot catalog, IResourceSource? nativeSource = null, IGameNativeMemorySource? nativeMemorySource = null)
    {
        HashSet<string> usable = new(StringComparer.OrdinalIgnoreCase);
        List<ModDiagnostic> diagnostics = [];
        Dictionary<int, int?> nativeCounts = [];
        Dictionary<ResourceKey, int?> battleCounts = [];
        foreach (ModFile file in catalog.Files.Where(file => file.Format == ModFormat.Junction && file.RelativePath.StartsWith("mods/textures/", StringComparison.OrdinalIgnoreCase)))
        {
            string extension = Path.GetExtension(file.RelativePath);
            if (!extension.Equals(".dds", StringComparison.OrdinalIgnoreCase) && !extension.Equals(".png", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            string target = file.RelativePath[14..^extension.Length];
            if (IsNeverGeneratedBattlePaletteTarget(target) || nativeSource is not null && (IsImpossibleFieldModelTarget(target) || IsAbsentNativeBattleMember(nativeSource, target, battleCounts) || IsAbsentNativeWorldMember(nativeSource, target, nativeCounts)))
            {
                diagnostics.Add(new("ffnx-unused-native-target", $"Texture '{file.RelativePath}' has no corresponding member in the selected original source and remains unused.", file.PackageId, file.RelativePath));
            }
            else if (nativeSource is not null && IsDormantNativeWorldMember(nativeSource, target, nativeMemorySource))
            {
                diagnostics.Add(new("ffnx-unused-native-target", $"Texture '{file.RelativePath}' addresses a dormant original texture with no producer in the selected native world profile.", file.PackageId, file.RelativePath));
            }
            else if (source.Layouts.Contains("classic-ffnx", StringComparer.Ordinal) && IsSupportedTarget(target))
            {
                usable.Add(file.PackageId);
            }
            else
            {
                diagnostics.Add(new("ffnx-unsupported-target", $"Texture '{file.RelativePath}' has no native texture consumer in source '{source.Id}'.", file.PackageId, file.RelativePath));
            }
        }
        return new(usable.ToFrozenSet(StringComparer.OrdinalIgnoreCase), diagnostics.AsReadOnly());
    }

    private static bool IsSupportedTarget(string name)
    {
        string stem = name.ToLowerInvariant();
        if (stem.StartsWith("eng/", StringComparison.Ordinal))
        {
            stem = stem[4..];
        }
        return IsSupportedFieldPageTarget(stem) || IsSupportedBattleTarget(stem) || IsSupportedMenuArtworkTarget(stem) || IsSupportedVramTarget(stem) || stem.StartsWith("cardgame/", StringComparison.Ordinal) && new[] { "cards_00", "icons_00", "intro_00", "game_00" }.Contains(stem[9..])
            || System.Text.RegularExpressions.Regex.IsMatch(stem, @"^data/eng/menu/mc0[0-9]_00$")
            || System.Text.RegularExpressions.Regex.IsMatch(stem, @"^field/model/(main|second)_chr/[a-z0-9]+-[0-9]+_00$")
            || System.Text.RegularExpressions.Regex.IsMatch(stem, @"^world/esk/chara_one/model[0-9]+-[0-9]+_00$")
            || System.Text.RegularExpressions.Regex.IsMatch(stem, @"^world/dat/texl/texture[0-9]+_00$")
            || System.Text.RegularExpressions.Regex.IsMatch(stem, @"^world/dat/wmset/section42/texture[0-9]+_[0-9]{2}$")
            || System.Text.RegularExpressions.Regex.IsMatch(stem, @"^world/dat/wmset/section39/texture[0-9]+_[0-9]{2}$")
            || System.Text.RegularExpressions.Regex.IsMatch(stem, @"^world/dat/wmset/section40/texture0_00$")
            || System.Text.RegularExpressions.Regex.IsMatch(stem, @"^world/dat/wmset/section38/texture([0-7]|1[0-9]|2[0-8]|30|31|32|35)_[0-9]{2}$");
    }

    private static string? NativeName(TextureBuildContext context)
    {
        string owner = context.Key.Owner.Path;
        string[] parts = context.Key.Subresource.Split('/');
        if (owner == "world/texl.obj" && context.Layout == "classic-world-texl" && parts.Length == 2 && parts[0] == "texl" && Number(parts[1], out int texl))
        {
            return $"world/dat/texl/texture{texl}";
        }
        if (owner == "world/chara.one" && parts.Length is 3 or 4 && parts[0] == "model" && Number(parts[1], out int model) && Number(parts[^1], out int texture))
        {
            return $"world/esk/chara_one/model{model}-{texture}";
        }
        if (context.Layout == "classic-field-model" && (parts.Length == 6 || parts.Length == 7 && parts[4] == "registered") && parts[0] == "model" && parts[2] is "main" or "second" && parts[^2] == "texture" && Number(parts[^1], out int fieldTexture))
        {
            return $"field/model/{parts[2]}_chr/{parts[3]}-{fieldTexture}";
        }
        if (context.Layout == "classic-field-model" && owner.StartsWith("field/main_chr/", StringComparison.Ordinal) && owner.EndsWith(".mch", StringComparison.Ordinal) && parts.Length == 2 && parts[0] == "texture" && Number(parts[1], out int libraryTexture))
        {
            return $"field/model/main_chr/{Path.GetFileNameWithoutExtension(owner)}-{libraryTexture}";
        }
        if (owner.StartsWith("world/wmset", StringComparison.Ordinal) && context.Layout == "classic-world-section" && parts.Length == 4 && parts[0] == "section" && Number(parts[1], out int section) && parts[2] == "texture" && Number(parts[3], out int member))
        {
            return $"world/dat/wmset/section{section}/texture{member}";
        }
        if (owner.StartsWith("world/wmset", StringComparison.Ordinal) && context.Layout == "classic-model" && parts.Length == 4 && parts[0] == "static" && Number(parts[1], out int shape) && shape >= 64 && parts[2] == "palette")
        {
            return $"world/dat/wmset/section42/texture{shape - 64}";
        }
        if (owner.StartsWith("world/wmset", StringComparison.Ordinal) && parts.Length == 2 && parts[0] is "roads" or "water" && Number(parts[1], out int worldTexture))
        {
            return $"world/dat/wmset/section{(parts[0] == "roads" ? 39 : 38)}/texture{worldTexture}";
        }
        return null;
    }

    private TextureReplacement? ReadRegion(TextureBuildContext context, string name, int palette, int x, int y, int width, int height, int canvasWidth, int canvasHeight, int horizontalPageWidth = 0, int verticalPageHeight = 0, bool normalizedMaterial = false)
    {
        string[] languageRoots = context.Source.Language is null or "en" ? ["mods/textures/eng/", "mods/textures/"] : ["mods/textures/"];
        ModFile? file = new[] { ".dds", ".png" }.SelectMany(extension => languageRoots.SelectMany(root => catalog.FindAll(ModFormat.Junction, $"{root}{name}_{palette:00}{extension}"))).FirstOrDefault();
        file ??= catalog.Files.Where(candidate => candidate.Format == ModFormat.Junction).Select(candidate => (File: candidate, Palette: ExternalPalette(candidate.RelativePath, languageRoots, name))).Where(candidate => candidate.Palette >= 0).OrderBy(candidate => candidate.Palette).ThenBy(candidate => Path.GetExtension(candidate.File.RelativePath).Equals(".dds", StringComparison.OrdinalIgnoreCase) ? 0 : 1).Select(candidate => candidate.File).FirstOrDefault();
        if (file is null)
        {
            return null;
        }
        DecodedModImage decoded = ModImageDecoder.Decode(file);
        PixelImage sheet = decoded.Image;
        if ((horizontalPageWidth > 0 || verticalPageHeight > 0) && canvasWidth > 0 && canvasHeight > 0 && (long)sheet.Width * canvasHeight != (long)sheet.Height * canvasWidth)
        {
            int paddedWidth = horizontalPageWidth > 0 ? checked((canvasWidth + horizontalPageWidth - 1) / horizontalPageWidth * horizontalPageWidth) : canvasWidth;
            int paddedHeight = verticalPageHeight > 0 ? checked((canvasHeight + verticalPageHeight - 1) / verticalPageHeight * verticalPageHeight) : canvasHeight;
            foreach ((int candidateWidth, int candidateHeight) in new[] { (paddedWidth, canvasHeight), (canvasWidth, paddedHeight), (paddedWidth, paddedHeight) })
            {
                if ((long)sheet.Width * candidateHeight == (long)sheet.Height * candidateWidth && (long)x * sheet.Width % candidateWidth == 0 && (long)y * sheet.Height % candidateHeight == 0 && (long)width * sheet.Width % candidateWidth == 0 && (long)height * sheet.Height % candidateHeight == 0)
                {
                    canvasWidth = candidateWidth;
                    canvasHeight = candidateHeight;
                    break;
                }
            }
        }
        if (normalizedMaterial && x == 0 && y == 0 && width == canvasWidth && height == canvasHeight && width > 0 && height > 0)
        {
            return new(sheet, $"{file.PackageId}/{file.RelativePath}@{file.Revision}", context.LogicalWidth, context.LogicalHeight, decoded.MipLevels);
        }
        if (canvasWidth <= 0 || canvasHeight <= 0 || x < 0 || y < 0 || width <= 0 || height <= 0 || (long)x + width > canvasWidth || (long)y + height > canvasHeight || (long)sheet.Width * canvasHeight != (long)sheet.Height * canvasWidth || (long)x * sheet.Width % canvasWidth != 0 || (long)y * sheet.Height % canvasHeight != 0 || (long)width * sheet.Width % canvasWidth != 0 || (long)height * sheet.Height % canvasHeight != 0)
        {
            throw new ModTextureLoadException(file.PackageId, file.RelativePath, new InvalidDataException($"FFNx texture dimensions do not preserve its {canvasWidth} by {canvasHeight} native canvas and crop boundaries."));
        }
        if (x == 0 && y == 0 && width == canvasWidth && height == canvasHeight)
        {
            return new(sheet, $"{file.PackageId}/{file.RelativePath}@{file.Revision}", context.LogicalWidth, context.LogicalHeight, decoded.MipLevels);
        }
        int left = x * sheet.Width / canvasWidth;
        int top = y * sheet.Height / canvasHeight;
        PixelImage result = new(width * sheet.Width / canvasWidth, height * sheet.Height / canvasHeight);
        for (int row = 0; row < result.Height; row++)
        {
            sheet.Pixels.AsSpan((top + row) * sheet.Width + left, result.Width).CopyTo(result.Pixels.AsSpan(row * result.Width));
        }
        return new(result, $"{file.PackageId}/{file.RelativePath}@{file.Revision}", context.LogicalWidth, context.LogicalHeight);
    }

    private static bool Number(string text, out int number) => int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out number);

    private static bool PaletteNumber(string text, out int number) => Number(text, out number) && number <= byte.MaxValue && text == number.ToString("00", CultureInfo.InvariantCulture);

    private static int ExternalPalette(string path, string[] roots, string name)
    {
        string extension = Path.GetExtension(path);
        if (!extension.Equals(".dds", StringComparison.OrdinalIgnoreCase) && !extension.Equals(".png", StringComparison.OrdinalIgnoreCase))
        {
            return -1;
        }
        foreach (string root in roots)
        {
            string prefix = root + name + "_";
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && PaletteNumber(path[prefix.Length..^extension.Length], out int palette))
            {
                return palette;
            }
        }
        return -1;
    }
}
