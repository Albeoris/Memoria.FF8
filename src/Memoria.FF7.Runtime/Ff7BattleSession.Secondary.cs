using System.Buffers.Binary;
using System.Numerics;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7BattleSession
{
    public Ff7BattleSecondaryState? SecondaryEffects => state.SecondaryEffects;
    public Ff7BattleStageFadeState? StageFade => state.StageFade;
    public Ff7BattleStageFadeState? RenderStageFade => state.RenderStageFade ?? state.StageFade;

    private void ScheduleActionScreenPulse(byte delay)
    {
        state = state with { SecondaryEffects = Ff7BattleSecondaryEffects.ScheduleCallback(EnsureSecondaryEffects(), new Ff7BattleDelayedScreenPulseCallback(delay)) };
    }

    private void ScheduleActionScreenFade()
    {
        state = state with { SecondaryEffects = Ff7BattleSecondaryEffects.ScheduleCallback(EnsureSecondaryEffects(), new Ff7BattleScreenFadeCallback()) };
    }

    private void ScheduleActorGun(int slot, ReadOnlySpan<byte> operands)
    {
        int bone = unchecked((sbyte)animations!.JointSelector(ActorModel(slot), operands[0]));
        state = state with { SecondaryEffects = Ff7BattleSecondaryEffects.ScheduleGun(EnsureSecondaryEffects(), slot, bone, BinaryPrimitives.ReadInt16LittleEndian(operands[1..]), operands[3], operands[4]) };
    }

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
        return source with { Slots = source.Slots.ToArray(), Muzzles = source.Muzzles.ToArray(), Casings = source.Casings.ToArray(), Sparks = source.Sparks.ToArray(), Dust = source.Dust.ToArray(), Hitmarks = source.Hitmarks.ToArray(), SoundIds = source.SoundIds.ToArray(), ActorWrites = source.ActorWrites.ToArray(), ScreenPulses = source.ScreenPulses.ToArray(), LimitPreparations = source.LimitPreparations.ToArray(), WaveSparks = source.WaveSparks.ToArray(), TailLaserDraws = source.TailLaserDraws.ToArray(), ElectromagSprites = source.ElectromagSprites.ToArray(), DrawOrder = source.DrawOrder.ToArray() };
    }

    private static bool IsValidElectromagSecondary(Ff7BattleSecondaryCallback? callback) => callback switch
    {
        Ff7BattleElectromagFragment { Age: >= 0 and <= 15, Position: not null, Frame: >= 0 } => true,
        Ff7BattleElectromagSpinningFragment { Age: >= 0 and <= 15, Position: not null, Velocity: not null, Frame: >= 0 } => true,
        _ => false
    };

    private static void ValidateSecondaryEffects(Ff7BattleSnapshot snapshot)
    {
        if (snapshot.SecondaryEffects is not { } secondary)
        {
            return;
        }
        if (secondary.Slots is not { Count: Ff7BattleSecondaryEffects.Capacity } || secondary.Muzzles is null || secondary.Casings is null || secondary.Sparks is null || secondary.Dust is null || secondary.Hitmarks is null || secondary.SoundIds is null || secondary.DrawOrder is null || secondary.ActorWrites is null || secondary.ScreenPulses is null || secondary.LimitPreparations is null || secondary.WaveSparks is null || secondary.TailLaserDraws is null || secondary.ElectromagSprites is null || secondary.ElectromagSprites.Any(draw => draw is null || draw.Position is null || draw.Resource is < 0 or >= 20 || draw.Frame < 0) || secondary.DrawOrder.Any(draw => draw.Kind == Ff7BattleSecondaryDrawKind.Electromag && (uint)draw.Index >= secondary.ElectromagSprites.Count) || secondary.TailLaserDraws.Any(draw => !IsValidTailLaserDraw(draw)) || secondary.DrawOrder.Any(draw => draw.Kind == Ff7BattleSecondaryDrawKind.TailLaser && (uint)draw.Index >= secondary.TailLaserDraws.Count) || secondary.DrawOrder.Any(draw => draw.Kind == Ff7BattleSecondaryDrawKind.WaveSpark && (uint)draw.Index >= secondary.WaveSparks.Count) || secondary.DrawOrder.Count > Ff7BattleSecondaryEffects.Capacity || secondary.Muzzles.Count + secondary.Casings.Count + secondary.Sparks.Count + secondary.Dust.Count + secondary.Hitmarks.Count + secondary.SoundIds.Count + secondary.ScreenPulses.Count + secondary.LimitPreparations.Count + secondary.WaveSparks.Count + secondary.TailLaserDraws.Count + secondary.ElectromagSprites.Count > Ff7BattleSecondaryEffects.Capacity)
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
                Ff7BattleActorUvAnimationCallback uvCallback => uvCallback.Actor,
                Ff7BattleGunEmitter emitter => emitter.Actor,
                Ff7BattleGunMuzzleCallback muzzle => muzzle.Actor,
                Ff7BattleHitmarkOwnerCallback owner => owner.Target,
                Ff7BattleHitmarkSpriteCallback sprite => sprite.Target,
                Ff7BattleDelayedActorHitmarkCallback delayed => delayed.Actor,
                Ff7BattleBossDeathShakeCallback shake => shake.State.Target,
                Ff7BattleLimitPreparationEntry preparation => preparation.Actor,
                _ => -1
            };
            if (callback.Age is < 0 or int.MaxValue || actor != -1 && snapshot.Actors.All(candidate => candidate.Actor.Slot != actor) || callback is Ff7BattleBossDeathShakeCallback { State.Remaining: < 0 or > 64 } || callback is Ff7BattleLimitPreparationEntry { Bone: < 0 } || callback is Ff7BattleWaveSpark { Age: > 7 } || callback is Ff7BattleTailLaserSecondary && !IsValidTailLaserSecondary(callback) || (callback is Ff7BattleElectromagFragment or Ff7BattleElectromagSpinningFragment) && !IsValidElectromagSecondary(callback) || callback is Ff7BattleDelayedSoundCallback { Delay: < 0 or > 255 } || callback is Ff7BattleGunEmitter { Bone: < -1 } || callback is Ff7BattleGunMuzzleCallback { Bone: < -1 } || callback is Ff7BattleGunCasingCallback { Bounces: < 0 or > 1 } || callback is Ff7BattleHitmarkOwnerCallback { Definition: null } or Ff7BattleHitmarkOwnerCallback { Definition.Cues: null })
            {
                throw new InvalidDataException("FFVII secondary effect callback has an invalid owner or cursor.");
            }
            if (callback is Ff7BattleScreenFadeCallback fade && (fade.Age > Ff7BattleScreenFadeCallback.HoldAge || fade.Age != 0 && secondary.ScreenFadeOpacity is null))
            {
                throw new InvalidDataException("FFVII screen fade has an invalid callback or lost its shared opacity.");
            }
            if (callback is Ff7BattleActorUvAnimationCallback uv && (uv.State is null || uv.State.Delay > 255 || uv.Selector >= 8 || string.IsNullOrWhiteSpace(uv.Model)))
            {
                throw new InvalidDataException("FFVII actor UV animation lost its original selector, source model or delay.");
            }
            if (callback is Ff7BattleDelayedActorHitmarkCallback hitmark && (hitmark.Delay is < 0 or > 255 || hitmark.Definition?.Cues is null))
            {
                throw new InvalidDataException("FFVII delayed actor hitmark has an invalid definition or delay.");
            }
        }
    }
}
