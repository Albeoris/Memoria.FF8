namespace Albeoris.Games.FF8.MngrpBin.Abstractions;

/// <summary>
/// One block of a <see cref="MngrpTextBlockSection"/>: three 16-bit link ids, a 16-bit total
/// length (recalculated on serialization) and a payload of NUL-terminated texts.
/// </summary>
public sealed class MngrpTextBlock
{
    /// <summary>The id value meaning "no link".</summary>
    public const UInt16 NoLink = UInt16.MaxValue;

    private Byte[] _trailingBytes = [];

    /// <summary>The block this one was opened from, or <see cref="NoLink"/>.</summary>
    public UInt16 OriginId { get; set; } = NoLink;

    /// <summary>The block selected when navigating left, or <see cref="NoLink"/>.</summary>
    public UInt16 LeftId { get; set; } = NoLink;

    /// <summary>The block selected when navigating right, or <see cref="NoLink"/>.</summary>
    public UInt16 RightId { get; set; } = NoLink;

    /// <summary>
    /// The block's texts, each stored NUL-terminated. Every known block carries exactly two:
    /// a caption followed by the body.
    /// </summary>
    public List<MngrpText> Texts { get; } = [];

    /// <summary>
    /// Raw payload bytes after the last NUL terminator, if the block's stored length reaches
    /// past it. Empty in every known file; preserved verbatim.
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
