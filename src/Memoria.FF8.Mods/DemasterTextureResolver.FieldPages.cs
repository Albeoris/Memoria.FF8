using System.Text.RegularExpressions;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Mods;

public sealed partial class DemasterTextureResolver
{
    private const string FieldPageLayout = "native-field-background-page";
    private const string FieldPagePrefix = "textures/field_bg/";
    private static readonly Regex fieldPageTarget = new(@"\Atextures/field_bg/(?<prefix>[a-z0-9_]{2})/(?<map>[a-z0-9_]{2,})/\k<map>_(?<page>0|[1-9]|1[0-5])(?:_(?<palette>0|[1-9]|1[0-5]))?\z", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private TextureReplacement? ResolveFieldPage(TextureBuildContext context)
    {
        if (!TryFieldPageAddress(context, out string map, out int nativePage, out TextureSourceAddress address))
        {
            return null;
        }
        foreach (string path in FieldBackgroundCandidates(map, nativePage, address.Palette))
        {
            ModFile? file = catalog.FindAll(ModFormat.Demaster, path).FirstOrDefault();
            if (file is null)
            {
                continue;
            }
            DecodedModImage decoded = ModImageDecoder.Decode(file);
            if (decoded.Image.Width <= 0 || decoded.Image.Width != decoded.Image.Height || decoded.Image.Width % 256 != 0)
            {
                throw new ModTextureLoadException(file.PackageId, file.RelativePath, new InvalidDataException("Demaster field pages require a square image with a positive integral scale of the native 256x256 canvas."));
            }
            List<PixelImage> levels = [];
            foreach (PixelImage mip in decoded.MipLevels)
            {
                if ((long)address.X * mip.Width % 256 != 0 || (long)address.Y * mip.Height % 256 != 0 || (long)16 * mip.Width % 256 != 0 || (long)16 * mip.Height % 256 != 0)
                {
                    break;
                }
                int width = mip.Width / 16;
                int height = mip.Height / 16;
                if (width == 0 || height == 0)
                {
                    break;
                }
                int x = (int)((long)address.X * mip.Width / 256);
                int y = (int)((long)address.Y * mip.Height / 256);
                PixelImage crop = new(width, height);
                for (int row = 0; row < height; row++)
                {
                    mip.Pixels.AsSpan((y + row) * mip.Width + x, width).CopyTo(crop.Pixels.AsSpan(row * width, width));
                }
                levels.Add(crop);
            }
            return new(levels[0], $"{file.PackageId}/{file.RelativePath}@{file.Revision}", 16, 16, levels.AsReadOnly());
        }
        return null;
    }

    private static bool TryFieldPageAddress(TextureBuildContext context, out string map, out int nativePage, out TextureSourceAddress address)
    {
        map = "";
        nativePage = 0;
        address = context.NativeAlias!;
        if (!HasLayout(context.Source, FieldPageLayout) || context.LogicalWidth != 16 || context.LogicalHeight != 16 || address is null || context.Key.Owner != address.Owner || context.Key.Palette != address.Palette || address.Palette is < 0 or > 15 || address.Width != 16 || address.Height != 16 || address.CanvasWidth != 256 || address.CanvasHeight != 256 || address.X is < 0 or > 240 || address.Y is < 0 or > 240)
        {
            return false;
        }
        string tile = context.Key.Subresource;
        string page = address.Subresource;
        string[] owner = address.Owner.Path.Split('/');
        if (owner.Length != 3 || owner[0] != "field" || owner[1].Length < 2 || owner[1].Any(character => !char.IsAsciiLetterOrDigit(character) && character != '_') || owner[2] != $"{owner[1]}.mim" || !tile.StartsWith("tile/", StringComparison.Ordinal) || !TryNonnegative(tile[5..], out int tileIndex) || tile != $"tile/{tileIndex}" || !page.StartsWith("page/", StringComparison.Ordinal) || !TryNonnegative(page[5..], out nativePage) || nativePage is < 16 or > 31 || page != $"page/{nativePage}")
        {
            return false;
        }
        map = owner[1];
        return true;
    }

    private static bool IsFieldPageTarget(string stem)
    {
        Match match = fieldPageTarget.Match(stem);
        return match.Success && match.Groups["map"].Value.StartsWith(match.Groups["prefix"].Value, StringComparison.OrdinalIgnoreCase);
    }
}
