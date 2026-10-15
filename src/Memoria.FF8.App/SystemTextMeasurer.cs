using System.Drawing;
using System.Drawing.Text;

namespace Memoria.FF8;

/// <summary>Retains OS shaping resources and candidate advances for one layout operation.</summary>
internal sealed class SystemTextMeasurer : IDisposable
{
    private readonly Font font;
    private readonly Bitmap bitmap;
    private readonly Graphics graphics;
    private readonly StringFormat format;
    private readonly Dictionary<string, float> advances = new(StringComparer.Ordinal);

    internal SystemTextMeasurer(float size, string? fontFamily)
    {
        font = SystemTextLayout.CreateFont(size, fontFamily);
        bitmap = new Bitmap(1, 1);
        graphics = Graphics.FromImage(bitmap);
        graphics.TextRenderingHint = TextRenderingHint.AntiAlias;
        format = SystemTextLayout.CreateFormat();
        LineHeight = font.GetHeight(graphics) / SystemTextLayout.RasterScale;
    }

    public float LineHeight { get; }

    public float Measure(string value)
    {
        if (!advances.TryGetValue(value, out float width))
        {
            width = graphics.MeasureString(value, font, PointF.Empty, format).Width / SystemTextLayout.RasterScale;
            advances.Add(value, width);
        }
        return width;
    }

    public void Dispose()
    {
        format.Dispose();
        graphics.Dispose();
        bitmap.Dispose();
        font.Dispose();
    }
}
