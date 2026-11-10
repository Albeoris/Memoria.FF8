namespace Memoria.FF7.Runtime;

public sealed record Ff7BattleReactionState(Ff7BattleTargetCallbacks[] Pending);

public sealed record Ff7BattleReactionUpdate(Ff7BattleReactionState State, IReadOnlyList<int> Targets);

public static class Ff7BattleReactionClock
{
    public static Ff7BattleReactionState Schedule(Ff7BattleReactionState? state, ushort targets, byte delay)
    {
        Ff7BattleTargetCallbacks callback = Ff7BattleTargetCallbacks.Schedule(targets, delay);
        return new([.. state?.Pending ?? [], callback]);
    }

    public static Ff7BattleReactionUpdate Advance(Ff7BattleReactionState state, bool paused)
    {
        List<Ff7BattleTargetCallbacks> pending = [];
        List<int> targets = [];
        foreach (Ff7BattleTargetCallbacks callback in state.Pending)
        {
            Ff7BattleTargetCallbacks next = callback.Advance(paused, out int? target);
            if (target is int slot)
            {
                targets.Add(slot);
            }
            if (next.RemainingTargets != 0)
            {
                pending.Add(next);
            }
        }
        return new(new(pending.ToArray()), targets.AsReadOnly());
    }
}
