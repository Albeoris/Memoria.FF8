using System.Text.RegularExpressions;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Mods;

public sealed partial class DemasterTextureResolver
{
    private const string ModelSheetLayout = "native-battle-model-sheet";
    private const string BodySheetLayout = "native-battle-body-sheet";
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
            RejectUnresolvedMonsterOverlap(stem, address);
            return null;
        }
        DecodedModImage decoded = ModImageDecoder.Decode(file);
        if (address.Owner.Path[9] == 'c')
        {
            return ResolveBodySheet(context, file, stem, address, decoded);
        }
        if (address.Owner.Path.StartsWith("battle/c0m", StringComparison.Ordinal))
        {
            string prefix = stem[..stem.LastIndexOf('_')];
            ModFile? anchor = WithExtensions($"{prefix}_0").SelectMany(path => catalog.FindAll(ModFormat.Demaster, path)).FirstOrDefault();
            if (anchor is not null)
            {
                int planeWidth = ModImageDecoder.Decode(anchor).Image.Width;
                if (planeWidth <= 0 || planeWidth % 128 != 0)
                {
                    throw new ModTextureLoadException(anchor.PackageId, anchor.RelativePath, new InvalidDataException("Monster sheet row zero must anchor a positive integral native plane scale."));
                }
                int row = int.Parse(address.Subresource.AsSpan(4), System.Globalization.CultureInfo.InvariantCulture);
                int firstRow = row & ~1;
                ModFile? first = WithExtensions($"{prefix}_{firstRow}").SelectMany(path => catalog.FindAll(ModFormat.Demaster, path)).FirstOrDefault();
                ModFile? second = WithExtensions($"{prefix}_{firstRow + 1}").SelectMany(path => catalog.FindAll(ModFormat.Demaster, path)).FirstOrDefault();
                if (first is not null && second is not null)
                {
                    return ResolveMonsterAtlas(context, file, address, row, first, second, planeWidth);
                }
                return ResolveMonsterMaterialUpload(context, file, address, decoded, planeWidth, row);
            }
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

    private void RejectUnresolvedMonsterOverlap(string stem, TextureSourceAddress address)
    {
        if (!address.Owner.Path.StartsWith("battle/c0m", StringComparison.Ordinal))
        {
            return;
        }
        int row = int.Parse(address.Subresource.AsSpan(4), System.Globalization.CultureInfo.InvariantCulture);
        if ((row & 1) == 0)
        {
            return;
        }
        string prefix = stem[..stem.LastIndexOf('_')];
        ModFile? previous = WithExtensions($"{prefix}_{row - 1}").SelectMany(path => catalog.FindAll(ModFormat.Demaster, path)).FirstOrDefault();
        ModFile? anchor = WithExtensions($"{prefix}_0").SelectMany(path => catalog.FindAll(ModFormat.Demaster, path)).FirstOrDefault();
        if (previous is null || anchor is null)
        {
            return;
        }
        int pitch = ModImageDecoder.Decode(anchor).Image.Width;
        if (pitch <= 0 || pitch % 128 != 0)
        {
            throw new ModTextureLoadException(anchor.PackageId, anchor.RelativePath, new InvalidDataException("Monster sheet row zero must anchor a positive integral native plane scale."));
        }
        PixelImage image = ModImageDecoder.Decode(previous).Image;
        if ((long)address.X * pitch / 128 < image.Width && (long)(128 + address.Y) * pitch / 128 < image.Height)
        {
            throw new ModTextureLoadException(previous.PackageId, previous.RelativePath, new InvalidDataException("An overlapping monster upload requires its original companion pixels to compose the queried material."));
        }
    }

    private static TextureReplacement ResolveMonsterMaterialUpload(TextureBuildContext context, ModFile file, TextureSourceAddress address, DecodedModImage decoded, int planeWidth, int row)
    {
        if (decoded.Image.Width <= 0 || decoded.Image.Height <= 0 || (long)decoded.Image.Width > 2L * planeWidth || (long)decoded.Image.Height > (2L - (row & 1)) * planeWidth)
        {
            throw new ModTextureLoadException(file.PackageId, file.RelativePath, new InvalidDataException("Monster image copies must fit their native paired atlas allocation."));
        }
        List<PixelImage> levels = CropAnchoredPlane(decoded.MipLevels, address, planeWidth, 0);
        return new(levels[0], $"{file.PackageId}/{file.RelativePath}@{file.Revision}", context.LogicalWidth, context.LogicalHeight, levels.AsReadOnly());
    }

    private static TextureReplacement ResolveMonsterAtlas(TextureBuildContext context, ModFile file, TextureSourceAddress address, int row, ModFile first, ModFile second, int planeWidth)
    {
        ModFile[] files = [first, second];
        DecodedModImage[] images = files.Select(ModImageDecoder.Decode).ToArray();
        for (int layer = 0; layer < images.Length; layer++)
        {
            PixelImage image = images[layer].Image;
            if (image.Width <= 0 || image.Height <= 0 || (long)image.Width > 2L * planeWidth || (long)image.Height > (2L - layer) * planeWidth)
            {
                throw new ModTextureLoadException(files[layer].PackageId, files[layer].RelativePath, new InvalidDataException("Monster image copies must fit their native paired atlas allocation."));
            }
        }
        int x = checked(address.X * (planeWidth / 128));
        int y = checked(((row & 1) * 128 + address.Y) * (planeWidth / 128));
        int width = checked(address.Width * (planeWidth / 128));
        int height = checked(address.Height * (planeWidth / 128));
        int mipCount = int.MaxValue;
        for (int layer = 0; layer < images.Length; layer++)
        {
            PixelImage image = images[layer].Image;
            if (x < image.Width && y < (long)layer * planeWidth + image.Height && (long)y + height > (long)layer * planeWidth)
            {
                mipCount = Math.Min(mipCount, images[layer].MipLevels.Count);
            }
        }
        if (mipCount == int.MaxValue)
        {
            mipCount = images[row & 1].MipLevels.Count;
        }
        List<PixelImage> levels = [];
        for (int level = 0; level < mipCount; level++)
        {
            int pitch = Math.Max(1, planeWidth >> level);
            if ((long)address.X * pitch % 128 != 0 || (long)address.Y * pitch % 128 != 0 || (long)address.Width * pitch % 128 != 0 || (long)address.Height * pitch % 128 != 0)
            {
                break;
            }
            int queryX = (int)((long)address.X * pitch / 128);
            int queryY = (row & 1) * pitch + (int)((long)address.Y * pitch / 128);
            int cropWidth = (int)((long)address.Width * pitch / 128);
            int cropHeight = (int)((long)address.Height * pitch / 128);
            if (cropWidth <= 0 || cropHeight <= 0)
            {
                break;
            }
            PixelImage crop = new(cropWidth, cropHeight);
            for (int layer = 0; layer < images.Length; layer++)
            {
                if (level >= images[layer].MipLevels.Count)
                {
                    continue;
                }
                PixelImage image = images[layer].MipLevels[level];
                int left = Math.Max(queryX, 0);
                int top = Math.Max(queryY, layer * pitch);
                int right = (int)Math.Min((long)queryX + cropWidth, image.Width);
                int bottom = (int)Math.Min((long)queryY + cropHeight, (long)layer * pitch + image.Height);
                if (left >= right || top >= bottom)
                {
                    continue;
                }
                for (int line = top; line < bottom; line++)
                {
                    image.Pixels.AsSpan((line - layer * pitch) * image.Width + left, right - left).CopyTo(crop.Pixels.AsSpan((line - queryY) * cropWidth + left - queryX, right - left));
                }
            }
            levels.Add(crop);
        }
        return new(levels[0], $"{file.PackageId}/{file.RelativePath}@{file.Revision}", context.LogicalWidth, context.LogicalHeight, levels.AsReadOnly());
    }

    private static List<PixelImage> CropAnchoredPlane(IReadOnlyList<PixelImage> images, TextureSourceAddress address, int planeWidth, int plane)
    {
        List<PixelImage> levels = [];
        for (int level = 0; level < images.Count; level++)
        {
            PixelImage image = images[level];
            int mipPlaneWidth = Math.Max(1, planeWidth >> level);
            if ((long)address.X * mipPlaneWidth % 128 != 0 || (long)address.Y * mipPlaneWidth % 128 != 0 || (long)address.Width * mipPlaneWidth % 128 != 0 || (long)address.Height * mipPlaneWidth % 128 != 0)
            {
                break;
            }
            int sourceX = plane * mipPlaneWidth + (int)((long)address.X * mipPlaneWidth / 128);
            int sourceY = (int)((long)address.Y * mipPlaneWidth / 128);
            int width = (int)((long)address.Width * mipPlaneWidth / 128);
            int height = (int)((long)address.Height * mipPlaneWidth / 128);
            if (width <= 0 || height <= 0)
            {
                break;
            }
            PixelImage crop = new(width, height);
            int copyWidth = Math.Min(width, image.Width - sourceX);
            int copyHeight = Math.Min(height, image.Height - sourceY);
            if (copyWidth > 0)
            {
                for (int row = 0; row < copyHeight; row++)
                {
                    image.Pixels.AsSpan((sourceY + row) * image.Width + sourceX, copyWidth).CopyTo(crop.Pixels.AsSpan(row * width, copyWidth));
                }
            }
            levels.Add(crop);
        }
        return levels;
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
        if (!SupportsModelSheetName(context.Source, name) || !ValidModelSheetRow(name, row) || address.CanvasHeight != (name[2] == 'w' ? 32 : 128))
        {
            return false;
        }
        stem = $"{ModelSheetPrefix}{name}_{row}";
        return true;
    }

    private static bool IsModelSheetTarget(ResourceSourceDescriptor source, string stem)
    {
        Match match = modelSheetTarget.Match(stem);
        return match.Success && SupportsModelSheetName(source, match.Groups["name"].Value) && ValidModelSheetRow(match.Groups["name"].Value, match.Groups["row"].Value[0] - '0');
    }

    private static bool SupportsModelSheetName(ResourceSourceDescriptor source, string name)
    {
        return char.ToLowerInvariant(name[0]) != 'd' || char.ToLowerInvariant(name[2]) != 'c' || HasLayout(source, BodySheetLayout);
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
