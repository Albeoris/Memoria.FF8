using Memoria.FF7.Formats;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7BattleSession
{
    private void ScheduleActorDeath(int slot)
    {
        byte kind = animations!.DeathType(ActorModel(slot));
        if (kind == 1)
        {
            state = state with { ActorMotion = Ff7BattleActorMotionEffects.ScheduleDeath(state.ActorMotion ?? new(), slot) };
        }
        else if (kind is 0 or 17 or 18)
        {
            state = state with { PrimaryEffects = Ff7BattlePrimaryEffects.ScheduleActorDeath(EnsurePrimaryEffects(), slot, kind) };
        }
        else
        {
            throw new NotSupportedException($"FFVII actor death kind {kind} requires its native callback owner.");
        }
    }

    private void AdvanceActorMotionPresentation()
    {
        if (state.ActorMotion is not { } previous)
        {
            return;
        }
        Ff7BattleActorDeathResources resources = animations?.ActorResources?.Death ?? throw new InvalidDataException("FFVII actor motion requires its original callback resources.");
        Ff7BattleActorMotionState motion = Ff7BattleActorMotionEffects.Advance(previous, LiveActorModelOutput, resources);
        state = state with { ActorMotion = motion };
        foreach (Ff7BattleActorModelWrite write in motion.ActorWrites)
        {
            ApplyActorModelWrite(write);
        }
        foreach (Ff7BattleActorModelSound sound in motion.ActorSounds)
        {
            EmitActorModelSound(sound);
        }
    }

    private void EmitActorModelSound(Ff7BattleActorModelSound sound)
    {
        AppendSounds(Ff7NativeBattleSound.Emit(state.Sounds ?? Ff7BattleSoundState.Empty, sound.Sound, Ff7NativeBattleSound.ActorPan(SoundScreenX(sound.Target)), sound.Channel));
    }

    private static Ff7BattleActorMotionState CopyActorMotion(Ff7BattleActorMotionState motion) => motion with { Slots = motion.Slots.ToArray(), ActorWrites = motion.ActorWrites.ToArray(), ActorSounds = motion.ActorSounds.ToArray() };

    private void ValidateActorMotion(Ff7BattleSnapshot snapshot)
    {
        if (snapshot.ActorMotion is not { } motion)
        {
            return;
        }
        bool HasTarget(int slot) => snapshot.Actors.Any(actor => actor.Actor.Slot == slot);
        if (animations?.ActorResources is null || motion.Slots is not { Length: Ff7BattleActorMotionEffects.Capacity } || motion.ActorWrites is null || motion.ActorSounds is null || motion.ActorWrites.Count > Ff7BattleActorMotionEffects.Capacity || motion.ActorSounds.Count > Ff7BattleActorMotionEffects.Capacity || motion.Slots.Any(callback => callback is not null && (callback.Kind != 1 || callback.Remaining is < -1 or > 14 || !HasTarget(callback.Target))) || motion.ActorWrites.Any(write => write is null || !HasTarget(write.Target) || write.Modifiers is { } modifiers && !ValidModelModifiers(modifiers)) || motion.ActorSounds.Any(sound => sound is null || !HasTarget(sound.Target) || sound.Channel != 3))
        {
            throw new InvalidDataException("FFVII saved actor-motion callbacks or receipts are invalid.");
        }
    }
}
