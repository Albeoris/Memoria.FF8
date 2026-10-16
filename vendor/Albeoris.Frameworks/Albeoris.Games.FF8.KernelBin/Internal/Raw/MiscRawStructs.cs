namespace Albeoris.Games.FF8.KernelBin.Internal.Raw;

/// <summary>Binary layouts of the miscellaneous sections. See <see cref="BattleRawStructs"/> remarks.</summary>
internal struct SelphieMagicCountRaw
{
    public Byte MagicId;
    public Byte Count;
}

internal struct SelphieSlotSetRaw
{
    public SelphieMagicCountRaw Slot1;
    public SelphieMagicCountRaw Slot2;
    public SelphieMagicCountRaw Slot3;
    public SelphieMagicCountRaw Slot4;
    public SelphieMagicCountRaw Slot5;
    public SelphieMagicCountRaw Slot6;
    public SelphieMagicCountRaw Slot7;
    public SelphieMagicCountRaw Slot8;
}

internal struct DevourEffectRaw
{
    public UInt16 OffsetDescription;
    public Byte Effect;
    public Byte Quantity;
    public UInt32 Statuses1;
    public UInt16 Statuses0;
    public Byte StatFlags;
    public Byte Hp;
}

internal unsafe struct TimerSettingsRaw
{
    public fixed Byte StatusTimers[14];
    public Byte AtbSpeedMultiplier;
    public Byte DeadTimer;
    public fixed Byte StatusLimitEffects[32];
    public fixed Byte DuelTimersAndStartMoves[8];
    public fixed Byte ShotTimers[4];
}
