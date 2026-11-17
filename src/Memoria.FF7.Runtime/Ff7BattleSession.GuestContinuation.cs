using System.Collections.Immutable;

namespace Memoria.FF7.Runtime;

/// <summary>A source-selected child action with recipients retained by its already admitted parent.</summary>
public sealed record Ff7BattleGuestContinuation(ushort Ability, ushort Targets);

public sealed record Ff7BattleGuestDeferredResponse(int Target, bool CounterPending, bool KnockedOut, bool Physical);

public sealed record Ff7BattleGuestContinuationState(Ff7BattleQueuedCommand Origin, int CompletedContacts, ImmutableArray<Ff7BattleGuestDeferredResponse> Responses)
{
    public bool ResponsesPublished { get; init; }
}

public sealed partial class Ff7BattleSession
{
    private void DispatchGuestContinuation(Ff7BattleActionState parent, IFf7BattleGuestAttack guest, Ff7BattleGuestContinuation continuation)
    {
        if (guest is not IFf7BattleGuestCompositeActions composite || continuation.Ability == 0 || continuation.Targets == 0 || (continuation.Targets & ~0x3F7) != 0 || parent.GuestLimit is not { PendingHit: null, TargetCue: null } limit || limit.PresentedContacts != limit.Contacts)
        {
            throw new InvalidDataException("The selected continuation has no completed parent contact ownership.");
        }
        Ff7BattleGuestCompositeAction program = composite.DescribeCompositeAction(continuation.Ability) ?? throw new InvalidDataException("The selected continuation has no source contact schedule.");
        int originalTarget = parent.Command.GuestPrimaryTarget ?? System.Numerics.BitOperations.TrailingZeroCount((uint)parent.Command.TargetMask);
        ImmutableArray<Ff7BattleGuestDeferredResponse> responses = [new(originalTarget, limit.CounterPending, limit.KnockedOut, true)];
        Ff7BattleGuestContinuationState retained = new(parent.Command, limit.Contacts, responses);
        Ff7BattleQueuedCommand command = parent.Command with { CommandId = (byte)Ff7BattleCommand.Magic, Argument = continuation.Ability, GuestAbility = continuation.Ability, TargetMask = continuation.Targets, GroupTarget = System.Numerics.BitOperations.PopCount((uint)continuation.Targets) > 1, GuestPrimaryTarget = System.Numerics.BitOperations.TrailingZeroCount((uint)continuation.Targets) };
        DispatchGuestCompositeAction(command, guest, composite, program, GuestInvocation(command, Tick), continuation: retained);
        if (state.Action?.GuestContinuation != retained)
        {
            throw new InvalidDataException($"The admitted source continuation failed to acquire its original host turn: {GuestAbilityFailure?.Reason ?? "missing action ownership"}.");
        }
    }

    private void RecordGuestContinuationContact(int target, bool counter, bool knockout, bool physical)
    {
        Ff7BattleActionState active = state.Action ?? throw new InvalidDataException("The continuation lost its current host action.");
        Ff7BattleGuestContinuationState continuation = active.GuestContinuation ?? throw new InvalidDataException("The continuation lost its deferred host responses.");
        int index = -1;
        for (int row = 0; row < continuation.Responses.Length; row++)
        {
            if (continuation.Responses[row].Target == target)
            {
                index = row;
                break;
            }
        }
        ImmutableArray<Ff7BattleGuestDeferredResponse> responses = continuation.Responses;
        Ff7BattleGuestDeferredResponse next = index < 0 ? new(target, counter, knockout, physical) : responses[index] with { CounterPending = responses[index].CounterPending || counter, KnockedOut = responses[index].KnockedOut || knockout, Physical = responses[index].Physical || physical };
        responses = index < 0 ? responses.Add(next) : responses.SetItem(index, next);
        state = state with { Action = active with { GuestContinuation = continuation with { Responses = responses } } };
    }

    private void CompleteGuestContinuation(Ff7BattleActionState active)
    {
        if (active.GuestContinuation is not { } continuation)
        {
            return;
        }
        foreach (Ff7BattleGuestDeferredResponse response in continuation.Responses)
        {
            if (response.CounterPending)
            {
                PublishActorCounters(continuation.Origin, Actor(response.Target).Actor, response.KnockedOut, response.Physical);
            }
            if (response.KnockedOut)
            {
                short selector = Actor(response.Target).Actor.HurtAction;
                AdmitImpactModel(new(continuation.Origin.ActorSlot, response.Target, -1, -1, -1) { ModelAction = selector, PostActionKnockedOut = true });
            }
        }
    }

    private void CompleteGuestContinuationWhenReady(Ff7BattleActionState active)
    {
        if (!NativeActorModelsReady || HasPrimaryPresentationCallbacks)
        {
            state = state with { Action = active };
            return;
        }
        CompleteGuestMagic(active.Command);
        state = state with { Action = null };
        CompleteEconomyStatusPhase();
        FlushFormationAi();
    }
}
