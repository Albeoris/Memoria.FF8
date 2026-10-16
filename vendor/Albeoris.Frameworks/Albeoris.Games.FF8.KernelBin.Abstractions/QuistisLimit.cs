namespace Albeoris.Games.FF8.KernelBin.Abstractions;

/// <summary>
/// One of Quistis's Blue Magic combat parameter records. This section has no associated
/// display text in the original format (see <see cref="BlueMagic"/> for the spell list).
/// </summary>
public sealed class QuistisLimit
{
    public Int32 Statuses1 { get; set; }
    public Int16 Statuses0 { get; set; }
    public Byte AttackPower { get; set; }
    public Byte AttackParam { get; set; }
}
