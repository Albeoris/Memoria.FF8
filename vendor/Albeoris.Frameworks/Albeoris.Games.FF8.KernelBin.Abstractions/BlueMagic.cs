namespace Albeoris.Games.FF8.KernelBin.Abstractions;

/// <summary>One of Quistis's Blue Magic spells, learned by observing enemy attacks.</summary>
public sealed class BlueMagic
{
    /// <summary>The display name, or <see langword="null"/> if this slot has no name.</summary>
    public String? Name { get; set; }

    /// <summary>The display description, or <see langword="null"/> if this slot has no description.</summary>
    public String? Description { get; set; }

    public UInt16 MagicId { get; set; }

    /// <summary>Purpose not documented in the original reverse-engineered format.</summary>
    public Byte Unknown0 { get; set; }

    public Byte AttackType { get; set; }

    /// <summary>Purpose not documented in the original reverse-engineered format.</summary>
    public Byte Unknown1 { get; set; }

    public Byte Target { get; set; }
    public Byte AttackFlags { get; set; }

    /// <summary>Purpose not documented in the original reverse-engineered format.</summary>
    public Byte Unknown2 { get; set; }

    public Byte Element { get; set; }
    public Byte StatusAttack { get; set; }
    public Byte Crit { get; set; }

    /// <summary>Purpose not documented in the original reverse-engineered format.</summary>
    public Byte Unknown3 { get; set; }
}
