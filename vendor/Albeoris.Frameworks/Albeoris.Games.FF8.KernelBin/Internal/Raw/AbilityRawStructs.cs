namespace Albeoris.Games.FF8.KernelBin.Internal.Raw;

/// <summary>Binary layouts of the ability sections. See <see cref="BattleRawStructs"/> remarks.</summary>
internal struct JunctionAbilityRaw
{
    public UInt16 OffsetName;
    public UInt16 OffsetDescription;
    public Byte AbilityPoints;
    public Byte Flag1;
    public Byte Flag2;
    public Byte Flag3;
}

internal struct CommandAbilityRaw
{
    public UInt16 OffsetName;
    public UInt16 OffsetDescription;
    public Byte AbilityPoints;
    public Byte Index;
    public UInt16 Unknown0;
}

internal struct CharacterStatAbilityRaw
{
    public UInt16 OffsetName;
    public UInt16 OffsetDescription;
    public Byte AbilityPoints;
    public Byte Stat;
    public Byte Value;
    public Byte Unknown0;
}

internal struct CharacterAbilityRaw
{
    public UInt16 OffsetName;
    public UInt16 OffsetDescription;
    public Byte AbilityPoints;
    public Byte Flag1;
    public Byte Flag2;
    public Byte Flag3;
}

internal struct PartyAbilityRaw
{
    public UInt16 OffsetName;
    public UInt16 OffsetDescription;
    public Byte AbilityPoints;
    public Byte Flag1;
    public UInt16 Flag2;
}

internal struct GuardianAbilityRaw
{
    public UInt16 OffsetName;
    public UInt16 OffsetDescription;
    public Byte AbilityPoints;
    public Byte Boost;
    public Byte Stat;
    public Byte Value;
}

internal struct MenuAbilityRaw
{
    public UInt16 OffsetName;
    public UInt16 OffsetDescription;
    public Byte AbilityPoints;
    public Byte Index;
    public Byte Start;
    public Byte End;
}
