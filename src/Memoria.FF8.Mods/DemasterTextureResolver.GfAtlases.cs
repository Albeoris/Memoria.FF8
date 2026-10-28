using System.Text.RegularExpressions;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Mods;

public sealed partial class DemasterTextureResolver
{
    private const string GfAtlasLayout = "native-battle-gf-atlas";
    private static readonly Regex gfAtlasTarget = new(@"\Atextures/battle\.fs/hd_new/mag[0-9]{3}_[0-7](?:_jp)?\z", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex gfAtlasImageTarget = new(@"\Atextures/battle\.fs/hd_new/mag[0-9]{3}_[0246](?:_jp)?\z", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static bool IsGfAtlasTarget(string stem) => gfAtlasTarget.IsMatch(stem);
    private static bool IsGfAtlasImageTarget(string stem) => gfAtlasImageTarget.IsMatch(stem);

    private static IReadOnlyList<string> CandidateGfAtlasPaths(TextureBuildContext context)
    {
        if (context.NativeAtlas is not { } address || context.Layout != GfAtlasLayout || !HasLayout(context.Source, GfAtlasLayout))
        {
            return [];
        }
        address.Validate();
        return WithExtensions($"textures/battle.fs/hd_new/{address.Name}_{address.FirstImage}{address.Variant}");
    }

    private TextureReplacement? ResolveGfAtlas(TextureBuildContext context)
    {
        if (context.NativeAtlas is not { } address || context.Layout != GfAtlasLayout || !HasLayout(context.Source, GfAtlasLayout))
        {
            return null;
        }
        address.Validate();
        ModFile? file = CandidateGfAtlasPaths(context).SelectMany(path => catalog.FindAll(ModFormat.Demaster, path)).FirstOrDefault();
        if (file is null)
        {
            return null;
        }
        if (address.UnsupportedReason is { } unsupported)
        {
            throw new ModTextureLoadException(file.PackageId, file.RelativePath, new NotSupportedException(unsupported));
        }
        DecodedModImage decoded = ModImageDecoder.Decode(file);
        if (decoded.Image.Width <= 0 || decoded.Image.Height <= 0 || decoded.Image.Width % address.CanvasWidth != 0 || decoded.Image.Height % address.CanvasHeight != 0 || decoded.Image.Width / address.CanvasWidth != decoded.Image.Height / address.CanvasHeight)
        {
            throw new ModTextureLoadException(file.PackageId, file.RelativePath, new InvalidDataException("Whole native GF atlas replacements require equal positive integral scale over their source-published canvas."));
        }
        List<PixelImage> levels = [];
        foreach (PixelImage image in decoded.MipLevels)
        {
            if ((long)address.X * image.Width % address.CanvasWidth != 0 || (long)address.Y * image.Height % address.CanvasHeight != 0 || (long)address.Width * image.Width % address.CanvasWidth != 0 || (long)address.Height * image.Height % address.CanvasHeight != 0 || (long)address.SampleXOffset * image.Width % address.CanvasWidth != 0)
            {
                break;
            }
            int x = (int)((long)address.X * image.Width / address.CanvasWidth);
            int y = (int)((long)address.Y * image.Height / address.CanvasHeight);
            int width = (int)((long)address.Width * image.Width / address.CanvasWidth);
            int height = (int)((long)address.Height * image.Height / address.CanvasHeight);
            if (width == 0 || height == 0)
            {
                break;
            }
            PixelImage crop = new(width, height);
            for (int row = 0; row < height; row++)
            {
                int sourceX = checked(x + (int)((long)address.SampleXOffset * image.Width / address.CanvasWidth));
                int copyWidth = Math.Clamp(image.Width - sourceX, 0, width);
                Span<Rgba32> target = crop.Pixels.AsSpan(row * width, width);
                if (copyWidth > 0)
                {
                    image.Pixels.AsSpan((y + row) * image.Width + sourceX, copyWidth).CopyTo(target);
                }
                if (copyWidth < width)
                {
                    target[copyWidth..].Fill(image.Pixels[(y + row) * image.Width + image.Width - 1]);
                }
            }
            levels.Add(crop);
        }
        return new(levels[0], $"{file.PackageId}/{file.RelativePath}@{file.Revision}", context.LogicalWidth, context.LogicalHeight, levels.AsReadOnly());
    }
}
