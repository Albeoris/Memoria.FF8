namespace Albeoris.Games.FF8.KernelBin.Abstractions;

/// <summary>
/// An additional battle command unlocked separately from the base <see cref="BattleCommand"/>
/// list. This section has no associated display text in the original format.
/// </summary>
public sealed class AdditionalCommand
{
    public UInt16 MagicId { get; set; }

    /// <summary>Purpose not documented in the original reverse-engineered format.</summary>
    public UInt16 Unknown { get; set; }

    public Byte AttackType { get; set; }
    public Byte AttackPower { get; set; }
    public Byte AttackFlags { get; set; }
    public Byte HitCount { get; set; }
    public Element Element { get; set; }
    public Byte StatusAttack { get; set; }
    public UInt16 Status1 { get; set; }
    public UInt32 Status2 { get; set; }
}
