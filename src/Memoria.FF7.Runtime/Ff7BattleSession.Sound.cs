using System.Numerics;
using Memoria.FF7.Formats;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7BattleSession
{
    public Ff7BattleSoundReceipt[] DrainSoundRequests()
    {
        Ff7BattleSoundReceipt[] requests = state.SoundRequests;
        state = state with { SoundRequests = [] };
        return requests;
    }

    private void BeginSoundAction()
    {
        Ff7BattleSoundState sounds = state.Sounds ?? Ff7BattleSoundState.Empty;
        state = state with { Sounds = sounds with { PreparedImpacts = [] } };
    }

    private void CaptureImpactPresentation(Ff7BattleQueuedCommand command, int target, Ff7BattleActorState source, Ff7BattleActionReceipt receipt, Ff7BattleDamageAction damageAction, Ff7BattleAttack? record)
    {
        if (animations is null || command.CommandId == 0x23)
        {
            return;
        }
        Ff7BattleActorState previous = Actor(target);
        byte hurtIndex = command.CommandId == (byte)Ff7BattleCommand.Item ? (byte)0 : record?.HurtAction ?? 0;
        bool revived = (previous.Actor.Statuses & 1) != 0 && (receipt.Target.Statuses & 1) == 0;
        short modelAction = Ff7NativeBattleModelReaction.Select(target < 3, previous.Actor.HurtAction, hurtIndex, animations.ActorResources?.PlayerHurtActions, damageAction.Power, previous.Actor.Statuses, receipt.Damage.Missed, receipt.Damage.Damage, damageAction.TargetsMagicPoints, revived, (receipt.AddedStatuses & 1) != 0);
        Ff7BattleImpactPresentation impact = ReadImpactPresentation(command, target, source, receipt.Damage, record) with { ModelAction = modelAction };
        Ff7BattleSoundState sounds = state.Sounds ?? Ff7BattleSoundState.Empty;
        state = state with { Sounds = sounds with { PreparedImpacts = [.. sounds.PreparedImpacts.Where(previous => previous.Target != target), impact] } };
    }

    private Ff7BattleImpactPresentation ReadImpactPresentation(Ff7BattleQueuedCommand command, int target, Ff7BattleActorState source, Ff7BattleDamageResult result, Ff7BattleAttack? record)
    {
        int sound;
        int hitmark;
        if (record is null && command.CommandId == (byte)Ff7BattleCommand.Attack)
        {
            ReadOnlySpan<byte> character = characters[command.ActorSlot]!.NativeRecord;
            ReadOnlySpan<byte> weapon = kernel[5].Data.Span.Slice(character[28] * 44, 44);
            bool frog = (source.Actor.Statuses & 0x800) != 0;
            sound = Ff7NativeBattleSound.WeaponImpactSound(weapon, result.Critical, result.Missed, frog);
            hitmark = frog ? 0 : weapon[39];
        }
        else
        {
            sound = record is null ? -1 : unchecked((short)record.ImpactSound);
            hitmark = record?.ImpactEffect ?? 255;
        }
        return new(command.ActorSlot, target, sound, result.Missed ? -1 : 0, result.Missed || hitmark == 255 ? -1 : hitmark);
    }

    private void QueueImpactPresentation(Ff7BattleQueuedCommand command, int target)
    {
        if (animations is null)
        {
            return;
        }
        Ff7BattleSoundState sounds = state.Sounds ?? Ff7BattleSoundState.Empty;
        Ff7BattleImpactPresentation? impact = sounds.PreparedImpacts.FirstOrDefault(candidate => candidate.Source == command.ActorSlot && candidate.Target == target);
        if (impact is null)
        {
            Ff7BattleAttack? record = command.CommandId switch
            {
                0x20 => Data.Attacks.Single(attack => attack.Slot == command.Argument && attack.Id == command.AttackId).Data,
                (byte)Ff7BattleCommand.Magic => ReadKernelAttack(command.Argument),
                (byte)Ff7BattleCommand.Item => ReadBattleItem(command.Argument),
                (byte)Ff7BattleCommand.Limit => limits[command.Argument - 0x80],
                _ => null
            };
            Ff7BattleDamageResult damage = state.LastHit is { } hit && hit.Source == command.ActorSlot && hit.Target == target ? hit.Damage : new(false, false, 0);
            impact = ReadImpactPresentation(command, target, Actor(command.ActorSlot), damage, record);
        }
        state = state with { Sounds = sounds with { DueImpacts = [.. sounds.DueImpacts, impact] } };
    }

    private void AdvanceBattleSounds()
    {
        if (animations is null)
        {
            return;
        }
        EnsureSecondaryEffects();
        Ff7BattleSoundState before = state.Sounds ?? Ff7BattleSoundState.Empty;
        foreach (Ff7BattleImpactPresentation impact in before.PendingHitmarks.Concat(before.DueImpacts))
        {
            int style = impact.Hitmark;
            if (style >= 0 && SoundScreenX(state.SoundAttacker) < SoundScreenX(impact.Target))
            {
                style |= 0x100;
            }
            StartHitmark(impact.Target, style);
        }
        Ff7BattleSecondaryState secondary = Ff7BattleSecondaryEffects.Advance(state.SecondaryEffects!, SecondaryActorPoint, slot => new(0, (short)VisualPlacement(slot).Yaw, 0), random.NextCrt);
        state = state with { SecondaryEffects = secondary, Sounds = before with { DueImpacts = [], PendingHitmarks = [], DueEffects = [] } };
        foreach (Ff7BattleActorModelWrite write in secondary.ActorWrites)
        {
            ApplyActorModelWrite(write);
        }
        foreach (ushort sound in secondary.SoundIds)
        {
            AppendSounds(Ff7NativeBattleSound.Emit(state.Sounds!, sound, Ff7NativeBattleSound.ActorPan(SoundScreenX(state.SoundAttacker))));
        }
        foreach (Ff7BattleAuthoredSound effect in before.DueEffects)
        {
            AppendSounds(Ff7NativeBattleSound.Emit(state.Sounds!, effect.Sound, effect.Pan));
        }
        foreach (Ff7BattleImpactPresentation impact in before.DueImpacts)
        {
            AppendSounds(Ff7NativeBattleSound.Impact(state.Sounds!, impact.Sound, impact.ReactionEffect, impact.Source, SoundScreenX(impact.Target)));
        }
    }

    private void PublishSoundScreenPositions()
    {
        if (animations is null)
        {
            return;
        }
        short[] projected = new short[Ff7NativeAtb.SlotCount];
        foreach (Ff7BattleActorState actor in state.Actors)
        {
            int slot = actor.Actor.Slot;
            projected[slot] = ActorProjectedScreenX(slot);
        }
        state = state with { SoundProjectedX = projected };
    }

    private void EffectEntrySound(Ff7BattleActionState before, Ff7BattleActionState after)
    {
        ushort? sound = (before, after) switch
        {
            ({ Fire: null }, { Fire: not null }) => 9,
            ({ Ice: null }, { Ice: not null }) => 24,
            ({ SmokeShot: null }, { SmokeShot: not null }) => 31,
            ({ Cure: null }, { Cure.Kind: Ff7BattleCureKind.Cure }) => 6,
            ({ Cure: null }, { Cure.Kind: Ff7BattleCureKind.Ether }) => 31,
            ({ Cure: null }, { Cure.Kind: Ff7BattleCureKind.PhoenixDown }) => 382,
            ({ Cure: null }, { Cure.Kind: Ff7BattleCureKind.Antidote }) => 92,
            _ => null
        };
        if (sound is ushort id)
        {
            AppendSounds(Ff7NativeBattleSound.Emit(state.Sounds ?? Ff7BattleSoundState.Empty, id, SoundMaskPan(after.Command.TargetMask)));
        }
        if (before.Grenade is null && after.Grenade is not null)
        {
            AppendSounds(Ff7NativeBattleSound.Emit(state.Sounds ?? Ff7BattleSoundState.Empty, 20, Ff7NativeBattleSound.ActorPan(SoundScreenX(after.Command.ActorSlot))));
        }
        if (before.Smog is null && after.Smog is not null)
        {
            AppendSounds(Ff7NativeBattleSound.Emit(state.Sounds ?? Ff7BattleSoundState.Empty, 138, Ff7NativeBattleSound.ActorPan(SoundScreenX(after.Command.ActorSlot))));
        }
        if (before.Destroyed is null && after.Destroyed is not null)
        {
            AppendSounds(Ff7NativeBattleSound.Emit(state.Sounds ?? Ff7BattleSoundState.Empty, 390, Ff7NativeBattleSound.ActorPan(SoundScreenX(after.Command.ActorSlot))));
        }
        if (before.SearchScope is null && after.SearchScope is not null)
        {
            AppendSounds(Ff7NativeBattleSound.Emit(state.Sounds ?? Ff7BattleSoundState.Empty, 98, Ff7NativeBattleSound.ActorPan(SoundScreenX(after.Command.ActorSlot))));
        }
        if (before.Rockets is null && after.Rockets is not null)
        {
            AppendSounds(Ff7NativeBattleSound.Emit(state.Sounds ?? Ff7BattleSoundState.Empty, 130, Ff7NativeBattleSound.ActorPan(SoundScreenX(after.Command.ActorSlot))));
        }
    }

    private void EffectCallbackSounds(Ff7BattleActionState before, Ff7BattleActionState after)
    {
        if (after.Bolt is { } bolt && bolt.Frame != before.Bolt?.Frame && bolt.Frame >= 0 && (bolt.Frame & 1) == 0)
        {
            int[] targets = state.Actors.Where(actor => (bolt.TargetMask & (1 << actor.Actor.Slot)) != 0).Select(actor => actor.Actor.Slot).ToArray();
            int ordinal = bolt.Frame / 2;
            if (ordinal < targets.Length)
            {
                QueueEffectSound(11, ImpactAnchor(targets[ordinal]));
            }
        }
        if (before.Braver?.Frame != 26 && after.Braver is { Frame: 26, Impact: { } impact })
        {
            QueueEffectSound(27, impact.Anchor.Position);
        }
        if (before.BigShot?.Frame != 0 && after.BigShot is { Frame: 0 })
        {
            QueueEffectSound(40, VisualPlacement(after.Command.ActorSlot).Position);
        }
        if (after.BeatRushEffect is { } beat && beat.Frame != before.BeatRushEffect?.Frame)
        {
            foreach (Ff7BattleBeatRushSprite sprite in (beat.Sprites ?? []).Where(sprite => sprite.StartedAt == beat.Frame))
            {
                QueueEffectSound(347, sprite.Anchor.Position);
            }
        }
    }

    private void QueueEffectSound(ushort sound, Vector3 point)
    {
        AppendSounds(Ff7NativeBattleSound.Emit(state.Sounds ?? Ff7BattleSoundState.Empty, sound, SoundPointPan(point)));
    }

    private byte SoundPointPan(Vector3 point)
    {
        Vector3 projected = BattleCameraBasis.Create(RenderCameraOutput).Transform(point);
        short screenX = projected.Z == 0 ? (short)-1 : unchecked((short)(int)(160 + projected.X * 512 / projected.Z));
        return Ff7NativeBattleSound.PointPan(screenX);
    }

    private byte SoundMaskPan(ushort mask)
    {
        Vector3[] positions = state.Actors.Where(actor => (mask & (1 << actor.Actor.Slot)) != 0).Select(actor => VisualPlacement(actor.Actor.Slot).Position).ToArray();
        if (positions.Length == 0)
        {
            throw new InvalidDataException("FFVII effect sound requires an original target mask.");
        }
        int x = ((short)positions.Min(point => point.X) + (short)positions.Max(point => point.X)) / 2;
        int z = ((short)positions.Min(point => point.Z) + (short)positions.Max(point => point.Z)) / 2;
        return SoundPointPan(new(x, 0, z));
    }

    private short SoundScreenX(int slot)
    {
        return slot >= 0 && state.SoundProjectedX is { } coordinates && slot < coordinates.Length ? coordinates[slot] : (short)160;
    }

    private void AppendSounds(Ff7BattleSoundUpdate update)
    {
        state = state with { Sounds = update.State, SoundRequests = [.. state.SoundRequests, .. update.Sounds] };
    }

    private static Ff7BattleSoundState CopySoundState(Ff7BattleSoundState sounds)
    {
        return sounds with
        {
            Pending = (Ff7BattleDelayedSound[])sounds.Pending.Clone(),
            DueEffects = (Ff7BattleAuthoredSound[])sounds.DueEffects.Clone(),
            PreparedImpacts = (Ff7BattleImpactPresentation[])sounds.PreparedImpacts.Clone(),
            DueImpacts = (Ff7BattleImpactPresentation[])sounds.DueImpacts.Clone(),
            PendingHitmarks = (Ff7BattleImpactPresentation[])sounds.PendingHitmarks.Clone()
        };
    }

    private static Ff7BattleActionState? CopyActionReactions(Ff7BattleActionState? action)
    {
        return action?.TargetReactions is { } reactions ? action with { TargetReactions = new((Ff7BattleTargetCallbacks[])reactions.Pending.Clone()) } : action;
    }

    private static void ValidateSoundSnapshot(Ff7BattleSnapshot snapshot)
    {
        if (snapshot.SoundAttacker is < 0 or >= Ff7NativeAtb.SlotCount || snapshot.SoundProjectedX is { Length: not Ff7NativeAtb.SlotCount } || snapshot.SoundRequests is null)
        {
            throw new InvalidDataException("FFVII battle snapshot has an invalid sound owner or projection.");
        }
        if (snapshot.Sounds is { } sounds)
        {
            if (sounds.Sequence < 0 || sounds.Pending is null || sounds.Pending.Length > 60 || sounds.Pending.Any(sound => sound is null || sound.Delay is < 0 or > 255) || sounds.DueEffects is null || sounds.DueEffects.Length > 100 || sounds.DueEffects.Any(sound => sound is null || sound.Pan > 127) || sounds.PreparedImpacts is null || sounds.DueImpacts is null || sounds.PendingHitmarks is null || sounds.PreparedImpacts.Length > 10 || sounds.DueImpacts.Length > 100 || sounds.PendingHitmarks.Length > 100 || sounds.PreparedImpacts.Concat(sounds.DueImpacts).Concat(sounds.PendingHitmarks).Any(impact => impact is null || snapshot.Actors.All(actor => actor.Actor.Slot != impact.Source) || snapshot.Actors.All(actor => actor.Actor.Slot != impact.Target) || impact.Sound is < -1 or > ushort.MaxValue || impact.ReactionEffect is < -1 or > 0 || impact.Hitmark is < -1 or > 255))
            {
                throw new InvalidDataException("FFVII battle snapshot has corrupt sound callbacks or impact records.");
            }
        }
        long sequence = 0;
        foreach (Ff7BattleSoundReceipt sound in snapshot.SoundRequests)
        {
            if (sound is null || sound.Sequence <= sequence || sound.Sequence > (snapshot.Sounds?.Sequence ?? 0) || sound.Pan > 127 || sound.Channel is < 1 or > 5)
            {
                throw new InvalidDataException("FFVII battle snapshot has invalid sound receipt ordering.");
            }
            sequence = sound.Sequence;
        }
        int actorMask = snapshot.Actors.Sum(actor => 1 << actor.Actor.Slot);
        if (snapshot.Action?.TargetReactions is { } reactions && (reactions.Pending is null || reactions.Pending.Length > 100 || reactions.Pending.Any(callback => callback is null || callback.RemainingTargets == 0 || (callback.RemainingTargets & ~actorMask) != 0 || callback.Elapsed > callback.Delay + Ff7NativeAtb.SlotCount)))
        {
            throw new InvalidDataException("FFVII battle snapshot has corrupt target reaction callbacks.");
        }
    }
}
