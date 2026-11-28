namespace Albeoris.Games.FF8.MngrpBin.Abstractions;

/// <summary>
/// A section of fixed eight-byte records with all-zero positions for absent records. Each
/// occupied record starts with a 16-bit offset addressing its text inside a companion slot.
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

    /// <summary>Records by physical index, including null holes before the last occupied record.</summary>
    public List<MngrpTextRecord?> Records { get; } = [];

    /// <summary>Bytes in the companion slot before the first referenced text.</summary>
    public Byte[] LeadingTextBytes { get; set; } = [];

    /// <summary>Raw bytes following the last occupied record, trimmed of trailing zeros.</summary>
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
