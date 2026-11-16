using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private sealed class PreparedActionPublication
    {
        private readonly BattleSession owner;
        private readonly BattleSession candidate;
        private readonly BattleSnapshot state;
        private readonly long frame;
        private readonly BattleActionPresentation? action;
        private readonly ExpandedHostedEvent? hostedEvent;
        private bool published;

        internal PreparedActionPublication(BattleSession owner, BattleSession candidate, BattleSession preflight)
        {
            this.owner = owner;
            this.candidate = candidate;
            frame = owner.Frame;
            action = owner.ActiveAction;
            hostedEvent = owner.expandedHostedEvent;
            state = candidate.CaptureState();
            preflight.expandedPartyPresentation = owner.expandedPartyPresentation;
            preflight.CopyCandidateState(candidate, state, preparedExpandedAction: true);
        }

        internal void Publish()
        {
            if (published || owner.Frame != frame || !ReferenceEquals(owner.ActiveAction, action) || !ReferenceEquals(owner.expandedHostedEvent, hostedEvent))
            {
                throw new InvalidOperationException("The prepared action was already published or its logical owner changed.");
            }
            owner.CopyCandidateState(candidate, state, preparedExpandedAction: true);
            published = true;
        }
    }

    private BattleSession CreatePreparedHostedActionCandidate(BattleSnapshot snapshot, IBattleStatusEffectHandler[] customHandlers, HostedCandidateProof? proof = null)
    {
        BattleSession candidate = RestoreSnapshot(snapshot, customHandlers, proof);
        candidate.preparingExpandedHostedAction = true;
        candidate.stagingExpandedHostedGuardianForms = true;
        candidate.expandedPartyPresentation = expandedPartyPresentation;
        candidate.nativeAffinityAction = nativeAffinityAction;
        if (expandedPresentationProcess is { } process)
        {
            candidate.readHostedAiHeapMessage = process.ReadAiHeapMessage;
        }
        return candidate;
    }

    private void PrepareAndPublishExpandedAction(BattleActionPresentation command)
    {
        ExpandedBattlePresentationProcess process = expandedPresentationProcess ?? throw new InvalidOperationException("Action preparation requires its attached presentation process.");
        BattleSnapshot before = NativeBattleStorageSnapshots.CapturePrivate(CaptureState());
        IBattleStatusEffectHandler[] customHandlers = handlers.Values.Where(handler => handler is not ScaffoldBattleEffectHandler).ToArray();
        BattleSession candidate = CreatePreparedHostedActionCandidate(before, customHandlers);
        candidate.CommitExpandedAction(command);
        PublishPreparedExpandedActionCandidate(before, customHandlers, candidate, command, process);
    }

    private void PublishPreparedExpandedActionCandidate(BattleSnapshot before, IBattleStatusEffectHandler[] customHandlers, BattleSession candidate, BattleActionPresentation command, ExpandedBattlePresentationProcess process, BattleModelOwner? capturedCompletionOwner = null)
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
            BattleModelRoster admittedRoster = allocations.IsEmpty ? roster : new(roster.Owners.Concat(allocations.Select(plan => plan.Load.Owner)));
            candidate.ValidateExpandedHostedInvocations(admittedRoster);
            ExpandedHostedPrefix prefix = candidate.PrepareExpandedHostedPrefix(process, allocations: allocations, guardianForm: form);
            admission = prefix.Dispatch;
            candidate.BindExpandedHostedPrefix(prefix, admission.Packets.Roster);
            hostedEvent = new(command, candidate.ActiveAction!, callback, admission.Completion?.Request.Token, CompletionOwner: completionOwner);
        }
        PreparedActionPublication publication = new(this, candidate, Restore(before, customHandlers));
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
        }
        else if (form is not null)
        {
            process.CommitGuardianFormChange(form);
        }
        PublishHostedGuardianFormAudio(form);
    }

    private void PublishStagedHostedOperations(BattleSession candidate)
    {
        expandedOperationCursor = candidate.expandedOperationCursor;
        expandedInvocationProgress = candidate.expandedInvocationProgress;
        nativeAffinityAction = candidate.nativeAffinityAction;
    }
}
