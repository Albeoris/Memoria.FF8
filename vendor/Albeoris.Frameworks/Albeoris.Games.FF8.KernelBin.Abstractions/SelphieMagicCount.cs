namespace Albeoris.Games.FF8.KernelBin.Abstractions;

/// <summary>How many uses of a magic spell one of Selphie's Slot reels contributes.</summary>
public sealed class SelphieMagicCount
{
    public Byte MagicId { get; set; }
    public Byte Count { get; set; }
}
