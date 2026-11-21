using System.Globalization;
using Memoria.FF8.Media;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SkiaSharp;

namespace Memoria.FF8;

internal sealed class MovieSubtitleRaster : IDisposable
{
    private const int MaxTextureDimension = 2048;
    private readonly GraphicsDevice device;
    private readonly SKBitmap bitmap;
    private readonly Texture2D sharp;
    private Texture2D? blurred;
    private SKBitmap? blurBitmap;
    private byte[]? uploadPixels;
    private float blurSigma;

    public MovieSubtitleRaster(GraphicsDevice device, MovieSubtitleCue cue, int viewportHeight)
    {
        this.device = device;
        Color = ParseColor(cue.Style.Color);
        float size = cue.Style.FontSize * viewportHeight;
        using PortableTextShaper shaper = new(size, cue.Style.FontFamily, cue.Style.Bold);
        string[] lines = cue.Text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        float[] advances = lines.Select(line => shaper.Measure(line) * PortableTextShaper.RasterScale).ToArray();
        float maxAdvance = advances.Max();
        float lineHeight = shaper.LineHeight * PortableTextShaper.RasterScale;
        float maxBlur = cue.Keyframes.Select(frame => frame.Blur ?? 0).DefaultIfEmpty().Max();
        float margin = (1 + maxBlur * 3) * size * PortableTextShaper.RasterScale + 2;
        float naturalWidth = maxAdvance + 2 * margin;
        float naturalHeight = lines.Length * lineHeight + 2 * margin;
        float rasterScale = Math.Min(1, MaxTextureDimension / Math.Max(naturalWidth, naturalHeight));
        PixelsPerScreenPixel = rasterScale * PortableTextShaper.RasterScale;
        EmPixels = size * PixelsPerScreenPixel;
        int width = Math.Clamp((int)MathF.Ceiling(naturalWidth * rasterScale), 1, MaxTextureDimension);
        int height = Math.Clamp((int)MathF.Ceiling(naturalHeight * rasterScale), 1, MaxTextureDimension);

        bitmap = new(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        try
        {
            using (SKCanvas canvas = new(bitmap))
            using (SKPaint paint = new() { Color = SKColors.White, IsAntialias = true })
            {
                canvas.Clear(SKColors.Transparent);
                canvas.Scale(rasterScale);
                float baseline = margin + shaper.Baseline * PortableTextShaper.RasterScale;
                for (int index = 0; index < lines.Length; index++)
                {
                    shaper.Draw(canvas, lines[index], margin + (maxAdvance - advances[index]) / 2, baseline + index * lineHeight, paint);
                }
            }
            Span<byte> pixels = bitmap.GetPixelSpan();
            Ink = FindInk(pixels, width, height, bitmap.RowBytes);
            sharp = Upload(width, height, pixels.ToArray());
        }
        catch
        {
            bitmap.Dispose();
            throw;
        }
    }

    public float PixelsPerScreenPixel { get; }
    public float EmPixels { get; }
    public Color Color { get; }
    public Rectangle Ink { get; }

    public Texture2D GetTexture(float blur, out int padding)
    {
        // Reuse the same raster across redraws and subpixel changes in blur radius.
        float sigma = MathF.Round(blur * EmPixels * 2) / 2;
        padding = (int)MathF.Ceiling(3 * sigma);
        if (sigma == 0)
        {
            ReleaseBlur();
            return sharp;
        }
        if (blurred is null || sigma != blurSigma)
        {
            blurBitmap ??= new(bitmap.Info);
            using SKCanvas canvas = new(blurBitmap);
            using SKImageFilter filter = SKImageFilter.CreateBlur(sigma, sigma);
            using SKPaint paint = new() { ImageFilter = filter };
            canvas.Clear(SKColors.Transparent);
            canvas.DrawBitmap(bitmap, 0, 0, paint);
            canvas.Flush();
            Span<byte> pixels = blurBitmap.GetPixelSpan();
            uploadPixels ??= new byte[pixels.Length];
            pixels.CopyTo(uploadPixels);
            if (blurred is null)
            {
                blurred = Upload(bitmap.Width, bitmap.Height, uploadPixels);
            }
            else
            {
                blurred.SetData(uploadPixels);
            }
            blurSigma = sigma;
        }
        return blurred;
    }

    private Texture2D Upload(int width, int height, byte[] pixels)
    {
        Texture2D texture = new(device, width, height);
        try
        {
            texture.SetData(pixels);
            return texture;
        }
        catch
        {
            texture.Dispose();
            throw;
        }
    }

    private static Rectangle FindInk(ReadOnlySpan<byte> pixels, int width, int height, int stride)
    {
        int left = width;
        int right = 0;
        int top = height;
        int bottom = 0;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (pixels[y * stride + x * 4 + 3] == 0)
                {
                    continue;
                }
                left = Math.Min(left, x);
                right = Math.Max(right, x + 1);
                top = Math.Min(top, y);
                bottom = Math.Max(bottom, y + 1);
            }
        }
        return right > left ? new(left, top, right - left, bottom - top) : Rectangle.Empty;
    }

    private static Color ParseColor(string value)
    {
        uint rgba = uint.Parse(value.AsSpan(1), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
        if (value.Length == 7)
        {
            rgba = (rgba << 8) | 255u;
        }
        return new((byte)(rgba >> 24), (byte)(rgba >> 16), (byte)(rgba >> 8), (byte)rgba);
    }

    public void Dispose()
    {
        ReleaseBlur();
        sharp.Dispose();
        bitmap.Dispose();
    }

    private void ReleaseBlur()
    {
        blurred?.Dispose();
        blurBitmap?.Dispose();
        blurred = null;
        blurBitmap = null;
        uploadPixels = null;
    }
}
