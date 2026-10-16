namespace Albeoris.Games.FF8.KernelBin.Abstractions;

/// <summary>
/// A Guardian Force: its special attack, stats, ability unlock list and magic compatibility.
/// </summary>
public sealed class Guardian
{
    /// <summary>The display name of the GF's special attack, or <see langword="null"/> if none.</summary>
    public String? AttackName { get; set; }

    /// <summary>The display description of the GF's special attack, or <see langword="null"/> if none.</summary>
    public String? AttackDescription { get; set; }

    public UInt16 MagicId { get; set; }
    public Byte AttackType { get; set; }
    public Byte Power { get; set; }
    public Byte Flags { get; set; }
    public Element AttackElement { get; set; }
    public Byte AttackFlags { get; set; }

    /// <summary>Purpose not documented in the original reverse-engineered format.</summary>
    public Byte Unknown2 { get; set; }

    /// <summary>Purpose not documented in the original reverse-engineered format.</summary>
    public Byte Unknown3 { get; set; }

    /// <summary>
    /// A second elemental affinity of unclear purpose. Named <c>element</c> (lowercase) in the
    /// original reverse-engineered source; renamed here to follow this project's naming
    /// conventions, but its exact meaning relative to <see cref="AttackElement"/> is undocumented.
    /// </summary>
    public Element SecondaryElement { get; set; }

    public UInt16 Statuses0 { get; set; }
    public UInt32 Statuses1 { get; set; }
    public Byte HpModifier { get; set; }

    /// <summary>Purpose not documented in the original reverse-engineered format.</summary>
    public Byte Unknown4 { get; set; }

    /// <summary>Purpose not documented in the original reverse-engineered format.</summary>
    public Byte Unknown5 { get; set; }

    /// <summary>Purpose not documented in the original reverse-engineered format.</summary>
    public Byte Unknown6 { get; set; }

    public Byte ExpPerLevel { get; set; }

    /// <summary>Purpose not documented in the original reverse-engineered format.</summary>
    public Byte Unknown7 { get; set; }

    /// <summary>Purpose not documented in the original reverse-engineered format.</summary>
    public Byte Unknown8 { get; set; }

    public Byte StatusAttack { get; set; }

    /// <summary>The 21 ability identifiers this GF can teach, in unlock order.</summary>
    public Int32[] Abilities { get; set; } = new Int32[21];

    /// <summary>The compatibility bonus this GF grants for each of the 16 magic slots.</summary>
    public Byte[] MagicCompatibility { get; set; } = new Byte[16];

    /// <summary>Purpose not documented in the original reverse-engineered format.</summary>
    public Byte Unknown9 { get; set; }

    /// <summary>Purpose not documented in the original reverse-engineered format.</summary>
    public Byte Unknown10 { get; set; }

    public Byte PowerModifier { get; set; }
    public Byte LevelModifier { get; set; }
}
