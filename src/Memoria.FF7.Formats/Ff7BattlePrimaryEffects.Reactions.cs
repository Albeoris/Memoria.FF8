using System.Numerics;

namespace Memoria.FF7.Formats;

public sealed record Ff7BattlePrimaryTargetReaction(ushort RemainingTargets, byte Delay) : Ff7BattlePrimaryCallback;

public static partial class Ff7BattlePrimaryEffects
{
    public static Ff7BattlePrimaryState ScheduleTargetReactions(Ff7BattlePrimaryState state, ushort targets, byte delay, int owner = 0)
    {
        return ImportTargetReaction(state, new(targets, delay) { Owner = owner });
    }

    public static Ff7BattlePrimaryState ImportTargetReaction(Ff7BattlePrimaryState state, Ff7BattlePrimaryTargetReaction callback)
    {
        if (callback.RemainingTargets is 0 or > 0x3FF)
        {
            throw new ArgumentOutOfRangeException(nameof(callback));
        }
        AdvanceTargetReaction(callback, true, out _);
        PrimaryPass pass = new(state, null);
        pass.Allocate(callback);
        return pass.Finish(state.Tick, preserveReceipts: true);
    }

    public static Ff7BattlePrimaryTargetReaction AdvanceTargetReaction(Ff7BattlePrimaryTargetReaction callback, bool paused, out int? targetSlot)
    {
        if (callback.RemainingTargets > 0x3FF || callback.Age < 0 || callback.Age > callback.Delay + 10)
        {
            throw new InvalidDataException("The FFVII target reaction schedule is corrupt.");
        }
        targetSlot = null;
        if (paused || callback.RemainingTargets == 0)
        {
            return callback;
        }
        ushort remaining = callback.RemainingTargets;
        if (callback.Age >= callback.Delay)
        {
            int slot = BitOperations.TrailingZeroCount((uint)remaining);
            targetSlot = slot;
            remaining = (ushort)(remaining & ~(1 << slot));
        }
        return callback with { RemainingTargets = remaining, Age = callback.Age + 1 };
    }

    private static Ff7BattlePrimaryCallback? ExecuteTargetReaction(Ff7BattlePrimaryTargetReaction callback, PrimaryPass pass)
    {
        Ff7BattlePrimaryTargetReaction next = AdvanceTargetReaction(callback, false, out int? target);
        if (target is int slot)
        {
            pass.TargetReactions.Add(new(slot, false));
        }
        return next.RemainingTargets == 0 ? null : next;
    }
}
