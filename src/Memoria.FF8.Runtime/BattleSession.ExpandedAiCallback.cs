namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private sealed record ExpandedAiInvocationReservation(BattleModelOwner Source, BattleActionPresentation Command);
    private bool producingExpandedAiCallback;
    private ExpandedAiInvocationReservation? producingExpandedAiInvocation;

    private void QueueExpandedAiCallback(BattleCombatant actor, int stage)
    {
        BattleExpandedAiCallbackRequest request = new(TargetActorReference(actor), checked((byte)stage));
        BattleAttackCommand packet = ExpandedAiCallbackPacket(request);
        if (!CanDispatchExpandedAiCallback(ValidateExpandedAiCallback(packet, request), request))
        {
            return;
        }
        StopTurnAdvance();
        actor.IsBusy = true;
        actions.Enqueue(packet);
    }

    private static BattleAttackCommand ExpandedAiCallbackPacket(BattleExpandedAiCallbackRequest request)
    {
        return new(request.Actor.ActorId, request.Actor.ActorId, BattleTargetRequest.None, BattleAbility.EnemyAbility) { ExpandedAiCallback = request };
    }

    private static BattleActionPresentation ExpandedAiCallbackPresentation(BattleExpandedAiCallbackRequest request)
    {
        return new(request.Actor.ActorId, request.Actor.ActorId, BattleActionPhase.Recovery, 1, BattleTargetRequest.None, BattleAbility.EnemyAbility) { ExpandedAiCallback = request };
    }

    private BattleCombatant ValidateExpandedAiCallback(BattleAttackCommand packet, BattleExpandedAiCallbackRequest request)
    {
        if (!UsesExpandedRoster || request.Stage > 3 || request.Actor is null || FindTargetActor(request.Actor) is not { NativeAi: not null } actor ||
            !packet.MagicCasts.IsEmpty || (packet with { MagicCasts = [] }) != ExpandedAiCallbackPacket(request))
        {
            throw new InvalidDataException("An expanded AI callback must identify its exact allocated actor and native stage.");
        }
        return actor;
    }

    private BattleCombatant ValidateExpandedAiCallbackPresentation(BattleActionPresentation action, BattleExpandedAiCallbackRequest request)
    {
        if (expandedHostedEvent?.Command.ExpandedAiCallback != request || !action.MagicCasts.IsEmpty ||
            (action with { MagicCasts = [] }) != ExpandedAiCallbackPresentation(request))
        {
            throw new InvalidDataException("An expanded AI callback recovery requires its actual hosted completion owner.");
        }
        return ValidateExpandedAiCallback(ExpandedAiCallbackPacket(request), request);
    }

    private static bool CanDispatchExpandedAiCallback(BattleCombatant actor, BattleExpandedAiCallbackRequest request)
    {
        return actor.IsActive && actor.Slot is not null && (request.Stage switch
        {
            0 => !actor.Suspended,
            1 => CanNativeBattleActorAct(actor),
            2 => (NativeSavedStatusBits(actor) & 1) == 0,
            3 => true,
            _ => false
        });
    }

    private void DispatchExpandedAiCallback(BattleAttackCommand packet, BattleExpandedAiCallbackRequest request)
    {
        BattleCombatant actor = ValidateExpandedAiCallback(packet, request);
        if (!CanDispatchExpandedAiCallback(actor, request))
        {
            actions.Dequeue();
            return;
        }
        if (expandedPresentationProcess is not { } process)
        {
            InterpretExpandedAiCallback(packet, request, actor);
            return;
        }

        BattleSnapshot before = Capture();
        IBattleStatusEffectHandler[] customHandlers = handlers.Values.Where(handler => handler is not ScaffoldBattleEffectHandler).ToArray();
        BattleModelOwner completionOwner = RequirePresentationModelOwner(process.Graphics.ActorModels.Roster, actor);
        BattleSession candidate = Restore(before, customHandlers);
        candidate.preparingExpandedHostedAction = true;
        candidate.expandedPartyPresentation = expandedPartyPresentation;
        candidate.nativeAffinityAction = nativeAffinityAction;
        BattleActionPresentation mirror = ExpandedAiCallbackPresentation(request);
        candidate.ActiveAction = mirror;
        candidate.BeginNativeBattleActionPause();
        candidate.BeginNativeReactionBatch();
        candidate.nativeActionScratch = candidate.nativeActionScratch with { EventLane = request.Stage == 1 ? (byte)2 : (byte)0 };
        candidate.producingExpandedAiCallback = true;
        try
        {
            candidate.InterpretExpandedAiCallback(packet, request, candidate.GetActor(actor.Id));
        }
        finally
        {
            candidate.producingExpandedAiCallback = false;
        }
        PublishPreparedExpandedActionCandidate(before, customHandlers, candidate, mirror, process, completionOwner);
    }

    private void InterpretExpandedAiCallback(BattleAttackCommand packet, BattleExpandedAiCallbackRequest request, BattleCombatant actor)
    {
        if (!actions.TryPeek(out BattleAttackCommand? head) || head != packet)
        {
            throw new InvalidDataException("The current FIFO head does not identify the admitted expanded AI callback.");
        }
        actions.Dequeue();
        BattleAttackCommand[] tail = actions.ToArray();
        actions.Clear();
        try
        {
            if (request.Stage == 3)
            {
                RunExpandedEnemyDeathAi(actor);
            }
            else
            {
                RunExpandedNativeAi(actor, request.Stage);
            }
        }
        finally
        {
            foreach (BattleAttackCommand pending in tail)
            {
                actions.Enqueue(pending);
            }
        }
    }

    private void RequireExpandedAiLifecycleOperation(string operation)
    {
        if (producingExpandedAiCallback)
        {
            // TODO(TD-009): Emit the original lifecycle descriptors within the same hosted AI event.
            throw new NotSupportedException($"Hosted AI callback requires its native {operation} descriptors.");
        }
    }

    private void ProduceExpandedAiCallbackCommand(BattleAttackCommand packet)
    {
        BattleActionPresentation command = new(packet.ActorId, packet.TargetId, BattleActionPhase.Recovery, 1, packet.TargetRequest, packet.Ability, packet.ActionId)
        {
            NativeQueuedCommand = packet.NativeQueuedCommand,
            NativeAction = packet.NativeAction,
            NativeGraphic = packet.NativeGraphic,
            NativeAiCallbackStage = packet.NativeAiCallbackStage,
            NativeSequence = packet.NativeSequence
        };
        BattleActionPresentation? outer = resolvingNativeAction;
        bool outerImpacts = deferringExpandedImpacts;
        int? outerRecoverSourceHp = expandedRecoverSourceHp;
        ExpandedAiInvocationReservation? outerReservation = producingExpandedAiInvocation;
        producingExpandedAiInvocation = null;
        try
        {
            CommitExpandedAction(command);
        }
        finally
        {
            resolvingNativeAction = outer;
            deferringExpandedImpacts = outerImpacts;
            expandedRecoverSourceHp = outerRecoverSourceHp;
            producingExpandedAiInvocation = outerReservation;
        }
        FlushEncounterCounterCommands();
    }
}
