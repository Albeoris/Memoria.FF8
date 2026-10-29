using System.Globalization;
using System.Text;

namespace Memoria.FF8.Resources.Text;

public sealed record NativeEncodedText(byte[] Bytes, IReadOnlyList<NativeTextDiagnostic> Diagnostics)
{
    public bool Success => Diagnostics.Count == 0;
}
public sealed record NativeDecodedToken(int BytePosition, byte[] Bytes, string Text);
public sealed record NativeDecodedText(string Text, IReadOnlyList<NativeDecodedToken> Tokens, IReadOnlyList<NativeTextDiagnostic> Diagnostics)
{
    public bool Success => Diagnostics.Count == 0;
    /// <summary>Preserves imported alias sequences. This is distinct from canonical Unicode re-encoding.</summary>
    public byte[] OriginalBytes() => Tokens.SelectMany(token => token.Bytes).ToArray();
}

/// <summary>Finite explicit native tables, including escaped pages and field tables. No fallback encoding is used.</summary>
public static class NativeStrictCodec
{
    public static NativeEncodedText Encode(NativeFontProfile profile, string text, NativeTextContext context, int maximumBytes = int.MaxValue)
    {
        List<NativeTextDiagnostic> diagnostics = Validate(profile, context);
        if (diagnostics.Count != 0)
        {
            return new([], diagnostics);
        }
        NativeCodecMapping[] entries = Effective(profile, context).Where(entry => entry.Canonical).OrderByDescending(entry => entry.Text.Length).ToArray();
        List<byte> bytes = [];
        for (int position = 0; position < text.Length;)
        {
            NativeCodecMapping? entry = entries.FirstOrDefault(candidate => text.AsSpan(position).StartsWith(candidate.Text, StringComparison.Ordinal));
            if (entry is null)
            {
                int length = StringInfo.GetNextTextElementLength(text, position);
                if (text[position] == '{')
                {
                    int end = text.IndexOf('}', position);
                    length = end < 0 ? text.Length - position : end - position + 1;
                }
                diagnostics.Add(new("unrepresentable", $"'{text.Substring(position, length)}' has no canonical native sequence in profile '{profile.Id}'.", position, length));
                position += length;
                continue;
            }
            byte[] sequence = Convert.FromHexString(entry.Hex);
            if ((long)bytes.Count + sequence.Length > maximumBytes)
            {
                diagnostics.Add(new("byte-limit", $"Native output exceeds the resource limit of {maximumBytes} bytes.", position, entry.Text.Length));
                break;
            }
            bytes.AddRange(sequence);
            position += entry.Text.Length;
        }
        // Failed results never expose a partially substituted/truncated resource as exportable bytes.
        return new(diagnostics.Count == 0 ? bytes.ToArray() : [], diagnostics);
    }

    public static NativeDecodedText Decode(NativeFontProfile profile, ReadOnlySpan<byte> bytes, NativeTextContext context)
    {
        List<NativeTextDiagnostic> diagnostics = Validate(profile, context);
        if (diagnostics.Count != 0)
        {
            return new("", [], diagnostics);
        }
        var entries = Effective(profile, context).Select(entry => (Entry: entry, Bytes: Convert.FromHexString(entry.Hex))).OrderByDescending(entry => entry.Bytes.Length).ToArray();
        List<NativeDecodedToken> tokens = [];
        StringBuilder text = new();
        for (int position = 0; position < bytes.Length;)
        {
            bool found = false;
            foreach (var entry in entries)
            {
                if (bytes[position..].StartsWith(entry.Bytes))
                {
                    text.Append(entry.Entry.Text);
                    tokens.Add(new(position, entry.Bytes.ToArray(), entry.Entry.Text));
                    position += entry.Bytes.Length;
                    found = true;
                    break;
                }
            }
            if (!found)
            {
                diagnostics.Add(new("unknown-sequence", $"Unknown or incomplete native sequence at byte {position}: {bytes[position]:X2}.", position, 1));
                break;
            }
        }
        return new(text.ToString(), tokens, diagnostics);
    }

    private static IEnumerable<NativeCodecMapping> Effective(NativeFontProfile profile, NativeTextContext context)
    {
        HashSet<string> overridden = profile.Codec.Where(entry => entry.Field == context.Field && entry.Field is not null).Select(entry => entry.Hex.ToUpperInvariant()).ToHashSet(StringComparer.Ordinal);
        return profile.Codec.Where(entry => entry.Field == context.Field || entry.Field is null && !overridden.Contains(entry.Hex.ToUpperInvariant()));
    }

    private static List<NativeTextDiagnostic> Validate(NativeFontProfile profile, NativeTextContext context)
    {
        List<NativeTextDiagnostic> diagnostics = [.. NativeFontProfileValidation.Validate(profile)];
        if (diagnostics.Count != 0)
        {
            return diagnostics;
        }
        if (!profile.Domains.Any(domain => domain.Matches(context)))
        {
            diagnostics.Add(new("native-domain", "No supported native domain matches the explicitly selected edition, resource, field and dictionary context. A render-only profile cannot establish native-resource compatibility."));
        }
        return diagnostics;
    }
}
