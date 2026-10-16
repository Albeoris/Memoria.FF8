namespace Albeoris.Games.FF8.MngrpBin.Abstractions;

/// <summary>
/// A section holding a table of nested string tables: a 16-bit count followed by one 16-bit
/// offset per nested table, zero meaning "no table". Offsets are recalculated on serialization.
/// </summary>
public sealed class MngrpStringTableGroupSection : MngrpSection
{
    private Byte[] _leadingBytes = [];

    public MngrpStringTableGroupSection(Int32 slotIndex)
        : base(slotIndex)
    {
    }

    public override MngrpSectionLayout Layout => MngrpSectionLayout.StringTableGroup;

    /// <summary>The nested tables, in slot order. Absent tables are <see langword="null"/>.</summary>
    public List<MngrpStringTable?> Tables { get; } = [];

    /// <summary>
    /// The raw bytes between the offset table and the first nested table — two alignment bytes
    /// in every known file. Preserved verbatim.
    /// </summary>
    public Byte[] LeadingBytes
    {
        get => _leadingBytes;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _leadingBytes = value;
        }
    }
}
