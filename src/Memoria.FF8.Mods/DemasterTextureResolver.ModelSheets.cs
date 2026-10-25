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
