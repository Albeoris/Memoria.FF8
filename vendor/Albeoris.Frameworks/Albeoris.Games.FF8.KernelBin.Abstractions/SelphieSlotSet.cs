namespace Albeoris.Games.FF8.KernelBin.Abstractions;

/// <summary>
/// One fixed set of the 8 possible Slot reel outcomes for Selphie's limit break. This section
/// has no associated display text in the original format.
/// </summary>
public sealed class SelphieSlotSet
{
    /// <summary>The 8 reel outcomes of this set, in fixed order.</summary>
    public SelphieMagicCount[] Slots { get; set; } = new SelphieMagicCount[8];
}
