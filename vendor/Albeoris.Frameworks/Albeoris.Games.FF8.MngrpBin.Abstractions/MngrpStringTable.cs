namespace Albeoris.Games.FF8.MngrpBin.Abstractions;

/// <summary>
/// An offset-indexed table of NUL-terminated strings: a 16-bit entry count followed by one
/// 16-bit offset per entry (zero meaning "no entry"), then the string data. Used both as a
/// stand-alone section body and nested inside a <see cref="MngrpStringTableGroupSection"/>.
/// All offsets are recalculated from the entries when the table is serialized.
/// </summary>
public sealed class MngrpStringTable
{
    private Byte[] _leadingBytes = [];

    /// <summary>The table's entries, in slot order. Absent entries have a <see langword="null"/> text.</summary>
    public List<MngrpTextEntry> Entries { get; } = [];

    /// <summary>
    /// The raw bytes between the offset table and the first addressed string. Empty in every
    /// known file; preserved verbatim in case a file deviates.
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

    /// <summary>Gets or sets the text of the entry at <paramref name="index"/>.</summary>
    public String? this[Int32 index]
    {
        get => Entries[index].Text?.Value;
        set => Entries[index].Text = value is null ? null : new MngrpText(value);
    }
}
