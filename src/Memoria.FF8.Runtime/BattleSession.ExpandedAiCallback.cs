using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

internal enum BattleParticipantCallbackPhase
{
    Queued,
    Started,
    Completed
}

internal sealed record BattleParticipantCallbackReceipt(BattleExpandedAiCallbackRequest Request, BattleParticipantCallbackPhase Phase, ImmutableArray<BattleActionHeaderReference> Headers, NativeBattlePendingReaction? Reaction = null, string? ScriptSha256 = null)
{
    internal BattleDiagnosticPlayerStage2NoOp? NoOp { get; init; }
    internal NativeBattlePresentationCompletionRequested? Completion { get; init; }
}

public sealed partial class BattleSession
{
    internal sealed record ExpandedAiInvocationReservation(BattleModelOwner Source, BattleActionPresentation Command);
    private bool producingExpandedAiCallback;
    private ExpandedAiInvocationReservation? producingExpandedAiInvocation;
    private List<BattleParticipantCallbackReceipt>? participantCallbackObservations;

    private void QueueExpandedAiCallback(BattleCombatant actor, int stage, NativeBattlePendingReaction? reaction = null)
    {
        BattleExpandedAiCallbackRequest request = new(TargetActorReference(actor), checked((byte)stage));
        BattleAttackCommand packet = ExpandedAiCallbackPacket(request);
        if (!CanDispatchExpandedAiCallback(ValidateExpandedAiCallback(packet, request), request))
        {
            return;
        }
        // A tracked enemy can complete its ordinary turn without producing an action.
        if (stage != 1 || turnAdvance?.EnemyActorIds.Contains(actor.Id) != true)
        {
            StopTurnAdvance();
        }
        actor.IsBusy = true;
        actions.Enqueue(packet);
        if (participantCallbackObservations is { } staged)
        {
            staged.Add(new(request, BattleParticipantCallbackPhase.Queued, [], reaction));
        }
        else if (ObservingDiagnosticParticipantCallbacks)
        {
            ObserveDiagnosticParticipantCallback(new(request, BattleParticipantCallbackPhase.Queued, [], reaction));
        }
    }

    private static BattleAttackCommand ExpandedAiCallbackPacket(BattleExpandedAiCallbackRequest request)
    {
        return new(request.Actor.ActorId, request.Actor.ActorId, BattleTargetRequest.None, BattleAbility.EnemyAbility) { ExpandedAiCallback = request };
    }

    private static BattleActionPresentation ExpandedAiCallbackPresentation(BattleExpandedAiCallbackRequest request)
    {
        return new(request.Actor.ActorId, request.Actor.ActorId, BattleActionPhase.Recovery, BattleTargetRequest.None, BattleAbility.EnemyAbility) { ExpandedAiCallback = request };
    }

    private BattleCombatant ValidateExpandedAiCallback(BattleAttackCommand packet, BattleExpandedAiCallbackRequest request)
    {
        if (request.Stage is > 3 and not 6 || request.Actor is null || FindTargetActor(request.Actor) is not { } actor ||
            request.Stage != 6 && (actor.IsPlayer ? request.Stage is not (2 or 3) : actor.NativeAi is null) ||
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
            2 or 6 => (NativeSavedStatusBits(actor) & 1) == 0,
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
        BattlePresentationProcess process = Presentation ?? throw new InvalidOperationException("AI callback requires its presentation process.");

        BattleSnapshot before = Capture();
        BattleModelOwner completionOwner = RequirePresentationModelOwner(process.Graphics.ActorModels.Roster, actor);
        BattleSession candidate = CreatePreparedHostedActionCandidate(before);
        PrepareDiagnosticCounterCandidate(candidate, request);
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
        PublishPreparedExpandedActionCandidate(before, candidate, mirror, process, completionOwner);
        if (ObservingDiagnosticParticipantCallbacks)
        {
            ImmutableArray<BattleActionHeaderReference> headers = expandedOperationCursor.Operations.OfType<ExpandedHostedInvocation>()
                .Select(invocation => invocation.HeaderReference).OfType<BattleActionHeaderReference>().ToImmutableArray();
            BattleParticipantCallbackReceipt admitted = new(request, BattleParticipantCallbackPhase.Started, headers)
            {
                NoOp = CaptureDiagnosticPlayerStage2NoOp(candidate, request, completionOwner, headers)
            };
            ObserveDiagnosticParticipantCallback(admitted);
            PublishDiagnosticCounterCandidate(candidate, admitted);
        }
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
            if (request.Stage == 6)
            {
                //4877F0 dispatches command0/action4 to the same still-eligible participant.
                StageExpandedHostedInternalContact(actor, 4);
            }
            else if (actor.IsPlayer)
            {
                InterpretNativePlayerReaction(actor, request.Stage);
            }
            else if (request.Stage == 3)
            {
                RunExpandedEnemyDeathAi(actor);
            }
            else
            {
                RunNativeAi(actor, request.Stage);
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



    private void ProduceExpandedAiCallbackCommand(BattleAttackCommand packet)
    {
        BattleActionPresentation command = NativeCommandPresentation(packet) with { Phase = BattleActionPhase.Recovery };
        BattleActionPresentation? outer = resolvingNativeAction;
        bool outerImpacts = deferringExpandedImpacts;
        int? outerRecoverSourceHp = expandedRecoverSourceHp;
        ExpandedAiInvocationReservation? outerReservation = producingExpandedAiInvocation;
        producingExpandedAiInvocation = null;
        try
        {
            CommitExpandedAction(command);
            if (producingExpandedAiInvocation is { } admitted && IsStagedExpandedAiInvocation(admitted))
            {
                PrepareNativeAiReturnDamage(GetActor(packet.ActorId), packet);
            }
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
