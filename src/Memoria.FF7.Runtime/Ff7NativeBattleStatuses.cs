using Memoria.FF7.Formats;

namespace Memoria.FF7.Runtime;

public static class Ff7NativeBattleStatuses
{
    public const uint Silence = 0x80;
    public const uint Barrier = 0x10000;
    public const uint MagicBarrier = 0x20000;
    public const uint Shield = 0x100000;
    public const uint DeathForce = 0x10000000;
    public const uint Resist = 0x20000000;
    public const uint Imprisoned = 0x80000000;
    public const uint SupportedRecordStatuses = Silence | Barrier | MagicBarrier | Shield | DeathForce | Resist | Imprisoned;

    public static uint ReadRecordMask(Ff7BattleAttack? record)
    {
        if (record is null || record.StatusChange >= 0xC0)
        {
            return 0;
        }
        return (record.StatusMask & Imprisoned) != 0 ? Imprisoned : record.StatusMask;
    }

    public static int ReadRecordMode(Ff7BattleAttack? record)
    {
        return ReadImprisonedType(record) is not null ? 0 : record is null ? 3 : record.StatusChange >> 6;
    }

    public static byte? ReadImprisonedType(Ff7BattleAttack? record)
    {
        return record is { StatusChange: < 0xC0 } && (record.StatusMask & Imprisoned) != 0 ? (byte)(record.StatusMask & 3) : null;
    }

    public static bool IsBattleParticipant(uint statuses, byte imprisonedType)
    {
        return (statuses & 0x4001) == 0 && ((statuses & Imprisoned) == 0 || imprisonedType is not (0 or 3));
    }

    public static uint ProjectClockStatuses(uint statuses, byte imprisonedType)
    {
        if ((statuses & Imprisoned) == 0)
        {
            return statuses;
        }
        return imprisonedType switch
        {
            0 or 3 => statuses | 0x400,
            1 => statuses | 0x02000000,
            _ => statuses
        };
    }

    public static Ff7BattleDamageAffinity ShieldAffinity(uint statuses, ushort elements, bool item, Ff7BattleDamageAffinity affinity)
    {
        if ((statuses & Shield) == 0 || item || elements == ushort.MaxValue)
        {
            return affinity;
        }
        if ((elements & 0x1FF) != 0)
        {
            affinity |= Ff7BattleDamageAffinity.Absorb;
        }
        if ((elements & 0x7E00) != 0)
        {
            affinity |= Ff7BattleDamageAffinity.Nullify;
        }
        return affinity;
    }
}

public sealed partial class Ff7BattleSession
{
    private byte NativeImprisonedType => state.ImprisonedType ?? 0;

    private void PublishNativeStatusContext(Ff7BattleAttack? record)
    {
        if (Ff7NativeBattleStatuses.ReadImprisonedType(record) is byte type)
        {
            state = state with { ImprisonedType = type };
        }
    }
}
