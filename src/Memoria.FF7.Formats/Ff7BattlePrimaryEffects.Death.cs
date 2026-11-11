namespace Memoria.FF7.Formats;

public sealed record Ff7BattlePrimaryDeathController(Ff7BattleActorDeathState State) : Ff7BattlePrimaryCallback;

public static partial class Ff7BattlePrimaryEffects
{
    public static Ff7BattlePrimaryState ScheduleActorDeath(Ff7BattlePrimaryState state, int target, byte kind, bool playSound = true)
    {
        if (kind is not (0 or 17 or 18))
        {
            throw new InvalidDataException("FFVII primary actor death requires its original primary-pool kind.");
        }
        return Schedule(state, new Ff7BattlePrimaryDeathController(new(target, kind, playSound)) { Owner = -1 });
    }

    private static Ff7BattlePrimaryCallback? ExecuteDeath(Ff7BattlePrimaryDeathController callback, PrimaryPass pass)
    {
        Ff7BattleActorDeathUpdate update = Ff7BattleActorDeath.Advance(callback.State, pass.ReadActorModel(callback.State.Target));
        pass.WriteActorModel(update.Write);
        if (update.Sound is { } sound)
        {
            pass.ActorSounds.Add(sound);
        }
        if (update.StartShake)
        {
            pass.BossDeathShakes.Add(new(callback.State.Target, pass.ReadActorRootZ(callback.State.Target)));
        }
        return update.State is { } state ? callback with { State = state, Age = callback.Age + 1 } : null;
    }

    private sealed partial class PrimaryPass
    {
        public readonly List<Ff7BattleActorModelWrite> ActorWrites = [];
        public readonly List<Ff7BattleActorModelSound> ActorSounds = [];
        public readonly List<Ff7BattleBossDeathShakeRequest> BossDeathShakes = [];
        private readonly Dictionary<int, Ff7BattleActorModelModifiers> actorModels = [];
        private readonly Dictionary<int, short> actorRootZ = [];

        public Ff7BattleActorModelModifiers ReadActorModel(int target)
        {
            if (!actorModels.TryGetValue(target, out Ff7BattleActorModelModifiers? model))
            {
                model = Context?.ActorModel?.Invoke(target) ?? throw new InvalidDataException("FFVII actor death requires its native actor model fields.");
                actorModels.Add(target, model);
            }
            return model;
        }

        public short ReadActorRootZ(int target)
        {
            return actorRootZ.TryGetValue(target, out short z) ? z : Context?.ActorRootZ?.Invoke(target) ?? throw new InvalidDataException("FFVII boss death requires the actor's native root Z.");
        }

        public void WriteActorModel(Ff7BattleActorModelWrite write)
        {
            ActorWrites.Add(write);
            if (write.Modifiers is { } model)
            {
                actorModels[write.Target] = model;
            }
            if (write.WorldZ is short z)
            {
                actorRootZ[write.Target] = z;
            }
            if (write.RestoreHome)
            {
                actorRootZ[write.Target] = Context?.ActorHomeZ?.Invoke(write.Target) ?? throw new InvalidDataException("FFVII actor death completion requires its native home root Z.");
            }
        }
    }
}
