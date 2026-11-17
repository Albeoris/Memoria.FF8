using static Memoria.FF8.Runtime.BattlePresentationProcess;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

internal sealed class HostedRenzokukenOwner(BattlePartyModelOwner actor, string actorId, ushort magicId, byte? finisher)
{
    internal BattlePartyModelOwner Actor { get; } = actor;
    internal string ActorId { get; } = actorId;
    internal ushort MagicId { get; } = magicId;
    internal byte? Finisher { get; } = finisher;
    internal uint FinisherCallback => Finisher switch
    {
        3 => 0x48F350,
        < 3 => 0x48F3F0,
        _ => throw new InvalidOperationException("This Renzokuken event has no finisher callback.")
    };
}

internal sealed record HostedRenzokukenFrame(HostedRenzokukenOwner Owner, long ActionToken, BattleNativeRenzokukenState State, int Contacts);
internal sealed record NativeBattleRenzokukenWidgetStepped(HostedRenzokukenFrame Frame, NativeBattleTriggerWidget Widget, NativeBattleTriggerReceipt? Pending, bool Unregistered, long Epoch, int Order);
internal sealed record HostedRenzokukenContact(HostedRenzokukenOwner Owner, long Token, short Index);
internal sealed record NativeBattleRenzokukenFinisherReached(HostedRenzokukenOwner EventOwner, BattleActionHeaderReference Header, int Slot, byte Serial) : NativeBattleModelLifecycleReceipt(EventOwner.Actor, EventOwner.ActorId);

public sealed partial class BattleSession
{
    private sealed record HostedRenzokukenCandidateProof(BattleSnapshot Snapshot, ExpandedHostedEvent Event, NativeBattleTriggerReceipt? Trigger, NativeBattleRenzokukenFinisherReached? Finisher) : HostedCandidateProof;

    internal void ApplyExpandedHostedRenzokukenWidgetSteps(ImmutableArray<NativeBattleRenzokukenWidgetStepped> steps, long epoch)
    {
        BattlePresentationProcess process = expandedPresentationProcess ?? throw new InvalidOperationException("Hosted Renzokuken UI lost its attached process.");
        if (CaptureExpandedHostedRenzokukenFrame() is not { } live || steps.IsDefaultOrEmpty ||
            !ReferenceEquals(steps[0].Frame.Owner, live.Owner) || !ReferenceEquals(steps[0].Frame.State, live.State) ||
            steps[0].Frame.ActionToken != live.ActionToken || steps[0].Frame.Contacts != live.Contacts || process.ActiveActionToken != live.ActionToken)
        {
            throw new InvalidDataException("Hosted Renzokuken UI does not belong to the canonical input handoff.");
        }
        BattleNativeRenzokukenState state = live.State;
        for (int i = 0; i < steps.Length; i++)
        {
            NativeBattleRenzokukenWidgetStepped next = steps[i];
            NativeBattleTriggerWidget widget = next.Widget;
            if (!ReferenceEquals(next.Frame, steps[0].Frame) || next.Epoch != epoch || next.Order != i || widget.Actor != live.Owner.Actor ||
                widget.SourceMagicId != live.Owner.MagicId || widget.Input.ContactIndex < state.Input.ContactIndex ||
                widget.Input.ContactIndex > state.Input.ContactIndex + 1 ||
                (next.Pending is { } pending
                    ? !ReferenceEquals(process.PendingTrigger, pending) || pending.Actor != live.Owner.Actor || pending.ContactIndex != widget.Input.ContactIndex || pending.ContactIndex != live.Contacts + 1
                    : widget.Input.ContactIndex != state.Input.ContactIndex) ||
                next.Unregistered && (i + 1 != steps.Length || process.TriggerWidget is not null))
            {
                throw new InvalidDataException("Hosted Renzokuken UI received an invalid or out-of-order widget step.");
            }
            state = state with { Input = widget.Input, PoseFrames = widget.Input.PoseClock / 4, PendingTrigger = widget.PendingTrigger };
        }
        nativeRenzokukenProcesses[live.Owner.ActorId] = state;
    }

    private void ValidateExpandedHostedRenzokukenContact(NativeBattlePresentationContactReceived contact, long token)
    {
        if (CurrentExpandedHostedInvocation?.Header.Command == 5)
        {
            if (expandedHostedEvent?.Renzokuken is not { } owner || contact.Renzokuken is not { } hit ||
                !ReferenceEquals(hit.Owner, owner) || hit.Token != token || hit.Index != nativeTimedAction?.Contacts)
            {
                throw new InvalidDataException("Hosted Renzokuken impact does not match its current trigger occurrence.");
            }
        }
        else if (contact.Renzokuken is not null)
        {
            throw new InvalidDataException("A hosted trigger occurrence arrived outside its Renzokuken invocation.");
        }
    }

    private static void ValidateExpandedHostedRenzokukenProof(BattleSnapshot snapshot, HostedRenzokukenCandidateProof proof)
    {
        if (!ReferenceEquals(snapshot, proof.Snapshot) || snapshot.Outcome != BattleOutcome.Active ||
            proof.Event is not { Renzokuken: { } owner, LogicallyComplete: false } || !ReferenceEquals(snapshot.ActiveAction, proof.Event.ActivePresentation) ||
            snapshot.NativeTimedAction is not { } timed || !ReferenceEquals(timed.Command, proof.Event.ActivePresentation) ||
            snapshot.NativeLimits.SingleOrDefault(limit => limit.ActorId == owner.ActorId)?.Renzokuken is not { } state || state.MagicId != owner.MagicId ||
            state.FinisherActionId != owner.Finisher ||
            (proof.Trigger is { } trigger
                ? proof.Finisher is not null || trigger.Actor != owner.Actor || trigger.ContactIndex != timed.Contacts + 1 || trigger.ContactIndex != state.Input.ContactIndex
                : proof.Finisher is not { } finisher || !ReferenceEquals(finisher.EventOwner, owner) || state.Input.ContactIndex != timed.Contacts || state.Input.Phase != 8))
        {
            throw new InvalidDataException("A hosted Renzokuken candidate requires its exact event, timed input and reached formula.");
        }
    }

    private bool OwnsHostedRenzokukenRestore(ExpandedHostedEvent? hostedEvent, string actorId)
    {
        return hostedEvent is { Renzokuken: { } owner } hosted && owner.ActorId == actorId &&
            owner.Actor == CreatePresentationModelOwner(GetActor(actorId)) &&
            (ReferenceEquals(ActiveAction, hosted.ActivePresentation) || ActiveAction is null && !CanNativeBattleActorAct(GetActor(actorId)));
    }

    private void PrepareAndPublishExpandedHostedRenzokuken(BattleCombatant actor, BattleAttackCommand command, BattlePresentationProcess process)
    {
        if (!actions.TryPeek(out BattleAttackCommand? head) || !ReferenceEquals(command, head) || command.NativeRenzokuken is not { } selection)
        {
            throw new InvalidDataException("Hosted Renzokuken requires its original input at the FIFO head.");
        }
        BattleCombatant target = GetActor(command.TargetId);
        if (!IsExpandedRenzokukenTarget(target) || !CanNativeBattleActorAct(actor))
        {
            actions.Dequeue();
            actor.IsBusy = false;
            nativeLimitLevels.Remove(actor.Id);
            Log("Cancelled", actor.Id, target.Id, 0, "Renzokuken has no available original target.");
            return;
        }

        BattleSnapshot before = Capture();
        BattleSession candidate = CreatePreparedHostedActionCandidate(before);
        if (!candidate.actions.TryDequeue(out BattleAttackCommand? prepared) || prepared != command)
        {
            throw new InvalidDataException("The hosted Renzokuken candidate lost its original FIFO input.");
        }
        BattleCombatant source = candidate.GetActor(actor.Id);
        BattleCombatant recipient = candidate.GetActor(target.Id);
        (ushort magic, byte? finisher) = candidate.SelectNativeRenzokukenResource(source, recipient, selection.CrisisLevel);
        if (RequirePresentationModelOwner(process.PlannedModelRoster, actor) is not BattlePartyModelOwner caster)
        {
            throw new InvalidDataException("Hosted Renzokuken requires its canonical party model.");
        }
        BattleModelOwner targetOwner = RequirePresentationModelOwner(process.PlannedModelRoster, target);
        if (!process.Graphics.ActorModels.Contains(caster) || !process.Graphics.ActorModels.Contains(targetOwner))
        {
            throw new InvalidDataException("Hosted Renzokuken requires its actual caster and selected target models.");
        }
        HostedRenzokukenOwner owner = new(caster, source.Id, magic, finisher);
        BattleTargetRequest targets = SelectBattleTarget(recipient, eligibility: BattleTargetEligibility.SavedStatus);
        BattleActionPresentation mirror = NativeCommandPresentation(command) with { Phase = BattleActionPhase.Recovery, TargetRequest = targets };
        ExpandedHostedEvent hostedEvent = new(mirror, mirror, 0x47DE70, null, CompletionOwner: caster) { Renzokuken = owner };
        candidate.ActiveAction = mirror;
        candidate.expandedHostedEvent = hostedEvent;
        candidate.BeginNativeBattleActionPause();
        candidate.BeginNativeReactionBatch();
        candidate.nativeTimedAction = new(mirror, 0);
        candidate.nativeActionCommitDepth = 1;
        candidate.nativeAffinityAction = null;
        candidate.nativeRenzokukenProcesses.Add(source.Id, new(magic, targets, finisher, new()));
        candidate.nativeActionScratch = candidate.nativeActionScratch with
        {
            EventLane = 1,
            DeferredEffectsE00 = false,
            FinisherPendingE03 = finisher.HasValue,
            FinisherActionIndexE2E = finisher ?? candidate.nativeActionScratch.FinisherActionIndexE2E
        };
        BattleNativeAction initial = NativeRenzokukenInitialAction(source, magic, finisher is null ? (ushort)65533 : (ushort)65534);
        candidate.StageExpandedHostedRenzokukenPreliminary(source, mirror, initial, sequence: 21);
        if (finisher is byte selected)
        {
            BattleNativeAction action = candidate.FindInstalledNativeAction(249, selected);
            ValidateExpandedRenzokukenFinisher(action);
            BattleTargetRequest automatic = candidate.CreateAutomaticTargetRequest(action.TargetFlags!.Value);
            BattleTargetRequest request = (action.TargetFlags.Value & 0x30) is 0 or 0x20 ? automatic : targets;
            candidate.StageExpandedHostedControl(new HostedDispatchControl.RenzokukenFinisher(owner));
            BattleNativeAction preliminary = action with { Command = 250, ActionId = ushort.MaxValue, SupplementalKernelIndex = selected };
            candidate.StageExpandedHostedRenzokukenPreliminary(source, mirror with { TargetRequest = request }, preliminary, sequence: 0);
        }
        candidate.StageExpandedHostedCompletion(mirror, 0x47DE70, caster, capturedBeforeAi: false);
        candidate.LogRenzokukenBoundary(mirror, "Windup", $"{source.Name}: Renzokuken.");
        candidate.LogRenzokukenBoundary(mirror, "Renzokuken", $"{source.Name}: Renzokuken.");

        foreach (ExpandedHostedInvocation invocation in candidate.expandedOperationCursor.Operations.OfType<ExpandedHostedInvocation>())
        {
            NativeBattleOriginalEffectState effect = NativeBattleOriginalEffects.Select(invocation.Header.Effect, definition.NativeEffectCatalog);
            _ = NativeBattleEffectResourceResolver.MaterializeSelected(effect, nativeEffectResourceResolver);
        }
        candidate.ValidateExpandedHostedInvocations(process.PlannedModelRoster);
        PreparedHostedGuardianFormChange? form = PrepareStagedHostedGuardianForm(process, candidate);
        ExpandedHostedPrefix prefix = candidate.PrepareExpandedHostedPrefix(process, guardianForm: form);
        PreparedHostedDispatch dispatch = prefix.Dispatch;
        candidate.BindExpandedHostedPrefix(prefix, dispatch.Packets.Roster);
        PreparedActionPublication publication = new(this, candidate, Restore(before));
        process.ValidatePreparedDispatch(dispatch);
        publication.Publish();
        PublishStagedHostedOperations(candidate);
        expandedHostedEvent = hostedEvent with { CompletionToken = dispatch.Completion?.Request.Token };
        expandedHostedActionToken = dispatch.Action?.Invocation.Token;
        process.CommitDispatch(dispatch);
        publication.PublishParticipantCallbackObservations();
        PublishHostedGuardianFormAudio(form);
    }

    private void StageExpandedHostedRenzokukenPreliminary(BattleCombatant source, BattleActionPresentation mirror, BattleNativeAction native, byte sequence)
    {
        string name = native.Command == 5 ? "Renzokuken" : native.Name ?? "Renzokuken finisher";
        BattleActionPresentation command = mirror with { NativeAction = native, NativeQueuedCommand = native.Command, ActionId = native.ActionId, NativeSequence = sequence };
        NativeBattleActionHeaderPreparation header = BuildExpandedActionHeaderPreparation(command, native);
        BattleActionDefinition action = new(command.Ability, command.ActionId, name, BattleActionEffect.Native, native.Power, false) { NativeAction = native };
        nativeActionScratch = nativeActionScratch with { ActorIndex = ProjectOriginalActionActor(source), Command = native.Command, Action = header.Action };
        BattleActionPresentation? previous = resolvingNativeAction;
        resolvingNativeAction = command;
        deferringExpandedImpacts = true;
        try
        {
            var plan = PlanNativeHits(source, command, native);
            NativeContactPlanResult produced = ExecuteHostedContactPlan(source, command, action, native, plan, preliminary: true);
            ImmutableArray<ExpandedBattleContact> contacts = ToHostedContacts(produced);
            if (contacts.IsEmpty || produced.RepeatAborted || contacts.Length != plan.Sum(hit => hit.Targets.Length) || native.Command == 5 && contacts.Length != 1)
            {
                throw new InvalidDataException("Hosted Renzokuken lost its actual preliminary contact extent.");
            }
            RecordPublishedNativeTargetCount(contacts.Length);
            StageExpandedHostedInvocation(command, header, contacts, null);
        }
        finally
        {
            deferringExpandedImpacts = false;
            resolvingNativeAction = previous;
        }
    }

    private HostedRenzokukenFrame? CaptureExpandedHostedRenzokukenFrame()
    {
        if (expandedHostedEvent is not { Renzokuken: { } owner, LogicallyComplete: false } ||
            expandedHostedActionToken is not long token || CurrentExpandedHostedInvocation?.Header.Command != 5 ||
            nativeTimedAction is not { } timed || !nativeRenzokukenProcesses.TryGetValue(owner.ActorId, out BattleNativeRenzokukenState? state))
        {
            return null;
        }
        return new(owner, token, state, timed.Contacts);
    }

    private bool CanTriggerExpandedHostedRenzokuken(BattlePresentationProcess process)
    {
        return Outcome == BattleOutcome.Active && CaptureExpandedHostedRenzokukenFrame() is { } frame &&
            process.ActiveActionToken == frame.ActionToken && process.PendingTrigger is null &&
            process.TriggerWidget is { } widget && widget.Actor == frame.Owner.Actor && widget.SourceMagicId == frame.Owner.MagicId &&
            BattlePresentationProcess.CanPressNativeTriggerWidget(widget, process.TriggerOptions);
    }

    private void AcknowledgeExpandedHostedRenzokukenRegistration(BattlePresentationProcess process, NativeBattleModelTriggerRegistered receipt, long? actionToken)
    {
        if (CaptureExpandedHostedRenzokukenFrame() is not { } frame || frame.ActionToken != actionToken || process.ActiveActionToken != actionToken ||
            receipt.Owner != frame.Owner.Actor || receipt.ActorId != frame.Owner.ActorId || receipt.Widget.Actor != receipt.Owner ||
            receipt.Widget.SourceMagicId != frame.Owner.MagicId || !ReferenceEquals(receipt.Widget, process.TriggerWidget) ||
            !receipt.Widget.Block.Timeline.InputThresholds.SequenceEqual(RenzokukenResource(frame.Owner.MagicId).Timeline.InputThresholds))
        {
            throw new InvalidDataException("Hosted Renzokuken widget registration lost its selected installed resource and caster.");
        }
    }

    private void DispatchExpandedHostedRenzokukenTrigger(BattlePresentationProcess process, NativeBattleTriggerReceipt receipt)
    {
        if (CaptureExpandedHostedRenzokukenFrame() is not { } frame || nativeTimedAction is not { } timed ||
            CurrentExpandedHostedInvocation is not { Header.Command: 5, Contacts: [var previous] } invocation ||
            !ReferenceEquals(timed.Command, ActiveAction) || process.ActiveActionToken != frame.ActionToken || receipt.Actor != frame.Owner.Actor ||
            receipt.ContactIndex != timed.Contacts + 1 || receipt.ContactIndex != frame.State.Input.ContactIndex ||
            receipt.TotalContacts != RenzokukenResource(frame.Owner.MagicId).Timeline.InputThresholds.Length ||
            expandedInvocationProgress is not { ExpectedContacts: 1 } progress || !ReferenceEquals(progress.Command, invocation.Command))
        {
            throw new InvalidDataException("Hosted Renzokuken trigger does not identify its active widget, resource and next contact.");
        }
        if (progress.Occurrences.Count != (timed.Contacts == 0 ? 0 : 1) || progress.PresentedRoutedContacts != invocation.RoutedGraphicsContacts.Length)
        {
            throw new InvalidDataException("The prior hosted Renzokuken trigger has not published its required contact.");
        }
        BattleModelRoster roster = process.Graphics.ActorModels.Roster;
        receipt.Validate(roster);
        if (RequirePresentationModelOwner(roster, GetActor(previous.RecipientId)) != previous.Packet.Primary.Recipient)
        {
            throw new InvalidDataException("Hosted Renzokuken trigger lost its original selected target.");
        }
        PublishExpandedHostedRecompute(process, roster, frame.ActionToken, invocation, candidate =>
        {
            candidate.pendingEnemyDraw ??= invocation.EnemyDraw;
            NativeBattleContactPacket packet = candidate.ResolveExpandedHostedRenzokukenTrigger(invocation, previous, receipt, frame.Owner);
            return invocation with { Contacts = [new(previous.RecipientId, packet)], EnemyDraw = candidate.TakePendingHostedEnemyDraw() };
        }, trigger: receipt);
    }

    private NativeBattleContactPacket ResolveExpandedHostedRenzokukenTrigger(ExpandedHostedInvocation invocation, ExpandedBattleContact previous, NativeBattleTriggerReceipt receipt, HostedRenzokukenOwner owner)
    {
        BattleCombatant actor = GetActor(invocation.Command.ActorId);
        BattleCombatant recipient = GetActor(previous.RecipientId);
        (BattleNativeAction native, short timing) = PrepareNativeTriggerHit(actor, recipient, invocation.Header.Command, receipt);
        BattleActionPresentation command = invocation.Command with
        {
            NativeQueuedCommand = owner.Finisher.HasValue ? (byte)250 : (byte)5,
            NativeAction = native,
            TargetRequest = SelectBattleTarget(recipient),
            GunbladeTriggerTiming = timing
        };
        BattleActionDefinition action = new(command.Ability, command.ActionId, native.Name ?? "Renzokuken", BattleActionEffect.Native, native.Power, false) { NativeAction = native };
        BattleActionPresentation? previousAction = resolvingNativeAction;
        bool previousSuppression = suppressNativeImmediateCounter;
        resolvingNativeAction = command;
        suppressNativeImmediateCounter = receipt.ContactIndex != receipt.TotalContacts || owner.Finisher.HasValue;
        deferringExpandedImpacts = true;
        nativeActionCommitDepth++;
        try
        {
            NativeBattleContactPacket packet = ResolveInstalledNativeHitWithContact(actor, recipient, command, action, native);
            nativeTimedAction = nativeTimedAction! with { Contacts = receipt.ContactIndex };
            return packet;
        }
        finally
        {
            nativeActionCommitDepth--;
            deferringExpandedImpacts = false;
            suppressNativeImmediateCounter = previousSuppression;
            resolvingNativeAction = previousAction;
        }
    }

    private void ValidateExpandedHostedRenzokukenInitialRetirement(ExpandedHostedEvent hostedEvent)
    {
        if (hostedEvent.Renzokuken is not { } owner || CurrentExpandedHostedInvocation?.Header.Command != 5)
        {
            return;
        }
        if (nativeTimedAction is not { } timed || !nativeRenzokukenProcesses.TryGetValue(owner.ActorId, out BattleNativeRenzokukenState? state) ||
            timed.Contacts != RenzokukenResource(owner.MagicId).Timeline.InputThresholds.Length ||
            state.Input is not { Phase: 8 } || state.Input.ContactIndex != timed.Contacts ||
            expandedPresentationProcess is { } process && (process.PendingTrigger is not null || process.TriggerWidget?.SourceMagicId == owner.MagicId))
        {
            NativeBattleTriggerWidget? widget = expandedPresentationProcess?.TriggerWidget;
            byte? inputPhase = nativeRenzokukenProcesses.GetValueOrDefault(owner.ActorId)?.Input.Phase;
            throw new InvalidDataException($"Hosted Renzokuken {owner.MagicId} retired before its timed contacts and widget finished. Contacts {nativeTimedAction?.Contacts}, input phase {inputPhase}, widget input/close {widget?.Input.Phase}/{widget?.ClosingPhase}.");
        }
    }

    private void CompleteExpandedHostedRenzokuken(ExpandedHostedEvent hostedEvent)
    {
        if (hostedEvent.Renzokuken is not { } owner)
        {
            return;
        }
        if (nativeTimedAction is not { } timed || !ReferenceEquals(timed.Command, hostedEvent.ActivePresentation) ||
            nativeActionCommitDepth != 1 || timed.Contacts != RenzokukenResource(owner.MagicId).Timeline.InputThresholds.Length ||
            !nativeRenzokukenProcesses.Remove(owner.ActorId))
        {
            throw new InvalidDataException("Hosted Renzokuken final callback has no completed timed owner.");
        }
        CompleteNativeTimedActionOwners(GetActor(owner.ActorId), timed.Command);
        nativeLimitLevels.Remove(owner.ActorId);
        nativeTimedAction = null;
        nativeActionCommitDepth = 0;
    }



    private void ApplyExpandedHostedRenzokukenFinisher(NativeBattleModelLifecycleServices services, NativeBattleRenzokukenFinisherReached receipt, long? frameToken)
    {
        _ = RequireEmittingHostedActor(services, receipt);
        BattlePresentationProcess process = expandedPresentationProcess ?? throw new InvalidOperationException("Hosted Renzokuken finisher lost its attached process.");
        if (expandedHostedEvent is not { Renzokuken: { } owner } hostedEvent || !ReferenceEquals(owner, receipt.EventOwner) ||
            !ReferenceEquals(services.Renzokuken?.Owner, owner) || !ReferenceEquals(process.PendingRenzokukenFinisher, receipt) ||
            expandedHostedActionToken is not long token || token != frameToken || !process.IsActionComplete(token) || !process.IsControlPrefixRetired ||
            CurrentExpandedHostedInvocation?.Header.Command != 5 || expandedOperationCursor.Next >= expandedOperationCursor.Operations.Length ||
            expandedOperationCursor.Operations[expandedOperationCursor.Next] is not ExpandedHostedInvocation { Header.Command: 250 } invocation ||
            invocation.HeaderReference != receipt.Header || owner.Finisher is not byte finisher)
        {
            throw new InvalidDataException("Native Renzokuken formula does not own the pending command250 after its retired command5.");
        }
        ValidateExpandedHostedRenzokukenInitialRetirement(hostedEvent);
        ValidateExpandedEffectContactCompletion();

        NativeBattleActionPacketsState packets = process.ActionPackets;
        BattleSnapshot before = Capture();
        HostedRenzokukenCandidateProof proof = new(before, hostedEvent, null, receipt);
        BattleSession candidate = CreatePreparedHostedActionCandidate(before, proof);
        candidate.expandedHostedEvent = hostedEvent;
        candidate.expandedOperationCursor = expandedOperationCursor with { Current = null };
        candidate.producingExpandedAiCallback = true;
        ExpandedAiInvocationReservation reservation = new(invocation.Header.Source, invocation.Command);
        candidate.producingExpandedAiInvocation = reservation;
        (ImmutableArray<ExpandedBattleContact> contacts, ImmutableArray<NativeBattleContactPacket> routed) = candidate.CollectDeferredRoutedGraphicsContacts(true, () => candidate.ResolveExpandedHostedRenzokukenFinisher(invocation, finisher));
        candidate.expandedOperationCursor.Operations = candidate.expandedOperationCursor.Operations.SetItem(candidate.expandedOperationCursor.Next,
            invocation with { Contacts = contacts, RoutedGraphicsContacts = routed, EnemyDraw = candidate.TakePendingHostedEnemyDraw(), Reservation = reservation });
        candidate.FlushEncounterCounterCommands();
        if (candidate.encounterCounterCommands.Count != 0)
        {
            throw new InvalidDataException("Hosted Renzokuken finisher left captured counters unpublished.");
        }
        ImmutableArray<HostedModelLoadAdmission> allocations = candidate.PrepareExpandedModelLoadAdmissions(process);
        BattleModelRoster planned = allocations.IsEmpty ? process.PlannedModelRoster : new(process.PlannedModelRoster.Owners.Concat(allocations.Select(plan => plan.Load.ModelOwner)));
        candidate.ValidateExpandedHostedInvocations(planned);
        HostedRenzokukenFinisherReplacement replacement = new(receipt, packets, invocation.FirstContact, contacts.Select(contact => contact.Packet).ToImmutableArray(), routed);
        PreparedHostedGuardianFormChange? form = PrepareStagedHostedGuardianForm(process, candidate);
        ExpandedHostedPrefix prefix = candidate.PrepareExpandedHostedPrefix(process, allocations: allocations, guardianForm: form, renzokukenFinisher: replacement);
        PreparedHostedDispatch dispatch = prefix.Dispatch;
        candidate.BindExpandedHostedPrefix(prefix, dispatch.Packets.Roster);
        PreparedActionPublication publication = new(this, candidate, RestoreSnapshot(before, proof));
        process.ValidatePreparedDispatch(dispatch);
        publication.Publish();
        PublishStagedHostedOperations(candidate);
        expandedHostedEvent = hostedEvent with { CompletionToken = dispatch.Completion?.Request.Token };
        expandedHostedActionToken = dispatch.Action?.Invocation.Token;
        process.CommitDispatch(dispatch);
        publication.PublishParticipantCallbackObservations();
        PublishHostedGuardianFormAudio(form);
    }

    private ImmutableArray<ExpandedBattleContact> ResolveExpandedHostedRenzokukenFinisher(ExpandedHostedInvocation invocation, byte index)
    {
        BattleCombatant actor = GetActor(invocation.Command.ActorId);
        BattleNativeAction native = FindInstalledNativeAction(249, index) with { ActionId = invocation.Header.Action };
        int count = index == 3 ? native.HitCount : invocation.Contacts.Length;
        if (invocation.Header.Command != 250 || count == 0 || count != invocation.Contacts.Length)
        {
            throw new InvalidDataException("Hosted Renzokuken finisher formula must retain its preliminary hit extent.");
        }
        if (index == 3)
        {
            GetActor(invocation.Contacts[0].RecipientId).NativeRemainingHits = native.HitCount;
        }
        BattleActionPresentation command = invocation.Command with { NativeAction = native, ActionId = index };
        BattleActionDefinition action = new(command.Ability, command.ActionId, native.Name ?? "Renzokuken finisher", BattleActionEffect.Native, native.Power, false) { NativeAction = native };
        BattleActionPresentation? previous = resolvingNativeAction;
        nativeActionScratch = nativeActionScratch with { ActorIndex = ProjectOriginalActionActor(actor), Command = 249, DeferredEffectsE00 = false };
        resolvingNativeAction = command;
        deferringExpandedImpacts = true;
        nativeActionCommitDepth++;
        var result = ImmutableArray.CreateBuilder<ExpandedBattleContact>(count);
        try
        {
            foreach (ExpandedBattleContact contact in invocation.Contacts)
            {
                NativeBattleContactPacket packet = ResolveInstalledNativeHitWithContact(actor, GetActor(contact.RecipientId), command, action, native);
                result.Add(new(contact.RecipientId, packet));
            }
        }
        finally
        {
            nativeActionCommitDepth--;
            deferringExpandedImpacts = false;
            resolvingNativeAction = previous;
        }
        return result.MoveToImmutable();
    }
}
