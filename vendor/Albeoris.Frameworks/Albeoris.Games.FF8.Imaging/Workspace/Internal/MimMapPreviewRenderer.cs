using Albeoris.Games.FF8.Imaging.Map;

namespace Albeoris.Games.FF8.Imaging.Workspace.Internal;

internal static class MimMapPreviewRenderer
{
    private const Int32 MaximumThumbnailSize = 256;

    public static PixelImage Render(MimMapLayout layout, IReadOnlyDictionary<UInt32, MimMapRenderedTexture> textures)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(textures);
        PixelImage result = new(layout.Width, layout.Height);
        foreach (MimMapTextureGroup group in layout.Groups)
        {
            MimMapTile tile = group.Tiles[0];
            if (tile.AnimationId != Byte.MaxValue && tile.AnimationState != 0)
                continue;
            Blend(result, textures[group.PupuId].Image, tile.BlendMode);
        }
        return result;
    }

    public static PixelImage CreateThumbnail(PixelImage source)
    {
        ArgumentNullException.ThrowIfNull(source);
        Double scale = Math.Min(1.0, Math.Min(MaximumThumbnailSize / (Double)source.Width, MaximumThumbnailSize / (Double)source.Height));
        Int32 width = Math.Max(1, (Int32)Math.Round(source.Width * scale));
        Int32 height = Math.Max(1, (Int32)Math.Round(source.Height * scale));
        PixelImage result = new(width, height);
        for (Int32 y = 0; y < height; y++)
        {
            Int32 sourceY = Math.Min(source.Height - 1, y * source.Height / height);
            for (Int32 x = 0; x < width; x++)
            {
                Int32 sourceX = Math.Min(source.Width - 1, x * source.Width / width);
                result[x, y] = source[sourceX, sourceY];
            }
        }
        return result;
    }

    private static void Blend(PixelImage destination, PixelImage source, MapBlendMode blendMode)
    {
        for (Int32 index = 0; index < source.Pixels.Length; index++)
        {
            Rgba32 foreground = source.Pixels[index];
            if (foreground.A == 0)
                continue;
            destination.Pixels[index] = blendMode switch
            {
                MapBlendMode.Average => BlendAdd(destination.Pixels[index], foreground, 0.5),
                MapBlendMode.Add => BlendAdd(destination.Pixels[index], foreground, 1.0),
                MapBlendMode.Subtract => BlendSubtract(destination.Pixels[index], foreground),
                MapBlendMode.QuarterAdd => BlendAdd(destination.Pixels[index], foreground, 0.25),
                _ => BlendNormal(destination.Pixels[index], foreground),
            };
        }
    }

    private static Rgba32 BlendNormal(Rgba32 background, Rgba32 foreground)
    {
        Double sourceAlpha = foreground.A / (Double)Byte.MaxValue;
        Double destinationAlpha = background.A / (Double)Byte.MaxValue;
        Double alpha = sourceAlpha + destinationAlpha * (1.0 - sourceAlpha);
        if (alpha <= 0)
            return default;
        return new Rgba32(BlendChannel(background.R, foreground.R, sourceAlpha, destinationAlpha, alpha), BlendChannel(background.G, foreground.G, sourceAlpha, destinationAlpha, alpha), BlendChannel(background.B, foreground.B, sourceAlpha, destinationAlpha, alpha), ToByte(alpha * Byte.MaxValue));
    }

    private static Rgba32 BlendAdd(Rgba32 background, Rgba32 foreground, Double factor)
    {
        Double contribution = factor * foreground.A / Byte.MaxValue;
        return new Rgba32(ToByte(background.R + foreground.R * contribution), ToByte(background.G + foreground.G * contribution), ToByte(background.B + foreground.B * contribution), Math.Max(background.A, foreground.A));
    }

    private static Rgba32 BlendSubtract(Rgba32 background, Rgba32 foreground)
    {
        Double contribution = foreground.A / (Double)Byte.MaxValue;
        return new Rgba32(ToByte(background.R - foreground.R * contribution), ToByte(background.G - foreground.G * contribution), ToByte(background.B - foreground.B * contribution), Math.Max(background.A, foreground.A));
    }

    private static Byte BlendChannel(Byte background, Byte foreground, Double sourceAlpha, Double destinationAlpha, Double outputAlpha) => ToByte((foreground * sourceAlpha + background * destinationAlpha * (1.0 - sourceAlpha)) / outputAlpha);

    private static Byte ToByte(Double value) => (Byte)Math.Clamp((Int32)Math.Round(value), Byte.MinValue, Byte.MaxValue);
}
