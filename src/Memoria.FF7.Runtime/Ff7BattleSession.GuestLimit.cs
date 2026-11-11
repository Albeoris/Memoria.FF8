namespace Memoria.FF7.Runtime;

/// <summary>Owns one source-native Limit selection and its ordered contact publications.</summary>
public interface IFf7BattleGuestLimit
{
    bool LimitReady { get; }
    int PublishedContacts { get; }
    void ObserveLimitReady(long hostTick, Ff7BattleAiActor source);
    bool SupportsLimit(Ff7BattleGuestAttackTarget target);
}

public sealed record Ff7BattleGuestLimitState(int Contacts = 0, bool CounterPending = false, bool KnockedOut = false, bool PresentationCompleted = false)
{
    public int PresentedContacts { get; init; }
    public Ff7BattleHit? PendingHit { get; init; }
    public byte? TargetCue { get; init; }
}

public sealed partial class Ff7BattleSession
{
    private void ObserveGuestLimitReady(Ff7BattleActorState source)
    {
        if (state.GuestLimitReadyAt is null && IsGuestVitalsSlot(source.Actor.Slot) && CanAct(source) && guestAttack is IFf7BattleGuestLimit limit)
        {
            limit.ObserveLimitReady(Tick, source.Actor);
            state = state with { GuestLimitReadyAt = Tick };
        }
    }

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
        if (update.LimitTargetCue is byte cue)
        {
            if (cue is not (4 or 6) || current.TargetCue is not null || current.PresentedContacts >= 9)
            {
                throw new InvalidDataException("The guest Limit target cue has no next original contact.");
            }
            current = current with { TargetCue = cue };
            if (IsAlive(Actor(invocation.TargetSlot)) || current.PendingHit is { Damage: { Missed: false, Damage: > 0 } })
            {
                AdmitGuestLimitReaction(active.Command, invocation.TargetSlot);
            }
        }
        if (update.Hit is { } strike)
        {
            if (current.PendingHit is not null)
            {
                throw new InvalidDataException("The guest Limit published a numerical hit before presenting its previous contact.");
            }
            current = ApplyGuestLimitStrike(active.Command, invocation.TargetSlot, strike, current) with { Contacts = contacts };
        }
        if (update.LimitPresentedContact is int presented)
        {
            if (presented != current.PresentedContacts + 1 || presented != contacts || current.PendingHit is not { } hit)
            {
                throw new InvalidDataException("The guest Limit presented a contact without its original numerical receipt.");
            }
            state = state with { DisplayedHit = hit with { Tick = Tick } };
            if (!hit.Damage.Missed && hit.Damage.Damage > 0)
            {
                state = state with { Reaction = new(invocation.TargetSlot, Tick) };
                if (current.TargetCue is null)
                {
                    AdmitGuestLimitReaction(active.Command, invocation.TargetSlot);
                }
            }
            current = current with { PresentedContacts = presented, PendingHit = null, TargetCue = null };
        }
        active = active with { Applied = contacts != 0, GuestLimit = current };
        if (update.Completed)
        {
            if (current.PresentedContacts != contacts || current.TargetCue is not null)
            {
                throw new InvalidDataException("The guest Limit completed before its last original contact was presented.");
            }
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
        if (strike is not { Elements: 0, PreparedContact: null, ItemEffect: null, Attack: { Formula: 0x11, UsePhysicalStats: true, SuppressCritical: true, TargetsMagicPoints: false, HasStatusOperation: false, Healing: false } } || strike.Attack.ForceCritical && !strike.Attack.AlwaysHits)
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
        return current with { PendingHit = state.LastHit, CounterPending = current.CounterPending || !result.Damage.Missed, KnockedOut = current.KnockedOut || (result.AddedStatuses & 1) != 0 };
    }

    private void AdmitGuestLimitReaction(Ff7BattleQueuedCommand command, int targetSlot)
    {
        AdmitImpactModel(new(command.ActorSlot, targetSlot, -1, -1, -1) { ModelAction = Actor(targetSlot).Actor.HurtAction });
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

    private static void ValidateGuestLimitSnapshot(Ff7BattleSnapshot snapshot)
    {
        if (snapshot.GuestLimitReadyAt is long readyAt && (readyAt < 0 || readyAt > snapshot.Tick || snapshot.GuestVitals is null))
        {
            throw new InvalidDataException("The saved guest Limit readiness contradicts the host clock.");
        }
        if (snapshot.Action is not { } action)
        {
            return;
        }
        Ff7BattleGuestLimitState? limit = action.GuestLimit;
        if ((limit is not null) != (action.Command is { GuestAttack: true, CommandId: (byte)Ff7BattleCommand.Limit }))
        {
            throw new InvalidDataException("The saved guest Limit has no matching host action.");
        }
        if (limit is null)
        {
            return;
        }
        int pending = limit.Contacts - limit.PresentedContacts;
        bool invalidPendingHit = limit.PendingHit is { } hit && (hit.Source != action.Command.ActorSlot || hit.Target is < 4 or > 9 || (action.Command.TargetMask & 1 << hit.Target) == 0 || hit.Tick < action.StartedAt || hit.Tick > snapshot.Tick);
        if (limit.Contacts is < 0 or > 9 || limit.PresentedContacts < 0 || pending is < 0 or > 1 || (pending != 0) != (limit.PendingHit is not null) || action.Applied != (limit.Contacts != 0) || limit.TargetCue is not (null or 4 or 6) || limit.KnockedOut && !limit.CounterPending || (limit.PresentationCompleted || limit.CounterPending) && limit.Contacts == 0 || limit.PresentationCompleted && (pending != 0 || limit.TargetCue is not null) || invalidPendingHit)
        {
            throw new InvalidDataException("The saved guest Limit has an invalid contact cursor.");
        }
    }
}
