namespace Memoria.FF7.Formats;

public sealed record Ff7BattleActorVerticalMotion(int Target, short Step, ushort Remaining);

public sealed record Ff7BattleActorMotionState
{
    public Ff7BattleActorDeathState?[] Slots { get; init; } = new Ff7BattleActorDeathState?[Ff7BattleActorMotionEffects.Capacity];
    public Ff7BattleActorVerticalMotion?[] VerticalSlots { get; init; } = new Ff7BattleActorVerticalMotion?[Ff7BattleActorMotionEffects.Capacity];
    public IReadOnlyList<Ff7BattleActorModelWrite> ActorWrites { get; init; } = [];
    public IReadOnlyList<Ff7BattleActorModelSound> ActorSounds { get; init; } = [];
}

public static class Ff7BattleActorMotionEffects
{
    public const int Capacity = 10;

    public static Ff7BattleActorMotionState ScheduleDeath(Ff7BattleActorMotionState state, int target, bool playSound = true, bool scriptedFade = false)
    {
        ValidateSlots(state);
        Ff7BattleActorDeathState?[] slots = (Ff7BattleActorDeathState?[])state.Slots.Clone();
        int free = Enumerable.Range(0, Capacity).FirstOrDefault(index => slots[index] is null && state.VerticalSlots[index] is null, -1);
        if (free < 0)
        {
            throw new InvalidOperationException("FFVII actor-motion callback pool is full.");
        }
        slots[free] = new(target, 1, playSound) { ScriptedFade = scriptedFade };
        return state with { Slots = slots };
    }

    public static Ff7BattleActorMotionState ScheduleVertical(Ff7BattleActorMotionState state, int target, short distance, byte duration)
    {
        ValidateSlots(state);
        if (duration == 0)
        {
            throw new InvalidDataException("FFVII vertical actor motion has a zero native divisor.");
        }
        Ff7BattleActorVerticalMotion?[] slots = state.VerticalSlots.ToArray();
        int free = Enumerable.Range(0, Capacity).FirstOrDefault(index => state.Slots[index] is null && slots[index] is null, -1);
        if (free < 0)
        {
            throw new InvalidOperationException("FFVII actor-motion callback pool is full.");
        }
        slots[free] = new(target, (short)(distance / duration), duration);
        return state with { VerticalSlots = slots };
    }
    public static Ff7BattleActorMotionState Advance(Ff7BattleActorMotionState state, Func<int, Ff7BattleActorModelModifiers> actor, Ff7BattleActorDeathResources resources, Func<int, short>? actorY = null)
    {
        ValidateSlots(state);
        Ff7BattleActorDeathState?[] slots = (Ff7BattleActorDeathState?[])state.Slots.Clone();
        Ff7BattleActorVerticalMotion?[] vertical = state.VerticalSlots.ToArray();
        Dictionary<int, short> heights = [];
        Dictionary<int, Ff7BattleActorModelModifiers> models = [];
        List<Ff7BattleActorModelWrite> writes = [];
        List<Ff7BattleActorModelSound> sounds = [];
        for (int index = 0; index < slots.Length; index++)
        {
            if (vertical[index] is { } translation)
            {
                if (translation.Remaining == 0)
                {
                    vertical[index] = null;
                }
                else
                {
                    short height = heights.TryGetValue(translation.Target, out short currentY) ? currentY : (actorY ?? throw new InvalidOperationException("FFVII vertical actor motion requires its current actor height."))(translation.Target);
                    height = unchecked((short)(height + translation.Step));
                    heights[translation.Target] = height;
                    writes.Add(new(translation.Target, WorldY: height));
                    vertical[index] = translation with { Remaining = (ushort)(translation.Remaining - 1) };
                }
            }
            if (slots[index] is not { } callback)
            {
                continue;
            }
            Ff7BattleActorModelModifiers current = models.TryGetValue(callback.Target, out Ff7BattleActorModelModifiers? saved) ? saved : actor(callback.Target);
            Ff7BattleActorDeathUpdate update = Ff7BattleActorDeath.Advance(callback, current, resources);
            slots[index] = update.State;
            writes.Add(update.Write);
            models[callback.Target] = update.Write.Modifiers!;
            if (update.Sound is { } sound)
            {
                sounds.Add(sound);
            }
        }
        return new() { Slots = slots, VerticalSlots = vertical, ActorWrites = writes, ActorSounds = sounds };
    }

    private static void ValidateSlots(Ff7BattleActorMotionState state)
    {
        if (state.Slots is not { Length: Capacity } || state.VerticalSlots is not { Length: Capacity } || Enumerable.Range(0, Capacity).Any(index => state.Slots[index] is not null && state.VerticalSlots[index] is not null))
        {
            throw new InvalidDataException("FFVII actor-motion callbacks overlap or exceed their native pool.");
        }
    }
}
