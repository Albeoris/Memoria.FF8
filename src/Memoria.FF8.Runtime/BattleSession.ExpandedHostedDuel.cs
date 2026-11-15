using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

internal sealed class HostedDuelPoll(BattleModelOwner owner, string actorId, uint readinessCallback)
{
    internal BattleModelOwner Owner { get; } = owner;
    internal string ActorId { get; } = actorId;
    internal uint ReadinessCallback { get; } = readinessCallback;
}

internal sealed record HostedDuelFrameOwner(HostedDuelPoll Poll, BattleNativeDuelState Widget);
internal sealed record NativeBattleDuelReadinessReached(HostedDuelFrameOwner Frame, uint Callback) : NativeBattleModelLifecycleReceipt(Frame.Poll.Owner, Frame.Poll.ActorId);
internal sealed record NativeBattleDuelSelectionReached(HostedDuelFrameOwner Frame, int Slot, byte Serial) : NativeBattleModelLifecycleReceipt(Frame.Poll.Owner, Frame.Poll.ActorId);

public sealed partial class BattleSession
{
    private sealed record HostedDuelCandidateProof(BattleSnapshot Snapshot, ExpandedHostedEvent Event, NativeBattleDuelSelectionReached Selection) : HostedCandidateProof;
    private NativeBattleDuelSelectionReached? pendingHostedDuelSelection;

    internal NativeBattleDuelSelectionReached? PendingHostedDuelSelection => pendingHostedDuelSelection;

    private static void ValidateExpandedHostedDuelProof(BattleSnapshot snapshot, HostedDuelCandidateProof proof)
    {
        BattleActionPresentation mirror = proof.Event.ActivePresentation;
        HostedDuelFrameOwner frame = proof.Selection.Frame;
        if (!ReferenceEquals(snapshot, proof.Snapshot) || !snapshot.Definition.UsesExpandedRoster || snapshot.NativeGraphics is not null ||
            snapshot.Outcome != BattleOutcome.Active || mirror.NativeDuel is null || snapshot.ActiveAction is not null && !ReferenceEquals(snapshot.ActiveAction, mirror) ||
            !ReferenceEquals(proof.Event.DuelPoll, frame.Poll) || mirror.ActorId != frame.Poll.ActorId || proof.Event.CompletionOwner != frame.Poll.Owner ||
            frame.Widget.Selection1D28DEF is not byte selection || selection == 254 ||
            !snapshot.NativeLimits.Any(limit => limit.ActorId == mirror.ActorId && ReferenceEquals(limit.Duel, frame.Widget)))
        {
            throw new InvalidDataException("A hosted Duel candidate requires its exact reached selection, widget and event mirror.");
        }
    }

    private bool OwnsHostedDuelRestore(ExpandedHostedEvent? hostedEvent, string actorId)
    {
        return hostedEvent is { Command.NativeDuel: not null } && hostedEvent.Command.ActorId == actorId &&
            (ReferenceEquals(hostedEvent.ActivePresentation, ActiveAction) || ActiveAction is null && !CanNativeBattleActorAct(GetActor(actorId))) &&
            hostedEvent.CompletionOwner == CreatePresentationModelOwner(GetActor(actorId));
    }

    private NativeBattleGraphicsState RunExpandedHostedDuelSelection(NativeBattleGraphicsState graphics)
    {
        NativeBattleModelLifecycleServices services = hostedFrameServices ?? throw new InvalidOperationException("Hosted Duel polling requires its current frame facts.");
        if (services.Duel is not { } frame || frame.Widget.Selection1D28DEF is not byte selection || pendingHostedDuelSelection is not null)
        {
            throw new InvalidDataException("Native Duel polling requires its current unconsumed widget selection.");
        }
        int slot = graphics.Descriptors.Queue[0];
        NativeBattleDescriptorPacket packet = graphics.Descriptors.Packets[slot];
        int tracked = -1;
        for (int index = 0; index < hostedControlPackets.Length; index++)
        {
            HostedControlPacket control = hostedControlPackets[index];
            if (!control.Retired && control.Slot == slot && control.Serial == packet.Serial && control is { Command: 10, Callback: 0x4852B0 } && ReferenceEquals(control.DuelPoll, frame.Poll))
            {
                tracked = index;
                break;
            }
        }
        if (tracked < 0)
        {
            throw new InvalidDataException("Native Duel poll lost its original queued control occurrence.");
        }
        if (selection == 254)
        {
            (NativeBattleDescriptorState descriptors, int next) = graphics.Descriptors.Enqueue(10, 128, 0x4852B0);
            hostedControlPackets = hostedControlPackets.SetItem(tracked, new(next, descriptors.Packets[next].Serial, 10, 0x4852B0) { DuelPoll = frame.Poll });
            return graphics with { Descriptors = descriptors };
        }
        NativeBattleDuelSelectionReached receipt = new(frame, slot, packet.Serial);
        pendingHostedDuelSelection = receipt;
        services.Emit(receipt);
        return graphics;
    }

    private void CompleteExpandedHostedDuelClosing(ExpandedHostedEvent hostedEvent)
    {
        if (hostedEvent.Command.NativeDuel is null)
        {
            return;
        }
        if (hostedEvent.DuelPoll is not null || !ReferenceEquals(hostedEvent.ActivePresentation, ActiveAction) ||
            nativeDuelProcesses.Count != 1 || !nativeDuelProcesses.TryGetValue(hostedEvent.Command.ActorId, out BattleNativeDuelState? state) ||
            state is not { Phase: BattleNativeDuelPhase.Finisher, Selection1D28DEF: 254 })
        {
            throw new InvalidDataException("Hosted Duel completion requires its closing event and consumed input.");
        }
        EndNativeDuel(hostedEvent.Command.ActorId);
    }

    private void PrepareAndPublishExpandedHostedDuel(BattleCombatant actor, BattleAttackCommand command, ExpandedBattlePresentationProcess process)
    {
        if (!actions.TryPeek(out BattleAttackCommand? head) || !ReferenceEquals(head, command) || command.NativeDuel is not { } invocation)
        {
            throw new InvalidDataException("Hosted Duel requires its original input at the FIFO head.");
        }
        BattleCombatant target = GetActor(command.TargetId);
        if (!CanNativeBattleActorAct(actor) || !IsExpandedDuelTarget(target))
        {
            actions.Dequeue();
            actor.IsBusy = false;
            nativeLimitLevels.Remove(actor.Id);
            Log("Cancelled", actor.Id, target.Id, 0, "Duel has no available original target.");
            return;
        }

        BattleSnapshot before = Capture();
        IBattleStatusEffectHandler[] customHandlers = handlers.Values.Where(handler => handler is not ScaffoldBattleEffectHandler).ToArray();
        BattleSession candidate = Restore(before, customHandlers);
        if (!candidate.actions.TryDequeue(out BattleAttackCommand? prepared) || prepared != command)
        {
            throw new InvalidDataException("The prepared Duel candidate does not retain the admitted FIFO input.");
        }
        candidate.preparingExpandedHostedAction = true;
        candidate.expandedPartyPresentation = expandedPartyPresentation;
        BattleCombatant source = candidate.GetActor(actor.Id);
        BattleNativeLimitDefinition rules = source.NativeLimit!;
        byte moveSet = NativeBattleDuelRules.InitialMoveSet(rules, invocation.CrisisLevel);
        short timer = NativeBattleDuelRules.Timer(rules, invocation.CrisisLevel);
        candidate.nativeDuelProcesses.Add(source.Id, new(moveSet, command.TargetRequest, timer, timer, BattleNativeDuelPhase.Opening)
        {
            Selection1D28DEF = 254,
            Ui = new(BattleTimedInputUiPhase.Opening)
        });
        BattleActionPresentation mirror = NativeCommandPresentation(command) with { Phase = BattleActionPhase.Recovery, RemainingFrames = 1 };
        candidate.ActiveAction = mirror;
        candidate.BeginNativeBattleActionPause();
        candidate.BeginNativeReactionBatch();
        candidate.nativeActionScratch = candidate.nativeActionScratch with { EventLane = 1 };
        BattleModelOwner owner = RequirePresentationModelOwner(process.PlannedModelRoster, actor);
        HostedDuelPoll poll = new(owner, actor.Id, 0x48E5A0);
        ExpandedHostedEvent hostedEvent = new(mirror, mirror, 0x47DE70, null, CompletionOwner: owner) { DuelPoll = poll };
        candidate.expandedHostedEvent = hostedEvent;
        BattleNativeAction control = candidate.NativeDuelControl(source, invocation.CrisisLevel);
        if (!candidate.StageExpandedHostedDuelMove(source, control, command.TargetRequest, null, resetCounts: false))
        {
            throw new InvalidDataException("Hosted Duel lost its validated opening target.");
        }
        candidate.StageExpandedHostedControl(new HostedDispatchControl.DuelReadiness(poll));
        BattleNativeAction initial = candidate.FindInstalledNativeAction(241, NativeBattleDuelRules.ActionId(rules, moveSet));
        if (!candidate.StageExpandedHostedDuelMove(source, initial, command.TargetRequest, NativeLimitCallback.DuelOpening, resetCounts: false))
        {
            throw new InvalidDataException("Hosted Duel lost its initial move target.");
        }
        candidate.StageExpandedHostedControl(new HostedDispatchControl.DuelSelection(poll));
        candidate.Log("Windup", source.Id, target.Id, 0, control.Name ?? "Duel");
        PublishExpandedHostedDuelCandidate(before, customHandlers, candidate, process, hostedEvent);
    }

    private bool StageExpandedHostedDuelMove(BattleCombatant actor, BattleNativeAction native, BattleTargetRequest request, NativeLimitCallback? callback, bool resetCounts)
    {
        BattleCombatant[] targets = ResolveBattleTargets(request);
        if (targets.Length == 0)
        {
            return false;
        }
        BattleActionPresentation formulaCommand = new(actor.Id, targets[0].Id, BattleActionPhase.Recovery, 1, request, BattleAbility.LimitBreak, native.Command << 16 | native.ActionId)
        {
            NativeQueuedCommand = native.Command,
            NativeAction = native,
            NativeSequence = 0,
            NativeLimitCallback = callback
        };
        string name = native.Name ?? "Duel";
        BattleActionPresentation command = native.Name is null ? formulaCommand with { NativeAction = native with { Name = name } } : formulaCommand;
        NativeBattleActionHeaderPreparation header = BuildExpandedActionHeaderPreparation(command, native);
        BattleActionDefinition action = new(BattleAbility.LimitBreak, command.ActionId, name, BattleActionEffect.Native, native.Power, false) { NativeAction = native };
        ReserveExpandedAiInvocation(command, header);
        BattleActionPresentation? previous = resolvingNativeAction;
        bool previousDeferral = deferringExpandedImpacts;
        nativeActionScratch = nativeActionScratch with { Command = native.Command, Action = native.ActionId };
        resolvingNativeAction = formulaCommand;
        deferringExpandedImpacts = true;
        nativeActionCommitDepth++;
        try
        {
            (ImmutableArray<ExpandedBattleContact> contacts, _) = ResolveExpandedHitPlan(actor, formulaCommand, action, native, [targets], preliminary: native.Command == 11);
            StageExpandedHostedInvocation(command, header, contacts, null);
        }
        finally
        {
            nativeActionCommitDepth--;
            deferringExpandedImpacts = previousDeferral;
            resolvingNativeAction = previous;
        }
        if (resetCounts)
        {
            StageExpandedHostedControl(new HostedDispatchControl.ResetCounts());
        }
        return true;
    }

    private void PublishExpandedHostedDuelCandidate(BattleSnapshot before, IBattleStatusEffectHandler[] customHandlers, BattleSession candidate,
        ExpandedBattlePresentationProcess process, ExpandedHostedEvent hostedEvent, HostedDuelCandidateProof? proof = null)
    {
        foreach (ExpandedHostedInvocation invocation in candidate.expandedOperationCursor.Operations.OfType<ExpandedHostedInvocation>())
        {
            if (invocation.Header.Command is 11 or 239)
            {
                NativeBattleOriginalEffectState selected = NativeBattleOriginalEffects.Select(invocation.Header.Effect, definition.NativeEffectCatalog);
                _ = NativeBattleEffectResourceResolver.MaterializeSelected(selected, nativeEffectResourceResolver);
            }
        }
        ImmutableArray<HostedModelLoadAdmission> allocations = candidate.PrepareExpandedModelLoadAdmissions(process);
        BattleModelRoster roster = process.PlannedModelRoster;
        BattleModelRoster admittedRoster = allocations.IsEmpty ? roster : new(roster.Owners.Concat(allocations.Select(plan => plan.Load.Owner)));
        candidate.ValidateExpandedHostedInvocations(admittedRoster);
        ExpandedHostedPrefix prefix = candidate.PrepareExpandedHostedPrefix(process, allocations: allocations, duelSelection: proof?.Selection);
        PreparedHostedDispatch dispatch = prefix.Dispatch;
        candidate.BindExpandedHostedPrefix(prefix, dispatch.Packets.Roster);
        PreparedActionPublication publication = new(this, candidate, RestoreSnapshot(before, customHandlers, proof));
        process.ValidatePreparedDispatch(dispatch);
        publication.Publish();
        PublishStagedHostedOperations(candidate);
        expandedHostedEvent = hostedEvent with { CompletionToken = dispatch.Completion?.Request.Token };
        expandedHostedActionToken = dispatch.Action?.Invocation.Token;
        process.CommitDispatch(dispatch);
    }

    private HostedDuelFrameOwner? CaptureExpandedHostedDuelOwner()
    {
        if (expandedHostedEvent?.DuelPoll is not { } poll || !nativeDuelProcesses.TryGetValue(poll.ActorId, out BattleNativeDuelState? widget) ||
            nativeDuelProcesses.Count != 1 || widget.Selection1D28DEF is null)
        {
            return null;
        }
        return new(poll, widget);
    }

    private void EmitExpandedHostedDuelReadiness(NativeBattleGraphicsState graphics, uint callback)
    {
        NativeBattleModelLifecycleServices services = hostedFrameServices ?? throw new InvalidOperationException("Hosted Duel readiness requires its frame facts.");
        int slot = graphics.Descriptors.Queue[0];
        byte serial = graphics.Descriptors.Packets[slot].Serial;
        if (services.Duel is not { } duel || duel.Poll.ReadinessCallback != callback ||
            !hostedControlPackets.Any(control => !control.Retired && control.Slot == slot && control.Serial == serial && control.Command == 10 &&
                control.Callback == callback && ReferenceEquals(control.DuelPoll, duel.Poll)))
        {
            throw new InvalidDataException("Native Duel readiness does not identify the current hosted widget and move.");
        }
        services.Emit(new NativeBattleDuelReadinessReached(duel, callback));
    }

    private void ApplyExpandedHostedDuelReadiness(NativeBattleModelLifecycleServices services, NativeBattleDuelReadinessReached receipt)
    {
        BattleCombatant actor = RequireEmittingHostedActor(services, receipt);
        if (!ReferenceEquals(services.Duel, receipt.Frame) || !ReferenceEquals(expandedHostedEvent?.DuelPoll, receipt.Frame.Poll) ||
            !ReferenceEquals(expandedHostedEvent?.ActivePresentation, ActiveAction) || receipt.Frame.Poll.ReadinessCallback != receipt.Callback ||
            !nativeDuelProcesses.TryGetValue(actor.Id, out BattleNativeDuelState? widget) || !ReferenceEquals(widget, receipt.Frame.Widget))
        {
            throw new InvalidDataException("Native Duel readiness does not belong to the active hosted Duel event.");
        }
        if (widget.Selection1D28DEF == 255 && !CanNativeBattleActorAct(actor))
        {
            return;
        }
        BattleNativeDuelPhase expected = receipt.Callback == 0x48E5A0 ? BattleNativeDuelPhase.Opening : BattleNativeDuelPhase.AwaitingEffect;
        if (widget.Phase != expected || widget.Selection1D28DEF != 254)
        {
            throw new InvalidDataException("Native Duel readiness reached a different input phase.");
        }
        nativeDuelProcesses[actor.Id] = ResetNativeDuelInput(widget);
    }

    private void ApplyExpandedHostedDuelSelection(NativeBattleModelLifecycleServices services, NativeBattleDuelSelectionReached receipt, long? frameToken)
    {
        BattleCombatant actor = RequireEmittingHostedActor(services, receipt);
        ExpandedBattlePresentationProcess process = expandedPresentationProcess ?? throw new InvalidOperationException("Hosted Duel polling lost its attached process.");
        if (expandedHostedEvent is not { DuelPoll: { } poll } hostedEvent || !ReferenceEquals(hostedEvent.ActivePresentation, ActiveAction) ||
            !ReferenceEquals(services.Duel, receipt.Frame) || !ReferenceEquals(poll, receipt.Frame.Poll) || !ReferenceEquals(process.PendingDuelSelection, receipt) ||
            !nativeDuelProcesses.TryGetValue(actor.Id, out BattleNativeDuelState? widget) || !ReferenceEquals(widget, receipt.Frame.Widget) ||
            widget.Selection1D28DEF is not byte selection || selection == 254 || !process.IsControlPrefixRetired ||
            expandedHostedActionToken is long token && (frameToken != token || !process.IsActionComplete(token)))
        {
            throw new InvalidDataException("Native Duel poll does not identify the current selected input after its retired move.");
        }
        ValidateExpandedEffectContactCompletion();
        BattleSnapshot current = CaptureState();
        BattleSnapshot before = NativeBattleStorageSnapshots.CapturePrivate(CanNativeBattleActorAct(actor) ? current : current with { ActiveAction = null });
        HostedDuelCandidateProof proof = new(before, hostedEvent, receipt);
        IBattleStatusEffectHandler[] handlersForCandidate = handlers.Values.Where(handler => handler is not ScaffoldBattleEffectHandler).ToArray();
        BattleSession candidate = RestoreSnapshot(before, handlersForCandidate, proof);
        candidate.preparingExpandedHostedAction = true;
        candidate.expandedPartyPresentation = expandedPartyPresentation;
        candidate.ActiveAction = hostedEvent.ActivePresentation;
        BattleCombatant source = candidate.GetActor(actor.Id);
        byte action = selection == 255 ? (byte)255 : NativeBattleDuelRules.ActionId(source.NativeLimit!, selection);
        bool canAct = CanNativeBattleActorAct(source);
        bool targetExists = candidate.ActionHasValidTarget(widget.TargetRequest, null);
        bool closing = !canAct || !targetExists || action >= 6;
        candidate.nativeDuelProcesses[source.Id] = widget with { Selection1D28DEF = 254, Phase = closing ? BattleNativeDuelPhase.Finisher : BattleNativeDuelPhase.AwaitingEffect };
        ExpandedHostedEvent nextEvent = hostedEvent with { DuelPoll = closing ? null : new HostedDuelPoll(poll.Owner, poll.ActorId, 0x48E5E0) };
        candidate.expandedHostedEvent = nextEvent;
        if (closing)
        {
            if (canAct)
            {
                if (targetExists)
                {
                    BattleNativeAction control = CreateNativeDuelControl241(selection == 255 ? (ushort)65532 : (ushort)65530, selection == 255 ? (ushort)0 : action);
                    candidate.StageExpandedHostedDuelMove(source, control, widget.TargetRequest, null, resetCounts: true);
                }
                if (selection != 255 && action >= 6)
                {
                    BattleNativeAction finisher = candidate.FindInstalledNativeAction(239, action);
                    byte flags = finisher.TargetFlags!.Value;
                    BattleTargetRequest targets = (flags & 0x30) == 0x10 ? widget.TargetRequest : candidate.CreateAutomaticTargetRequest(flags);
                    candidate.StageExpandedHostedDuelMove(source, finisher, targets, NativeLimitCallback.DuelFinisher, resetCounts: true);
                }
            }
            candidate.StageExpandedHostedCompletion(hostedEvent.Command, 0x47DE70, poll.Owner, capturedBeforeAi: false);
            candidate.FlushEncounterCounterCommands();
        }
        else
        {
            candidate.StageExpandedHostedControl(new HostedDispatchControl.DuelReadiness(nextEvent.DuelPoll!));
            BattleNativeAction move = candidate.FindInstalledNativeAction(241, action);
            if (!candidate.StageExpandedHostedDuelMove(source, move, widget.TargetRequest, NativeLimitCallback.DuelEffect, resetCounts: true))
            {
                throw new InvalidDataException("Hosted Duel lost its selected move target during preparation.");
            }
            candidate.StageExpandedHostedControl(new HostedDispatchControl.DuelSelection(nextEvent.DuelPoll!));
        }
        PublishExpandedHostedDuelCandidate(before, handlersForCandidate, candidate, process, nextEvent, proof);
    }
}
