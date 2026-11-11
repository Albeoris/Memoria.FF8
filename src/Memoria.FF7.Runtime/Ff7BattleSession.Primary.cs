using Memoria.FF7.Formats;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7BattleSession
{
    public Ff7BattlePrimaryState? PrimaryEffects => state.PrimaryEffects;

    private bool HasPrimaryPresentationCallbacks => state.PrimaryEffects?.HasCallbacks == true;

    private Ff7BattlePrimaryContext PrimaryContext()
    {
        return new(SecondaryActorPoint, slot => new(0, (short)VisualPlacement(slot).Yaw, 0), CollisionRadius, random.NextCrt, slot => animations!.BoneCount(ActorModel(slot)), JointPoint, (slot, selector) => animations!.JointSelector(ActorModel(slot), selector), PrimaryWeaponPoint, LiveActorModelOutput, slot => (short)VisualPlacement(slot).Position.Z, slot => (short)HomePlacement(slot).Position.Z);
    }

    private System.Numerics.Vector3 PrimaryWeaponPoint(int slot, System.Numerics.Vector3 point)
    {
        Ff7BattleActionState action = state.Action is { } current && current.Command.ActorSlot == slot ? current : throw new InvalidOperationException("FFVII linked-weapon presentation requires its retained source action.");
        return LimitWeaponPoint(action, point);
    }

    private Ff7BattlePrimaryState EnsurePrimaryEffects()
    {
        if (state.PrimaryEffects is { } existing)
        {
            return existing;
        }
        Ff7BattlePrimaryState primary = new() { SearchScopeCounter = state.SearchScopeScaleCounter, SilkLines = new() { Direction = state.SilkDirection } };
        if (state.StageFade is { } fade)
        {
            primary = Ff7BattlePrimaryEffects.ImportStageFade(primary, fade);
        }
        if (state.Action is { } action)
        {
            foreach (Ff7BattleTargetCallbacks reaction in TargetReactionState(action).Pending)
            {
                primary = Ff7BattlePrimaryEffects.ImportTargetReaction(primary, new(reaction.RemainingTargets, reaction.Delay) { Age = reaction.Elapsed });
            }
            if (action.SearchScope is { } scope)
            {
                primary = Ff7BattlePrimaryEffects.ImportSearchScope(primary, scope);
            }
            if (action.Fire is { } fire)
            {
                primary = Ff7BattlePrimaryEffects.ImportFire(primary, fire, PrimaryContext());
            }
            if (action.Ice is { } ice)
            {
                primary = Ff7BattlePrimaryEffects.ImportIce(primary, ice, PrimaryContext());
            }
            if (action.Braver is { } braver)
            {
                primary = Ff7BattlePrimaryEffects.ImportBraver(primary, braver, action.Command.ActorSlot);
            }
            if (action.BeatRushEffect is { } beatRush)
            {
                primary = Ff7BattlePrimaryEffects.ImportBeatRush(primary, beatRush, action.Command.ActorSlot);
            }
            if (action.BigShot is { } bigShot)
            {
                primary = Ff7BattlePrimaryEffects.ImportBigShot(primary, bigShot, action.Command.ActorSlot);
            }
            if (action.SmokeShot is { } smokeShot)
            {
                primary = Ff7BattlePrimaryEffects.ImportSmokeShot(primary, smokeShot, action.Command.ActorSlot);
            }
            if (action.Smog is { } smog)
            {
                primary = Ff7BattlePrimaryEffects.ImportSmog(primary, smog, action.Command.ActorSlot);
            }
            if (action.Rockets is { } rockets)
            {
                primary = Ff7BattlePrimaryEffects.ImportRocket(primary, rockets, action.Command.ActorSlot);
            }
            if (action.Cure is { } cure)
            {
                primary = Ff7BattlePrimaryEffects.ImportCure(primary, cure);
            }
            if (action.Bolt is { } bolt)
            {
                primary = Ff7BattlePrimaryEffects.ImportBolt(primary, bolt, PrimaryContext());
            }
            if (action.Destroyed is { } destroyed)
            {
                primary = Ff7BattlePrimaryEffects.ImportDestroyed(primary, destroyed);
            }
        }
        state = state with { PrimaryEffects = primary, Action = state.Action is { } imported ? imported with { Reactions = null, TargetReactions = null } : null };
        return primary;
    }

    private void AdvancePrimaryPresentation()
    {
        if (animations is null)
        {
            if (state.Action is { Applied: true, Command.GuestAttack: false } numerical)
            {
                FinishActionPresentation(numerical);
            }
            return;
        }
        Ff7BattlePrimaryState primary = Ff7BattlePrimaryEffects.Advance(EnsurePrimaryEffects(), PrimaryContext());
        state = state with { PrimaryEffects = primary, StageFade = primary.StageFade, SearchScopeScaleCounter = primary.SearchScopeCounter, SilkDirection = primary.SilkLines.Direction };
        foreach (Ff7BattleActorModelWrite write in primary.ActorWrites)
        {
            ApplyActorModelWrite(write);
        }
        foreach (Ff7BattleActorModelSound sound in primary.ActorSounds)
        {
            EmitActorModelSound(sound);
        }
        foreach (Ff7BattleBossDeathShakeRequest shake in primary.BossDeathShakes)
        {
            state = state with { SecondaryEffects = Ff7BattleSecondaryEffects.ScheduleBossDeathShake(EnsureSecondaryEffects(), shake.Target, shake.BaseZ) };
        }
        foreach (Ff7BattlePrimaryHitmark hitmark in primary.Hitmarks)
        {
            StartHitmark(hitmark.Target, hitmark.Style);
        }
        foreach (ushort sound in primary.SoundIds)
        {
            AppendSounds(Ff7NativeBattleSound.Emit(state.Sounds ?? Ff7BattleSoundState.Empty, sound, Ff7NativeBattleSound.ActorPan(SoundScreenX(state.SoundAttacker))));
        }
        ApplyPrimaryEffectReceipts(primary);
        if (state.Action is not { Applied: true, Command.GuestAttack: false } action)
        {
            return;
        }
        Ff7BattleActionState before = action;
        if (action.Fire is { Completed: false } fire)
        {
            action = action with { Fire = Ff7BattlePrimaryEffects.ProjectFire(primary, fire) };
        }
        if (action.Ice is { Completed: false } ice)
        {
            action = action with { Ice = Ff7BattlePrimaryEffects.ProjectIce(primary, ice) };
        }
        if (action.Braver is { Completed: false } braver)
        {
            action = action with { Braver = Ff7BattlePrimaryEffects.ProjectBraver(primary, braver) };
        }
        if (action.BeatRushEffect is { Completed: false } beatRush)
        {
            action = action with { BeatRushEffect = Ff7BattlePrimaryEffects.ProjectBeatRush(primary, beatRush) };
        }
        if (action.BigShot is { Completed: false } bigShot)
        {
            Ff7BattleBigShotState projected = Ff7BattlePrimaryEffects.ProjectBigShot(primary, bigShot);
            action = action with { BigShot = projected };
            state = state with { BigShotChargeRotation = projected.ChargeRotation, BigShotCounterRotation = projected.CounterRotation };
        }
        if (action.SmokeShot is { Completed: false } smokeShot)
        {
            action = action with { SmokeShot = Ff7BattlePrimaryEffects.ProjectSmokeShot(primary, smokeShot) };
        }
        if (action.Smog is { Completed: false } smog)
        {
            action = action with { Smog = Ff7BattlePrimaryEffects.ProjectSmog(primary, smog) };
        }
        if (action.Rockets is { Completed: false } rockets)
        {
            action = action with { Rockets = Ff7BattlePrimaryEffects.ProjectRocket(primary, rockets) };
        }
        if (action.SearchScope is { Completed: false } scope)
        {
            action = action with { SearchScope = Ff7BattlePrimaryEffects.ProjectSearchScope(primary, scope) };
        }
        if (action.Cure is { Completed: false } cure)
        {
            action = action with { Cure = Ff7BattlePrimaryEffects.ProjectCure(primary, cure) };
        }
        if (action.Bolt is { Completed: false } bolt)
        {
            action = action with { Bolt = Ff7BattlePrimaryEffects.ProjectBolt(primary, bolt) };
        }
        if (action.Destroyed is { Completed: false } destroyed)
        {
            action = action with { Destroyed = Ff7BattlePrimaryEffects.ProjectDestroyed(primary, destroyed) };
        }
        EffectCallbackSounds(before, action);
        action = PublishTargetReactions(action, primary.TargetReactions.Select(reaction => reaction.Target));
        state = state with { Action = action };
        action = action with { PrimaryCallbacksRunning = primary.HasOwner(0) };
        FinishActionPresentation(action);
    }

    private Ff7BattleActionState StartPrimaryCure(Ff7BattleActionState action, Ff7BattleCureKind kind)
    {
        state = state with { PrimaryEffects = Ff7BattlePrimaryEffects.ScheduleCure(EnsurePrimaryEffects(), action.Command.TargetMask, kind) };
        return action with { Cure = Ff7BattleCureEffect.Start(action.Command.TargetMask, kind) with { Primary = null }, PrimaryCallbacksRunning = true };
    }

    private Ff7BattleActionState StartPrimaryFire(Ff7BattleActionState action)
    {
        state = state with { PrimaryEffects = Ff7BattlePrimaryEffects.ScheduleFire(EnsurePrimaryEffects(), action.Command.TargetMask) };
        return action with { Fire = Ff7BattleFireEffect.Start(action.Command.TargetMask) with { Primary = null }, PrimaryCallbacksRunning = true };
    }

    private Ff7BattleActionState StartPrimaryIce(Ff7BattleActionState action)
    {
        state = state with { PrimaryEffects = Ff7BattlePrimaryEffects.ScheduleIce(EnsurePrimaryEffects(), action.Command.TargetMask) };
        return action with { Ice = Ff7BattleIceEffect.Start(action.Command.TargetMask) with { Primary = null }, PrimaryCallbacksRunning = true };
    }

    private Ff7BattleActionState StartPrimaryBolt(Ff7BattleActionState action)
    {
        state = state with { PrimaryEffects = Ff7BattlePrimaryEffects.ScheduleBolt(EnsurePrimaryEffects(), action.Command.TargetMask) };
        return action with { Bolt = Ff7BattleBoltEffect.Start(action.Command.TargetMask) with { Primary = null }, PrimaryCallbacksRunning = true };
    }

    private Ff7BattleActionState StartPrimaryBraver(Ff7BattleActionState action)
    {
        state = state with { PrimaryEffects = Ff7BattlePrimaryEffects.ScheduleBraver(EnsurePrimaryEffects(), action.Command.ActorSlot, action.Command.TargetMask) };
        return action with { Braver = Ff7BattleBraverEffect.Start(action.Command.TargetMask) with { Primary = null }, PrimaryCallbacksRunning = true };
    }

    private Ff7BattleActionState StartPrimaryBeatRush(Ff7BattleActionState action)
    {
        state = state with { PrimaryEffects = Ff7BattlePrimaryEffects.ScheduleBeatRush(EnsurePrimaryEffects(), action.Command.ActorSlot, action.Command.TargetMask) };
        return action with { BeatRushEffect = Ff7BattleBeatRushEffect.Start(action.Command.TargetMask) with { Primary = null }, PrimaryCallbacksRunning = true };
    }

    private Ff7BattleActionState StartPrimaryBigShot(Ff7BattleActionState action)
    {
        state = state with { PrimaryEffects = Ff7BattlePrimaryEffects.ScheduleBigShot(EnsurePrimaryEffects(), action.Command.ActorSlot, action.Command.TargetMask, state.BigShotChargeRotation, state.BigShotCounterRotation) };
        return action with { BigShot = Ff7BattleBigShotEffect.Start(action.Command.TargetMask, state.BigShotChargeRotation, state.BigShotCounterRotation) with { Primary = null }, PrimaryCallbacksRunning = true };
    }

    private Ff7BattleActionState StartPrimarySmokeShot(Ff7BattleActionState action)
    {
        state = state with { PrimaryEffects = Ff7BattlePrimaryEffects.ScheduleSmokeShot(EnsurePrimaryEffects(), action.Command.ActorSlot, action.Command.TargetMask) };
        return action with { SmokeShot = Ff7BattleSmokeShotEffect.Start(action.Command.TargetMask) with { Primary = null }, PrimaryCallbacksRunning = true };
    }

    private Ff7BattleActionState StartPrimarySmog(Ff7BattleActionState action)
    {
        state = state with { PrimaryEffects = Ff7BattlePrimaryEffects.ScheduleSmog(EnsurePrimaryEffects(), action.Command.ActorSlot, action.Command.TargetMask) };
        return action with { Smog = Ff7BattleSmogEffect.Start(action.Command.TargetMask) with { Primary = null }, PrimaryCallbacksRunning = true };
    }

    private Ff7BattleActionState StartPrimaryRocket(Ff7BattleActionState action)
    {
        state = state with { PrimaryEffects = Ff7BattlePrimaryEffects.ScheduleRocket(EnsurePrimaryEffects(), action.Command.ActorSlot, action.Command.TargetMask) };
        return action with { Rockets = Ff7BattleRocketEffect.Start(action.Command.TargetMask) with { Primary = null }, PrimaryCallbacksRunning = true };
    }

    private Ff7BattleActionState StartPrimaryDestroyed(Ff7BattleActionState action)
    {
        state = state with { PrimaryEffects = Ff7BattlePrimaryEffects.ScheduleDestroyed(EnsurePrimaryEffects(), action.Command.TargetMask) };
        return action with { Destroyed = Ff7BattleDestroyedEffect.Start(action.Command.TargetMask) with { Primary = null }, PrimaryCallbacksRunning = true };
    }

    private Ff7BattleActionState StartPrimarySilk(Ff7BattleActionState action)
    {
        int target = System.Numerics.BitOperations.TrailingZeroCount((uint)action.Command.TargetMask);
        state = state with { PrimaryEffects = Ff7BattlePrimaryEffects.ScheduleSilk(EnsurePrimaryEffects(), action.Command.TargetMask, action.Command.ActorSlot, target) };
        AppendSounds(Ff7NativeBattleSound.Emit(state.Sounds ?? Ff7BattleSoundState.Empty, 0x6A, Ff7NativeBattleSound.ActorPan(SoundScreenX(action.Command.ActorSlot))));
        return action with { SilkStarted = true, PrimaryCallbacksRunning = true };
    }

    private static Ff7BattlePrimaryState CopyPrimaryEffects(Ff7BattlePrimaryState source)
    {
        return Ff7BattlePrimaryEffects.Copy(source);
    }

    private static Ff7BattleActionState? CopyActionPrimaryEffects(Ff7BattleActionState? action)
    {
        return action is null ? null : action with
        {
            Fire = action.Fire is { } fire ? fire with { Targets = fire.Targets.ToArray(), Sprites = fire.Sprites.ToArray(), Primary = fire.Primary is null ? null : CopyPrimaryEffects(fire.Primary) } : null,
            Ice = action.Ice is { } ice ? ice with { Crystals = ice.Crystals?.ToArray(), Primary = ice.Primary is null ? null : CopyPrimaryEffects(ice.Primary) } : null,
            Braver = action.Braver is { } braver ? braver with { Primary = braver.Primary is null ? null : CopyPrimaryEffects(braver.Primary) } : null,
            BeatRushEffect = action.BeatRushEffect is { } beatRush ? beatRush with { Trails = beatRush.Trails?.Select(trail => trail with { Samples = trail.Samples.ToArray() }).ToArray(), Sprites = beatRush.Sprites?.ToArray(), Primary = beatRush.Primary is null ? null : CopyPrimaryEffects(beatRush.Primary) } : null,
            BigShot = action.BigShot is { } bigShot ? bigShot with { Particles = bigShot.Particles?.ToArray(), Primary = bigShot.Primary is null ? null : CopyPrimaryEffects(bigShot.Primary) } : null,
            SmokeShot = action.SmokeShot is { } smokeShot ? smokeShot with { Targets = smokeShot.Targets.ToArray(), Sprites = smokeShot.Sprites.ToArray(), Primary = smokeShot.Primary is null ? null : CopyPrimaryEffects(smokeShot.Primary) } : null,
            Smog = action.Smog is { } smog ? smog with { Targets = smog.Targets.ToArray(), Sprites = smog.Sprites.ToArray(), Primary = smog.Primary is null ? null : CopyPrimaryEffects(smog.Primary) } : null,
            Rockets = action.Rockets is { } rockets ? rockets with { Targets = rockets.Targets.ToArray(), Explosions = rockets.Explosions.ToArray(), Missiles = rockets.Missiles.ToArray(), Sprites = rockets.Sprites.ToArray(), Points = rockets.Points.ToArray(), Primary = rockets.Primary is null ? null : CopyPrimaryEffects(rockets.Primary) } : null,
            Cure = action.Cure is { } cure ? cure with { Targets = cure.Targets.ToArray(), Particles = cure.Particles.ToArray(), Primary = cure.Primary is null ? null : CopyPrimaryEffects(cure.Primary) } : null,
            Bolt = action.Bolt is { } bolt ? bolt with { Strikes = bolt.Strikes?.ToArray(), Sparks = bolt.Sparks?.ToArray(), Primary = bolt.Primary is null ? null : CopyPrimaryEffects(bolt.Primary) } : null,
            Destroyed = action.Destroyed is { } destroyed ? destroyed with { Sprites = destroyed.Sprites?.ToArray(), Primary = destroyed.Primary is null ? null : CopyPrimaryEffects(destroyed.Primary) } : null,
            SearchScope = action.SearchScope is { } scope ? scope with { Meshes = scope.Meshes.ToArray(), Sprites = scope.Sprites.ToArray(), Primary = scope.Primary is null ? null : CopyPrimaryEffects(scope.Primary) } : null
        };
    }

    private static void ValidatePrimaryEffects(Ff7BattleSnapshot snapshot)
    {
        if (snapshot.PrimaryEffects is not { } primary)
        {
            return;
        }
        if (primary.Slots is not { Count: Ff7BattlePrimaryEffects.Capacity } || primary.Draws is null || primary.SoundIds is null || primary.Hitmarks is null || primary.TargetReactions is null || primary.Bubbles?.Particles is not { Count: 200 } || primary.Bubbles.Particles.Any(particle => particle is { Age: < 0 or > 24 }) || primary.WaveGlobals is null || primary.EnergyBallGlobals is null || primary.WaveSparks is null || primary.PositionedSounds is null || primary.WaveSparks.Count > Ff7BattleSecondaryEffects.Capacity || primary.PositionedSounds.Any(cue => cue is null || cue.Actor != -1 && snapshot.Actors.All(actor => actor.Actor.Slot != cue.Actor) || (!cue.RawPan && cue.FallbackPan > 127 || cue.RawPan && cue.Actor != -1) || cue.Channel is < 1 or > 5) || primary.Tick < -1 || primary.Slots.Any(callback => callback is { Age: < 0 } || callback is { Owner: < -1 }) || primary.Draws.Any(draw => draw is null || draw.Slot is < 0 or >= Ff7BattlePrimaryEffects.Capacity) || primary.SilkLines?.Slots is not { Count: 200 } || primary.SilkLines.Direction is not (-1 or 1) || primary.SilkLines.Slots.Any(line => line is { Age: < 0 or > 9 }))
        {
            throw new InvalidDataException("FFVII primary presentation has invalid native slots or callback state.");
        }
        ValidateTailLaserPrimary(primary, snapshot);
    }
}
