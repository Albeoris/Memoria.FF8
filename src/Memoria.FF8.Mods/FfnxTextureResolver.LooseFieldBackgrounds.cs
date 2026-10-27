using System.Text.RegularExpressions;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Mods;

public sealed partial class FfnxTextureResolver
{
    /// <summary>Recognizes native loose background pages, including the older page numbering convention.</summary>
    public static bool IsNativeFieldBackgroundPath(string path)
    {
        Match match = Regex.Match(path, @"^field/mapdata/(?<prefix>[a-z0-9_]{2})/(?<field>[a-z0-9_]+)/\k<field>_(?<page>[0-9]|1[0-9]|2[0-5])\.(?:png|dds)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return match.Success && match.Groups["field"].Value.StartsWith(match.Groups["prefix"].Value, StringComparison.OrdinalIgnoreCase) && Number(match.Groups["page"].Value, out int page) && page < 26;
    }

    /// <summary>Includes numeric page variants whose source selection contract must be validated separately.</summary>
    public static bool IsNativeFieldBackgroundFamilyPath(string path)
    {
        Match match = Regex.Match(path, @"^field/mapdata/(?<prefix>[a-z0-9_]{2})/(?<field>[a-z0-9_]+)/\k<field>_[0-9]+(?:_[0-9]+)*\.(?:png|dds)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return match.Success && match.Groups["field"].Value.StartsWith(match.Groups["prefix"].Value, StringComparison.OrdinalIgnoreCase);
    }

    private TextureReplacement? ResolveLooseFieldBackground(TextureBuildContext context)
    {
        if (context.NativeAlias is not { } page || page.Owner != context.Key.Owner || !page.Subresource.StartsWith("page/", StringComparison.Ordinal) || !Number(page.Subresource[5..], out int nativePage) || nativePage < 16 || nativePage >= 29)
        {
            return null;
        }
        string[] owner = page.Owner.Path.Split('/');
        if (owner.Length != 3 || owner[0] != "field" || owner[1].Length < 2 || owner[2] != owner[1] + ".mim")
        {
            return null;
        }
        string name = $"field/mapdata/{owner[1][..2]}/{owner[1]}/{owner[1]}";
        int index = nativePage - 16;
        ModFile? file = new[] { index + 13, index }.SelectMany(candidate => new[] { ".dds", ".png" }.SelectMany(extension => catalog.FindAll(ModFormat.Junction, $"{name}_{candidate}{extension}"))).FirstOrDefault();
        if (file is null)
        {
            return null;
        }
        PixelImage sheet = ModImageDecoder.Decode(file).Image;
        if (page.CanvasWidth != 256 || page.CanvasHeight != 256 || page.Width != 16 || page.Height != 16 || context.LogicalWidth != 16 || context.LogicalHeight != 16 || page.X < 0 || page.Y < 0 || page.X % 16 != 0 || page.Y % 16 != 0 || (long)page.X + 16 > 256 || (long)page.Y + 16 > 256 || sheet.Width < 256 || sheet.Width % 256 != 0 || sheet.Height != sheet.Width)
        {
            throw new ModTextureLoadException(file.PackageId, file.RelativePath, new InvalidDataException("Loose field background pages must preserve their native 256 by 256 canvas and source tile at a uniform integer scale."));
        }
        int scale = sheet.Width / 256;
        PixelImage image = new(16 * scale, 16 * scale);
        for (int row = 0; row < image.Height; row++)
        {
            for (int column = 0; column < image.Width; column++)
            {
                Rgba32 pixel = sheet[page.X * scale + column, page.Y * scale + row];
                image[column, row] = new Rgba32(pixel.R, pixel.G, pixel.B, pixel.A >= 127 ? (byte)128 : (byte)0);
            }
        }
        return new(image, $"{file.PackageId}/{file.RelativePath}@{file.Revision}", 16, 16);
    }
}
