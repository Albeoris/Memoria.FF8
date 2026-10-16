namespace Albeoris.Games.FF8.MngrpBin.Abstractions;

/// <summary>
/// A text stored in the archive: its decoded string plus, while it stays unedited, the exact
/// byte form it was read from.
/// </summary>
/// <remarks>
/// The archive encodings are not lossless — several distinct byte forms decode to the same
/// characters (display-only glyphs, duplicated codepage entries), so re-encoding a decoded
/// string does not always reproduce the file's bytes. Keeping the original byte form makes
/// serialization byte-exact for every text the caller did not touch; setting
/// <see cref="Value"/> discards it, and the new text is then encoded from scratch.
/// </remarks>
public sealed class MngrpText
{
    private String _value;

    public MngrpText(String value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _value = value;
    }

    public MngrpText(String value, ReadOnlyMemory<Byte>? encodedValue)
        : this(value)
    {
        EncodedValue = encodedValue;
    }

    /// <summary>The decoded text. Assigning a new value discards <see cref="EncodedValue"/>.</summary>
    public String Value
    {
        get => _value;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _value = value;
            EncodedValue = null;
        }
    }

    /// <summary>
    /// The exact bytes this text was decoded from, or <see langword="null"/> once the text has
    /// been edited. Serialization prefers this form to keep unedited texts byte-exact.
    /// </summary>
    public ReadOnlyMemory<Byte>? EncodedValue { get; private set; }

    public static implicit operator MngrpText(String value) => new(value);

    public override String ToString() => _value;
}
