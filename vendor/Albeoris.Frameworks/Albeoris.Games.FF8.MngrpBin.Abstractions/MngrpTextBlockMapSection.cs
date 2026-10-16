namespace Albeoris.Games.FF8.MngrpBin.Abstractions;

/// <summary>
/// A section mapping external use-sites to text blocks: a 32-bit reference count followed by
/// one (block offset, section number) pair per reference. Block offsets are recalculated from
/// the referenced <see cref="MngrpTextBlockSection"/>s on serialization.
/// </summary>
public sealed class MngrpTextBlockMapSection : MngrpSection
{
    private Byte[] _trailingData = [];

    public MngrpTextBlockMapSection(Int32 slotIndex)
        : base(slotIndex)
    {
    }

    public override MngrpSectionLayout Layout => MngrpSectionLayout.TextBlockMap;

    /// <summary>The references, in file order.</summary>
    public List<MngrpTextBlockReference> References { get; } = [];

    /// <summary>
    /// Raw bytes following the last reference, trimmed of trailing zeros. Empty in every known
    /// file; preserved verbatim in case a file carries packer garbage there.
    /// </summary>
    public Byte[] TrailingData
    {
        get => _trailingData;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _trailingData = value;
        }
    }
}
