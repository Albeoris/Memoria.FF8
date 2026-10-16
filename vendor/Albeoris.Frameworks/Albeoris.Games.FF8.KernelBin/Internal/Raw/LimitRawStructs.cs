using Albeoris.Games.FF8.KernelBin.Abstractions;

namespace Albeoris.Games.FF8.KernelBin.Internal.Raw;

/// <summary>Binary layouts of the character/limit-break sections. See <see cref="BattleRawStructs"/> remarks.</summary>
internal struct SquallLimitRaw
{
    public UInt16 OffsetName;
    public UInt16 OffsetDescription;
    public UInt16 MagicId;
    public Byte AttackType;
    public Byte Unknown0;
    public Byte AttackPower;
    public Byte Unknown1;
    public Byte Target;
    public Byte AttackFlags;
    public Byte HitCount;
    public Element Element;
    public Byte ElementPercent;
    public Byte StatusAttack;
    public Byte Unknown2;
    public Byte Unknown3;
    public UInt16 Statuses0;
    public UInt32 Statuses1;
}

internal struct CharacterRaw
{
    public UInt16 OffsetName;
    public Byte CrisisLevel;
    public Byte Gender;
    public Byte LimitId;
    public Byte LimitParam;
    public Byte Exp1;
    public Byte Exp2;
    public Byte Hp1;
    public Byte Hp2;
    public Byte Hp3;
    public Byte Hp4;
    public Byte Str1;
    public Byte Str2;
    public Byte Str3;
    public Byte Str4;
    public Byte Vit1;
    public Byte Vit2;
    public Byte Vit3;
    public Byte Vit4;
    public Byte Mag1;
    public Byte Mag2;
    public Byte Mag3;
    public Byte Mag4;
    public Byte Spr1;
    public Byte Spr2;
    public Byte Spr3;
    public Byte Spr4;
    public Byte Spd1;
    public Byte Spd2;
    public Byte Spd3;
    public Byte Spd4;
    public Byte Luck1;
    public Byte Luck2;
    public Byte Luck3;
    public Byte Luck4;
}

internal struct NpcLimitRaw
{
    public UInt16 OffsetName;
    public UInt16 OffsetDescription;
    public UInt16 MagicId;
    public Byte AttackType;
    public Byte AttackPower;
    public UInt16 Unknown0;
    public Byte Target;
    public Byte AttackFlags;
    public Byte HitCount;
    public Byte Element;
    public Byte ElementPercent;
    public Byte StatusAttack;
    public UInt16 Statuses0;
    public UInt16 Unknown1;
    public UInt32 Statuses1;
}

internal struct BlueMagicRaw
{
    public UInt16 OffsetName;
    public UInt16 OffsetDescription;
    public UInt16 MagicId;
    public Byte Unknown0;
    public Byte AttackType;
    public Byte Unknown1;
    public Byte Target;
    public Byte AttackFlags;
    public Byte Unknown2;
    public Byte Element;
    public Byte StatusAttack;
    public Byte Crit;
    public Byte Unknown3;
}

internal struct QuistisLimitRaw
{
    public Int32 Statuses1;
    public Int16 Statuses0;
    public Byte AttackPower;
    public Byte AttackParam;
}

internal struct IrvinLimitRaw
{
    public UInt16 OffsetName;
    public UInt16 OffsetDescription;
    public UInt16 MagicId;
    public Byte AttackType;
    public Byte AttackPower;
    public UInt16 Unknown0;
    public Byte Target;
    public Byte AttackFlags;
    public Byte HitCount;
    public Byte Element;
    public Byte ElementPercent;
    public Byte StatusAttack;
    public UInt16 Statuses0;
    public Byte ItemIndex;
    public Byte Crit;
    public UInt32 Statuses1;
}

internal struct ZellLimitRaw
{
    public UInt16 OffsetName;
    public UInt16 OffsetDescription;
    public UInt16 MagicId;
    public Byte AttackType;
    public Byte AttackPower;
    public Byte AttackFlags;
    public Byte Unknown0;
    public Byte Target;
    public Byte Unknown1;
    public Byte HitCount;
    public Element Element;
    public Byte ElementPercent;
    public Byte StatusAttack;
    public UInt16 Combo1;
    public UInt16 Combo2;
    public UInt16 Combo3;
    public UInt16 Combo4;
    public UInt16 Combo5;
    public UInt16 Status0;
    public UInt32 Status1;
}

internal struct ZellDuelMoveRaw
{
    public Byte StartMove;
    public Byte NextSequence1;
    public Byte NextSequence2;
    public Byte NextSequence3;
}

internal struct RinoaLimitRaw
{
    public UInt16 OffsetName;
    public UInt16 OffsetDescription;
    public Byte Unknown;
    public Byte Target;
    public Byte AbilityId;
    public Byte Unknown1;
}

internal struct RinoaAngeloAttackRaw
{
    public UInt16 OffsetName;
    public UInt16 MagicId;
    public Byte AttackType;
    public Byte AttackPower;
    public Byte AttackFlags;
    public Byte Unknown0;
    public Byte Target;
    public Byte Unknown1;
    public Byte HitCount;
    public Byte Element;
    public Byte ElementPercent;
    public Byte StatusAttack;
    public UInt16 Statuses0;
    public UInt32 Statuses1;
}
