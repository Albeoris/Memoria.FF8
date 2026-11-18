using static Memoria.FF8.Runtime.BattlePresentationProcess;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private sealed record PreparedActionSeed(BattleSnapshot Snapshot, OwnedBattleDefinition Definition);

    private static PreparedActionSeed CapturePreparedActionSeed(BattleSnapshot snapshot, OwnedBattleDefinition definition)
    {
        definition.RequireIdentity(snapshot.Definition);
        BattleSnapshot captured = NativeBattleStorageSnapshots.CapturePrivate(snapshot with { Definition = null! }) with { Definition = definition.Definition };
        return new(captured, definition);
    }

    private sealed class PreparedActionPublication
    {
        private readonly BattleSession owner;
        private readonly BattleSession candidate;
        private readonly BattleSnapshot state;
        private readonly long frame;
        private readonly BattleActionPresentation? action;
        private readonly ExpandedHostedEvent? hostedEvent;
        private readonly int previousEventCount;
        private readonly BattleEvent? previousLastEvent;
        private readonly BattleEvent[] committedEvents;
        private bool published;
        private bool observationsPublished;

        internal PreparedActionPublication(BattleSession owner, BattleSession candidate, BattleSession preflight, OwnedBattleDefinition? ownedDefinition = null)
        {
            this.owner = owner;
            this.candidate = candidate;
            frame = owner.Frame;
            action = owner.ActiveAction;
            hostedEvent = owner.expandedHostedEvent;
            state = candidate.CaptureState(ownedDefinition);
            previousEventCount = owner.events.Count;
            previousLastEvent = previousEventCount == 0 ? null : owner.events[^1];
            if (preflight.events.Count != previousEventCount || state.Events.Length < previousEventCount)
            {
                throw new InvalidOperationException("A prepared action cannot replace its event history.");
            }
            for (int index = 0; index < previousEventCount; index++)
            {
                if (state.Events[index] != preflight.events[index])
                {
                    throw new InvalidOperationException("A prepared action changed a previously committed event.");
                }
            }
            committedEvents = state.Events.AsSpan(previousEventCount).ToArray();
            preflight.expandedPartyPresentation = owner.expandedPartyPresentation;
            preflight.CopyCandidateState(candidate, state, preparedExpandedAction: true);
        }

        internal void Publish()
        {
            if (published || owner.Frame != frame || !ReferenceEquals(owner.ActiveAction, action) || !ReferenceEquals(owner.expandedHostedEvent, hostedEvent) ||
                !HasOriginalEventBoundary())
            {
                throw new InvalidOperationException("The prepared action was already published or its logical owner changed.");
            }
            owner.CopyCandidateState(candidate, state, preparedExpandedAction: true);
            published = true;
        }

        internal void PublishCommittedObservations()
        {
            if (!published || observationsPublished || !HasOriginalEventBoundary())
            {
                throw new InvalidOperationException("Action observations require their once-committed logical event history.");
            }
            observationsPublished = true;
            owner.events.PublishCommitted(committedEvents);
            if (candidate.preparedResolutionObservations is { } resolutions)
            {
                candidate.preparedResolutionObservations = null;
                foreach (BattleDiagnosticResolution resolution in resolutions)
                {
                    owner.PublishDiagnosticResolution(resolution);
                }
            }
            if (candidate.participantCallbackObservations is { } observations)
            {
                candidate.participantCallbackObservations = null;
                foreach (BattleParticipantCallbackReceipt observation in observations)
                {
                    owner.ObserveDiagnosticParticipantCallback(observation);
                }
            }
        }

        private bool HasOriginalEventBoundary()
        {
            return owner.events.Count == previousEventCount && (previousEventCount == 0 || ReferenceEquals(owner.events[^1], previousLastEvent));
        }
    }

    private BattleSession CreatePreparedHostedActionCandidate(BattleSnapshot snapshot, HostedCandidateProof? proof = null, OwnedBattleDefinition? ownedDefinition = null)
    {
        BattleSession candidate = RestoreSnapshot(snapshot, proof, ownedDefinition);
        candidate.preparingExpandedHostedAction = true;
        candidate.stagingExpandedHostedGuardianForms = true;
        candidate.preparePresentationModelBinding = preparePresentationModelBinding;
        candidate.nativeEffectResourceResolver = nativeEffectResourceResolver;
        candidate.expandedPartyPresentation = expandedPartyPresentation;
        candidate.expandedPartyReadyStatusMirrors = expandedPartyReadyStatusMirrors;
        candidate.nativeAffinityAction = nativeAffinityAction;
        candidate.participantCallbackObservations = ObservingDiagnosticParticipantCallbacks ? [] : null;
        candidate.preparedResolutionObservations = DiagnosticResolution is null ? null : [];
        candidate.diagnosticCounterTracksContacts = ObservingDiagnosticCounter;
        candidate.trackDiagnosticReflections = DiagnosticEffectDispatched is not null;
        if (expandedPresentationProcess is { } process)
        {
            candidate.readHostedAiHeapMessage = process.ReadAiHeapMessage;
        }
        return candidate;
    }

    private void PrepareAndPublishExpandedAction(BattleActionPresentation command)
    {
        BattlePresentationProcess process = expandedPresentationProcess ?? throw new InvalidOperationException("Action preparation requires its attached presentation process.");
        OwnedBattleDefinition definition = new(this.definition);
        PreparedActionSeed before = CapturePreparedActionSeed(CaptureState(definition), definition);
        BattleSession candidate = CreatePreparedHostedActionCandidate(before.Snapshot, ownedDefinition: before.Definition);
        candidate.CommitExpandedAction(command);
        PublishPreparedExpandedActionCandidate(before.Snapshot, candidate, command, process, ownedDefinition: before.Definition);
    }

    private void PublishPreparedExpandedActionCandidate(BattleSnapshot before, BattleSession candidate, BattleActionPresentation command, BattlePresentationProcess process, BattleModelOwner? capturedCompletionOwner = null, OwnedBattleDefinition? ownedDefinition = null)
    {
        PreparedHostedDispatch? admission = null;
        ExpandedHostedEvent? hostedEvent = null;
        PreparedHostedGuardianFormChange? form = PrepareStagedHostedGuardianForm(process, candidate);
        if (candidate.HasStagedExpandedHostedOperations || command.ExpandedAiCallback is not null)
        {
            BattleModelRoster roster = process.PlannedModelRoster;
            uint callback = UsesNativeOrdinaryCompletion(command) ? 0x47DE70u : 0x47DDA0u;
            BattleModelOwner completionOwner = capturedCompletionOwner ?? RequirePresentationModelOwner(roster, GetActor(command.ActorId));
            candidate.StageExpandedHostedCompletion(command, callback, completionOwner, capturedCompletionOwner is not null);
            // Routed ordinary counters can publish only after their source mirror and its actual completion are staged.
            candidate.FlushEncounterCounterCommands();
            ImmutableArray<HostedModelLoadAdmission> allocations = candidate.PrepareExpandedModelLoadAdmissions(process);
            BattleModelRoster admittedRoster = allocations.IsEmpty ? roster : new(roster.Owners.Concat(allocations.Select(plan => plan.Load.ModelOwner)));
            candidate.ValidateExpandedHostedInvocations(admittedRoster);
            ExpandedHostedPrefix prefix = candidate.PrepareExpandedHostedPrefix(process, allocations: allocations, guardianForm: form);
            admission = prefix.Dispatch;
            candidate.BindExpandedHostedPrefix(prefix, admission.Packets.Roster);
            hostedEvent = new(command, candidate.ActiveAction!, callback, admission.Completion?.Request.Token, CompletionOwner: completionOwner);
        }
        PreparedActionPublication publication = new(this, candidate, RestoreSnapshot(before, null, ownedDefinition), ownedDefinition);
        if (admission is not null)
        {
            process.ValidatePreparedDispatch(admission);
        }
        else if (form is not null)
        {
            process.ValidatePreparedGuardianFormChange(form);
        }
        publication.Publish();
        PublishStagedHostedOperations(candidate);
        if (admission is not null)
        {
            expandedHostedEvent = hostedEvent;
            expandedHostedActionToken = admission.Action?.Invocation.Token;
            process.CommitDispatch(admission);
            PublishDiagnosticReflectionCandidates(candidate);
            PublishDiagnosticCounterContacts(candidate, process, admission.AppendedHeaders);
        }
        else if (form is not null)
        {
            process.CommitGuardianFormChange(form);
        }
        publication.PublishCommittedObservations();
        PublishHostedGuardianFormAudio(form);
    }

    private void PublishStagedHostedOperations(BattleSession candidate)
    {
        expandedOperationCursor = candidate.expandedOperationCursor;
        expandedInvocationProgress = candidate.expandedInvocationProgress;
        nativeAffinityAction = candidate.nativeAffinityAction;
    }
}
