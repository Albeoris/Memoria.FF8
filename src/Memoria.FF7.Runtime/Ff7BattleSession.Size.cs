using System.Collections.Immutable;

namespace Memoria.FF7.Runtime;

public sealed record Ff7BattleSizeChange(int Slot, byte Size);
public sealed record Ff7BattleSmallScale(int Slot, short Delta, byte Remaining);

public sealed record Ff7BattleSizePresentation(ImmutableArray<short> Scales, ImmutableArray<Ff7BattleSizeChange> Pending, int? ActiveSlot = null, short Delta = 0, short Remaining = 0)
{
    public ushort? PresentedSmallMask { get; init; }
    public ImmutableArray<Ff7BattleSmallScale> SmallScales { get; init; } = [];
}

public static class Ff7NativeBattleActorSize
{
    public static Ff7BattleSizePresentation Create() => new(Enumerable.Repeat((short)4096, 10).ToImmutableArray(), []);

    public static Ff7BattleSizePresentation Enqueue(Ff7BattleSizePresentation state, IEnumerable<Ff7BattleSizeChange> changes)
    {
        return state with { Pending = state.Pending.AddRange(changes) };
    }

    public static Ff7BattleSizePresentation PresentSmall(Ff7BattleSizePresentation state, int slot, bool small)
    {
        int bit = 1 << slot;
        ushort mask = state.PresentedSmallMask.GetValueOrDefault();
        if (((mask & bit) != 0) == small)
        {
            return state;
        }
        return state with { PresentedSmallMask = (ushort)(mask ^ bit), SmallScales = state.SmallScales.Add(new(slot, small ? (short)-128 : (short)128, 16)) };
    }

    public static Ff7BattleSizePresentation Advance(Ff7BattleSizePresentation state, bool advanceScript = true)
    {
        if (!state.SmallScales.IsEmpty)
        {
            ImmutableArray<Ff7BattleSmallScale>.Builder callbacks = ImmutableArray.CreateBuilder<Ff7BattleSmallScale>(state.SmallScales.Length);
            ImmutableArray<short>.Builder scales = state.Scales.ToBuilder();
            foreach (Ff7BattleSmallScale callback in state.SmallScales)
            {
                if (callback.Remaining != 0)
                {
                    scales[callback.Slot] = unchecked((short)(scales[callback.Slot] + callback.Delta));
                    callbacks.Add(callback with { Remaining = (byte)(callback.Remaining - 1) });
                }
            }
            state = state with { Scales = scales.MoveToImmutable(), SmallScales = callbacks.ToImmutable() };
        }
        return advanceScript ? AdvanceScriptSize(state) : state;
    }

    private static Ff7BattleSizePresentation AdvanceScriptSize(Ff7BattleSizePresentation state)
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
        if (state.Scales.IsDefault || state.Scales.Length != 10 || state.Pending.IsDefault || state.Pending.Any(change => change is null || change.Slot is < 0 or >= 10) || state.ActiveSlot is < 0 or >= 10 || state.Remaining is < 0 or > 16 || state.ActiveSlot is null && (state.Remaining != 0 || state.Delta != 0) || state.PresentedSmallMask > 0x3FF || state.SmallScales.IsDefault || state.SmallScales.Any(callback => callback is null || callback.Slot is < 0 or >= 10 || callback.Delta is not (-128 or 128) || callback.Remaining > 16) || state.PresentedSmallMask is null && !state.SmallScales.IsEmpty)
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

    private ushort InitializeSmallActorSizes()
    {
        if (state.ActorSizePresentation?.PresentedSmallMask is not null)
        {
            return 0;
        }
        ushort mask = 0;
        Ff7BattleSizePresentation? size = state.ActorSizePresentation;
        foreach (Ff7BattleActorState actor in state.Actors)
        {
            int slot = actor.Actor.Slot;
            if (!IsGuestVitalsSlot(slot) && (actor.Actor.Statuses & Ff7NativeBattleStatuses.Small) != 0)
            {
                mask |= (ushort)(1 << slot);
                size ??= Ff7NativeBattleActorSize.Create();
                size = size with { Scales = size.Scales.SetItem(slot, unchecked((short)(size.Scales[slot] - 2048))) };
            }
        }
        if (size is not null && mask != 0)
        {
            state = state with { ActorSizePresentation = size with { PresentedSmallMask = mask } };
        }
        return mask;
    }

    private void PresentActorSmallSize(int slot, bool small)
    {
        if (IsGuestVitalsSlot(slot) || !small && state.ActorSizePresentation is null)
        {
            return;
        }
        Ff7BattleSizePresentation size = state.ActorSizePresentation ?? Ff7NativeBattleActorSize.Create();
        Ff7BattleSizePresentation updated = Ff7NativeBattleActorSize.PresentSmall(size, slot, small);
        if (!ReferenceEquals(updated, size))
        {
            state = state with { ActorSizePresentation = updated };
        }
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
        if (state.Action is null)
        {
            foreach (Ff7BattleActorState actor in state.Actors)
            {
                PresentActorSmallSize(actor.Actor.Slot, (actor.Actor.Statuses & Ff7NativeBattleStatuses.Small) != 0);
            }
        }
        if (state.ActorSizePresentation is { } size)
        {
            bool advanceScript = size.ActiveSlot is not null || state.Action is null;
            state = state with { ActorSizePresentation = Ff7NativeBattleActorSize.Advance(size, advanceScript) };
        }
    }
}
