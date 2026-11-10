namespace Memoria.FF7.Runtime;

public readonly record struct Ff7BattleStatusTimers(ushort Remainder, byte Sleep, byte Poison);

public readonly record struct Ff7BattleStatusTick(Ff7BattleStatusTimers Timers, uint Statuses, bool PoisonDue);

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
        return timers with { Sleep = sleep, Poison = poison };
    }

    public static Ff7BattleStatusTick Advance(Ff7BattleStatusTimers timers, uint statuses, byte battleSpeed)
    {
        if (timers.Remainder >= TimeUnit || timers.Sleep > SleepDuration || timers.Poison > PoisonInterval)
        {
            throw new InvalidDataException("FFVII battle status timer continuation is invalid.");
        }
        if ((statuses & StopOrDeath) != 0)
        {
            return new(timers, statuses, false);
        }

        int rate = Ff7NativeAtb.BaseRate(battleSpeed);
        if ((statuses & Slow) == 0)
        {
            rate *= (statuses & Haste) == 0 ? 2 : 4;
        }
        int time = timers.Remainder + rate;
        timers = timers with { Remainder = (ushort)(time % TimeUnit) };
        if (time < TimeUnit)
        {
            return new(timers, statuses, false);
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
        return new(timers, statuses, poisonDue);
    }

    public static byte ReadPersistentStatuses(byte saved) => (byte)(saved & 0x30);
}
