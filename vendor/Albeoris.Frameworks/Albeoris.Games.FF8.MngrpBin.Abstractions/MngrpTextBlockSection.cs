namespace Albeoris.Games.FF8.MngrpBin.Abstractions;

/// <summary>
/// A section holding a sequence of <see cref="MngrpTextBlock"/>s, each aligned to four bytes and
/// terminated by a block whose stored length is zero. Blocks are addressed by their byte offset
/// from a <see cref="MngrpTextBlockMapSection"/>; those offsets are recalculated on serialization.
/// </summary>
public sealed class MngrpTextBlockSection : MngrpSection
{
    private Byte[] _trailingData = [];

    public MngrpTextBlockSection(Int32 slotIndex)
        : base(slotIndex)
    {
    }

    public override MngrpSectionLayout Layout => MngrpSectionLayout.TextBlockList;

    /// <summary>The section's blocks, in file order.</summary>
    public List<MngrpTextBlock> Blocks { get; } = [];

    /// <summary>
    /// Raw bytes following the last block, trimmed of trailing zeros. Empty in every known
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
