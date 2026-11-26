using static Memoria.FF8.Runtime.BattlePresentationProcess;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private void CompleteExpandedHostedEvent(NativeBattlePresentationCompletionRequested receipt, long? graphicsToken)
    {
        BattlePresentationProcess process = expandedPresentationProcess ?? throw new InvalidOperationException("Hosted completion lost its presentation process.");
        BattleModelRoster roster = process.PlannedModelRoster;
        if (expandedHostedEvent is not { } hostedEvent || hostedEvent.CompletionToken != receipt.Token ||
            hostedEvent.Callback != receipt.Callback || hostedEvent.Command.ActorId != receipt.ActorId ||
            !ReferenceEquals(hostedEvent.ActivePresentation, ActiveAction) ||
            hostedEvent.CompletionOwner != receipt.Owner || CreatePresentationModelOwner(GetActor(receipt.ActorId)) != receipt.Owner ||
            !ReferenceEquals(process.PendingCompletion, receipt) || graphicsToken is long frameToken && frameToken != expandedHostedActionToken)
        {
            throw new InvalidDataException("Native completion does not belong to the current hosted event.");
        }
        if (expandedHostedActionToken is long token)
        {
            if (!process.IsActionComplete(token) || CurrentExpandedHostedInvocation is not { PendingGunblade: false, PendingGuardian: false })
            {
                throw new InvalidDataException("Hosted completion requires its retired final action.");
            }
            ValidateExpandedEffectContactCompletion();
            ValidateExpandedHostedRenzokukenInitialRetirement(hostedEvent);
        }
        else if (CurrentExpandedHostedInvocation is not null)
        {
            throw new InvalidDataException("A completion-only event cannot own outstanding contacts.");
        }

        ObserveDiagnosticHostedCompletionBeforeAck(process, receipt);
        OwnedBattleDefinition definition = new(this.definition);
        BattleSnapshot before = CaptureState(definition);
        if (!ReferenceEquals(before.ActiveAction, hostedEvent.ActivePresentation))
        {
            throw new InvalidDataException("Hosted completion captured a different logical action from its validated live event.");
        }
        BattleCombatant liveActor = GetActor(receipt.ActorId);
        bool retainCommittedMirror = hostedEvent.ActivePresentation.ExpandedAiCallback is null &&
            !CanRetainBattleActionActor(liveActor, hostedEvent.ActivePresentation.NativePlayerReaction, hostedEvent.ActivePresentation.NativeAiCallbackStage, hostedEvent.ActivePresentation.TargetRequest);
        PreparedActionSeed seed = CapturePreparedActionSeed(retainCommittedMirror ? before with { ActiveAction = null } : before, definition);
        HostedEventCompletionCandidateProof proof = new(seed.Snapshot, hostedEvent, receipt);
        BattleSession candidate = CreatePreparedHostedActionCandidate(seed.Snapshot, proof, seed.Definition);
        if (hostedEvent.Command.NativeDuel is not null || hostedEvent.Renzokuken is not null)
        {
            // A timed Limit retains this exact owner while the native aftercare suffix is pending.
            candidate.expandedHostedEvent = hostedEvent;
        }
        candidate.expandedOperationCursor = expandedOperationCursor with { Current = null };
        int beforeAftercare = expandedOperationCursor.Operations.Length;
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

        bool hasAftercare = candidate.expandedOperationCursor.Operations.Length != beforeAftercare;
        PreparedHostedCompletionAck ack = process.PrepareCompletionAck(receipt.Token, resetCounts: !hasAftercare);
        ExpandedHostedEvent nextEvent = hostedEvent with { CompletionToken = null };
        if (hasAftercare)
        {
            candidate.ValidateExpandedHostedInvocations(roster);
            candidate.StageExpandedHostedCompletion(hostedEvent.Command, 0x47E120, receipt.Owner, hostedEvent.Command.ExpandedAiCallback is not null);
            nextEvent = nextEvent with { Callback = 0x47E120 };
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
            candidate.CompleteExpandedHostedDuelClosing(hostedEvent);
            candidate.CompleteExpandedHostedRenzokuken(hostedEvent);
            candidate.CompleteActionPresentation(hostedEvent.ActivePresentation, "Expanded action completed.");
            if (candidate.encounterPolicies?.PendingSummon is not null && candidate.expandedOperationCursor.Operations.OfType<ExpandedHostedInvocation>().SingleOrDefault(invocation => invocation.Command.NativeAction?.PresentationOnly == true) is { } summon)
            {
                candidate.CompleteEncounterPolicyPresentation(summon.Command);
            }
            nextEvent = nextEvent with { LogicallyComplete = true };
            if (candidate.expandedOperationCursor.Next < candidate.expandedOperationCursor.Operations.Length || !process.IsControlPrefixRetired)
            {
                candidate.ActiveAction = hostedEvent.ActivePresentation;
            }
        }
        PreparedHostedGuardianFormChange? form = PrepareStagedHostedGuardianForm(process, candidate);
        ImmutableArray<HostedModelLoadAdmission> allocations = candidate.PrepareExpandedModelLoadAdmissions(process, beforeAftercare);
        candidate.PrepareHostedEncounterBatch(process, allocations);
        ExpandedHostedPrefix prefix = candidate.PrepareExpandedHostedPrefix(process, ack,
            acknowledgeOnly: !process.IsControlPrefixRetired || candidate.expandedOperationCursor.Next == candidate.expandedOperationCursor.Operations.Length, allocations: allocations, guardianForm: form);
        PreparedHostedDispatch dispatch = prefix.Dispatch;
        candidate.BindExpandedHostedPrefix(prefix, dispatch.Packets.Roster);
        nextEvent = nextEvent with { CompletionToken = dispatch.Completion?.Request.Token };

        PreparedActionPublication publication = new(this, candidate, RestoreSnapshot(seed.Snapshot, proof, seed.Definition), seed.Definition);
        process.ValidatePreparedDispatch(dispatch);
        HostedEncounterLoadCheckpoint? encounterLoad = process.PrepareEncounterLoad(candidate.EncounterAllocation, dispatch);
        publication.Publish();
        PublishStagedHostedOperations(candidate);
        expandedHostedEvent = nextEvent;
        expandedHostedActionToken = dispatch.Action?.Invocation.Token;
        process.CommitDispatch(dispatch, encounterLoad);
        PublishDiagnosticReflectionCandidates(candidate);
        PublishDiagnosticCounterContacts(candidate, process, dispatch.AppendedHeaders);
        publication.PublishCommittedObservations();
        ObserveDiagnosticHostedCompletionAcknowledged(process, receipt);
        if (ObservingDiagnosticParticipantCallbacks && nextEvent.LogicallyComplete && hostedEvent.Command.ExpandedAiCallback is { } participant)
        {
            ObserveDiagnosticParticipantCallback(new(participant, BattleParticipantCallbackPhase.Completed, []) { Completion = receipt });
        }
        PublishHostedGuardianFormAudio(form);
    }

    private void PrepareExpandedHostedAftercare(BattleCombatant actor, BattleActionPresentation original)
    {
        byte? actionCommand = original.ExpandedAiCallback is not null ? nativeActionScratch.Command : original.NativeQueuedCommand ?? original.NativeAction?.Command;
        // A form can defeat its own GF by targeting itself. Its Darkside cost belongs to that GF's HP pool.
        if ((NativeSavedStatusBits(actor) & 1) == 0 && actionCommand is 28 or 243 && (!original.HasGuardianFormAction || actor.GuardianFormVitals is not null))
        {
            StageExpandedHostedInternalContact(actor, 6);
        }
        if ((NativeSavedStatusBits(actor) & 3) == 2)
        {
            StageExpandedHostedInternalContact(actor, 2);
        }
    }

    private void StageExpandedHostedInternalContact(BattleCombatant actor, ushort id)
    {
        BattleNativeAction native = NativePostActionEffect(id);
        BattleActionPresentation command = new(actor.Id, actor.Id, BattleActionPhase.Recovery, SelectBattleTarget(actor), BattleAbility.EnemyAbility, id) { NativeQueuedCommand = 0, NativeAction = native, NativeSequence = 0 };
        BattleActionDefinition definition = new(command.Ability, id, native.Name!, BattleActionEffect.Native, 0, true) { NativeAction = native };
        NativeBattleActionHeaderPreparation header = BuildExpandedActionHeaderPreparation(command, native);
        if (producingExpandedAiCallback)
        {
            ReserveExpandedAiInvocation(command, header);
        }
        nativeActionScratch = nativeActionScratch with { Command = 0, Action = id, DeferredEffectsE00 = false };
        nativeActionCommitDepth++;
        deferringExpandedImpacts = true;
        resolvingNativeAction = command;
        try
        {
            actor.NativeRemainingHits = 1;
            NativeBattleContactPacket contact = ResolveInstalledNativeHitWithContact(actor, actor, command, definition, native);
            ImmutableArray<ExpandedBattleContact> contacts = [new(actor.Id, contact)];
            StageExpandedHostedInvocation(command, header, contacts, TakePendingHostedEnemyDraw());
        }
        finally
        {
            resolvingNativeAction = null;
            deferringExpandedImpacts = false;
            nativeActionCommitDepth--;
        }
    }
}
