using Memoria.FF8.Resources;
using Albeoris.Games.FF8.Imaging;

namespace Memoria.FF8.Mods;

public sealed partial class FfnxTextureResolver
{
    private static bool IsSupportedMenuArtworkTarget(string stem) => System.Text.RegularExpressions.Regex.IsMatch(stem, @"^data/eng/menu/(hires/((face_b|gf_big)0[01]_00|iconfl0[0-3]_(0[0-9]|1[0-5]))|icon_(0[0-9]|1[0-5]))$");

    private TextureReplacement? ResolveArtwork(TextureBuildContext context)
    {
        string owner = context.Key.Owner.Path;
        string part = context.Key.Subresource;
        if (part == "atlas" && context.Source.Language is null or "en")
        {
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
        if (owner != "cards/cards")
        {
            return null;
        }
        if (part.StartsWith("front/", StringComparison.Ordinal) && Number(part[6..], out int card) && card < 110)
        {
            return ReadRegion(context, "cardgame/cards", 0, card / 8 * 128 + card % 2 * 64, card % 8 / 2 * 64, 64, 64, 1792, 256);
        }
        if (part == "back")
        {
            return ReadRegion(context, "cardgame/cards", 0, 1664, 192, 64, 64, 1792, 256);
        }
        if (part.StartsWith("element/", StringComparison.Ordinal) && Number(part[8..], out int element) && element < 8)
        {
            return ReadCardIconRegion(context, element % 4 * 64, (element / 4 + 1) * 16, 16, 16, 0, 225 + element);
        }
        if (part.StartsWith("outcome/", StringComparison.Ordinal) && Number(part[8..], out int outcome) && outcome < 3)
        {
            return ReadCardIconRegion(context, 256, outcome * 48, 256, 48, 0, 255);
        }
        if (part is "background" or "board-background")
        {
            return ReadRegion(context, part == "background" ? "cardgame/intro" : "cardgame/game", 0, 0, 0, 384, 224, 384, 224);
        }
        return null;
    }

    private TextureReplacement? ReadCardIconRegion(TextureBuildContext context, int x, int y, int width, int height, int paletteX, int paletteY)
    {
        return ReadVramRegion(context, "cardgame/icons", new TextureSourceRegion(768, 0, 4, PaletteX: paletteX, PaletteY: paletteY), x, y, width, height)
            ?? ReadRegion(context, "cardgame/icons", 0, x, y, width, height, 768, 192);
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

    private ModFile? FindMenuArtwork(string name, int palette) => new[] { ".dds", ".png" }.SelectMany(extension => new[] { "mods/textures/eng/", "mods/textures/" }.SelectMany(root => catalog.FindAll(ModFormat.Junction, $"{root}data/eng/menu/hires/{name}_{palette:00}{extension}"))).FirstOrDefault();
}
