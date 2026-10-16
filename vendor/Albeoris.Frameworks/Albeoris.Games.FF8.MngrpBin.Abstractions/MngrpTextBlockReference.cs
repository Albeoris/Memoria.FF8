namespace Albeoris.Games.FF8.MngrpBin.Abstractions;

/// <summary>
/// One entry of a <see cref="MngrpTextBlockMapSection"/>: a reference to a block inside one of the
/// archive's text-block sections. On disk the block is addressed by its byte offset; the offset
/// is resolved to <see cref="BlockIndex"/> when reading and recalculated when writing, so edits
/// that move blocks around keep every reference intact.
/// </summary>
public sealed class MngrpTextBlockReference
{
    public MngrpTextBlockReference(Int32 sectionNumber, Int32 blockIndex)
    {
        SectionNumber = sectionNumber;
        BlockIndex = blockIndex;
    }

    public MngrpTextBlockReference(Int32 sectionNumber, Int32? blockIndex, UInt16 storedOffset)
    {
        SectionNumber = sectionNumber;
        BlockIndex = blockIndex;
        StoredOffset = storedOffset;
    }

    /// <summary>
    /// The zero-based number of the target <see cref="MngrpTextBlockSection"/>, counting the
    /// archive's text-block sections in slot order.
    /// </summary>
    public Int32 SectionNumber { get; set; }

    /// <summary>
    /// The zero-based block within the target section, or <see langword="null"/> when the stored
    /// offset did not match any block start (a corrupted reference, written back verbatim from
    /// <see cref="StoredOffset"/>).
    /// </summary>
    public Int32? BlockIndex { get; set; }

    /// <summary>The raw byte offset read from the file; only used to re-serialize unresolved references.</summary>
    public UInt16 StoredOffset { get; set; }
}
