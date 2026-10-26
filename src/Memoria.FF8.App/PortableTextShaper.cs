using System.Globalization;
using System.Text;
using SkiaSharp;
using SkiaSharp.HarfBuzz;
using Memoria.FF8.Text;
using HarfBuzzSharp;
using HbBuffer = HarfBuzzSharp.Buffer;

namespace Memoria.FF8;

/// <summary>Uses the same HarfBuzz shaping and installed-font fallback for advances and rasterization.</summary>
internal class PortableTextShaper : IDisposable
{
    internal const int RasterScale = 2;
    private readonly SKTypeface typeface;
    private readonly float rasterSize;
    private readonly Dictionary<string, ShapedLine> lines = new(StringComparer.Ordinal);
    private readonly BidiLayout bidi = new();
    private readonly Dictionary<nint, FontRun> fonts = [];
    private readonly string family;

    internal PortableTextShaper(float size, string? fontFamily)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);
        family = SystemFontCatalog.ResolveFamily(fontFamily);
        rasterSize = size * RasterScale;
        typeface = SKTypeface.FromFamilyName(family);
        FontRun primary = Font(typeface);
        SKFontMetrics metrics = primary.Font.Metrics;
        LineHeight = MathF.Ceiling(metrics.Descent - metrics.Ascent + metrics.Leading) / RasterScale;
        Baseline = -metrics.Ascent / RasterScale;
    }

    public float LineHeight { get; }
    internal float Baseline { get; }

    private FontRun Font(SKTypeface face)
    {
        if (!fonts.TryGetValue(face.Handle, out FontRun? result))
        {
            result = new FontRun(face, new SKFont(face, rasterSize) { Edging = SKFontEdging.Antialias, Subpixel = true }, new SKShaper(face));
            fonts.Add(face.Handle, result);
        }
        else if (!ReferenceEquals(face, result.Face))
        {
            face.Dispose();
        }
        return result;
    }

    private ShapedLine Shape(string value)
    {
        if (lines.TryGetValue(value, out ShapedLine? cached))
        {
            return cached;
        }
        int[] codePoints = value.EnumerateRunes().Select(rune => rune.Value).ToArray();
        sbyte[] levels = bidi.ResolveLevels(codePoints);
        List<TextElement> elements = [];
        int codePoint = 0;
        Script previousScript = Script.Common;
        TextElementEnumerator iterator = StringInfo.GetTextElementEnumerator(value);
        while (iterator.MoveNext())
        {
            string text = iterator.GetTextElement();
            FontRun selected = fonts[typeface.Handle];
            Script script = Script.Common;
            foreach (Rune rune in text.EnumerateRunes())
            {
                Script candidate = UnicodeFunctions.Default.GetScript(rune.Value);
                if (candidate != Script.Common && candidate != Script.Inherited && candidate != Script.Unknown)
                {
                    script = candidate;
                }
                if (Rune.GetUnicodeCategory(rune) is UnicodeCategory.Format || rune.Value is >= 0xFE00 and <= 0xFE0F || selected.Font.ContainsGlyphs(rune.ToString()))
                {
                    continue;
                }
                SKTypeface? fallback = SKFontManager.Default.MatchCharacter(family, rune.Value);
                if (fallback is not null)
                {
                    selected = Font(fallback);
                }
            }
            if (script == Script.Common)
            {
                script = previousScript;
            }
            else
            {
                previousScript = script;
            }
            elements.Add(new(text, selected, script, levels[codePoint]));
            codePoint += text.EnumerateRunes().Count();
        }
        // Leading neutral elements take the following script. Inherited marks
        // remain in their extended grapheme and can never split their base.
        Script followingScript = Script.Common;
        for (int index = elements.Count - 1; index >= 0; index--)
        {
            TextElement element = elements[index];
            if (element.Script == Script.Common)
            {
                elements[index] = element with { Script = followingScript };
            }
            else
            {
                followingScript = element.Script;
            }
        }
        List<LogicalRun> runs = [];
        foreach (TextElement element in elements)
        {
            if (runs.Count > 0 && runs[^1].Font == element.Font && runs[^1].Script == element.Script && runs[^1].Level == element.Level)
            {
                runs[^1].Text.Append(element.Text);
            }
            else
            {
                runs.Add(new(new StringBuilder(element.Text), element.Font, element.Script, element.Level));
            }
        }
        // UAX #9 L2: reverse contiguous sequences from the highest embedding
        // level down to the lowest odd level. HarfBuzz orders glyphs within runs.
        int minimumOdd = runs.Where(run => (run.Level & 1) != 0).Select(run => (int)run.Level).DefaultIfEmpty(int.MaxValue).Min();
        int maximum = runs.Select(run => (int)run.Level).DefaultIfEmpty(0).Max();
        for (int level = maximum; level >= minimumOdd; level--)
        {
            for (int index = 0; index < runs.Count; index++)
            {
                if (runs[index].Level < level)
                {
                    continue;
                }
                int first = index;
                while (index + 1 < runs.Count && runs[index + 1].Level >= level)
                {
                    index++;
                }
                runs.Reverse(first, index - first + 1);
            }
        }
        List<ShapedRun> shaped = [];
        float width = 0;
        foreach (LogicalRun run in runs)
        {
            string text = run.Text.ToString();
            using HbBuffer buffer = new();
            buffer.AddUtf16(text);
            buffer.Direction = (run.Level & 1) == 0 ? Direction.LeftToRight : Direction.RightToLeft;
            buffer.Script = run.Script;
            buffer.GuessSegmentProperties();
            SKShaper.Result result = run.Font.Shaper.Shape(buffer, run.Font.Font);
            SKTextBlob? blob = null;
            if (result.Codepoints.Length > 0)
            {
                using SKTextBlobBuilder builder = new();
                SKPositionedRunBuffer positioned = builder.AllocatePositionedRun(run.Font.Font, result.Codepoints.Length);
                for (int index = 0; index < result.Codepoints.Length; index++)
                {
                    positioned.Glyphs[index] = checked((ushort)result.Codepoints[index]);
                    positioned.Positions[index] = result.Points[index];
                }
                blob = builder.Build();
            }
            shaped.Add(new(blob, width, text, run.Level, run.Script));
            width += result.Width;
        }
        ShapedLine line = new(shaped.ToArray(), width / RasterScale);
        lines.Add(value, line);
        return line;
    }

    internal (string Text, bool RightToLeft)[] VisualRuns(string value)
    {
        return Shape(value).Runs.Select(run => (run.Text, (run.Level & 1) != 0)).ToArray();
    }

    internal void Draw(SKCanvas canvas, string value, float x, float baseline, SKPaint paint)
    {
        foreach (ShapedRun run in Shape(value).Runs)
        {
            if (run.Blob is not null)
            {
                canvas.DrawText(run.Blob, x + run.X, baseline, paint);
            }
        }
    }

    public virtual float Measure(string value)
    {
        return Shape(value).Width;
    }

    public void Dispose()
    {
        foreach (FontRun run in fonts.Values)
        {
            run.Shaper.Dispose();
            run.Font.Dispose();
            run.Face.Dispose();
        }
        fonts.Clear();
        foreach (ShapedLine line in lines.Values)
        {
            foreach (ShapedRun run in line.Runs)
            {
                run.Blob?.Dispose();
            }
        }
        lines.Clear();
    }

    private sealed record TextElement(string Text, FontRun Font, Script Script, sbyte Level);
    private sealed record LogicalRun(StringBuilder Text, FontRun Font, Script Script, sbyte Level);
    private sealed record ShapedRun(SKTextBlob? Blob, float X, string Text, sbyte Level, Script Script);
    private sealed record ShapedLine(ShapedRun[] Runs, float Width);
    private sealed record FontRun(SKTypeface Face, SKFont Font, SKShaper Shaper);
}
