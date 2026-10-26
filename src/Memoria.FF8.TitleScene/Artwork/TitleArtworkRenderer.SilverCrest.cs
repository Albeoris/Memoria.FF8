using System.Text.Json;
using SkiaSharp;

namespace Memoria.FF8.TitleScene;

public sealed partial class TitleArtworkRenderer
{
    private static SKPath ReadContours(JsonElement contours)
    {
        const double scale = 381.0 / 1215.0;
        SKPath geometry = new() { FillType = SKPathFillType.EvenOdd };
        foreach (JsonElement contour in contours.EnumerateArray())
        {
            bool first = true;
            foreach (JsonElement coordinate in contour.EnumerateArray())
            {
                SKPoint point = new((float)(800 + (coordinate[0].GetDouble() - 620) * scale), (float)(72 + (coordinate[1].GetDouble() - 19) * scale));
                if (first)
                {
                    geometry.MoveTo(point);
                    first = false;
                }
                else
                {
                    geometry.LineTo(point);
                }
            }
            geometry.Close();
        }
        return geometry;
    }
    /// <summary>Rasterizes the authored silver contours without a graphics device or window system.</summary>
    public static SKBitmap CreateSilverCrestImage(int height = 256)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 3);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(height, 2048);
        using Stream stream = typeof(TitleArtworkRenderer).Assembly.GetManifestResourceStream("Memoria.FF8.TitleScene.SilverCrest.contours.json") ?? throw new InvalidOperationException("The embedded silver crest vector contours are missing.");
        using JsonDocument document = JsonDocument.Parse(stream);
        JsonElement root = document.RootElement;
        using SKPath silhouette = ReadContours(root.GetProperty("silhouette"));
        SKRect bounds = silhouette.Bounds;
        float scale = (height - 2f) / bounds.Height;
        int width = Math.Max(1, (int)MathF.Ceiling(bounds.Width * scale) + 2);
        SKBitmap image = new(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using SKCanvas canvas = new(image);
        canvas.Clear(SKColors.Transparent);
        canvas.Translate(1 - bounds.Left * scale, 1 - bounds.Top * scale);
        canvas.Scale(scale);
        using SKPaint paint = new() { IsAntialias = true, Color = new SKColor(16, 17, 19) };
        canvas.DrawPath(silhouette, paint);
        foreach (JsonElement layer in root.GetProperty("layers").EnumerateArray())
        {
            int value = layer.GetProperty("value").GetInt32();
            paint.Color = new SKColor((byte)Math.Max(0, value - 3), (byte)Math.Max(0, value - 1), (byte)value);
            using SKPath contours = ReadContours(layer.GetProperty("contours"));
            canvas.DrawPath(contours, paint);
        }
        paint.Color = new SKColor(230, 235, 242, 80);
        paint.Style = SKPaintStyle.Stroke;
        paint.StrokeWidth = 0.22f;
        canvas.DrawPath(silhouette, paint);
        return image;
    }
}
