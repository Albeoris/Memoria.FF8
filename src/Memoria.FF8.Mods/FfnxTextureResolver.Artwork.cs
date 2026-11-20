using System.Collections.Frozen;
using Memoria.FF8.Resources;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets;

namespace Memoria.FF8.Mods;

public sealed partial class FfnxTextureResolver
{
    private sealed record CardArtworkRegion(string Sheet, int X, int Y, int Width, int Height, int CanvasWidth, int CanvasHeight, int PaletteX = 0, int? PaletteY = null);

    private static readonly FrozenDictionary<string, CardArtworkRegion> CardArtworkRegions = CreateCardArtworkRegions();

    private static FrozenDictionary<string, CardArtworkRegion> CreateCardArtworkRegions()
    {
        Dictionary<string, CardArtworkRegion> regions = new(StringComparer.Ordinal)
        {
            ["back"] = new("cardgame/cards", 1664, 192, 64, 64, 1792, 256),
            ["background"] = new("cardgame/intro", 0, 0, 384, 224, 384, 224),
            ["board-background"] = new("cardgame/game", 0, 0, 384, 224, 384, 224)
        };
        for (int card = 0; card < CardArtwork.CardCount; card++)
        {
            regions.Add($"front/{card}", new("cardgame/cards", card / 8 * 128 + card % 2 * 64, card % 8 / 2 * 64, 64, 64, 1792, 256));
        }
        for (int element = 0; element < 8; element++)
        {
            regions.Add($"element/{element}", new("cardgame/icons", element % 4 * 64, (element / 4 + 1) * 16, 16, 16, 768, 192, PaletteY: 225 + element));
        }
        for (int banner = 0; banner < 3; banner++)
        {
            regions.Add($"outcome/{banner}", new("cardgame/icons", 256, banner * 48, 256, 48, 768, 192, PaletteY: 255));
            regions.Add($"capture/{banner}", new("cardgame/icons", 512, banner * 64, 255, 64, 768, 192, PaletteX: 32, PaletteY: 224 + banner));
        }
        return regions.ToFrozenDictionary(StringComparer.Ordinal);
    }

    internal static IReadOnlyList<string> DescribeCardAtlasMembers(string path)
    {
        if (!path.StartsWith("mods/textures/", StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }
        string stem = path[14..^Path.GetExtension(path).Length].ToLowerInvariant();
        if (stem.StartsWith("eng/", StringComparison.Ordinal))
        {
            stem = stem[4..];
        }
        if (!stem.StartsWith("cardgame/", StringComparison.Ordinal))
        {
            return [];
        }
        return CardArtworkRegions.Where(binding => IsAtlasMember(binding.Value, stem)).Select(binding => binding.Key).ToArray();

        static bool IsAtlasMember(CardArtworkRegion region, string stem)
        {
            if (region.Sheet is not ("cardgame/cards" or "cardgame/icons"))
            {
                return false;
            }
            if (stem == region.Sheet + "_00")
            {
                return true;
            }
            if (region.PaletteY is not int paletteY)
            {
                return false;
            }
            for (int x = region.X / 256 * 256; x < region.X + region.Width; x += 256)
            {
                int page = VramPageIndex(x, 0, 4);
                if (stem == VramFileStem(region.Sheet, page, region.PaletteX, paletteY) || stem == VramFileStem(region.Sheet, page, 0, 0))
                {
                    return true;
                }
            }
            return false;
        }
    }

    /// <summary>Recognizes native menu image addresses separately from the external texture directory.</summary>
    public static bool IsNativeMenuArtworkFamilyPath(string path) => System.Text.RegularExpressions.Regex.IsMatch(path, @"^menu/(?:mag[0-9]{2}\.tex|hires/iconfl0[0-3]_[0-9]{2})\.(?:png|dds)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    private static bool IsSupportedMenuArtworkTarget(string stem) => System.Text.RegularExpressions.Regex.IsMatch(stem, @"^data/eng/menu/(hires/((face_b|gf_big)0[01]_00|iconfl0[0-3]_(0[0-9]|1[0-5]))|icon_(0[0-9]|1[0-5]))$");

    private TextureReplacement? ResolveArtwork(TextureBuildContext context)
    {
        string owner = context.Key.Owner.Path;
        string part = context.Key.Subresource;
        if (part == "atlas" && context.Source.Language is null or "en")
        {
            if (System.Text.RegularExpressions.Regex.IsMatch(owner, @"^menu/mag[0-9]{2}\.tex$"))
            {
                ModFile? file = new[] { ".dds", ".png" }.SelectMany(extension => catalog.FindAll(ModFormat.Junction, owner + extension)).FirstOrDefault();
                if (file is not null)
                {
                    DecodedModImage decoded = ModImageDecoder.Decode(file);
                    PixelImage image = decoded.Image;
                    if (context.LogicalWidth != 256 || context.LogicalHeight != 256 || image.Width < 256 || image.Width % 256 != 0 || image.Height != image.Width)
                    {
                        throw new ModTextureLoadException(file.PackageId, file.RelativePath, new InvalidDataException("Native menu atlas images must preserve their 256 by 256 canvas at a uniform integer scale."));
                    }
                    return new(image, $"{file.PackageId}/{file.RelativePath}@{file.Revision}", 256, 256, decoded.MipLevels);
                }
            }
            if (owner is "menu/face_b00.tex" or "menu/face_b01.tex" or "menu/gf_big00.tex" or "menu/gf_big01.tex")
            {
                return ResolveMenuPortrait(context, owner[5..^4]);
            }
            if (owner == "menu/icon.tex")
            {
                return ResolveMenuIcons(context);
            }
        }
        if (owner.StartsWith("menu/mc", StringComparison.Ordinal) && owner.EndsWith(".tex", StringComparison.Ordinal) && owner.Length == 13 && Number(owner[7..9], out int menu) && menu < 10 && part == "atlas" && context.Source.Language is null or "en")
        {
            return ReadRegion(context, $"data/eng/menu/mc{menu:00}", 0, 0, 0, 256, 256, 256, 256);
        }
        if (owner != "cards/cards" || !CardArtworkRegions.TryGetValue(part, out CardArtworkRegion? region))
        {
            return null;
        }
        if (region.PaletteY is int paletteY && ReadVramRegion(context, region.Sheet, new TextureSourceRegion(768, 0, 4, PaletteX: region.PaletteX, PaletteY: paletteY), region.X, region.Y, region.Width, region.Height) is { } page)
        {
            return page;
        }
        return ReadRegion(context, region.Sheet, 0, region.X, region.Y, region.Width, region.Height, region.CanvasWidth, region.CanvasHeight);
    }

    private TextureReplacement? ResolveMenuIcons(TextureBuildContext context)
    {
        List<(ModFile File, PixelImage Image, int Page)> pages = [];
        int scale = 0;
        for (int page = 0; page < 4; page++)
        {
            ModFile? file = FindMenuArtwork($"iconfl{page:00}", context.Key.Palette);
            if (file is null)
            {
                continue;
            }
            PixelImage image = ModImageDecoder.Decode(file).Image;
            int pageScale = image.Width / 128;
            if (pageScale < 1 || image.Width != 128 * pageScale || image.Height != 128 * pageScale || scale != 0 && scale != pageScale)
            {
                throw new ModTextureLoadException(file.PackageId, file.RelativePath, new InvalidDataException("FFNx split menu icon pages must preserve the 128 by 128 canvas at the same integer scale."));
            }
            scale = pageScale;
            pages.Add((file, image, page));
        }
        if (pages.Count == 0)
        {
            return ReadRegion(context, "data/eng/menu/icon", context.Key.Palette, 0, 0, 256, 256, 256, 256, paletteFallback: false);
        }
        PixelImage original = context.Original;
        PixelImage atlas = new(256 * scale, 256 * scale);
        for (int y = 0; y < atlas.Height; y++)
        {
            for (int x = 0; x < atlas.Width; x++)
            {
                atlas[x, y] = original[x * original.Width / atlas.Width, y * original.Height / atlas.Height];
            }
        }
        foreach ((ModFile file, PixelImage image, int page) in pages)
        {
            int left = page % 2 * image.Width;
            int top = page / 2 * image.Height;
            for (int y = 0; y < image.Height; y++)
            {
                image.Pixels.AsSpan(y * image.Width, image.Width).CopyTo(atlas.Pixels.AsSpan((top + y) * atlas.Width + left, image.Width));
            }
        }
        string revision = string.Join(";", pages.Select(page => $"{page.File.PackageId}/{page.File.RelativePath}@{page.File.Revision}"));
        return new(atlas, revision, context.LogicalWidth, context.LogicalHeight);
    }

    private TextureReplacement? ResolveMenuPortrait(TextureBuildContext context, string name)
    {
        ModFile? file = FindMenuArtwork(name, 0);
        if (file is null)
        {
            return null;
        }
        DecodedModImage decoded = ModImageDecoder.Decode(file);
        PixelImage image = decoded.Image;
        if (image.Width < 256 || image.Width % 256 != 0 || image.Width != image.Height)
        {
            throw new ModTextureLoadException(file.PackageId, file.RelativePath, new InvalidDataException("FFNx menu portraits must preserve their 256 by 256 canvas at an integer scale."));
        }
        return new(image, $"{file.PackageId}/{file.RelativePath}@{file.Revision}", context.LogicalWidth, context.LogicalHeight, decoded.MipLevels);
    }

    private ModFile? FindMenuArtwork(string name, int palette) => new[] { ".dds", ".png" }.SelectMany(extension => new[] { "mods/textures/eng/data/eng/menu/hires/", "mods/textures/data/eng/menu/hires/", "menu/hires/" }.SelectMany(root => catalog.FindAll(ModFormat.Junction, $"{root}{name}_{palette:00}{extension}"))).FirstOrDefault();
}
