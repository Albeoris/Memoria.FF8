using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Xml.Linq;

namespace Memoria.FF8;

/// <summary>Rasterizes the embedded, original SVG prompt geometry at the requested display resolution.</summary>
internal static class ControllerVectorArtwork
{
    private static readonly IReadOnlyDictionary<ControllerPresentation, IReadOnlyDictionary<int, XElement>> Atlases = new Dictionary<ControllerPresentation, IReadOnlyDictionary<int, XElement>>
    {
        [ControllerPresentation.PlayStation] = Load("sony"),
        [ControllerPresentation.Xbox] = Load("xbox")
    };

    internal static bool Supports(int icon, ControllerPresentation presentation) => Atlases.TryGetValue(presentation, out var atlas) && atlas.ContainsKey(icon);

    internal static Bitmap Render(int icon, ControllerPresentation presentation, int size)
    {
        if (!Supports(icon, presentation))
        {
            throw new ArgumentOutOfRangeException(nameof(icon), "No vector artwork exists for this presentation and physical button.");
        }
        Bitmap bitmap = new(Math.Max(1, size), Math.Max(1, size), System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using Graphics graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.ScaleTransform(bitmap.Width / 64f, bitmap.Height / 64f);
        foreach (XElement shape in Atlases[presentation][icon].Elements())
        {
            Draw(graphics, shape);
        }
        return bitmap;
    }

    private static IReadOnlyDictionary<int, XElement> Load(string family)
    {
        using Stream stream = typeof(ControllerVectorArtwork).Assembly.GetManifestResourceStream($"Memoria.FF8.Artwork.Controller.{family}.svg") ?? throw new InvalidOperationException("Embedded controller vector atlas is missing.");
        return XDocument.Load(stream).Root!.Elements().Where(element => element.Name.LocalName == "g").ToDictionary(element => int.Parse(element.Attribute("id")!.Value.AsSpan(7), CultureInfo.InvariantCulture));
    }

    private static void Draw(Graphics graphics, XElement shape)
    {
        float Number(string name, float fallback = 0) => float.TryParse(shape.Attribute(name)?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float value) ? value : fallback;
        using GraphicsPath path = new();
        switch (shape.Name.LocalName)
        {
            case "circle":
                float radius = Number("r");
                path.AddEllipse(Number("cx") - radius, Number("cy") - radius, radius * 2, radius * 2);
                break;
            case "rect":
                float x = Number("x");
                float y = Number("y");
                float width = Number("width");
                float height = Number("height");
                float diameter = Number("rx") * 2;
                if (diameter > 0)
                {
                    path.AddArc(x, y, diameter, diameter, 180, 90);
                    path.AddArc(x + width - diameter, y, diameter, diameter, 270, 90);
                    path.AddArc(x + width - diameter, y + height - diameter, diameter, diameter, 0, 90);
                    path.AddArc(x, y + height - diameter, diameter, diameter, 90, 90);
                    path.CloseFigure();
                }
                else
                {
                    path.AddRectangle(new RectangleF(x, y, width, height));
                }
                break;
            case "polygon":
            case "polyline":
                PointF[] points = shape.Attribute("points")!.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(pair => pair.Split(',')).Select(pair => new PointF(float.Parse(pair[0], CultureInfo.InvariantCulture), float.Parse(pair[1], CultureInfo.InvariantCulture))).ToArray();
                path.AddLines(points);
                if (shape.Name.LocalName == "polygon")
                {
                    path.CloseFigure();
                }
                break;
            default:
                throw new InvalidOperationException($"Unsupported controller SVG geometry: {shape.Name.LocalName}.");
        }
        string fill = shape.Attribute("fill")?.Value ?? "none";
        if (fill != "none")
        {
            using SolidBrush brush = new(ColorTranslator.FromHtml(fill));
            graphics.FillPath(brush, path);
        }
        string stroke = shape.Attribute("stroke")?.Value ?? "none";
        if (stroke != "none")
        {
            using Pen pen = new(ColorTranslator.FromHtml(stroke), Number("stroke-width", 1));
            pen.StartCap = pen.EndCap = LineCap.Round;
            pen.LineJoin = LineJoin.Round;
            graphics.DrawPath(pen, path);
        }
    }
}
