using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private void CompleteExpandedHostedEvent(NativeBattlePresentationCompletionRequested receipt, long? graphicsToken)
    {
        ExpandedBattlePresentationProcess process = expandedPresentationProcess ?? throw new InvalidOperationException("Hosted completion lost its presentation process.");
        BattleModelRoster roster = process.Graphics.ActorModels.Roster;
        if (expandedHostedEvent is not { } hostedEvent || hostedEvent.CompletionToken != receipt.Token ||
            hostedEvent.Callback != receipt.Callback || hostedEvent.Command.ActorId != receipt.ActorId ||
            !ReferenceEquals(hostedEvent.ActivePresentation, ActiveAction) ||
            RequirePresentationModelOwner(roster, GetActor(receipt.ActorId)) != receipt.Owner ||
            !ReferenceEquals(process.PendingCompletion, receipt) || graphicsToken is long frameToken && frameToken != expandedHostedActionToken)
        {
            throw new InvalidDataException("Native completion does not belong to the current hosted event.");
        }
        if (expandedHostedActionToken is long token)
        {
            if (!process.IsActionComplete(token) || expandedHostedInvocations is not [{ PendingGunblade: false }])
            {
                throw new InvalidDataException("Hosted completion requires its retired final action.");
            }
            ValidateExpandedEffectContactCompletion();
        }
        else if (!expandedHostedInvocations.IsEmpty)
        {
            throw new InvalidDataException("A completion-only event cannot own outstanding contacts.");
        }

        BattleSnapshot before = CaptureState();
        if (!ReferenceEquals(before.ActiveAction, hostedEvent.ActivePresentation))
        {
            throw new InvalidDataException("Hosted completion captured a different logical action from its validated live event.");
        }
        BattleCombatant liveActor = GetActor(receipt.ActorId);
        bool retainCommittedMirror = !CanRetainBattleActionActor(liveActor, hostedEvent.ActivePresentation.NativePlayerReaction, hostedEvent.ActivePresentation.NativeAiCallbackStage, hostedEvent.ActivePresentation.TargetRequest);
        BattleSnapshot seed = NativeBattleStorageSnapshots.Capture(retainCommittedMirror ? before with { ActiveAction = null } : before);
        HostedEventCompletionCandidateProof proof = new(seed, hostedEvent, receipt);
        IBattleStatusEffectHandler[] customHandlers = handlers.Values.Where(handler => handler is not ScaffoldBattleEffectHandler).ToArray();
        BattleSession candidate = RestoreSnapshot(seed, customHandlers, proof);
        candidate.preparingExpandedHostedAction = true;
        candidate.expandedPartyPresentation = expandedPartyPresentation;
        BattleCombatant actor = candidate.GetActor(receipt.ActorId);
        if (retainCommittedMirror)
        {
            candidate.ActiveAction = hostedEvent.ActivePresentation;
            actor.IsBusy = liveActor.IsBusy;
        }
        if (receipt.Callback == 0x47DE70)
        {
            candidate.PrepareExpandedHostedAftercare(actor, hostedEvent.Command);
        }

        bool hasAftercare = !candidate.expandedHostedInvocations.IsEmpty;
        PreparedHostedCompletionAck ack = process.PrepareCompletionAck(receipt.Token, resetCounts: !hasAftercare);
        PreparedHostedDispatch dispatch;
        ExpandedHostedEvent? nextEvent = null;
        if (hasAftercare)
        {
            candidate.ValidateExpandedHostedInvocations(roster);
            dispatch = candidate.PrepareExpandedHostedEventSegment(process, roster, hostedEvent.Command, 0x47E120, ack);
            ExpandedHostedInvocation first = candidate.expandedHostedInvocations[0];
            candidate.BindExpandedHostedContactTracking(first, roster, PrepareExpandedHostedContactBridge(first, roster));
            nextEvent = hostedEvent with { ActivePresentation = candidate.ActiveAction!, Callback = 0x47E120, CompletionToken = dispatch.Completion?.Request.Token };
        }
        else
        {
            candidate.FlushExpandedEnemyReactions();
            foreach (BattleCombatant player in candidate.allies.Where(player => player.IsActive && player.Slot is not null))
            {
                candidate.ReprojectNativePlayerStatistics(player);
            }
            candidate.RefreshNativeKnownMagicStocks();
            candidate.RefreshNativeEnemyDrawSlots();
            candidate.CompleteExpandedHostedActionPause();
            if (receipt.Callback is 0x47DE70 or 0x47E120)
            {
                candidate.CompleteExpandedHostedEventGauge(hostedEvent.Command);
            }
            candidate.CompleteActionPresentation(hostedEvent.ActivePresentation, "Expanded action completed.");
            dispatch = process.PrepareDispatch([], acknowledgment: ack);
        }

        BattleSnapshot prepared = candidate.CaptureState();
        BattleSession preflight = RestoreSnapshot(seed, customHandlers, proof);
        preflight.expandedPartyPresentation = expandedPartyPresentation;
        preflight.CopyCandidateState(candidate, prepared, preparedExpandedAction: true);
        preflight.ValidatePreparedExpandedPresentation(candidate);
        process.ValidatePreparedDispatch(dispatch);
        CopyCandidateState(candidate, prepared, preparedExpandedAction: true);
        TakeExpandedActionPresentation(candidate);
        expandedHostedEvent = nextEvent;
        expandedHostedActionToken = dispatch.Action?.Invocation.Token;
        process.CommitDispatch(dispatch);
    }

    private void PrepareExpandedHostedAftercare(BattleCombatant actor, BattleActionPresentation original)
    {
        nativeActionCommitDepth++;
        deferringExpandedImpacts = true;
        try
        {
            if ((NativeSavedStatusBits(actor) & 1) == 0 && (original.NativeQueuedCommand ?? original.NativeAction?.Command) is 28 or 243)
            {
                StageExpandedHostedAftercare(actor, 6);
            }
            if ((NativeSavedStatusBits(actor) & 3) == 2)
            {
                StageExpandedHostedAftercare(actor, 2);
            }
        }
        finally
        {
            resolvingNativeAction = null;
            deferringExpandedImpacts = false;
            nativeActionCommitDepth--;
        }
    }

    private void StageExpandedHostedAftercare(BattleCombatant actor, ushort id)
    {
        BattleNativeAction native = NativePostActionEffect(id);
        BattleActionPresentation command = new(actor.Id, actor.Id, BattleActionPhase.Recovery, 1, SelectBattleTarget(actor), BattleAbility.EnemyAbility, id) { NativeQueuedCommand = 0, NativeAction = native, NativeSequence = 0 };
        BattleActionDefinition definition = new(command.Ability, id, native.Name!, BattleActionEffect.Native, 0, true) { NativeAction = native };
        NativeBattleActionHeaderPreparation header = BuildExpandedActionHeaderPreparation(command, native);
        _ = RequirePresentationModelOwner(ReadBattlePresentationRoster(), actor);
        nativeActionScratch = nativeActionScratch with { Command = 0, Action = id, DeferredEffectsE00 = false };
        resolvingNativeAction = command;
        actor.NativeRemainingHits = 1;
        NativeBattleContactPacket contact = ResolveInstalledNativeHitWithContact(actor, actor, command, definition, native);
        ImmutableArray<ExpandedBattleContact> contacts = [new(actor.Id, contact)];
        StageExpandedHostedInvocation(command, header, contacts, TakePendingHostedEnemyDraw());
    }
}
