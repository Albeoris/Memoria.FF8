using System.Drawing;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.GameUi;

/// <summary>Bakes the native card layers and system-font ranks into one animation texture.</summary>
internal sealed class CardFaceComposer(string fontFamily)
{
    public const int Size = 256;
    private const int Scale = Size / 64;
    private readonly Dictionary<int, PixelImage> ranks = [];

    public PixelImage Compose(PixelImage artwork, TripleTriadCard card, int owner, PixelImage? element)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(owner);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(owner, 1);
        PixelImage result = new(Size, Size);
        // Native 53A512 fills C75AD0's inset square before drawing the transparent card artwork.
        Rgba32 top = owner == 0 ? new(192, 224, 255) : new(255, 192, 224);
        Rgba32 bottom = owner == 0 ? new(48, 64, 128) : new(128, 48, 64);
        for (int y = Scale; y < Size - Scale; y++)
        {
            float amount = (y - Scale) / (float)(Size - 2 * Scale - 1);
            Rgba32 color = new((byte)(top.R + (bottom.R - top.R) * amount), (byte)(top.G + (bottom.G - top.G) * amount), (byte)(top.B + (bottom.B - top.B) * amount));
            for (int x = Scale; x < Size - Scale; x++)
            {
                result[x, y] = color;
            }
        }
        Overlay(result, artwork, 0, 0, Size, Size);
        // Native 53A100: C75B10 rank quads, in top/bottom/left/right order.
        Rank(card.Top, 6, 2);
        Rank(card.Bottom, 6, 18);
        Rank(card.Left, 2, 10);
        Rank(card.Right, 10, 10);
        if (element is not null)
        {
            Overlay(result, element, 44 * Scale, 4 * Scale, 16 * Scale, 16 * Scale);
        }
        return result;

        void Rank(int value, int x, int y)
        {
            if (!ranks.TryGetValue(value, out PixelImage? rank))
            {
                rank = RasterizeRank(value);
                ranks.Add(value, rank);
            }
            Overlay(result, rank, x * Scale, y * Scale, 12 * Scale, 12 * Scale);
        }
    }

    private PixelImage RasterizeRank(int value)
    {
        using Bitmap mask = SystemTextLayout.Rasterize(value == 10 ? "A" : value.ToString(System.Globalization.CultureInfo.InvariantCulture), 32, float.PositiveInfinity, fontFamily);
        PixelImage rank = new(12 * Scale, 12 * Scale);
        int height = 9 * Scale;
        int width = Math.Min(8 * Scale, Math.Max(1, (int)MathF.Round(mask.Width * height / (float)mask.Height)));
        int left = (rank.Width - width) / 2;
        int top = (rank.Height - height) / 2;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                byte alpha = mask.GetPixel(x * mask.Width / width, y * mask.Height / height).A;
                if (alpha == 0)
                {
                    continue;
                }
                for (int outlineY = -2; outlineY <= 2; outlineY++)
                {
                    for (int outlineX = -2; outlineX <= 2; outlineX++)
                    {
                        Blend(rank, left + x + outlineX + 1, top + y + outlineY + 1, new Rgba32(45, 27, 10, alpha));
                    }
                }
            }
        }
        for (int y = 0; y < height; y++)
        {
            float amount = y / (float)(height - 1);
            Rgba32 color = new((byte)(255 - 24 * amount), (byte)(251 - 77 * amount), (byte)(220 - 128 * amount));
            for (int x = 0; x < width; x++)
            {
                byte alpha = mask.GetPixel(x * mask.Width / width, y * mask.Height / height).A;
                Blend(rank, left + x, top + y, color with { A = alpha });
            }
        }
        return rank;
    }

    private static void Overlay(PixelImage target, PixelImage source, int left, int top, int width, int height)
    {
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Blend(target, left + x, top + y, source[x * source.Width / width, y * source.Height / height]);
            }
        }
    }

    private static void Blend(PixelImage image, int x, int y, Rgba32 source)
    {
        if (source.A == 0 || (uint)x >= image.Width || (uint)y >= image.Height)
        {
            return;
        }
        Rgba32 destination = image[x, y];
        int alpha = source.A * 255 + destination.A * (255 - source.A);
        image[x, y] = new Rgba32((byte)((source.R * source.A * 255 + destination.R * destination.A * (255 - source.A)) / alpha), (byte)((source.G * source.A * 255 + destination.G * destination.A * (255 - source.A)) / alpha), (byte)((source.B * source.A * 255 + destination.B * destination.A * (255 - source.A)) / alpha), (byte)((alpha + 127) / 255));
    }
}
