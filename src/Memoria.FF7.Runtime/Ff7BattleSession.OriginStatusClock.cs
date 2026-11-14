namespace Memoria.FF7.Runtime;

public sealed partial class Ff7BattleSession
{
    private short[]? originStatusDurations;
    private const short InactiveOriginStatusTimer = -1111;
    private const uint TimedOriginStatuses = Ff7GuestBattleStatuses.OriginAura | Ff7GuestBattleStatuses.OriginFloat;

    /// <summary>Binds the source kernel durations after its saved battle-speed multiplier has been applied.</summary>
    public void BindOriginStatusDurations(IReadOnlyList<short> durations)
    {
        ArgumentNullException.ThrowIfNull(durations);
        if (durations.Count != 14)
        {
            throw new InvalidDataException("Source status timing requires fourteen initialized durations.");
        }
        if (originStatusDurations is not null && !originStatusDurations.SequenceEqual(durations))
        {
            throw new InvalidOperationException("An encounter cannot change its source status timing rules.");
        }
        originStatusDurations = durations.ToArray();
        foreach (Ff7BattleActorState actor in state.Actors)
        {
            int slot = actor.Actor.Slot;
            uint current = GetOriginBattleStatuses(slot);
            if ((current & TimedOriginStatuses) != 0 && state.GuestOriginStatuses?.SingleOrDefault(value => value.HostSlot == slot)?.Timers is null)
            {
                StoreOriginBattleStatuses(slot, current, 0, current & TimedOriginStatuses);
            }
        }
    }

    private void StoreOriginBattleStatuses(int slot, uint statuses, uint removed, uint added = 0)
    {
        Ff7BattleGuestOriginStatus? previous = state.GuestOriginStatuses?.SingleOrDefault(value => value.HostSlot == slot);
        short[]? timers = previous?.Timers is { } saved ? (short[])saved.Clone() : null;
        if ((added & TimedOriginStatuses) != 0)
        {
            short[] durations = originStatusDurations ?? throw new NotSupportedException("Timed source statuses require their installed source timing rules.");
            timers ??= Enumerable.Repeat(InactiveOriginStatusTimer, 14).ToArray();
            foreach (int bit in new[] { 8, 13 })
            {
                if ((added & (1u << bit)) != 0)
                {
                    timers[bit] = durations[bit];
                }
            }
        }
        if (timers is not null)
        {
            foreach (int bit in new[] { 8, 13 })
            {
                if ((removed & (1u << bit)) != 0)
                {
                    timers[bit] = InactiveOriginStatusTimer;
                }
            }
        }
        Ff7BattleGuestOriginStatus row = new(slot, statuses) { Timers = timers };
        state = state with { GuestOriginStatuses = (state.GuestOriginStatuses ?? []).Where(value => value.HostSlot != slot).Append(row).OrderBy(value => value.HostSlot).ToArray() };
    }

    private uint RemovableOriginBattleStatuses(int slot, uint mask)
    {
        if (state.GuestOriginStatuses?.SingleOrDefault(value => value.HostSlot == slot)?.Timers is { } timers)
        {
            foreach (int bit in new[] { 8, 13 })
            {
                if (timers[bit] == InactiveOriginStatusTimer)
                {
                    mask &= ~(1u << bit);
                }
            }
        }
        return mask;
    }

    private bool AdvanceOriginStatusClock(out bool pendingTimers)
    {
        int phase = state.OriginStatusClockRemainder + 31;
        bool due = phase >= 120;
        state = state with { OriginStatusClockRemainder = (byte)(phase % 120) };
        pendingTimers = state.GuestOriginStatuses?.Any(row => IsAlive(Actor(row.HostSlot)) && row.Timers is { } timers && ((row.BattleStatuses & Ff7GuestBattleStatuses.OriginAura) != 0 && timers[8] != InactiveOriginStatusTimer || (row.BattleStatuses & Ff7GuestBattleStatuses.OriginFloat) != 0 && timers[13] != InactiveOriginStatusTimer)) == true;
        // The source dispatcher rechecks its action-pause owner after action dispatch.
        return due && state.Action is null && !HasPrimaryPresentationCallbacks;
    }

    private bool AdvanceOriginStatusTimers(int slot)
    {
        Ff7BattleGuestOriginStatus? row = state.GuestOriginStatuses?.SingleOrDefault(value => value.HostSlot == slot);
        if (row?.Timers is not { } saved || (row.BattleStatuses & TimedOriginStatuses) == 0)
        {
            return false;
        }
        uint current = GetOriginBattleStatuses(slot);
        short[] timers = (short[])saved.Clone();
        bool changed = false;
        foreach (int bit in new[] { 8, 13 })
        {
            uint mask = 1u << bit;
            if ((current & mask) == 0 || timers[bit] == InactiveOriginStatusTimer)
            {
                continue;
            }
            if (timers[bit] > 0)
            {
                int decrement = (current & 4) != 0 ? 1 : (current & 2) != 0 ? 3 : 2;
                if ((current & 9) != 0)
                {
                    decrement = 0;
                }
                timers[bit] = unchecked((short)(timers[bit] - decrement));
                changed |= decrement != 0;
            }
            else
            {
                timers[bit] = InactiveOriginStatusTimer;
                current &= ~mask;
                changed = true;
            }
        }
        if (changed)
        {
            Ff7BattleGuestOriginStatus next = row with { BattleStatuses = current, Timers = timers };
            state = state with { GuestOriginStatuses = state.GuestOriginStatuses!.Select(value => value.HostSlot == slot ? next : value).ToArray() };
        }
        return changed;
    }
}
