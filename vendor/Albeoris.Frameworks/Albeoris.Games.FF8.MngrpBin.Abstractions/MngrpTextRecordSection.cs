namespace Albeoris.Games.FF8.MngrpBin.Abstractions;

/// <summary>
/// A section of fixed eight-byte records, terminated by an all-zero record. Each record starts
/// with a 16-bit offset addressing its text inside a companion slot; this class represents both
/// slots as one logical section, so the companion slot's content and every text offset are
/// derived from <see cref="Records"/> on serialization.
/// </summary>
public sealed class MngrpTextRecordSection : MngrpSection
{
    private Byte[] _trailingData = [];

    public MngrpTextRecordSection(Int32 slotIndex, Int32 textSlotIndex)
        : base(slotIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(textSlotIndex);
        TextSlotIndex = textSlotIndex;
    }

    /// <summary>The slot of the companion section that stores the record texts.</summary>
    public Int32 TextSlotIndex { get; }

    /// <summary>
    /// The <see cref="IMngrpSection.ReservedSize"/> equivalent for the companion text slot.
    /// </summary>
    public Int32 TextReservedSize { get; set; }

    public override MngrpSectionLayout Layout => MngrpSectionLayout.TextRecordList;

    /// <summary>The section's records, in file order.</summary>
    public List<MngrpTextRecord> Records { get; } = [];

    /// <summary>
    /// Raw bytes following the last record, trimmed of trailing zeros. Empty in every known
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
