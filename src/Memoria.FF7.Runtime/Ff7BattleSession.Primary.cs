using Memoria.FF7.Formats;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7BattleSession
{
    public Ff7BattlePrimaryState? PrimaryEffects => state.PrimaryEffects;

    private bool HasPrimaryPresentationCallbacks => state.PrimaryEffects?.HasCallbacks == true || state.Action?.EffectRunning == true;

    private Ff7BattlePrimaryContext PrimaryContext()
    {
        return new(SecondaryActorPoint, slot => new(0, (short)VisualPlacement(slot).Yaw, 0), CollisionRadius, random.NextCrt, slot => animations!.BoneCount(ActorModel(slot)), JointPoint, (slot, selector) => animations!.JointSelector(ActorModel(slot), selector));
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
        foreach (Ff7BattlePrimaryHitmark hitmark in primary.Hitmarks)
        {
            StartHitmark(hitmark.Target, hitmark.Style);
        }
        foreach (ushort sound in primary.SoundIds)
        {
            AppendSounds(Ff7NativeBattleSound.Emit(state.Sounds ?? Ff7BattleSoundState.Empty, sound, Ff7NativeBattleSound.ActorPan(SoundScreenX(state.SoundAttacker))));
        }
        if (state.Action is not { Applied: true, Command.GuestAttack: false } action)
        {
            return;
        }
        Ff7BattleActionState before = action;
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
        action = AdvanceEffects(action);
        action = action with { PrimaryCallbacksRunning = primary.HasOwner(0) };
        FinishActionPresentation(action);
    }

    private Ff7BattleActionState StartPrimaryCure(Ff7BattleActionState action, Ff7BattleCureKind kind)
    {
        state = state with { PrimaryEffects = Ff7BattlePrimaryEffects.ScheduleCure(EnsurePrimaryEffects(), action.Command.TargetMask, kind) };
        return action with { Cure = Ff7BattleCureEffect.Start(action.Command.TargetMask, kind) with { Primary = null }, PrimaryCallbacksRunning = true };
    }

    private Ff7BattleActionState StartPrimaryBolt(Ff7BattleActionState action)
    {
        state = state with { PrimaryEffects = Ff7BattlePrimaryEffects.ScheduleBolt(EnsurePrimaryEffects(), action.Command.TargetMask) };
        return action with { Bolt = Ff7BattleBoltEffect.Start(action.Command.TargetMask) with { Primary = null }, PrimaryCallbacksRunning = true };
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
        return source with { Slots = source.Slots.ToArray(), Draws = source.Draws.ToArray(), SoundIds = source.SoundIds.ToArray(), Hitmarks = source.Hitmarks.ToArray(), TargetReactions = source.TargetReactions.ToArray(), SilkLines = source.SilkLines with { Slots = source.SilkLines.Slots.ToArray() } };
    }

    private static Ff7BattleActionState? CopyActionPrimaryEffects(Ff7BattleActionState? action)
    {
        return action is null ? null : action with
        {
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
        if (primary.Slots is not { Count: Ff7BattlePrimaryEffects.Capacity } || primary.Draws is null || primary.SoundIds is null || primary.Hitmarks is null || primary.TargetReactions is null || primary.Tick < -1 || primary.Slots.Any(callback => callback is { Age: < 0 } || callback is { Owner: < -1 }) || primary.Draws.Any(draw => draw is null || draw.Slot is < 0 or >= Ff7BattlePrimaryEffects.Capacity) || primary.SilkLines?.Slots is not { Count: 200 } || primary.SilkLines.Direction is not (-1 or 1) || primary.SilkLines.Slots.Any(line => line is { Age: < 0 or > 9 }))
        {
            throw new InvalidDataException("FFVII primary presentation has invalid native slots or callback state.");
        }
    }
}
