namespace Memoria.FF7.Runtime;

public readonly record struct Ff7BattleStatusTimers(ushort Remainder, byte Sleep, byte Poison)
{
    public byte? Barrier { get; init; }
    public byte? MagicBarrier { get; init; }
    public byte? Shield { get; init; }
    public ushort? HitPointRemainder { get; init; }
}

public readonly record struct Ff7BattleStatusTick(Ff7BattleStatusTimers Timers, uint Statuses, bool PoisonDue)
{
    public int HitPointDelta { get; init; }
}

/// <summary>The reached native status timers, advanced on eligible battle time before queued actions.</summary>
public static class Ff7NativeBattleStatusClock
{
    public const ushort PoisonElement = 0x10;
    public static Ff7BattleDamageAction PoisonAttack { get; } = new(0x04, 1, 255, LongRange: true);

    private const uint Sleep = Ff7NativeBattleActionResolver.Sleep;
    private const uint Poison = Ff7NativeBattleActionResolver.Poison;
    private const uint StopOrDeath = 0x401;
    private const uint Haste = 0x100;
    private const uint Slow = Ff7NativeBattleActionResolver.Slow;
    private const int TimeUnit = 8192;
    private const byte SleepDuration = 100;
    private const byte PoisonInterval = 10;

    public static Ff7BattleStatusTimers Apply(Ff7BattleStatusTimers timers, uint previousStatuses, uint statuses)
    {
        uint added = statuses & ~previousStatuses;
        byte sleep = (statuses & Sleep) == 0 ? (byte)0 : (added & Sleep) != 0 ? SleepDuration : timers.Sleep;
        byte poison = (statuses & Poison) == 0 ? (byte)0 : (added & Poison) != 0 ? PoisonInterval : timers.Poison;
        byte? Duration(uint mask, byte? saved, byte duration) => (statuses & mask) == 0 ? saved is null ? null : (byte)0 : (added & mask) != 0 || saved is null or 0 ? duration : saved.Value;
        return timers with { Sleep = sleep, Poison = poison, Barrier = Duration(Ff7NativeBattleStatuses.Barrier, timers.Barrier, 127), MagicBarrier = Duration(Ff7NativeBattleStatuses.MagicBarrier, timers.MagicBarrier, 127), Shield = Duration(Ff7NativeBattleStatuses.Shield, timers.Shield, 64) };
    }

    public static Ff7BattleStatusTick Advance(Ff7BattleStatusTimers timers, uint statuses, byte battleSpeed, byte imprisonedType = 0, uint maximumHitPoints = 0)
    {
        if (timers.Remainder >= TimeUnit || timers.Sleep > SleepDuration || timers.Poison > PoisonInterval || timers.Barrier > 127 || timers.MagicBarrier > 127 || timers.Shield > 64 || timers.HitPointRemainder >= 32768 || imprisonedType > 3)
        {
            throw new InvalidDataException("FFVII battle status timer continuation is invalid.");
        }
        timers = Apply(timers, statuses, statuses);
        uint clockStatuses = Ff7NativeBattleStatuses.ProjectClockStatuses(statuses, imprisonedType);
        if ((clockStatuses & StopOrDeath) != 0)
        {
            return new(timers, statuses, false);
        }

        int rate = Ff7NativeAtb.BaseRate(battleSpeed);
        if ((statuses & Slow) == 0)
        {
            rate *= (statuses & Haste) == 0 ? 2 : 4;
        }
        int hpDelta = 0;
        if ((statuses & Ff7NativeBattleStatuses.Imprisoned) != 0 && imprisonedType == 1)
        {
            long accumulated = (timers.HitPointRemainder ?? 0) - (long)(maximumHitPoints >> 5) * rate;
            hpDelta = checked((int)(accumulated >> 15));
            timers = timers with { HitPointRemainder = (ushort)(accumulated & 32767) };
        }
        int time = timers.Remainder + rate;
        timers = timers with { Remainder = (ushort)(time % TimeUnit) };
        if (time < TimeUnit)
        {
            return new(timers, statuses, false) { HitPointDelta = hpDelta };
        }

        bool poisonDue = timers.Poison == 1 && (statuses & Poison) != 0;
        if (timers.Sleep != 0)
        {
            timers = timers with { Sleep = (byte)(timers.Sleep - 1) };
            if (timers.Sleep == 0)
            {
                statuses &= ~Sleep;
            }
        }
        if (timers.Poison != 0)
        {
            timers = timers with { Poison = poisonDue ? PoisonInterval : (byte)(timers.Poison - 1) };
        }
        byte? Expire(byte? remaining, uint mask)
        {
            if (remaining > 0)
            {
                remaining--;
                if (remaining == 0)
                {
                    statuses &= ~mask;
                }
            }
            return remaining;
        }
        timers = timers with { Barrier = Expire(timers.Barrier, Ff7NativeBattleStatuses.Barrier), MagicBarrier = Expire(timers.MagicBarrier, Ff7NativeBattleStatuses.MagicBarrier), Shield = Expire(timers.Shield, Ff7NativeBattleStatuses.Shield) };
        return new(timers, statuses, poisonDue) { HitPointDelta = hpDelta };
    }

    public static byte ReadPersistentStatuses(byte saved) => (byte)(saved & 0x30);
}
