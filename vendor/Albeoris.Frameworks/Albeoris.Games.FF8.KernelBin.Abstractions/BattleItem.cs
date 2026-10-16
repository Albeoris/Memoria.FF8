namespace Albeoris.Games.FF8.KernelBin.Abstractions;

/// <summary>An item usable in battle.</summary>
public sealed class BattleItem
{
    /// <summary>The display name, or <see langword="null"/> if this slot has no name.</summary>
    public String? Name { get; set; }

    /// <summary>The display description, or <see langword="null"/> if this slot has no description.</summary>
    public String? Description { get; set; }

    public UInt16 MagicId { get; set; }
    public Byte AttackType { get; set; }
    public Byte AttackPower { get; set; }

    /// <summary>Purpose not documented in the original reverse-engineered format.</summary>
    public Byte Unknown0 { get; set; }

    public Byte Target { get; set; }

    /// <summary>Purpose not documented in the original reverse-engineered format.</summary>
    public Byte Unknown1 { get; set; }

    public Byte AttackFlags { get; set; }

    /// <summary>Purpose not documented in the original reverse-engineered format.</summary>
    public Byte Unknown2 { get; set; }

    public Byte StatusAttack { get; set; }
    public UInt16 Statuses0 { get; set; }
    public UInt32 Statuses1 { get; set; }
    public Byte AttackParam { get; set; }

    /// <summary>Purpose not documented in the original reverse-engineered format.</summary>
    public Byte Unknown3 { get; set; }

    public Byte HitCount { get; set; }
    public Element Element { get; set; }
}
