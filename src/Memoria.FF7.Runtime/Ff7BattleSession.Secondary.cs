using System.Numerics;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7BattleSession
{
    public Ff7BattleSecondaryState? SecondaryEffects => state.SecondaryEffects;
    public Ff7BattleStageFadeState? StageFade => state.StageFade;

    private Ff7BattleSecondaryState EnsureSecondaryEffects()
    {
        if (state.SecondaryEffects is { } current)
        {
            return current;
        }
        Ff7BattleSecondaryState secondary = new();
        foreach (Ff7BattleDelayedSound sound in state.Sounds?.Pending ?? [])
        {
            secondary = Ff7BattleSecondaryEffects.ScheduleSound(secondary, sound.Sound, sound.Delay);
        }
        foreach (Ff7BattleHitmarkState hitmark in state.Hitmarks)
        {
            secondary = hitmark.Anchor is null
                ? Ff7BattleSecondaryEffects.ScheduleHitmark(secondary, hitmark.Target, hitmark.Definition, ImpactAnchor(hitmark.Target), CollisionRadius(hitmark.Target), hitmark.Flipped)
                : Ff7BattleSecondaryEffects.ImportHitmark(secondary, hitmark);
        }
        state = state with { SecondaryEffects = secondary, Hitmarks = [], Sounds = state.Sounds is { } sounds ? sounds with { Pending = [] } : null };
        return secondary;
    }

    private Vector3 SecondaryActorPoint(int slot, int bone, Vector3 point)
    {
        (int clip, int frame) = RenderActorPose(slot);
        string model = ActorModel(slot);
        Ff7BattlePlacement placement = RenderActorPlacement(slot);
        BattleActorPoseTransform? extra = ActorModelOutput(slot).PoseTransform;
        if (RenderActorLimit(slot) is not null)
        {
            ushort limit = ActorModelState(slot).Render?.Limit ?? VisualLimitId(slot)!.Value;
            return bone < 0
                ? animations!.LimitWeaponPosition(model, clip, frame, placement, limit, point, extra)
                : animations!.LimitBonePosition(model, clip, frame, placement, limit, bone, point, extra);
        }
        return bone < 0
            ? animations!.BaseWeaponPosition(model, clip, frame, placement, point, extra)
            : animations!.BaseBonePoint(model, clip, frame, placement, bone, point, extra);
    }

    private static Ff7BattleSecondaryState CopySecondaryEffects(Ff7BattleSecondaryState source)
    {
        return source with { Slots = source.Slots.ToArray(), Muzzles = source.Muzzles.ToArray(), Casings = source.Casings.ToArray(), Sparks = source.Sparks.ToArray(), Dust = source.Dust.ToArray(), Hitmarks = source.Hitmarks.ToArray(), SoundIds = source.SoundIds.ToArray(), ActorWrites = source.ActorWrites.ToArray(), ScreenPulses = source.ScreenPulses.ToArray(), LimitPreparations = source.LimitPreparations.ToArray(), DrawOrder = source.DrawOrder.ToArray() };
    }

    private static void ValidateSecondaryEffects(Ff7BattleSnapshot snapshot)
    {
        if (snapshot.SecondaryEffects is not { } secondary)
        {
            return;
        }
        if (secondary.Slots is not { Count: Ff7BattleSecondaryEffects.Capacity } || secondary.Muzzles is null || secondary.Casings is null || secondary.Sparks is null || secondary.Dust is null || secondary.Hitmarks is null || secondary.SoundIds is null || secondary.DrawOrder is null || secondary.ActorWrites is null || secondary.ScreenPulses is null || secondary.LimitPreparations is null || secondary.DrawOrder.Count > Ff7BattleSecondaryEffects.Capacity || secondary.Muzzles.Count + secondary.Casings.Count + secondary.Sparks.Count + secondary.Dust.Count + secondary.Hitmarks.Count + secondary.SoundIds.Count + secondary.ScreenPulses.Count + secondary.LimitPreparations.Count > Ff7BattleSecondaryEffects.Capacity)
        {
            throw new InvalidDataException("FFVII secondary effects require their original callback slots and draw receipts.");
        }
        foreach (Ff7BattleSecondaryCallback? callback in secondary.Slots)
        {
            if (callback is null)
            {
                continue;
            }
            int actor = callback switch
            {
                Ff7BattleGunEmitter emitter => emitter.Actor,
                Ff7BattleGunMuzzleCallback muzzle => muzzle.Actor,
                Ff7BattleHitmarkOwnerCallback owner => owner.Target,
                Ff7BattleHitmarkSpriteCallback sprite => sprite.Target,
                Ff7BattleBossDeathShakeCallback shake => shake.State.Target,
                Ff7BattleLimitPreparationEntry preparation => preparation.Actor,
                _ => -1
            };
            if (callback.Age is < 0 or int.MaxValue || actor != -1 && snapshot.Actors.All(candidate => candidate.Actor.Slot != actor) || callback is Ff7BattleBossDeathShakeCallback { State.Remaining: < 0 or > 64 } || callback is Ff7BattleLimitPreparationEntry { Bone: < 0 } || callback is Ff7BattleDelayedSoundCallback { Delay: < 0 or > 255 } || callback is Ff7BattleGunEmitter { Bone: < -1 } || callback is Ff7BattleGunMuzzleCallback { Bone: < -1 } || callback is Ff7BattleGunCasingCallback { Bounces: < 0 or > 1 } || callback is Ff7BattleHitmarkOwnerCallback { Definition: null } or Ff7BattleHitmarkOwnerCallback { Definition.Cues: null })
            {
                throw new InvalidDataException("FFVII secondary effect callback has an invalid owner or cursor.");
            }
        }
    }
}
