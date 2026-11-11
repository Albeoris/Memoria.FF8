namespace Memoria.FF7.Formats;

public sealed record Ff7BattleActorMotionState
{
    public Ff7BattleActorDeathState?[] Slots { get; init; } = new Ff7BattleActorDeathState?[Ff7BattleActorMotionEffects.Capacity];
    public IReadOnlyList<Ff7BattleActorModelWrite> ActorWrites { get; init; } = [];
    public IReadOnlyList<Ff7BattleActorModelSound> ActorSounds { get; init; } = [];
}

public static class Ff7BattleActorMotionEffects
{
    public const int Capacity = 10;

    public static Ff7BattleActorMotionState ScheduleDeath(Ff7BattleActorMotionState state, int target, bool playSound = true)
    {
        Ff7BattleActorDeathState?[] slots = (Ff7BattleActorDeathState?[])state.Slots.Clone();
        int free = Array.FindIndex(slots, slot => slot is null);
        if (free < 0)
        {
            throw new InvalidOperationException("FFVII actor-motion callback pool is full.");
        }
        slots[free] = new(target, 1, playSound);
        return state with { Slots = slots };
    }

    public static Ff7BattleActorMotionState Advance(Ff7BattleActorMotionState state, Func<int, Ff7BattleActorModelModifiers> actor, Ff7BattleActorDeathResources resources)
    {
        Ff7BattleActorDeathState?[] slots = (Ff7BattleActorDeathState?[])state.Slots.Clone();
        Dictionary<int, Ff7BattleActorModelModifiers> models = [];
        List<Ff7BattleActorModelWrite> writes = [];
        List<Ff7BattleActorModelSound> sounds = [];
        for (int index = 0; index < slots.Length; index++)
        {
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
        return new() { Slots = slots, ActorWrites = writes, ActorSounds = sounds };
    }
}
