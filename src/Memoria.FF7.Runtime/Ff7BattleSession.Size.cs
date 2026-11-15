using System.Collections.Immutable;

namespace Memoria.FF7.Runtime;

public sealed record Ff7BattleSizeChange(int Slot, byte Size);
public sealed record Ff7BattleSizePresentation(ImmutableArray<short> Scales, ImmutableArray<Ff7BattleSizeChange> Pending, int? ActiveSlot = null, short Delta = 0, short Remaining = 0);

public static class Ff7NativeBattleActorSize
{
    public static Ff7BattleSizePresentation Create() => new(Enumerable.Repeat((short)4096, 10).ToImmutableArray(), []);

    public static Ff7BattleSizePresentation Enqueue(Ff7BattleSizePresentation state, IEnumerable<Ff7BattleSizeChange> changes)
    {
        return state with { Pending = state.Pending.AddRange(changes) };
    }

    public static Ff7BattleSizePresentation Advance(Ff7BattleSizePresentation state)
    {
        if (state.ActiveSlot is null)
        {
            if (state.Pending.Length == 0)
            {
                return state;
            }
            Ff7BattleSizeChange change = state.Pending[0];
            int target = unchecked((sbyte)change.Size) << 8;
            int current = state.Scales[change.Slot];
            int distance = target >= current ? target - current : current - target;
            state = state with { Pending = state.Pending.RemoveAt(0), ActiveSlot = change.Slot, Delta = unchecked((short)(distance / 16)), Remaining = 16 };
        }
        if (state.Remaining == 0)
        {
            return state with { ActiveSlot = null, Delta = 0 };
        }
        int slot = state.ActiveSlot.Value;
        return state with { Scales = state.Scales.SetItem(slot, unchecked((short)(state.Scales[slot] + state.Delta))), Remaining = (short)(state.Remaining - 1) };
    }

    public static void Validate(Ff7BattleSizePresentation state)
    {
        if (state.Scales.IsDefault || state.Scales.Length != 10 || state.Pending.IsDefault || state.Pending.Any(change => change is null || change.Slot is < 0 or >= 10) || state.ActiveSlot is < 0 or >= 10 || state.Remaining is < 0 or > 16 || state.ActiveSlot is null && (state.Remaining != 0 || state.Delta != 0))
        {
            throw new InvalidDataException("FFVII saved actor size presentation is invalid.");
        }
    }
}

public sealed partial class Ff7BattleSession
{
    public short GetActorScale(int slot)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)slot, 10U);
        return state.ActorSizePresentation?.Scales[slot] ?? 4096;
    }

    private void PublishActorSizeChanges(IReadOnlyList<Ff7BattleAiActor> previous, IReadOnlyList<Ff7BattleAiActor> updates)
    {
        Ff7BattleSizeChange[] changes = updates.Where(updated => (updated.Size ?? 16) != (previous.Single(actor => actor.Slot == updated.Slot).Size ?? 16)).OrderBy(updated => updated.Slot).Select(updated => new Ff7BattleSizeChange(updated.Slot, updated.Size ?? 16)).ToArray();
        if (changes.Length != 0)
        {
            state = state with { ActorSizePresentation = Ff7NativeBattleActorSize.Enqueue(state.ActorSizePresentation ?? Ff7NativeBattleActorSize.Create(), changes) };
        }
    }

    private void AdvanceActorSizePresentation()
    {
        if (state.ActorSizePresentation is { } size && (size.ActiveSlot is not null || state.Action is null))
        {
            state = state with { ActorSizePresentation = Ff7NativeBattleActorSize.Advance(size) };
        }
    }
}
