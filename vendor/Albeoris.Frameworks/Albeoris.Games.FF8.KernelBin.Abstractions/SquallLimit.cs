namespace Albeoris.Games.FF8.KernelBin.Abstractions;

/// <summary>One of Squall's Renzokuken limit break finishers.</summary>
public sealed class SquallLimit
{
    /// <summary>The display name, or <see langword="null"/> if this slot has no name.</summary>
    public String? Name { get; set; }

    /// <summary>The display description, or <see langword="null"/> if this slot has no description.</summary>
    public String? Description { get; set; }

    public UInt16 MagicId { get; set; }
    public Byte AttackType { get; set; }

    /// <summary>Purpose not documented in the original reverse-engineered format.</summary>
    public Byte Unknown0 { get; set; }

    public Byte AttackPower { get; set; }

    /// <summary>Purpose not documented in the original reverse-engineered format.</summary>
    public Byte Unknown1 { get; set; }

    public Byte Target { get; set; }
    public Byte AttackFlags { get; set; }
    public Byte HitCount { get; set; }
    public Element Element { get; set; }
    public Byte ElementPercent { get; set; }
    public Byte StatusAttack { get; set; }

    /// <summary>Purpose not documented in the original reverse-engineered format.</summary>
    public Byte Unknown2 { get; set; }

    /// <summary>
    /// Undetermined byte. Present in the original file layout but never assigned a known
    /// meaning by the reference implementation; preserved verbatim for a lossless round trip.
    /// </summary>
    public Byte Unknown3 { get; set; }

    public UInt16 Statuses0 { get; set; }
    public UInt32 Statuses1 { get; set; }
}
