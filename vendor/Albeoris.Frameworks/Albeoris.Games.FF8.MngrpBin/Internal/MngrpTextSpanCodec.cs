using System.Text;
using Albeoris.Games.FF8.MngrpBin.Abstractions;

namespace Albeoris.Games.FF8.MngrpBin.Internal;

/// <summary>
/// Decodes and encodes one text within the byte span the original packer reserved for it.
/// The span's bounds come from the surrounding offsets, never from scanning for a terminator,
/// so texts the packer prefixed with a NUL byte and garbage the packer left behind a text are
/// both handled deterministically.
/// </summary>
internal static class MngrpTextSpanCodec
{
    /// <summary>
    /// Splits <paramref name="reservedSpan"/> into a decoded text and its raw trailing bytes.
    /// A span of NUL bytes only is an empty text. A NUL in the first byte directly followed by
    /// more text belongs to the text and decodes through <paramref name="encoding"/>; the text
    /// then runs to the next NUL. Everything after the text (terminator, padding, garbage) is
    /// kept as trailing bytes, trimmed of trailing zeros when <paramref name="trimTrailingZeros"/>
    /// is set — the caller then recreates those zeros as computed padding.
    /// </summary>
    public static MngrpTextEntry ReadEntry(ReadOnlySpan<Byte> reservedSpan, Encoding encoding, Boolean trimTrailingZeros)
    {
        Int32 textLength = MeasureText(reservedSpan);
        ReadOnlySpan<Byte> trailingBytes = reservedSpan[textLength..];
        if (trimTrailingZeros)
        {
            trailingBytes = MngrpFormat.TrimTrailingZeros(trailingBytes);
        }

        return new MngrpTextEntry(ReadText(reservedSpan[..textLength], encoding))
        {
            TrailingBytes = trailingBytes.ToArray(),
        };
    }

    /// <summary>Decodes a text while keeping its exact byte form for byte-exact serialization.</summary>
    public static MngrpText ReadText(ReadOnlySpan<Byte> encodedText, Encoding encoding)
    {
        return new MngrpText(encoding.GetString(encodedText), encodedText.ToArray());
    }

    /// <summary>The byte form of a text: its preserved original bytes, or a fresh encoding after an edit.</summary>
    public static ReadOnlyMemory<Byte> GetBytes(MngrpText text, Encoding encoding)
    {
        return text.EncodedValue ?? encoding.GetBytes(text.Value);
    }

    /// <summary>The serialized length of a present entry: its text bytes plus trailing bytes.</summary>
    public static Int32 Measure(MngrpTextEntry entry, Encoding encoding)
    {
        return entry.Text is null ? 0 : GetBytes(entry.Text, encoding).Length + entry.TrailingBytes.Length;
    }

    /// <summary>Serializes a text and its preserved trailing bytes.</summary>
    public static void WriteEntry(MngrpTextEntry entry, Encoding encoding, MngrpByteWriter writer)
    {
        if (entry.Text is null)
        {
            throw new InvalidOperationException("An absent entry has no bytes to serialize.");
        }

        writer.WriteBytes(GetBytes(entry.Text, encoding).Span);
        writer.WriteBytes(entry.TrailingBytes);
    }

    private static Int32 MeasureText(ReadOnlySpan<Byte> span)
    {
        if (span.IsEmpty || span[0] == 0 && (span.Length == 1 || span[1] == 0))
        {
            return 0;
        }

        Int32 searchStart = span[0] == 0 ? 1 : 0;
        Int32 terminator = span[searchStart..].IndexOf((Byte)0);
        return terminator < 0 ? span.Length : searchStart + terminator;
    }
}
