namespace Memoria.FF7.Runtime;

public readonly record struct Ff7BattleGlobalStatusClock(ushort FirstRemainder, ushort SecondRemainder, byte Buffer)
{
    public void Validate()
    {
        if (FirstRemainder >= 8192 || SecondRemainder >= 8192 || Buffer > 1)
        {
            throw new InvalidDataException("FFVII global status clock continuation is invalid.");
        }
    }
}

public readonly record struct Ff7BattleStatusTimers(ushort Remainder, byte Sleep, byte Poison)
{
    public byte? Barrier { get; init; }
    public byte? MagicBarrier { get; init; }
    public byte? Shield { get; init; }
    public ushort? HitPointRemainder { get; init; }
    public ushort? FixedRemainder { get; init; }
    public byte? Regen { get; init; }
    public byte? DeathSentence { get; init; }
    public byte? SlowPetrify { get; init; }
    public byte? Peerless { get; init; }
    public byte? Stop { get; init; }
}

public readonly record struct Ff7BattleStatusTick(Ff7BattleStatusTimers Timers, uint Statuses, bool PoisonDue)
{
    public int HitPointDelta { get; init; }
    public bool DeathSentenceDue { get; init; }
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
    private const uint Regen = 0x8000;
    private const uint DeathSentence = 0x200000;
    private const uint SlowPetrify = 0x2000;
    private const uint Peerless = 0x1000000;
    private const uint Stop = 0x400;
    private const byte StopDuration = 30;

    public static Ff7BattleStatusTimers Apply(Ff7BattleStatusTimers timers, uint previousStatuses, uint statuses)
    {
        uint added = statuses & ~previousStatuses;
        byte sleep = (statuses & Sleep) == 0 ? (byte)0 : (added & Sleep) != 0 ? SleepDuration : timers.Sleep;
        byte poison = (statuses & Poison) == 0 ? (byte)0 : (added & Poison) != 0 ? PoisonInterval : timers.Poison;
        byte? Duration(uint mask, byte? saved, byte duration) => (statuses & mask) == 0 ? saved is null ? null : (byte)0 : (added & mask) != 0 || saved is null or 0 ? duration : saved.Value;
        byte? doom = (statuses & DeathSentence) == 0 ? timers.DeathSentence is null ? null : (byte)0 : (added & DeathSentence) != 0 || timers.DeathSentence is null ? (byte)60 : timers.DeathSentence;
        byte? stop = (statuses & Stop) == 0 || (statuses & Ff7NativeBattleActionResolver.Death) != 0 ? timers.Stop is null ? null : (byte)0 : (added & Stop) != 0 || timers.Stop is null ? StopDuration : timers.Stop;
        return timers with { Sleep = sleep, Poison = poison, Barrier = Duration(Ff7NativeBattleStatuses.Barrier, timers.Barrier, 127), MagicBarrier = Duration(Ff7NativeBattleStatuses.MagicBarrier, timers.MagicBarrier, 127), Shield = Duration(Ff7NativeBattleStatuses.Shield, timers.Shield, 64), Regen = Duration(Regen, timers.Regen, 127), DeathSentence = doom, SlowPetrify = Duration(SlowPetrify, timers.SlowPetrify, 30), Peerless = Duration(Peerless, timers.Peerless, 64), Stop = stop };
    }

    public static bool AdvanceGlobalClock(ref Ff7BattleGlobalStatusClock clock, byte battleSpeed)
    {
        clock.Validate();
        int time = (clock.Buffer == 0 ? clock.FirstRemainder : clock.SecondRemainder) + Ff7NativeAtb.BaseRate(battleSpeed);
        ushort remainder = (ushort)(time % TimeUnit);
        clock = clock.Buffer == 0 ? clock with { FirstRemainder = remainder, Buffer = 1 } : clock with { SecondRemainder = remainder, Buffer = 0 };
        return time >= TimeUnit;
    }

    public static Ff7BattleStatusTick Advance(Ff7BattleStatusTimers timers, uint statuses, byte battleSpeed, byte imprisonedType = 0, uint maximumHitPoints = 0, bool globalTick = false, ushort originSavedStatuses = 0)
    {
        if (timers.Remainder >= TimeUnit || timers.Sleep > SleepDuration || timers.Poison > PoisonInterval || timers.Barrier > 127 || timers.MagicBarrier > 127 || timers.Shield > 64 || timers.HitPointRemainder >= 32768 || timers.FixedRemainder >= 4096 || timers.Regen > 127 || timers.DeathSentence > 60 || timers.SlowPetrify > 30 || timers.Peerless > 64 || timers.Stop > StopDuration || imprisonedType > 3)
        {
            throw new InvalidDataException("FFVII battle status timer continuation is invalid.");
        }
        timers = Apply(timers, statuses, statuses);
        uint clockStatuses = Ff7NativeBattleStatuses.ProjectClockStatuses(statuses, imprisonedType);
        if (globalTick && timers.Stop > 0)
        {
            timers = timers with { Stop = (byte)(timers.Stop.Value - 1) };
            if (timers.Stop == 0)
            {
                statuses &= ~Stop;
            }
        }
        if ((clockStatuses & StopOrDeath) != 0)
        {
            return new(timers, statuses, false);
        }

        int rate = Ff7NativeAtb.BaseRate(battleSpeed);
        int fixedRate = 34;
        if ((statuses & Slow) == 0)
        {
            rate *= (statuses & Haste) == 0 ? 2 : 4;
            fixedRate *= (statuses & Haste) == 0 ? 2 : 4;
        }
        int hpDelta = 0;
        int hpRate = (statuses & Regen) != 0 ? 1 : 0;
        if (Ff7OriginSavedStatusRules.IsZombie(originSavedStatuses))
        {
            hpRate = -hpRate;
        }
        if ((statuses & 0x8000000) != 0 || (statuses & Ff7NativeBattleStatuses.Imprisoned) != 0 && imprisonedType == 1)
        {
            hpRate--;
        }
        if (hpRate != 0)
        {
            short coefficient = unchecked((short)((maximumHitPoints >> 5) * hpRate));
            long accumulated = (timers.HitPointRemainder ?? 0) + (long)coefficient * rate;
            hpDelta = checked((int)(accumulated >> 15));
            timers = timers with { HitPointRemainder = (ushort)(accumulated & 32767) };
        }
        int fixedTime = (timers.FixedRemainder ?? 0) + fixedRate;
        bool deathSentenceDue = fixedTime >= 4096 && timers.DeathSentence == 1;
        timers = timers with { FixedRemainder = (ushort)(fixedTime % 4096) };
        if (fixedTime >= 4096)
        {
            if (timers.DeathSentence > 0)
            {
                timers = timers with { DeathSentence = (byte)(timers.DeathSentence.Value - 1) };
            }
            if (timers.SlowPetrify > 0)
            {
                timers = timers with { SlowPetrify = (byte)(timers.SlowPetrify.Value - 1) };
                if (timers.SlowPetrify == 0)
                {
                    statuses = (statuses & ~SlowPetrify) | 0x4000;
                }
            }
        }
        int time = timers.Remainder + rate;
        timers = timers with { Remainder = (ushort)(time % TimeUnit) };
        if (time < TimeUnit)
        {
            return new(timers, statuses, false) { HitPointDelta = hpDelta, DeathSentenceDue = deathSentenceDue };
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
        timers = timers with { Barrier = Expire(timers.Barrier, Ff7NativeBattleStatuses.Barrier), MagicBarrier = Expire(timers.MagicBarrier, Ff7NativeBattleStatuses.MagicBarrier), Shield = Expire(timers.Shield, Ff7NativeBattleStatuses.Shield), Regen = Expire(timers.Regen, Regen), Peerless = Expire(timers.Peerless, Peerless) };
        return new(timers, statuses, poisonDue) { HitPointDelta = hpDelta, DeathSentenceDue = deathSentenceDue };
    }

    public static byte ReadPersistentStatuses(byte saved) => (byte)(saved & 0x30);
}
