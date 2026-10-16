namespace Albeoris.Games.FF8.MngrpBin.Abstractions;

/// <summary>A section whose whole body is a single <see cref="MngrpStringTable"/>.</summary>
public sealed class MngrpStringTableSection : MngrpSection
{
    public MngrpStringTableSection(Int32 slotIndex)
        : base(slotIndex)
    {
    }

    public override MngrpSectionLayout Layout => MngrpSectionLayout.StringTable;

    /// <summary>The table of strings this section stores.</summary>
    public MngrpStringTable Table { get; } = new();
}
