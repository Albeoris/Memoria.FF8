using Albeoris.Games.FF8.Imaging;
using SkiaSharp;

namespace Memoria.FF8.GameUi;

/// <summary>System-font timer glyphs rasterized above HUD resolution for the shared image compositor.</summary>
internal static class SystemTimerDigitArtwork
{
    internal static PixelImage Render(int digit)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(digit);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(digit, 9);
        const int rasterScale = 4;
        using SKBitmap bitmap = new(64 * rasterScale, 128 * rasterScale, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using SKCanvas canvas = new(bitmap);
        canvas.Clear(SKColors.Transparent);
        canvas.Scale(rasterScale);
        using SKTypeface typeface = SKTypeface.FromFamilyName(SystemFontCatalog.ResolveFamily("Segoe UI"), SKFontStyle.Bold);
        using SKFont font = new(typeface, 104);
        float widest = 0;
        for (int value = 0; value < 10; value++)
        {
            using SKPath glyph = font.GetGlyphPath(font.GetGlyph('0' + value)) ?? throw new InvalidOperationException("The system font does not provide a timer digit outline.");
            widest = Math.Max(widest, glyph.Bounds.Width);
        }
        // Keep the full outline inside every cell, including wider bold digits and fallback fonts.
        font.ScaleX = Math.Min(1, (64 - 2 * (5 + 1)) / widest);
        using SKPath path = font.GetGlyphPath(font.GetGlyph('0' + digit)) ?? throw new InvalidOperationException("The system font does not provide a timer digit outline.");
        path.Transform(SKMatrix.CreateTranslation(32 - path.Bounds.MidX, 86));
        // A five-unit outline radius on the 128-unit cell scales with each timer digit.
        using SKPaint outline = new() { Color = new SKColor(15, 15, 15), Style = SKPaintStyle.Stroke, StrokeWidth = 10, StrokeJoin = SKStrokeJoin.Round, IsAntialias = true };
        canvas.DrawPath(path, outline);
        using SKShader gradient = SKShader.CreateLinearGradient(new(0, 0), new(0, 128 * 0.72f), [new SKColor(251, 251, 251), new SKColor(215, 215, 215)], SKShaderTileMode.Clamp);
        using SKPaint fill = new() { Shader = gradient, IsAntialias = true };
        canvas.DrawPath(path, fill);
        SKColor[] pixels = bitmap.Pixels;
        PixelImage image = new(bitmap.Width, bitmap.Height);
        for (int index = 0; index < pixels.Length; index++)
        {
            SKColor color = pixels[index];
            image.Pixels[index] = new Rgba32(color.Red, color.Green, color.Blue, color.Alpha);
        }
        return image;
    }
}
