using System.Text.RegularExpressions;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Mods;

public sealed partial class DemasterTextureResolver
{
    private const string FieldModelSheetLayout = "native-field-model-sheet";
    private const string FieldModelNewPrefix = "textures/field.fs/field_hd_new/";
    private const string FieldModelBasePrefix = "textures/field.fs/field_hd/";
    private static readonly Regex fieldModelSheetOwner = new(@"\Afield/model/(?:main|second)_chr/(?<name>[dcnop][0-9]{3})\z", RegexOptions.CultureInvariant);
    private static readonly Regex fieldModelSheetTarget = new(@"\Atextures/field\.fs/field_hd(?:_new)?/(?<name>[dnop][0-9]{3})_(?<row>0|[1-9][0-9]*)(?<variant>_alpha)?\z", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private TextureReplacement? ResolveFieldModelSheet(TextureBuildContext context)
    {
        if (!TryFieldModelSheetAddress(context, out string name, out int row, out string variant, out TextureSourceAddress address))
        {
            return null;
        }
        ModFile? file = FieldModelSheetFile(name, row, variant);
        if (file is null)
        {
            return null;
        }
        DecodedModImage decoded = ModImageDecoder.Decode(file);
        int planeWidth = CompleteFieldModelPlaneWidth(decoded.Image);
        if (planeWidth == 0)
        {
            foreach (int sibling in catalog.Files.Where(candidate => candidate.Format == ModFormat.Demaster).Select(candidate => FieldModelSiblingRow(candidate.RelativePath, name, variant)).Where(candidate => candidate >= 0).Distinct().Order())
            {
                ModFile? companion = FieldModelSheetFile(name, sibling, variant);
                if (companion is null)
                {
                    continue;
                }
                int anchor = CompleteFieldModelPlaneWidth(ReferenceEquals(companion, file) ? decoded.Image : ModImageDecoder.Decode(companion).Image);
                if (anchor == 0 || decoded.Image.Width != anchor && (long)decoded.Image.Width != 2L * anchor || decoded.Image.Height <= 0 || decoded.Image.Height > anchor)
                {
                    continue;
                }
                if (planeWidth != 0 && planeWidth != anchor)
                {
                    throw new ModTextureLoadException(file.PackageId, file.RelativePath, new InvalidDataException("Partial field model sheets have conflicting complete source-material plane widths."));
                }
                planeWidth = anchor;
            }
        }
        if (planeWidth == 0 || decoded.Image.Width != planeWidth && (long)decoded.Image.Width != 2L * planeWidth || decoded.Image.Height <= 0 || decoded.Image.Height > planeWidth)
        {
            throw new ModTextureLoadException(file.PackageId, file.RelativePath, new InvalidDataException("Field model sheets require one or two horizontal native planes at an integral scale, with partial uploads anchored by a complete source material."));
        }
        int planes = decoded.Image.Width / planeWidth;
        List<PixelImage> levels = CropBodyPlane(decoded.MipLevels, address, planes, 0);
        IReadOnlyDictionary<string, PixelImage>? variants = planes == 2 ? new Dictionary<string, PixelImage> { ["eyes-closed"] = CropBodyPlane([decoded.Image], address, planes, 1)[0] } : null;
        return new(levels[0], $"{file.PackageId}/{file.RelativePath}@{file.Revision}", context.LogicalWidth, context.LogicalHeight, levels.AsReadOnly(), variants);
    }

    private ModFile? FieldModelSheetFile(string name, int row, string variant)
    {
        return FieldModelSheetPaths(name, row, variant).SelectMany(path => catalog.FindAll(ModFormat.Demaster, path)).FirstOrDefault();
    }

    private static IReadOnlyList<string> FieldModelSheetPaths(string name, int row, string variant)
    {
        return WithExtensions($"{FieldModelNewPrefix}{name}_{row}{variant}").Concat(WithExtensions($"{FieldModelBasePrefix}{name}_{row}{variant}")).ToArray();
    }

    private static int CompleteFieldModelPlaneWidth(PixelImage image)
    {
        int width = image.Width == image.Height ? image.Width : (long)image.Width == 2L * image.Height ? image.Height : 0;
        return width > 0 && width % 128 == 0 ? width : 0;
    }

    private static int FieldModelSiblingRow(string path, string name, string variant)
    {
        string extension = Path.GetExtension(path);
        if (!extension.Equals(".dds", StringComparison.OrdinalIgnoreCase) && !extension.Equals(".png", StringComparison.OrdinalIgnoreCase))
        {
            return -1;
        }
        Match match = fieldModelSheetTarget.Match(path[..^extension.Length]);
        return match.Success && match.Groups["name"].Value.Equals(name, StringComparison.OrdinalIgnoreCase) && match.Groups["variant"].Value.Equals(variant, StringComparison.OrdinalIgnoreCase) && TryNonnegative(match.Groups["row"].Value, out int row) ? row : -1;
    }

    private static bool TryFieldModelSheetAddress(TextureBuildContext context, out string name, out int row, out string variant, out TextureSourceAddress address)
    {
        name = "";
        row = 0;
        variant = "";
        address = context.NativeSheet!;
        if (context.Layout != "classic-field-model" || !HasLayout(context.Source, FieldModelSheetLayout) || address is null || address.Palette != 0 || context.Key.Palette != 0 || address.CanvasWidth != 128 || address.CanvasHeight != 128 || address.Width != context.LogicalWidth || address.Height != context.LogicalHeight || address.Width <= 0 || address.Height <= 0 || address.X < 0 || address.Y < 0 || (long)address.X + address.Width > 128 || (long)address.Y + address.Height > 128)
        {
            return false;
        }
        string[] owner = context.Key.Owner.Path.Split('/');
        Match source = fieldModelSheetOwner.Match(address.Owner.Path);
        string[] member = address.Subresource.Split('/');
        bool oneOwner = owner.Length == 3 && owner[0] == "field" && owner[2] == "chara.one";
        bool libraryOwner = source.Success && address.Owner.Path.StartsWith("field/model/main_chr/", StringComparison.Ordinal) && owner.Length == 3 && owner[0] == "field" && owner[1] == "main_chr" && owner[2] == source.Groups["name"].Value + ".mch";
        if ((!oneOwner && !libraryOwner) || !source.Success || member.Length is not (2 or 3) || member[0] != "row" || !TryNonnegative(member[1], out row) || member[1] != $"{row}" || member.Length == 3 && member[2] != "alpha")
        {
            return false;
        }
        name = source.Groups["name"].Value;
        if (name[0] == 'c')
        {
            name = "n" + name[1..];
        }
        variant = member.Length == 3 ? "_alpha" : "";
        return true;
    }

    private static bool IsFieldModelSheetTarget(string stem)
    {
        Match match = fieldModelSheetTarget.Match(stem);
        return match.Success && TryNonnegative(match.Groups["row"].Value, out _);
    }

    private static bool IsFieldModelSheetFamily(string stem)
    {
        return stem.StartsWith(FieldModelNewPrefix, StringComparison.OrdinalIgnoreCase) || stem.StartsWith(FieldModelBasePrefix, StringComparison.OrdinalIgnoreCase);
    }
}
