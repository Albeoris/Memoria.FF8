using System.Text;

namespace Memoria.FF8.Resources.Text;

public static class NativeFontProfileValidation
{
    public static IReadOnlyList<NativeTextDiagnostic> Validate(NativeFontProfile profile)
    {
        List<NativeTextDiagnostic> issues = [];
        if (profile.FormatVersion != 1 || string.IsNullOrWhiteSpace(profile.Id) || string.IsNullOrWhiteSpace(profile.DisplayName))
        {
            issues.Add(new("identity", "Profile requires formatVersion 1, a stable ID and a display name."));
        }
        if (!float.IsFinite(profile.LineHeight) || profile.LineHeight <= 0 || profile.LogicalWidth <= 0 || profile.LogicalHeight <= 0)
        {
            issues.Add(new("metrics", "Line height and logical viewport dimensions must be positive and finite."));
        }
        if (profile.Atlases is null || profile.Images is null || profile.Glyphs is null || profile.Codec is null || profile.Dependencies is null || profile.Domains is null || profile.ReservedPrefixes is null || profile.Assignments is null)
        {
            issues.Add(new("schema", "Profile collections cannot be null."));
            return issues;
        }
        if (profile.Atlases.Any(value => value is null) || profile.Images.Any(value => value is null) || profile.Glyphs.Any(value => value is null) || profile.Codec.Any(value => value is null) || profile.Dependencies.Any(value => value is null) || profile.Domains.Any(value => value is null) || profile.Assignments.Any(value => value is null))
        {
            issues.Add(new("schema", "Profile arrays cannot contain null entries."));
            return issues;
        }
        HashSet<string> atlasIds = new(StringComparer.Ordinal);
        foreach (NativeFontAtlas atlas in profile.Atlases)
        {
            if (string.IsNullOrWhiteSpace(atlas.Id) || !atlasIds.Add(atlas.Id) || atlas.Width <= 0 || atlas.Height <= 0 || atlas.Palette < 0)
            {
                issues.Add(new("atlas", $"Invalid or duplicate atlas '{atlas.Id}'."));
            }
            if (!IsPortablePath(atlas.Path))
            {
                issues.Add(new("dependency-path", $"Atlas path '{atlas.Path}' must be a portable relative path."));
            }
        }
        HashSet<int> imageIds = [];
        foreach (NativeGlyphImage image in profile.Images)
        {
            NativeFontAtlas? atlas = profile.Atlases.FirstOrDefault(value => value.Id == image.AtlasId);
            if (image.Index < 0 || !imageIds.Add(image.Index) || atlas is null || image.X < 0 || image.Y < 0 || image.Width <= 0 || image.Height <= 0 || (long)image.X + image.Width > atlas.Width || (long)image.Y + image.Height > atlas.Height || !float.IsFinite(image.Advance) || image.Advance < 0 || !float.IsFinite(image.BearingX) || !float.IsFinite(image.BearingY))
            {
                issues.Add(new("image", $"Image {image.Index} has an invalid identity, atlas rectangle or metrics."));
            }
        }
        HashSet<(string, string?)> glyphs = [];
        foreach (NativeGlyphMapping mapping in profile.Glyphs)
        {
            bool nativeGlyphToken = mapping.Text is not null && System.Text.RegularExpressions.Regex.IsMatch(mapping.Text, "^\\{Glyph [0-9A-Fa-f]+\\}$", System.Text.RegularExpressions.RegexOptions.CultureInvariant);
            if (!IsValidText(mapping.Text) || !glyphs.Add((mapping.Text!, mapping.Field)) || !imageIds.Contains(mapping.ImageIndex) || !nativeGlyphToken && (mapping.Text!.Contains('{') || mapping.Text.Contains('}')) || mapping.Text!.Contains('\n') || mapping.Text.Contains('\r'))
            {
                issues.Add(new("glyph", $"Glyph mapping '{mapping.Text}' has invalid text, duplicate field identity or missing image {mapping.ImageIndex}."));
            }
        }
        foreach (NativeProfileDependency dependency in profile.Dependencies)
        {
            if (!IsPortablePath(dependency.Path))
            {
                issues.Add(new("dependency-path", $"Dependency '{dependency.Path}' must be a portable relative path."));
            }
        }
        foreach (NativeTextDomain domain in profile.Domains)
        {
            if (string.IsNullOrWhiteSpace(domain.Edition) || string.IsNullOrWhiteSpace(domain.ResourceFamily))
            {
                issues.Add(new("domain", "Each supported native domain requires an explicit edition and resource family."));
            }
        }
        HashSet<(string, string, NativeTextContext)> assignments = [];
        foreach (NativeProfileAssignment assignment in profile.Assignments)
        {
            if (assignment.Role is not ("Source" or "Reference" or "Target") || string.IsNullOrWhiteSpace(assignment.Dataset) || string.IsNullOrWhiteSpace(assignment.ProfileId) || assignment.Context is null || string.IsNullOrWhiteSpace(assignment.Context.Edition) || string.IsNullOrWhiteSpace(assignment.Context.ResourceFamily) || !assignments.Add((assignment.Role, assignment.Dataset, assignment.Context)))
            {
                issues.Add(new("assignment", "Assignments require a unique Source/Reference/Target dataset/context, stable profile ID, edition and resource family."));
            }
        }
        List<(NativeCodecMapping Mapping, byte[] Bytes)> entries = [];
        foreach (NativeCodecMapping mapping in profile.Codec)
        {
            if (!TryHex(mapping.Hex, out byte[] bytes) || !IsValidText(mapping.Text))
            {
                issues.Add(new("codec", $"Invalid native sequence or text in mapping '{mapping.Hex}'."));
                continue;
            }
            if (mapping.Text == "{Page}")
            {
                issues.Add(new("native-page", "{Page} is a Memoria page feature; native page breaks use {Next}."));
            }
            if (mapping.Text.Contains('{') && !mapping.Control)
            {
                issues.Add(new("reserved-control", $"Token '{mapping.Text}' must be explicitly declared as a protected control."));
            }
            foreach (string prefix in profile.ReservedPrefixes)
            {
                if (!TryHex(prefix, out byte[] reserved))
                {
                    issues.Add(new("reserved-prefix", $"Invalid reserved prefix '{prefix}'."));
                }
                else if (bytes.AsSpan().StartsWith(reserved) && !mapping.Control)
                {
                    issues.Add(new("reserved-control", $"Sequence '{mapping.Hex}' occupies protected control bytes."));
                }
            }
            entries.Add((mapping, bytes));
        }
        foreach (string? field in entries.Select(entry => entry.Mapping.Field).Append(null).Distinct(StringComparer.Ordinal))
        {
            HashSet<string> replaced = entries.Where(entry => entry.Mapping.Field == field && field is not null).Select(entry => Convert.ToHexString(entry.Bytes)).ToHashSet(StringComparer.Ordinal);
            var effective = entries.Where(entry => entry.Mapping.Field == field || entry.Mapping.Field is null && !replaced.Contains(Convert.ToHexString(entry.Bytes))).ToArray();
            var ordered = effective.OrderBy(entry => Convert.ToHexString(entry.Bytes), StringComparer.Ordinal).ToArray();
            for (int i = 1; i < ordered.Length; i++)
            {
                var left = ordered[i - 1];
                var right = ordered[i];
                if (right.Bytes.AsSpan().StartsWith(left.Bytes))
                {
                    issues.Add(new("sequence-conflict", $"Sequences '{left.Mapping.Hex}' and '{right.Mapping.Hex}' collide or have an incomplete escape-prefix ambiguity."));
                }
            }
            foreach (var group in effective.GroupBy(entry => entry.Mapping.Text, StringComparer.Ordinal))
            {
                if (group.Count(entry => entry.Mapping.Canonical) != 1)
                {
                    issues.Add(new("canonical", $"Text '{group.Key}' requires exactly one canonical reverse mapping in field context '{field ?? "base"}'."));
                }
            }
        }
        return issues;
    }

    public static bool IsPortablePath(string? path) => !string.IsNullOrWhiteSpace(path) && !Path.IsPathRooted(path) && !path.Contains('\\') && !path.Contains(':') && path.Split('/').All(part => part.Length != 0 && part is not ("." or "..") && !part.EndsWith('.') && !part.EndsWith(' ') && part.IndexOfAny(['<', '>', '"', '|', '?', '*']) < 0 && !part.Any(char.IsControl) && !IsDeviceName(part));

    private static bool IsDeviceName(string part)
    {
        string stem = part.Split('.')[0].ToUpperInvariant();
        return stem is "CON" or "PRN" or "AUX" or "NUL" || stem.Length == 4 && stem[3] is >= '1' and <= '9' && (stem.StartsWith("COM", StringComparison.Ordinal) || stem.StartsWith("LPT", StringComparison.Ordinal));
    }

    public static bool TryHex(string? value, out byte[] bytes)
    {
        bytes = [];
        if (string.IsNullOrEmpty(value) || value.Length % 2 != 0)
        {
            return false;
        }
        try
        {
            bytes = Convert.FromHexString(value);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool IsValidText(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }
        ReadOnlySpan<char> rest = text;
        while (!rest.IsEmpty)
        {
            if (Rune.DecodeFromUtf16(rest, out _, out int consumed) != System.Buffers.OperationStatus.Done)
            {
                return false;
            }
            rest = rest[consumed..];
        }
        return true;
    }
}
