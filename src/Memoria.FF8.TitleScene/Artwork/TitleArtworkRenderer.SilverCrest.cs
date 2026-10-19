using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Memoria.FF8.TitleScene;

public sealed partial class TitleArtworkRenderer
{
    /// <summary>Rasterizes the title's authored silver contours for small interface emblems without a graphics device.</summary>
    public static BitmapSource CreateSilverCrestImage(int height = 256)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 3);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(height, 2048);
        using Stream stream = typeof(TitleArtworkRenderer).Assembly.GetManifestResourceStream("Memoria.FF8.TitleScene.SilverCrest.contours.json") ?? throw new InvalidOperationException("The embedded silver crest vector contours are missing.");
        using JsonDocument document = JsonDocument.Parse(stream);
        JsonElement root = document.RootElement;
        Geometry silhouette = ReadContours(root.GetProperty("silhouette"));
        Rect bounds = silhouette.Bounds;
        double scale = (height - 2.0) / bounds.Height;
        int width = Math.Max(1, (int)Math.Ceiling(bounds.Width * scale) + 2);
        DrawingVisual drawing = new();
        using (DrawingContext context = drawing.RenderOpen())
        {
            context.PushTransform(new TranslateTransform(1 - bounds.X * scale, 1 - bounds.Y * scale));
            context.PushTransform(new ScaleTransform(scale, scale));
            context.DrawGeometry(new SolidColorBrush(Color.FromRgb(16, 17, 19)), null, silhouette);
            foreach (JsonElement layer in root.GetProperty("layers").EnumerateArray())
            {
                int value = layer.GetProperty("value").GetInt32();
                SolidColorBrush silver = new(Color.FromRgb((byte)Math.Max(0, value - 3), (byte)Math.Max(0, value - 1), (byte)value));
                context.DrawGeometry(silver, null, ReadContours(layer.GetProperty("contours")));
            }
            context.DrawGeometry(null, new Pen(new SolidColorBrush(Color.FromArgb(80, 230, 235, 242)), 0.22), silhouette);
            context.Pop();
            context.Pop();
        }
        RenderTargetBitmap rendered = new(width, height, 96, 96, PixelFormats.Pbgra32);
        rendered.Render(drawing);
        FormatConvertedBitmap image = new(rendered, PixelFormats.Bgra32, null, 0);
        image.Freeze();
        return image;
    }
}
