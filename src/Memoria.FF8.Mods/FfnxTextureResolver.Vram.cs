using System.Text.RegularExpressions;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Mods;

public sealed partial class FfnxTextureResolver
{
    private static bool IsSupportedVramTarget(string stem)
    {
        Match match = Regex.Match(stem, @"^(world/dat/wmset/section[0-9]+/texture[0-9]+|cardgame/icons)_([0-9]+)_([0-9]+)_([0-9]+)$", RegexOptions.CultureInvariant);
        return match.Success && Number(match.Groups[2].Value, out int page) && page < 32
            && Number(match.Groups[3].Value, out int x) && x < 1024
            && Number(match.Groups[4].Value, out int y) && y < 512
            && IsSupportedTarget(match.Groups[1].Value + "_00");
    }

    private TextureReplacement? ReadVramPages(TextureBuildContext context, string name)
    {
        return ReadVramRegion(context, name, context.Region, 0, 0, context.LogicalWidth, context.LogicalHeight);
    }

    private TextureReplacement? ReadVramRegion(TextureBuildContext context, string name, TextureSourceRegion region, int cropLeft, int cropTop, int width, int height)
    {
        if (region.BitsPerPixel is not (4 or 8) || region.PaletteX is not int paletteX || region.PaletteY is not int paletteY)
        {
            return null;
        }
        int pixelsPerWord = 16 / region.BitsPerPixel;
        int startWord = region.X / 64 * 64;
        int startY = region.Y / 256 * 256;
        int left = (region.X - startWord) * pixelsPerWord + cropLeft;
        int top = region.Y - startY + cropTop;
        List<(ModFile File, int X, int Y)> pages = [];
        string[] roots = context.Source.Language is null or "en" ? ["mods/textures/eng/", "mods/textures/"] : ["mods/textures/"];
        for (int y = top / 256 * 256; y < top + height; y += 256)
        {
            for (int x = left / 256 * 256; x < left + width; x += 256)
            {
                int page = x / pixelsPerWord / 64 + y / 256 * 16;
                ModFile? file = FindVramPage(roots, name, page, paletteX, paletteY) ?? FindVramPage(roots, name, page, 0, 0);
                if (file is null)
                {
                    return null;
                }
                pages.Add((file, x, y));
            }
        }
        PixelImage? result = null;
        int scale = 0;
        List<string> provenance = [];
        foreach ((ModFile file, int x, int y) in pages)
        {
            PixelImage sheet = ModImageDecoder.Decode(file).Image;
            if (sheet.Width != sheet.Height || sheet.Width % 256 != 0 || sheet.Width < 256 || scale != 0 && sheet.Width / 256 != scale)
            {
                throw new ModTextureLoadException(file.PackageId, file.RelativePath, new InvalidDataException("FFNx VRAM pages must preserve the 256 by 256 native canvas and use a common integral scale."));
            }
            scale = sheet.Width / 256;
            result ??= new(width * scale, height * scale);
            int cropX = Math.Max(left, x);
            int cropY = Math.Max(top, y);
            int copyWidth = Math.Min(left + width, x + 256) - cropX;
            int copyHeight = Math.Min(top + height, y + 256) - cropY;
            for (int row = 0; row < copyHeight * scale; row++)
            {
                sheet.Pixels.AsSpan(((cropY - y) * scale + row) * sheet.Width + (cropX - x) * scale, copyWidth * scale).CopyTo(result.Pixels.AsSpan(((cropY - top) * scale + row) * result.Width + (cropX - left) * scale));
            }
            provenance.Add($"{file.PackageId}/{file.RelativePath}@{file.Revision}");
        }
        return result is null ? null : new(result, string.Join(";", provenance), context.LogicalWidth, context.LogicalHeight);
    }

    private ModFile? FindVramPage(string[] roots, string name, int page, int paletteX, int paletteY)
    {
        return new[] { ".dds", ".png" }.SelectMany(extension => roots.SelectMany(root => catalog.FindAll(ModFormat.Junction, $"{root}{name}_{page}_{paletteX}_{paletteY}{extension}"))).FirstOrDefault();
    }
}
