namespace Albeoris.Games.FF8.KernelBin.Abstractions;

/// <summary>One of Irvine's Shot limit break attacks.</summary>
public sealed class IrvinLimit
{
    /// <summary>The display name, or <see langword="null"/> if this slot has no name.</summary>
    public String? Name { get; set; }

    /// <summary>The display description, or <see langword="null"/> if this slot has no description.</summary>
    public String? Description { get; set; }

    public UInt16 MagicId { get; set; }
    public Byte AttackType { get; set; }
    public Byte AttackPower { get; set; }

    /// <summary>Purpose not documented in the original reverse-engineered format.</summary>
    public UInt16 Unknown0 { get; set; }

    public Byte Target { get; set; }
    public Byte AttackFlags { get; set; }
    public Byte HitCount { get; set; }

    /// <summary>
    /// Typed as a raw <see cref="Byte"/> rather than <see cref="Element"/> in the original
    /// reverse-engineered format, unlike the equivalent field in sibling limit-break sections
    /// (e.g. <see cref="SquallLimit.Element"/>); standardized here to <see cref="Element"/>.
    /// </summary>
    public Element Element { get; set; }

    public Byte ElementPercent { get; set; }
    public Byte StatusAttack { get; set; }
    public UInt16 Statuses0 { get; set; }
    public Byte ItemIndex { get; set; }
    public Byte Crit { get; set; }
    public UInt32 Statuses1 { get; set; }
}
