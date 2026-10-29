using System.Globalization;
using System.Runtime.CompilerServices;

namespace Memoria.FF8.Resources.Text;

/// <summary>Shared bitmap lookup and placement for game and editor adapters. Semantic substitutions happen before this call.</summary>
public static class NativeFontLayout
{
    private sealed class Compiled(NativeFontProfile profile)
    {
        public IReadOnlyList<NativeTextDiagnostic> Diagnostics { get; } = NativeFontProfileValidation.Validate(profile);
        public Dictionary<int, NativeGlyphImage> Images { get; } = (profile.Images ?? []).Where(image => image is not null).GroupBy(image => image.Index).ToDictionary(group => group.Key, group => group.First());
        public Dictionary<char, NativeGlyphMapping[]> Mappings { get; } = (profile.Glyphs ?? []).Where(mapping => mapping is not null && !string.IsNullOrEmpty(mapping.Text)).GroupBy(mapping => mapping.Text[0]).ToDictionary(group => group.Key, group => group.OrderByDescending(mapping => mapping.Text.Length).ThenByDescending(mapping => mapping.Field is not null).ToArray());
    }

    // Profiles are immutable generation values; reload replaces the profile object, retaining stable image identities.
    private static readonly ConditionalWeakTable<NativeFontProfile, Compiled> Cache = new();

    public static NativeFontLayoutResult Layout(NativeFontProfile profile, string text, NativeTextContext context, float maximumWidth = float.PositiveInfinity, float maximumHeight = float.PositiveInfinity)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(context);
        Compiled compiled = Cache.GetValue(profile, static value => new(value));
        List<NativeTextDiagnostic> diagnostics = [.. compiled.Diagnostics];
        if (diagnostics.Count != 0)
        {
            return new([], 0, 0, 1, diagnostics);
        }
        if (profile.Domains.Count != 0 && !profile.Domains.Any(domain => domain.Matches(context)))
        {
            return new([], 0, 0, 1, [new("domain", "The selected profile does not support this resource context.")]);
        }
        Dictionary<int, NativeGlyphImage> images = compiled.Images;
        List<NativeGlyphRun> runs = [];
        float x = 0;
        float y = 0;
        float width = 0;
        float height = profile.LineHeight;
        int page = 0;
        for (int position = 0; position < text.Length;)
        {
            if (text.AsSpan(position).StartsWith("{Line}", StringComparison.Ordinal) || text.AsSpan(position).StartsWith("\r\n", StringComparison.Ordinal) || text[position] is '\n' or '\r')
            {
                position += text.AsSpan(position).StartsWith("{Line}", StringComparison.Ordinal) ? 6 : text.AsSpan(position).StartsWith("\r\n", StringComparison.Ordinal) ? 2 : 1;
                x = 0;
                y += profile.LineHeight;
                height = Math.Max(height, y + profile.LineHeight);
                continue;
            }
            if (text.AsSpan(position).StartsWith("{Next}", StringComparison.Ordinal))
            {
                position += 6;
                x = 0;
                y = 0;
                page++;
                continue;
            }
            if (text.AsSpan(position).StartsWith("{End}", StringComparison.Ordinal))
            {
                position += 5;
                if (position != text.Length)
                {
                    diagnostics.Add(new("after-terminator", "Text follows the native terminator.", position, text.Length - position));
                }
                break;
            }
            NativeGlyphMapping? mapping = compiled.Mappings.TryGetValue(text[position], out NativeGlyphMapping[]? candidates) ? candidates.FirstOrDefault(candidate => (candidate.Field is null || candidate.Field == context.Field) && text.AsSpan(position).StartsWith(candidate.Text, StringComparison.Ordinal)) : null;
            NativeGlyphImage? image = mapping is null ? null : images[mapping.ImageIndex];
            int length = mapping?.Text.Length ?? StringInfo.GetNextTextElementLength(text, position);
            if (image is null)
            {
                if (text[position] == '{')
                {
                    int end = text.IndexOf('}', position);
                    length = end < 0 ? text.Length - position : end - position + 1;
                }
                diagnostics.Add(new("missing-glyph", $"No native image or supported layout control for '{text.Substring(position, length)}'. Expand dictionary/name tokens before layout.", position, length));
                position += length;
                continue;
            }
            runs.Add(new(position, length, image, x + image.BearingX, y + image.BearingY, page));
            x += image.Advance;
            width = Math.Max(width, Math.Max(x, x - image.Advance + image.BearingX + image.Width));
            height = Math.Max(height, y + image.BearingY + image.Height);
            if (x - image.Advance + image.BearingX < 0 || y + image.BearingY < 0 || x > maximumWidth || x - image.Advance + image.BearingX + image.Width > maximumWidth || y + Math.Max(profile.LineHeight, image.BearingY + image.Height) > maximumHeight)
            {
                diagnostics.Add(new("overflow", "Native text exceeds the selected logical window; it was not shrunk, wrapped or substituted.", position, length));
            }
            position += length;
        }
        return new(runs, width, height, page + 1, diagnostics) { FinalAdvance = x };
    }
}
