using SkiaSharp;
using System.Globalization;
using System.Xml.Linq;

namespace Memoria.FF8;

/// <summary>Rasterizes the embedded, original SVG prompt geometry at the requested display resolution.</summary>
internal static class ControllerVectorAtlas
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<int, XElement>> Atlases = new Dictionary<string, IReadOnlyDictionary<int, XElement>>
    {
        ["sony"] = Load("sony"),
        ["xbox"] = Load("xbox")
    };

    internal static bool Supports(int icon, string presentation) => Atlases.TryGetValue(presentation, out var atlas) && atlas.ContainsKey(icon);

    internal static SKBitmap Render(int icon, string presentation, int size)
    {
        if (!Supports(icon, presentation))
        {
            throw new ArgumentOutOfRangeException(nameof(icon), "No vector artwork exists for this presentation and physical button.");
        }
        SKBitmap bitmap = new(Math.Max(1, size), Math.Max(1, size), SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using SKCanvas graphics = new(bitmap);
        graphics.Clear(SKColors.Transparent);
        graphics.Scale(bitmap.Width / 64f, bitmap.Height / 64f);
        foreach (XElement shape in Atlases[presentation][icon].Elements())
        {
            Draw(graphics, shape);
        }
        return bitmap;
    }

    private static IReadOnlyDictionary<int, XElement> Load(string family)
    {
        using Stream stream = typeof(ControllerVectorAtlas).Assembly.GetManifestResourceStream($"Memoria.FF8.Artwork.Controller.{family}.svg") ?? throw new InvalidOperationException("Embedded controller vector atlas is missing.");
        return XDocument.Load(stream).Root!.Elements().Where(element => element.Name.LocalName == "g").ToDictionary(element => int.Parse(element.Attribute("id")!.Value.AsSpan(7), CultureInfo.InvariantCulture));
    }

    private static void Draw(SKCanvas graphics, XElement shape)
    {
        float Number(string name, float fallback = 0) => float.TryParse(shape.Attribute(name)?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float value) ? value : fallback;
        using SKPath path = new();
        switch (shape.Name.LocalName)
        {
            case "circle":
                float radius = Number("r");
                path.AddCircle(Number("cx"), Number("cy"), radius);
                break;
            case "rect":
                float x = Number("x");
                float y = Number("y");
                float width = Number("width");
                float height = Number("height");
                float diameter = Number("rx") * 2;
                path.AddRoundRect(SKRect.Create(x, y, width, height), diameter / 2, diameter / 2);
                break;
            case "polygon":
            case "polyline":
                SKPoint[] points = shape.Attribute("points")!.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(pair => pair.Split(',')).Select(pair => new SKPoint(float.Parse(pair[0], CultureInfo.InvariantCulture), float.Parse(pair[1], CultureInfo.InvariantCulture))).ToArray();
                path.AddPoly(points, false);
                if (shape.Name.LocalName == "polygon")
                {
                    path.Close();
                }
                break;
            default:
                throw new InvalidOperationException($"Unsupported controller SVG geometry: {shape.Name.LocalName}.");
        }
        string fill = shape.Attribute("fill")?.Value ?? "none";
        if (fill != "none")
        {
            using SKPaint brush = new() { Color = SKColor.Parse(fill), IsAntialias = true };
            graphics.DrawPath(path, brush);
        }
        string stroke = shape.Attribute("stroke")?.Value ?? "none";
        if (stroke != "none")
        {
            using SKPaint pen = new() { Color = SKColor.Parse(stroke), StrokeWidth = Number("stroke-width", 1), Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Round, StrokeJoin = SKStrokeJoin.Round, IsAntialias = true };
            graphics.DrawPath(path, pen);
        }
    }
}
