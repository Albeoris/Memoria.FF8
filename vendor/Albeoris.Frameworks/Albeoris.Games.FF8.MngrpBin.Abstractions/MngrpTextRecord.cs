namespace Albeoris.Games.FF8.MngrpBin.Abstractions;

/// <summary>
/// One record of a <see cref="MngrpTextRecordSection"/>: six opaque payload bytes plus the text
/// the record's stored offset addresses in the companion text slot. The offset is recalculated
/// on serialization.
/// </summary>
public sealed class MngrpTextRecord
{
    /// <summary>The number of payload bytes each record carries after its 16-bit text offset.</summary>
    public const Int32 PayloadLength = 6;

    private Byte[] _payload = new Byte[PayloadLength];
    private Byte[] _textTrailingBytes = [0];
    private MngrpText _text = new(String.Empty);

    /// <summary>The record's text, stored in the companion slot.</summary>
    public MngrpText Text
    {
        get => _text;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _text = value;
        }
    }

    /// <summary>
    /// The raw bytes between the end of <see cref="Text"/> and the next record's text: the NUL
    /// terminator plus any padding, preserved verbatim.
    /// </summary>
    public Byte[] TextTrailingBytes
    {
        get => _textTrailingBytes;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _textTrailingBytes = value;
        }
    }

    /// <summary>The record's six payload bytes, kept undecoded; their meaning is not reverse-engineered.</summary>
    public Byte[] Payload
    {
        get => _payload;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            ArgumentOutOfRangeException.ThrowIfNotEqual(value.Length, PayloadLength, nameof(value));
            _payload = value;
        }
    }
}
