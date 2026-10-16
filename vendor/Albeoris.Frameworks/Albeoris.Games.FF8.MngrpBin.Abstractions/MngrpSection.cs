namespace Albeoris.Games.FF8.MngrpBin.Abstractions;

/// <summary>The state every <see cref="IMngrpSection"/> shares: its slot and its size reservation.</summary>
public abstract class MngrpSection : IMngrpSection
{
    protected MngrpSection(Int32 slotIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(slotIndex);
        SlotIndex = slotIndex;
    }

    public Int32 SlotIndex { get; }

    public abstract MngrpSectionLayout Layout { get; }

    public Int32 ReservedSize { get; set; }
}
