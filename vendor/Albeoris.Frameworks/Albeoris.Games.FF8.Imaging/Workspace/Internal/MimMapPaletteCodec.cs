using Albeoris.Games.FF8.Imaging.Mim;

namespace Albeoris.Games.FF8.Imaging.Workspace.Internal;

internal static class MimMapPaletteCodec
{
    public const Int32 Width = MimFile.ColorsPerPalette;
    public const Int32 Height = MimFile.PaletteCount;

    public static PixelImage CreateImage(MimFile mim)
    {
        ArgumentNullException.ThrowIfNull(mim);
        PixelImage image = new(Width, Height);
        for (Int32 palette = 0; palette < Height; palette++)
        {
            for (Int32 color = 0; color < Width; color++)
                image[color, palette] = mim.GetColor(palette, color);
        }
        return image;
    }

    public static void Apply(MimFile mim, PixelImage image)
    {
        ArgumentNullException.ThrowIfNull(mim);
        ArgumentNullException.ThrowIfNull(image);
        if (image.Width != Width || image.Height != Height)
            throw new InvalidDataException($"The MIM palette layer is {image.Width}x{image.Height}; expected {Width}x{Height}.");
        for (Int32 index = 0; index < image.Pixels.Length; index++)
            mim.Colors[index] = image.Pixels[index].ToPsxBgr555();
    }
}
