using Albeoris.Games.FF8.KernelBin.Abstractions;

namespace Albeoris.Games.FF8.KernelBin.Internal.Raw;

/// <summary>
/// Binary layouts of the fixed-size record sections used by the kernel.bin format. These
/// mirror the exact byte layout expected by the game and are used only for I/O; public
/// consumers work with the richer types in <c>Albeoris.Games.FF8.KernelBin.Abstractions</c>.
/// </summary>
internal struct BattleCommandRaw
{
    public UInt16 OffsetName;
    public UInt16 OffsetDescription;
    public Byte AbilityId;
    public Byte Target;
    public Byte Unknown1;
    public Byte Unknown2;
}

internal struct MagicRaw
{
    public UInt16 OffsetName;
    public UInt16 OffsetDescription;
    public UInt16 MagicId;
    public Byte Unknown1;
    public Byte AttackType;
    public Byte SpellPower;
    public Byte Unknown2;
    public Byte DefaultTarget;
    public Byte Flags;
    public Byte DrawResist;
    public Byte HitCount;
    public Element Element;
    public Byte Unknown4;
    public Byte StatusMagic1;
    public Byte StatusMagic2;
    public Byte StatusMagic3;
    public Byte StatusMagic4;
    public Byte StatusMagic5;
    public Byte Unknown5;
    public Byte StatusAttack;
    public Byte HP;
    public Byte STR;
    public Byte VIT;
    public Byte MAG;
    public Byte SPR;
    public Byte SPD;
    public Byte EVA;
    public Byte HIT;
    public Byte LUCK;
    public Byte ElemAttackEnabled;
    public Byte ElemAttackValue;
    public Byte ElemDefenseEnabled;
    public Byte ElemDefenseValue;
    public Byte StatusAttackValue;
    public Byte StatusDefenseValue;
    public UInt16 StatusAttackEnabled;
    public UInt16 StatusDefenseEnabled;
    public Byte QuezacoltCompatibility;
    public Byte ShivaCompatibility;
    public Byte IfritCompatibility;
    public Byte SirenCompatibility;
    public Byte BrothersCompatibility;
    public Byte DiablosCompatibility;
    public Byte CarbuncleCompatibility;
    public Byte LeviathanCompatibility;
    public Byte PandemonaCompatibility;
    public Byte CerberusCompatibility;
    public Byte AlexanderCompatibility;
    public Byte DoomtrainCompatibility;
    public Byte BahamutCompatibility;
    public Byte CactuarCompatibility;
    public Byte TonberryCompatibility;
    public Byte EdenCompatibility;
    public UInt16 Unknown6;
}

internal unsafe struct GuardianRaw
{
    public Int16 OffsetAttackName;
    public Int16 OffsetAttackDescription;
    public UInt16 MagicId;
    public Byte AttackType;
    public Byte Power;
    public Byte Flags;
    public Element AttackElement;
    public Byte AttackFlags;
    public Byte Unknown2;
    public Byte Unknown3;
    public Element SecondaryElement;
    public UInt16 Statuses0;
    public UInt32 Statuses1;
    public Byte HpModifier;
    public Byte Unknown4;
    public Byte Unknown5;
    public Byte Unknown6;
    public Byte ExpPerLevel;
    public Byte Unknown7;
    public Byte Unknown8;
    public Byte StatusAttack;
    public fixed Int32 Abilities[21];
    public fixed Byte MagicCompatibility[16];
    public Byte Unknown9;
    public Byte Unknown10;
    public Byte PowerModifier;
    public Byte LevelModifier;
}

internal struct EnemyAttackRaw
{
    public UInt16 OffsetName;
    public UInt16 MagicId;
    public Byte CameraChange;
    public Byte Unknown0;
    public Byte AttackType;
    public Byte AttackPower;
    public Byte AttackFlags;
    public Byte Unknown1;
    public Element Element;
    public Byte Unknown2;
    public Byte StatusAttack;
    public Byte AttackParam;
    public UInt16 Statuses0;
    public UInt32 Statuses1;
}

internal struct WeaponRaw
{
    public UInt16 OffsetName;
    public Byte RenzokukenFinishers;
    public Byte CharacterId;
    public Byte AttackType;
    public Byte AttackPower;
    public Byte AttackParam;
    public Byte StrBonus;
    public Byte Tier;
    public Byte CritBonus;
    public Byte Melee;
    public Byte Unknown0;
}

internal struct AdditionalCommandRaw
{
    public UInt16 MagicId;
    public UInt16 Unknown;
    public Byte AttackType;
    public Byte AttackPower;
    public Byte AttackFlags;
    public Byte HitCount;
    public Element Element;
    public Byte StatusAttack;
    public UInt16 Status1;
    public UInt32 Status2;
}

internal struct BattleItemRaw
{
    public UInt16 OffsetName;
    public UInt16 OffsetDescription;
    public UInt16 MagicId;
    public Byte AttackType;
    public Byte AttackPower;
    public Byte Unknown0;
    public Byte Target;
    public Byte Unknown1;
    public Byte AttackFlags;
    public Byte Unknown2;
    public Byte StatusAttack;
    public UInt16 Statuses0;
    public UInt32 Statuses1;
    public Byte AttackParam;
    public Byte Unknown3;
    public Byte HitCount;
    public Element Element;
}

internal struct FieldItemRaw
{
    public UInt16 OffsetName;
    public UInt16 OffsetDescription;
}

internal struct IndependentGuardianAttackRaw
{
    public UInt16 OffsetAttackName;
    public UInt16 MagicId;
    public Byte AttackType;
    public Byte Power;
    public Byte Status;
    public Byte Unknown0;
    public Byte Flags;
    public Byte Unknown1;
    public Element Element;
    public Byte Unknown2;
    public UInt32 Statuses1;
    public UInt16 Statuses0;
    public Byte PowerModifier;
    public Byte LevelModifier;
}
