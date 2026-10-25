using System.Text.RegularExpressions;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Mods;

public sealed partial class DemasterTextureResolver
{
    private const string ModelSheetLayout = "native-battle-model-sheet";
    private const string ModelSheetPrefix = "textures/battle.fs/hd_new/";
    private static readonly Regex modelSheetOwner = new(@"\Abattle/(?<name>c0m[0-9]{3}|d[0-9a-f][cw][0-9]{3})\.dat\z", RegexOptions.CultureInvariant);
    private static readonly Regex modelSheetTarget = new(@"\Atextures/battle\.fs/hd_new/(?<name>c0m[0-9]{3}|d[0-9a-f][cw][0-9]{3})_(?<row>0|[1-5])\z", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private TextureReplacement? ResolveModelSheet(TextureBuildContext context)
    {
        if (!TryModelSheetAddress(context, out string stem, out TextureSourceAddress address))
        {
            return null;
        }
        ModFile? file = WithExtensions(stem).SelectMany(path => catalog.FindAll(ModFormat.Demaster, path)).FirstOrDefault();
        if (file is null)
        {
            return null;
        }
        DecodedModImage decoded = ModImageDecoder.Decode(file);
        if (address.Owner.Path[9] == 'c')
        {
            return ResolveBodySheet(context, file, stem, address, decoded);
        }
        if (decoded.Image.Width <= 0 || decoded.Image.Height <= 0 || decoded.Image.Width % address.CanvasWidth != 0 || decoded.Image.Height % address.CanvasHeight != 0 || decoded.Image.Width / address.CanvasWidth != decoded.Image.Height / address.CanvasHeight)
        {
            throw new ModTextureLoadException(file.PackageId, file.RelativePath, new InvalidDataException("Demaster model sheets require equal positive integral scale over their source canvas."));
        }
        List<PixelImage> levels = [];
        foreach (PixelImage mip in decoded.MipLevels)
        {
            if ((long)address.X * mip.Width % address.CanvasWidth != 0 || (long)address.Y * mip.Height % address.CanvasHeight != 0 || (long)address.Width * mip.Width % address.CanvasWidth != 0 || (long)address.Height * mip.Height % address.CanvasHeight != 0)
            {
                break;
            }
            int x = (int)((long)address.X * mip.Width / address.CanvasWidth);
            int y = (int)((long)address.Y * mip.Height / address.CanvasHeight);
            int width = (int)((long)address.Width * mip.Width / address.CanvasWidth);
            int height = (int)((long)address.Height * mip.Height / address.CanvasHeight);
            if (width <= 0 || height <= 0)
            {
                break;
            }
            PixelImage crop = new(width, height);
            for (int row = 0; row < height; row++)
            {
                mip.Pixels.AsSpan((y + row) * mip.Width + x, width).CopyTo(crop.Pixels.AsSpan(row * width, width));
            }
            levels.Add(crop);
        }
        return new(levels[0], $"{file.PackageId}/{file.RelativePath}@{file.Revision}", context.LogicalWidth, context.LogicalHeight, levels.AsReadOnly());
    }

    private TextureReplacement ResolveBodySheet(TextureBuildContext context, ModFile file, string stem, TextureSourceAddress address, DecodedModImage decoded)
    {
        string prefix = stem[..stem.LastIndexOf('_')];
        int planeWidth = 0;
        for (int row = 0; row < 2; row++)
        {
            ModFile? companion = WithExtensions($"{prefix}_{row}").SelectMany(path => catalog.FindAll(ModFormat.Demaster, path)).FirstOrDefault();
            if (companion is null)
            {
                continue;
            }
            PixelImage image = ModImageDecoder.Decode(companion).Image;
            if (image.Width <= 0 || image.Width != image.Height || image.Width % 128 != 0)
            {
                continue;
            }
            if (planeWidth != 0 && planeWidth != image.Width)
            {
                throw new ModTextureLoadException(companion.PackageId, companion.RelativePath, new InvalidDataException("Body sheet rows require the same physical width for each native plane."));
            }
            planeWidth = image.Width;
        }
        if (planeWidth == 0 || decoded.Image.Width != planeWidth && (long)decoded.Image.Width != 2L * planeWidth || decoded.Image.Height <= 0 || decoded.Image.Height > planeWidth)
        {
            throw new ModTextureLoadException(file.PackageId, file.RelativePath, new InvalidDataException("Body sheets require a complete square plane in their source row pair, one or two horizontal planes at that scale, and an upload height within the native row."));
        }
        int planeCount = decoded.Image.Width / planeWidth;
        List<PixelImage> levels = CropBodyPlane(decoded.MipLevels, address, planeCount, 0);
        IReadOnlyDictionary<string, PixelImage>? variants = planeCount == 2 ? new Dictionary<string, PixelImage> { ["eyes-closed"] = CropBodyPlane([decoded.Image], address, planeCount, 1)[0] } : null;
        return new(levels[0], $"{file.PackageId}/{file.RelativePath}@{file.Revision}", context.LogicalWidth, context.LogicalHeight, levels.AsReadOnly(), variants);
    }

    private static List<PixelImage> CropBodyPlane(IReadOnlyList<PixelImage> images, TextureSourceAddress address, int planeCount, int plane)
    {
        List<PixelImage> levels = [];
        foreach (PixelImage image in images)
        {
            if (image.Width % planeCount != 0)
            {
                break;
            }
            int planeWidth = image.Width / planeCount;
            if ((long)address.X * planeWidth % 128 != 0 || (long)address.Y * planeWidth % 128 != 0 || (long)address.Width * planeWidth % 128 != 0 || (long)address.Height * planeWidth % 128 != 0)
            {
                break;
            }
            int sourceX = plane * planeWidth + (int)((long)address.X * planeWidth / 128);
            int sourceY = (int)((long)address.Y * planeWidth / 128);
            int width = (int)((long)address.Width * planeWidth / 128);
            int height = (int)((long)address.Height * planeWidth / 128);
            if (width <= 0 || height <= 0)
            {
                break;
            }
            PixelImage crop = new(width, height);
            for (int row = 0; row < Math.Min(height, image.Height - sourceY); row++)
            {
                image.Pixels.AsSpan((sourceY + row) * image.Width + sourceX, width).CopyTo(crop.Pixels.AsSpan(row * width, width));
            }
            levels.Add(crop);
        }
        return levels;
    }

    private static bool TryModelSheetAddress(TextureBuildContext context, out string stem, out TextureSourceAddress address)
    {
        stem = "";
        address = context.NativeSheet!;
        if (context.Layout != "classic-battle" || !HasLayout(context.Source, ModelSheetLayout) || address is null || address.Owner != context.Key.Owner || address.Palette != 0 || context.Key.Palette != 0 || address.Width != context.LogicalWidth || address.Height != context.LogicalHeight || address.Width <= 0 || address.Height <= 0 || address.X < 0 || address.Y < 0 || (long)address.X + address.Width > address.CanvasWidth || (long)address.Y + address.Height > address.CanvasHeight || address.CanvasWidth != 128)
        {
            return false;
        }
        Match owner = modelSheetOwner.Match(address.Owner.Path);
        string member = context.Key.Subresource;
        if (!owner.Success || !member.StartsWith("texture/", StringComparison.Ordinal) || !TryNonnegative(member[8..], out int index) || member != $"texture/{index}" || !address.Subresource.StartsWith("row/", StringComparison.Ordinal) || !TryNonnegative(address.Subresource[4..], out int row) || address.Subresource != $"row/{row}")
        {
            return false;
        }
        string name = owner.Groups["name"].Value;
        if (!ValidModelSheetRow(name, row) || address.CanvasHeight != (name[2] == 'w' ? 32 : 128))
        {
            return false;
        }
        stem = $"{ModelSheetPrefix}{name}_{row}";
        return true;
    }

    private static bool IsModelSheetTarget(string stem)
    {
        Match match = modelSheetTarget.Match(stem);
        return match.Success && ValidModelSheetRow(match.Groups["name"].Value, match.Groups["row"].Value[0] - '0');
    }

    private static bool IsModelSheetFamily(string stem)
    {
        return stem.StartsWith(ModelSheetPrefix + "c0m", StringComparison.OrdinalIgnoreCase) || Regex.IsMatch(stem, @"\Atextures/battle\.fs/hd_new/d[0-9a-f][cw]", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static bool ValidModelSheetRow(string name, int row)
    {
        return row >= 0 && row <= (name.StartsWith("c0m", StringComparison.OrdinalIgnoreCase) ? 5 : char.ToLowerInvariant(name[2]) == 'w' ? 0 : 1);
    }
}
