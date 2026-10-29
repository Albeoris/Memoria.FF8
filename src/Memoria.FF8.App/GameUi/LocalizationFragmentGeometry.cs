using Memoria.FF8.Resources.Localization;
using Memoria.FF8.Resources.Text;
using Memoria.FF8.Ui;
using Microsoft.Xna.Framework;
using SkiaSharp;

namespace Memoria.FF8.GameUi;

internal sealed record LocalizationTextHit(LocalizationTextBinding Binding, UiRect Bounds, float? Size = null, string? FontFamily = null, float? MaximumWidth = null, string? NativeProfileId = null, NativeTextContext? NativeContext = null);

/// <summary>Projects source-owned shaped clusters, never matching a displayed word back to a resource.</summary>
internal static class LocalizationFragmentGeometry
{
    public static IReadOnlyList<LocalizationTextHit> Map(LocalizationTextBinding binding, IReadOnlyList<PortableTextCluster> clusters, Vector2 origin, float scale, UiRect clip)
    {
        List<LocalizationTextFragment> leaves = [];
        int cursor = 0;
        foreach (LocalizationTextFragment fragment in binding.Fragments.OrderBy(fragment => fragment.Start))
        {
            if (fragment.Start > cursor && binding.Key.Length != 0)
            {
                leaves.Add(new(cursor, fragment.Start - cursor, binding));
            }
            if (fragment.Length > 0 && fragment.Binding.Key.Length != 0)
            {
                leaves.Add(fragment);
            }
            cursor = Math.Max(cursor, fragment.Start + fragment.Length);
        }
        if (cursor < binding.Text.Length && binding.Key.Length != 0)
        {
            leaves.Add(new(cursor, binding.Text.Length - cursor, binding));
        }
        List<LocalizationTextHit> hits = [];
        foreach (var line in clusters.GroupBy(cluster => cluster.LineIndex))
        {
            int previousOwner = -1;
            UiRect combined = default;
            void Finish()
            {
                if (previousOwner >= 0 && !combined.IsEmpty)
                {
                    hits.Add(new(leaves[previousOwner].Binding, combined));
                }
            }
            foreach (PortableTextCluster cluster in line.OrderBy(cluster => cluster.Bounds.Left))
            {
                int[] owners = Enumerable.Range(0, leaves.Count).Where(index => cluster.SourcePositions.Any(position => position >= leaves[index].Start && position < leaves[index].Start + leaves[index].Length)).ToArray();
                UiRect rectangle = new(origin.X + cluster.Bounds.Left * scale, origin.Y + cluster.Bounds.Top * scale, cluster.Bounds.Width * scale, cluster.Bounds.Height * scale);
                rectangle = rectangle.Intersect(clip);
                if (rectangle.IsEmpty || owners.Length == 0)
                {
                    Finish();
                    previousOwner = -1;
                    continue;
                }
                foreach (int owner in owners)
                {
                    if (owner != previousOwner)
                    {
                        Finish();
                        previousOwner = owner;
                        combined = rectangle;
                    }
                    else
                    {
                        float left = Math.Min(combined.X, rectangle.X);
                        float top = Math.Min(combined.Y, rectangle.Y);
                        combined = new(left, top, Math.Max(combined.Right, rectangle.Right) - left, Math.Max(combined.Bottom, rectangle.Bottom) - top);
                    }
                }
            }
            Finish();
        }
        return hits;
    }

    public static IReadOnlyList<PortableTextCluster> NativeClusters(NativeFontLayoutResult layout)
    {
        Dictionary<(int Page, float Y), int> lines = [];
        return layout.Glyphs.Where(glyph => glyph.Page == 0).Select(glyph =>
        {
            var line = (glyph.Page, glyph.Y - glyph.Image.BearingY);
            if (!lines.TryGetValue(line, out int index))
            {
                index = lines.Count;
                lines.Add(line, index);
            }
            return new PortableTextCluster(Enumerable.Range(glyph.TextPosition, glyph.TextLength).ToArray(), new SKRect(glyph.X, glyph.Y, glyph.X + glyph.Image.Width, glyph.Y + glyph.Image.Height), index);
        }).ToArray();
    }

    public static IReadOnlyList<PortableTextCluster> ClippedNativeClusters(NativeFontLayoutResult layout, Vector2 origin, float scale, Rectangle clip)
    {
        IReadOnlyList<PortableTextCluster> source = NativeClusters(layout);
        NativeGlyphRun[] glyphs = layout.Glyphs.Where(glyph => glyph.Page == 0).ToArray();
        List<PortableTextCluster> result = [];
        for (int index = 0; index < source.Count; index++)
        {
            PortableTextCluster cluster = source[index];
            NativeGlyphRun glyph = glyphs[index];
            Rectangle destination = new((int)MathF.Round(origin.X + glyph.X * scale), (int)MathF.Round(origin.Y + glyph.Y * scale), (int)MathF.Ceiling(glyph.Image.Width * scale), (int)MathF.Ceiling(glyph.Image.Height * scale));
            if (NativeGlyphClipping.Clip(destination, new(glyph.Image.X, glyph.Image.Y, glyph.Image.Width, glyph.Image.Height), clip) is { } placement)
            {
                result.Add(cluster with { Bounds = new(placement.Position.X, placement.Position.Y, placement.Position.X + placement.Source.Width * placement.Scale.X, placement.Position.Y + placement.Source.Height * placement.Scale.Y) });
            }
        }
        return result;
    }

    public static UiRect SystemRasterClip(Vector2 position, int pixelWidth, int pixelHeight, Rectangle clip)
    {
        const float scale = 1f / SystemTextLayout.RasterScale;
        int left = Math.Clamp((int)MathF.Ceiling((clip.Left - position.X) / scale), 0, pixelWidth);
        int top = Math.Clamp((int)MathF.Ceiling((clip.Top - position.Y) / scale), 0, pixelHeight);
        int right = Math.Clamp((int)MathF.Floor((clip.Right - position.X) / scale), left, pixelWidth);
        int bottom = Math.Clamp((int)MathF.Floor((clip.Bottom - position.Y) / scale), top, pixelHeight);
        return new(position.X + left * scale, position.Y + top * scale, (right - left) * scale, (bottom - top) * scale);
    }
}
