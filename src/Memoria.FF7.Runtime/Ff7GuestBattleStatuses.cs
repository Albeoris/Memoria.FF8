using Memoria.FF7.Formats;

namespace Memoria.FF7.Runtime;

/// <summary>Projects common battle statuses between FFVIII records and the FFVII combat owner.</summary>
public static class Ff7GuestBattleStatuses
{
    private const uint Haste = 0x100;
    private const uint Stop = 0x400;
    private const uint Petrify = 0x4000;
    private const uint Barrier = 0x10000;
    private const uint MagicBarrier = 0x20000;
    private const uint Berserk = 0x800000;
    private const uint Silence = 0x80;

    public const uint SupportedHost = Ff7NativeBattleActionResolver.Death | Ff7NativeBattleActionResolver.Sleep | Ff7NativeBattleActionResolver.Poison | Ff7NativeBattleActionResolver.Slow | Ff7NativeBattleActionResolver.Darkness | Silence | Haste | Stop | Petrify | Barrier | MagicBarrier | Berserk;
    public const ushort SupportedOrigin = 0x3F;

    // FFVIII keeps the saved and battle banks separate. Its defense row indexes battle bits after the saved byte.
    private static readonly (int OriginIndex, uint HostMask)[] Common =
    [
        (0, Ff7NativeBattleActionResolver.Death),
        (1, Ff7NativeBattleActionResolver.Poison),
        (2, Petrify),
        (3, Ff7NativeBattleActionResolver.Darkness),
        (4, Silence),
        (5, Berserk),
        (8, Ff7NativeBattleActionResolver.Sleep),
        (9, Haste),
        (10, Ff7NativeBattleActionResolver.Slow),
        (11, Stop),
        (13, Barrier),
        (14, MagicBarrier)
    ];

    public static bool CanCastMagic(uint host) => (host & (Ff7NativeBattleActionResolver.Death | Ff7NativeBattleActionResolver.Sleep | Petrify | Silence | Stop | Berserk)) == 0;

    public static uint FromOrigin(ushort saved) => ToHostStatuses(saved, 0);

    public static uint ToHostStatuses(ushort saved, uint battle)
    {
        ulong origin = saved & 0xFFu | (ulong)battle << 8;
        uint host = 0;
        foreach ((int index, uint mask) in Common)
        {
            if ((origin & (1ul << index)) != 0)
            {
                host |= mask;
            }
        }
        return host;
    }

    public static ushort ToOrigin(uint host, ushort originalSaved = 0)
    {
        ushort saved = (ushort)(originalSaved & ~SupportedOrigin);
        foreach ((int index, uint mask) in Common)
        {
            if (index < 8 && (host & mask) != 0)
            {
                saved |= (ushort)(1 << index);
            }
        }
        return saved;
    }

    public static uint ToOriginBattle(uint host)
    {
        uint battle = 0;
        foreach ((int index, uint mask) in Common)
        {
            if (index >= 8 && (host & mask) != 0)
            {
                battle |= 1u << (index - 8);
            }
        }
        return battle;
    }

    public static ushort ToHostElements(byte elements) => Ff7GuestBattleElements.ToHost(elements);

    public static Ff7BattleAttack? ToHostStatusRecord(ushort saved, uint battle, byte chance)
    {
        uint mask = ToHostStatuses(saved, battle);
        if (mask == 0 || chance <= 100)
        {
            return null;
        }
        // FFVIII encodes its junction contribution above a 100 baseline. FFVII uses four-percent steps.
        byte hostChance = chance >= 200 ? (byte)63 : (byte)Math.Min(25, (chance - 100 + 4) / 4);
        return new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, hostChance, byte.MaxValue, 0, mask, 0, ushort.MaxValue);
    }

    internal static Ff7BattleAttack? ProjectIncoming(Ff7BattleAttack? record)
        => record is { StatusChange: < 0xC0 } && (record.StatusMask & Ff7NativeBattleStatuses.Imprisoned) == 0 ? record with { StatusMask = record.StatusMask & (SupportedHost | Ff7NativeBattleStatuses.SupportedRecordStatuses) } : record;

    internal static uint ResistIncoming(uint mask, uint present, IReadOnlyList<byte> defenses, Ff7BattleRng random)
    {
        foreach ((int index, uint bit) in Common)
        {
            if ((mask & ~present & bit) == 0)
            {
                continue;
            }
            int resistance = defenses[index] - 100;
            if (resistance >= 100 || resistance > 0 && (random.NextByte() * 100 >> 8) < resistance)
            {
                mask &= ~bit;
            }
        }
        return mask;
    }
}
