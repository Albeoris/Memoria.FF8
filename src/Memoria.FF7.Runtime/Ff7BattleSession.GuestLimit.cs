namespace Memoria.FF7.Runtime;

/// <summary>Owns one source-native Limit selection and its ordered contact publications.</summary>
public interface IFf7BattleGuestLimit
{
    bool LimitReady { get; }
    int PublishedContacts { get; }
    void ObserveLimitReady(long hostTick, Ff7BattleAiActor source);
    bool SupportsLimit(Ff7BattleGuestAttackTarget target);
}

public sealed record Ff7BattleGuestLimitState(int Contacts = 0, bool CounterPending = false, bool KnockedOut = false, bool PresentationCompleted = false);

public sealed partial class Ff7BattleSession
{
    public bool SubmitGuestLimit(int slot, int targetSlot)
    {
        if (Outcome != Ff7BattleOutcome.Fighting || guestAttack is not IFf7BattleGuestLimit { LimitReady: true } limit || guestAttack.ActorSlot != slot || ReadyPlayer != slot || !CanAct(Actor(slot)) || !state.Actors.Any(actor => actor.Actor.Slot == targetSlot && GuestLimitTarget(limit, actor)))
        {
            return false;
        }
        state = state with { Queue = Ff7BattleCommandQueue.StageReady(state.Queue, slot, (byte)Ff7BattleCommand.Limit, targetMask: (ushort)(1 << targetSlot), guestAttack: true) };
        Replace(Actor(slot) with { AwaitingTurn = false });
        return true;
    }

    private bool GuestLimitTarget(IFf7BattleGuestLimit limit, Ff7BattleActorState target)
    {
        return target.Actor.Slot >= 4 && IsAlive(target) && (target.Actor.Conditions & 8) != 0 && limit.SupportsLimit(DescribeGuestTarget(target.Actor.Slot));
    }

    private void AdvanceGuestLimit(Ff7BattleActionState active, IFf7BattleGuestAttack guest, Ff7BattleGuestInvocation invocation)
    {
        IFf7BattleGuestLimit limit = guest as IFf7BattleGuestLimit ?? throw new InvalidDataException("The active guest Limit lost its native source owner.");
        Ff7BattleGuestLimitState current = active.GuestLimit!;
        if (current.PresentationCompleted)
        {
            AdvanceGuestIdle(guest);
            CompleteGuestLimitWhenReady(active);
            return;
        }
        if (guest.Invocation != invocation || current.Contacts != limit.PublishedContacts)
        {
            throw new InvalidDataException("The guest Limit source and host contact cursors disagree.");
        }
        Ff7BattleGuestAttackUpdate update = guest.Advance(Tick);
        int contacts = current.Contacts + (update.Hit is null ? 0 : 1);
        if (contacts > 9 || (update.Completed ? guest.Invocation is not null || contacts == 0 : guest.Invocation != invocation || contacts != limit.PublishedContacts))
        {
            throw new InvalidDataException("The guest Limit published an invalid contact or completion receipt.");
        }
        if (update.Hit is { } strike)
        {
            current = ApplyGuestLimitStrike(active.Command, invocation.TargetSlot, strike, current) with { Contacts = contacts };
            active = active with { Applied = true, GuestLimit = current };
        }
        if (update.Completed)
        {
            int target = invocation.TargetSlot;
            if (current.CounterPending)
            {
                PublishEnemyCounters(active.Command, Actor(target).Actor, current.KnockedOut, physical: true);
            }
            if (current.KnockedOut)
            {
                short selector = Actor(target).Actor.HurtAction;
                AdmitImpactModel(new(active.Command.ActorSlot, target, -1, -1, -1) { ModelAction = selector, PostActionKnockedOut = true });
            }
            active = active with { GuestLimit = current with { PresentationCompleted = true } };
            CompleteGuestLimitWhenReady(active);
            return;
        }
        state = state with { Action = active };
    }

    private Ff7BattleGuestLimitState ApplyGuestLimitStrike(Ff7BattleQueuedCommand command, int targetSlot, Ff7BattleGuestPhysicalHit strike, Ff7BattleGuestLimitState current)
    {
        if (strike is not { Elements: 0, PreparedContact: null, ItemEffect: null, Attack: { Formula: 0x11, UsePhysicalStats: true, SuppressCritical: true, ForceCritical: false, TargetsMagicPoints: false, HasStatusOperation: false, Healing: false } })
        {
            throw new NotSupportedException("The current guest Limit profile requires its original physical HP contact.");
        }
        Ff7BattleActorState source = Actor(command.ActorSlot);
        Ff7BattleActorState target = Actor(targetSlot);
        (source, Ff7BattleDamageAction attack) = ProjectPhysicalFacing(source, target, strike.Attack);
        Replace(source);
        Ff7BattleActionVitals sourceVitals = new(source.Actor.HitPoints, source.Actor.MaxHitPoints, source.Actor.MagicPoints, source.Actor.MaxMagicPoints, source.Actor.Statuses);
        Ff7BattleActionVitals targetVitals = new(target.Actor.HitPoints, target.Actor.MaxHitPoints, target.Actor.MagicPoints, target.Actor.MaxMagicPoints, target.Actor.Statuses, ~Enemy(targetSlot).NativeStatusMask);
        Ff7BattleActionReceipt result = Ff7NativeBattleActionResolver.Resolve(strike.Source with { BackRow = (source.Actor.Conditions & 0x40) != 0 }, sourceVitals, Stats(target), targetVitals, attack, null, random);
        PublishHit(command, target, attack, result, runCounters: false);
        state = state with { DisplayedHit = state.LastHit };
        if (!result.Damage.Missed && result.Damage.Damage > 0)
        {
            state = state with { Reaction = new(targetSlot, Tick) };
            AdmitImpactModel(new(command.ActorSlot, targetSlot, -1, -1, -1) { ModelAction = target.Actor.HurtAction });
        }
        return current with { CounterPending = current.CounterPending || !result.Damage.Missed, KnockedOut = current.KnockedOut || (result.AddedStatuses & 1) != 0 };
    }

    private void CompleteGuestLimitWhenReady(Ff7BattleActionState active)
    {
        if (!NativeActorModelsReady || HasPrimaryPresentationCallbacks)
        {
            state = state with { Action = active };
            return;
        }
        FinishTurn(active.Command.ActorSlot);
        state = state with { Action = null };
    }
}
