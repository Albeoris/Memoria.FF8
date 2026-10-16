namespace Albeoris.Games.FF8.KernelBin.Abstractions;

/// <summary>One of Zell's Duel limit break combo finishers.</summary>
public sealed class ZellLimit
{
    /// <summary>The display name, or <see langword="null"/> if this slot has no name.</summary>
    public String? Name { get; set; }

    /// <summary>The display description, or <see langword="null"/> if this slot has no description.</summary>
    public String? Description { get; set; }

    public UInt16 MagicId { get; set; }
    public Byte AttackType { get; set; }
    public Byte AttackPower { get; set; }
    public Byte AttackFlags { get; set; }

    /// <summary>Purpose not documented in the original reverse-engineered format.</summary>
    public Byte Unknown0 { get; set; }

    public Byte Target { get; set; }

    /// <summary>Purpose not documented in the original reverse-engineered format.</summary>
    public Byte Unknown1 { get; set; }

    public Byte HitCount { get; set; }
    public Element Element { get; set; }
    public Byte ElementPercent { get; set; }
    public Byte StatusAttack { get; set; }

    public UInt16 Combo1 { get; set; }
    public UInt16 Combo2 { get; set; }
    public UInt16 Combo3 { get; set; }
    public UInt16 Combo4 { get; set; }
    public UInt16 Combo5 { get; set; }

    public UInt16 Status0 { get; set; }
    public UInt32 Status1 { get; set; }
}
