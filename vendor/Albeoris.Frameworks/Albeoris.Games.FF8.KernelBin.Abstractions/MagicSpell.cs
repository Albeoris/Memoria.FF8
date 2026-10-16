namespace Albeoris.Games.FF8.KernelBin.Abstractions;

/// <summary>
/// A magic spell, including its combat behavior, junction bonuses and GF draw compatibility.
/// </summary>
public sealed class MagicSpell
{
    /// <summary>The display name, or <see langword="null"/> if this slot has no name.</summary>
    public String? Name { get; set; }

    /// <summary>The display description, or <see langword="null"/> if this slot has no description.</summary>
    public String? Description { get; set; }

    /// <summary>The identifier used to reference this spell from other sections (e.g. items, attacks).</summary>
    public UInt16 MagicId { get; set; }

    /// <summary>Purpose not documented in the original reverse-engineered format.</summary>
    public Byte Unknown1 { get; set; }

    public Byte AttackType { get; set; }
    public Byte SpellPower { get; set; }

    /// <summary>Purpose not documented in the original reverse-engineered format.</summary>
    public Byte Unknown2 { get; set; }

    public Byte DefaultTarget { get; set; }
    public Byte Flags { get; set; }
    public Byte DrawResist { get; set; }
    public Byte HitCount { get; set; }
    public Element Element { get; set; }

    /// <summary>Purpose not documented in the original reverse-engineered format.</summary>
    public Byte Unknown4 { get; set; }

    public Byte StatusMagic1 { get; set; }
    public Byte StatusMagic2 { get; set; }
    public Byte StatusMagic3 { get; set; }
    public Byte StatusMagic4 { get; set; }
    public Byte StatusMagic5 { get; set; }

    /// <summary>Purpose not documented in the original reverse-engineered format.</summary>
    public Byte Unknown5 { get; set; }

    public Byte StatusAttack { get; set; }

    public Byte HP { get; set; }
    public Byte STR { get; set; }
    public Byte VIT { get; set; }
    public Byte MAG { get; set; }
    public Byte SPR { get; set; }
    public Byte SPD { get; set; }
    public Byte EVA { get; set; }
    public Byte HIT { get; set; }
    public Byte LUCK { get; set; }

    public Byte ElemAttackEnabled { get; set; }
    public Byte ElemAttackValue { get; set; }
    public Byte ElemDefenseEnabled { get; set; }
    public Byte ElemDefenseValue { get; set; }
    public Byte StatusAttackValue { get; set; }
    public Byte StatusDefenseValue { get; set; }
    public UInt16 StatusAttackEnabled { get; set; }
    public UInt16 StatusDefenseEnabled { get; set; }

    public Byte QuezacoltCompatibility { get; set; }
    public Byte ShivaCompatibility { get; set; }
    public Byte IfritCompatibility { get; set; }
    public Byte SirenCompatibility { get; set; }
    public Byte BrothersCompatibility { get; set; }
    public Byte DiablosCompatibility { get; set; }
    public Byte CarbuncleCompatibility { get; set; }
    public Byte LeviathanCompatibility { get; set; }
    public Byte PandemonaCompatibility { get; set; }
    public Byte CerberusCompatibility { get; set; }
    public Byte AlexanderCompatibility { get; set; }
    public Byte DoomtrainCompatibility { get; set; }
    public Byte BahamutCompatibility { get; set; }
    public Byte CactuarCompatibility { get; set; }
    public Byte TonberryCompatibility { get; set; }
    public Byte EdenCompatibility { get; set; }

    /// <summary>Purpose not documented in the original reverse-engineered format.</summary>
    public UInt16 Unknown6 { get; set; }
}
