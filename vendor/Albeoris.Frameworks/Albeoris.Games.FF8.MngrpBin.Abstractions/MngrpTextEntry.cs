namespace Albeoris.Games.FF8.MngrpBin.Abstractions;

/// <summary>
/// One slot of a string table: the decoded text plus the raw bytes that followed it inside the
/// span the original packer reserved for it.
/// </summary>
/// <remarks>
/// The reserved span is delimited by the table's stored offsets, so the trailing bytes are part
/// of the file even though nothing addresses them: normally a single NUL terminator plus zero
/// padding, but the original packer also left non-zero garbage there in places. They are
/// preserved verbatim so that an untouched table serializes back byte-for-byte.
/// </remarks>
public sealed class MngrpTextEntry
{
    private Byte[] _trailingBytes;

    /// <summary>Creates an absent entry (its table offset is the zero sentinel).</summary>
    public MngrpTextEntry()
    {
        Text = null;
        _trailingBytes = [];
    }

    /// <summary>Creates an entry holding <paramref name="text"/> followed by a single NUL terminator.</summary>
    public MngrpTextEntry(MngrpText text)
    {
        ArgumentNullException.ThrowIfNull(text);
        Text = text;
        _trailingBytes = [0];
    }

    /// <summary>
    /// The text, or <see langword="null"/> for an absent entry (stored as offset zero). An
    /// empty string is a present entry whose reserved span contains only NUL bytes. Text that
    /// the original packer prefixed with a NUL byte keeps that byte as part of the string; the
    /// encoding decodes it to its textual tag form.
    /// </summary>
    public MngrpText? Text { get; set; }

    /// <summary>
    /// The raw bytes between the end of <see cref="Text"/> and the next addressed entry: the NUL
    /// terminator, padding and any packer garbage, preserved verbatim. Empty for absent entries.
    /// </summary>
    public Byte[] TrailingBytes
    {
        get => _trailingBytes;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _trailingBytes = value;
        }
    }
}
