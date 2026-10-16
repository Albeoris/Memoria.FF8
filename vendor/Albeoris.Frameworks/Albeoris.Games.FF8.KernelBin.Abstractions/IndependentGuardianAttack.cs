namespace Albeoris.Games.FF8.KernelBin.Abstractions;

/// <summary>An attack usable by a Guardian Force independently of its main special attack.</summary>
public sealed class IndependentGuardianAttack
{
    /// <summary>The display name, or <see langword="null"/> if this slot has no name.</summary>
    public String? AttackName { get; set; }

    public UInt16 MagicId { get; set; }
    public Byte AttackType { get; set; }
    public Byte Power { get; set; }
    public Byte Status { get; set; }

    /// <summary>Purpose not documented in the original reverse-engineered format.</summary>
    public Byte Unknown0 { get; set; }

    public Byte Flags { get; set; }

    /// <summary>Purpose not documented in the original reverse-engineered format.</summary>
    public Byte Unknown1 { get; set; }

    public Element Element { get; set; }

    /// <summary>
    /// Undetermined byte. Present in the original file layout but never assigned a known
    /// meaning by the reference implementation; preserved verbatim for a lossless round trip.
    /// </summary>
    public Byte Unknown2 { get; set; }

    public UInt32 Statuses1 { get; set; }
    public UInt16 Statuses0 { get; set; }
    public Byte PowerModifier { get; set; }
    public Byte LevelModifier { get; set; }
}
