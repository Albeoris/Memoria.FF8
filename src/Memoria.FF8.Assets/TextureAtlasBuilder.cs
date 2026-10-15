using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Assets;

/// <summary>Native atlas preparation after source decoding and optional source-texture replacement.</summary>
internal static class TextureAtlasBuilder
{
    public static TextureValue Build(IReadOnlyList<TextureValue> images, IReadOnlyList<(int X, int Y)> positions, int width, int height)
    {
        if (images.Count != positions.Count)
        {
            throw new InvalidDataException("Atlas source textures and placements differ.");
        }
        int scale = 1;
        foreach (TextureValue value in images)
        {
            if (value.Image.Width % value.LogicalWidth != 0 || value.Image.Height % value.LogicalHeight != 0 || value.Image.Width / value.LogicalWidth != value.Image.Height / value.LogicalHeight)
            {
                throw new InvalidDataException("An atlas source must preserve one integral physical scale for its logical rectangle.");
            }
            scale = Math.Max(scale, value.Image.Width / value.LogicalWidth);
        }
        PixelImage atlas = new(checked(width * scale), checked(height * scale));
        for (int index = 0; index < images.Count; index++)
        {
            TextureValue value = images[index];
            (int x, int y) = positions[index];
            if (x < 0 || y < 0 || x + value.LogicalWidth > width || y + value.LogicalHeight > height)
            {
                throw new InvalidDataException("An atlas source lies outside the native logical canvas.");
            }
            for (int row = 0; row < value.LogicalHeight * scale; row++)
            {
                for (int column = 0; column < value.LogicalWidth * scale; column++)
                {
                    atlas[x * scale + column, y * scale + row] = value.Image[column * value.Image.Width / (value.LogicalWidth * scale), row * value.Image.Height / (value.LogicalHeight * scale)];
                }
            }
        }
        return new TextureValue(atlas, width, height, string.Join("; ", images.Select(image => image.Provenance).Distinct()));
    }
}
