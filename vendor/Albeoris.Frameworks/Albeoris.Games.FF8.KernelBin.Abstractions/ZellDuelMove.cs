namespace Albeoris.Games.FF8.KernelBin.Abstractions;

/// <summary>
/// One node of Zell's Duel button-input sequence tree. This section has no associated
/// display text in the original format.
/// </summary>
public sealed class ZellDuelMove
{
    public Byte StartMove { get; set; }
    public Byte NextSequence1 { get; set; }
    public Byte NextSequence2 { get; set; }
    public Byte NextSequence3 { get; set; }
}
