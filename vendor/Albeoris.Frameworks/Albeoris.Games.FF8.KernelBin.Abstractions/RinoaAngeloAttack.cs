namespace Albeoris.Games.FF8.KernelBin.Abstractions;

/// <summary>One of Rinoa's Angelo pet attacks.</summary>
public sealed class RinoaAngeloAttack
{
    /// <summary>The display name, or <see langword="null"/> if this slot has no name.</summary>
    public String? Name { get; set; }

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

    /// <summary>
    /// Typed as a raw <see cref="Byte"/> rather than <see cref="Element"/> in the original
    /// reverse-engineered format, unlike the equivalent field in sibling limit-break sections
    /// (e.g. <see cref="SquallLimit.Element"/>); standardized here to <see cref="Element"/>.
    /// </summary>
    public Element Element { get; set; }

    public Byte ElementPercent { get; set; }
    public Byte StatusAttack { get; set; }
    public UInt16 Statuses0 { get; set; }
    public UInt32 Statuses1 { get; set; }
}
