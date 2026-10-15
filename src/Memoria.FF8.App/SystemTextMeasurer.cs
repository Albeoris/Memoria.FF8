using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace Memoria.FF8;

/// <summary>Retains the WPF shaping typeface and candidate advances for one layout operation.</summary>
internal sealed class SystemTextMeasurer : IDisposable
{
    private readonly Typeface typeface;
    private readonly double rasterSize;
    private readonly Dictionary<string, float> advances = new(StringComparer.Ordinal);

    internal SystemTextMeasurer(float size, string? fontFamily)
    {
        rasterSize = size * SystemTextLayout.RasterScale;
        // The composite fallback supplies script and supplementary-plane fonts while
        // keeping the selected installed family first for the glyphs it provides.
        var family = new FontFamily((fontFamily ?? SystemFontCatalog.DefaultFamily) + ", Global User Interface");
        typeface = new Typeface(family, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        FormattedText metrics = Format("Mg");
        LineHeight = (float)metrics.Height / SystemTextLayout.RasterScale;
        Baseline = (float)metrics.Baseline / SystemTextLayout.RasterScale;
    }

    public float LineHeight { get; }
    internal float Baseline { get; }

    internal FormattedText Format(string value)
    {
        return new FormattedText(value, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, typeface, rasterSize, Brushes.White, 1);
    }

    public float Measure(string value)
    {
        if (!advances.TryGetValue(value, out float width))
        {
            width = (float)Format(value).WidthIncludingTrailingWhitespace / SystemTextLayout.RasterScale;
            advances.Add(value, width);
        }
        return width;
    }

    public void Dispose()
    {
        advances.Clear();
    }
}
