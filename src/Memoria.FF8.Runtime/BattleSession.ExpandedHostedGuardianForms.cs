namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private bool stagingExpandedHostedGuardianForms;
    private HostedGuardianFormVisualChange? stagedExpandedHostedGuardianForm;
    private bool HasExpandedHostedGuardianFormEntry => (expandedPresentationProcess is not null || stagingExpandedHostedGuardianForms) &&
        guardianForms?.Values.Any(form => form.Phase == BattleGuardianFormPhase.Entering) == true;

    private HostedGuardianFormVisualOwner GuardianFormVisualOwner(BattleCombatant actor, long invocation, int guardianForceId)
    {
        return new(CreatePresentationModelOwner(actor) as BattlePartyActorModel ?? throw new InvalidDataException("GF form requires its current party owner."), actor.Id, invocation, guardianForceId);
    }

    private HostedGuardianFormFrameFacts CaptureExpandedHostedGuardianFormFacts()
    {
        bool hasAction = ActiveAction is not null || resolvingNativeAction is not null || expandedHostedEvent is not null;
        if (guardianForms?.Values.SingleOrDefault() is not { Phase: not BattleGuardianFormPhase.Charging } form)
        {
            return new(null, null, hasAction, false);
        }
        HostedGuardianFormVisualOwner owner = GuardianFormVisualOwner(GetActor(form.ActorId), form.InvocationId, form.GuardianForceId);
        bool finishing = expandedHostedEvent is { LogicallyComplete: false, Command.GuardianFormInvocation: { Mode: GuardianFormInvocationMode.FinisherWithoutEntry } invocation } hosted &&
            hosted.Command.ActorId == form.ActorId && invocation.Id == form.InvocationId && invocation.GuardianForceId == form.GuardianForceId &&
            ReferenceEquals(hosted.ActivePresentation, ActiveAction);
        return new(owner, form.Phase, hasAction, finishing);
    }

    private void BeginExpandedHostedGuardianFormEntry(ExpandedBattlePresentationProcess process, BattleCombatant actor, BattleGuardianForceCast cast)
    {
        if (GetGuardianForm(actor.Id) is not { Phase: BattleGuardianFormPhase.Charging } form ||
            cast.Command.GuardianFormInvocation is not { Mode: GuardianFormInvocationMode.EntryOnly } invocation || invocation.Id != form.InvocationId ||
            invocation.GuardianForceId != form.GuardianForceId || !ReferenceEquals(guardianForceCasts.GetValueOrDefault(actor.Id), cast) || expandedHostedEvent is not null)
        {
            throw new InvalidDataException("Hosted form entry requires its exact completed charge and logical invocation.");
        }
        HostedGuardianFormVisualOwner owner = GuardianFormVisualOwner(actor, form.InvocationId, form.GuardianForceId);
        BattleSnapshot before = Capture();
        BattleSession candidate = CreateExpandedHostedFormCandidate(before);
        candidate.ReleaseGuardianForceCharge(candidate.GetActor(actor.Id));
        candidate.guardianForms![actor.Id] = form with { Phase = BattleGuardianFormPhase.Entering };
        candidate.BeginNativeBattleActionPause();
        PreparedHostedGuardianFormChange visual = process.PrepareGuardianFormChange(new(owner, Entering: true, candidate.CaptureExpandedHostedGuardianFormFacts()));
        PublishExpandedHostedFormCandidate(process, before, candidate, visual);
    }

    private void StageExpandedHostedGuardianFormReturn(BattleCombatant actor, BattleGuardianFormState form)
    {
        if (stagedExpandedHostedGuardianForm is not null)
        {
            throw new InvalidDataException("A hosted form cannot stage two visual returns.");
        }
        stagedExpandedHostedGuardianForm = new(GuardianFormVisualOwner(actor, form.InvocationId, form.GuardianForceId), Entering: false, CaptureExpandedHostedGuardianFormFacts());
    }

    private void ReturnExpandedHostedGuardianForm(ExpandedBattlePresentationProcess process, BattleCombatant actor, bool terminal)
    {
        BattleSnapshot before = Capture();
        BattleSession candidate = CreateExpandedHostedFormCandidate(before);
        candidate.ReturnGuardianForm(candidate.GetActor(actor.Id), terminal);
        PreparedHostedGuardianFormChange? visual = PrepareStagedHostedGuardianForm(process, candidate);
        PublishExpandedHostedFormCandidate(process, before, candidate, visual);
    }

    private void ApplyExpandedHostedGuardianFormLanding(ExpandedBattlePresentationProcess process, NativeBattleGuardianFormLanded receipt)
    {
        HostedGuardianFormVisualOwner owner = receipt.Form;
        NativeBattleGuardianFormPresentation visual = RequireHostedGuardianFormVisualOwner(process.Graphics, owner);
        if (visual.Phase != 3 || visual.Mode != GuardianFormInvocationMode.EntryOnly ||
            GetGuardianForm(owner.ActorId) is not { Phase: BattleGuardianFormPhase.Entering } form || form.InvocationId != owner.InvocationId ||
            form.GuardianForceId != owner.GuardianForceId || expandedHostedEvent is not null || ActiveAction is not null)
        {
            throw new InvalidDataException("Hosted form landing did not match its current entering invocation.");
        }
        BattleSnapshot before = Capture();
        BattleSession candidate = CreateExpandedHostedFormCandidate(before);
        if (!candidate.ActivateGuardianFormEntry(owner.ActorId, owner.InvocationId))
        {
            PublishExpandedHostedFormCandidate(process, before, candidate, PrepareStagedHostedGuardianForm(process, candidate));
            return;
        }
        BattleCombatant actor = candidate.GetActor(owner.ActorId);
        const uint callback = 0x47DE70;
        BattleNativeAction action = candidate.FindAction(BattleAbility.GuardianForce, owner.GuardianForceId).NativeAction!;
        BattleActionPresentation entry = new(owner.ActorId, owner.ActorId, BattleActionPhase.Recovery, 1, SelectBattleTarget(actor), BattleAbility.GuardianForce, owner.GuardianForceId)
        {
            GuardianFormInvocation = new(owner.InvocationId, owner.GuardianForceId, GuardianFormInvocationMode.EntryOnly),
            NativeQueuedCommand = 254,
            NativeAction = action
        };
        candidate.ActiveAction = entry;
        candidate.nativeActionScratch = candidate.nativeActionScratch with { Command = action.Command, Action = action.ActionId, EventLane = 1 };
        NativeBattlePartyPresentedVital row = RequireExpandedPartyPresentedRow(candidate.expandedPartyPresentation!, owner.ActorId);
        candidate.expandedPartyPresentation = candidate.expandedPartyPresentation!.SetItem(owner.ActorId, row with
        {
            GuardianForceHitPoints = checked((ushort)actor.CurrentHp),
            GuardianForceMaximumHitPoints = checked((ushort)actor.MaximumHp),
            DisplayedGuardianForceHitPointsQ12 = actor.CurrentHp << 12
        });
        candidate.Log("GuardianFormActive", owner.ActorId, owner.ActorId, owner.GuardianForceId, "GF form entrance completed.");
        PreparedHostedDispatch dispatch = process.PrepareDispatch([], completion: new(callback, owner.Actor, owner.ActorId));
        ExpandedHostedEvent hosted = new(entry, entry, callback, dispatch.Completion!.Request.Token, CompletionOwner: owner.Actor);
        PreparedActionPublication publication = new(this, candidate, CreateExpandedHostedFormCandidate(before));
        process.ValidatePreparedDispatch(dispatch);
        publication.Publish();
        expandedPartyPresentation = candidate.expandedPartyPresentation;
        expandedHostedEvent = hosted;
        process.CommitDispatch(dispatch);
    }

    private BattleSession CreateExpandedHostedFormCandidate(BattleSnapshot before)
    {
        BattleSession candidate = Restore(before, handlers.Values.Where(handler => handler is not ScaffoldBattleEffectHandler));
        candidate.stagingExpandedHostedGuardianForms = true;
        candidate.expandedPartyPresentation = expandedPartyPresentation;
        return candidate;
    }

    private static PreparedHostedGuardianFormChange? PrepareStagedHostedGuardianForm(ExpandedBattlePresentationProcess? process, BattleSession candidate)
    {
        return candidate.stagedExpandedHostedGuardianForm is { } form
            ? (process ?? throw new InvalidOperationException("A staged GF form transition lost its hosted presentation process.")).PrepareGuardianFormChange(form)
            : null;
    }

    private void PublishExpandedHostedFormCandidate(ExpandedBattlePresentationProcess process, BattleSnapshot before, BattleSession candidate, PreparedHostedGuardianFormChange? visual)
    {
        PreparedActionPublication publication = new(this, candidate, CreateExpandedHostedFormCandidate(before));
        if (visual is not null)
        {
            process.ValidatePreparedGuardianFormChange(visual);
        }
        publication.Publish();
        expandedPartyPresentation = candidate.expandedPartyPresentation;
        if (visual is not null)
        {
            process.CommitGuardianFormChange(visual);
            PublishHostedGuardianFormAudio(visual);
        }
    }

    private void PublishHostedGuardianFormAudio(PreparedHostedGuardianFormChange? visual)
    {
        if (visual is not null)
        {
            foreach (string message in visual.StopSounds)
            {
                PublishNativeStopAllSounds(message);
            }
        }
    }
}
