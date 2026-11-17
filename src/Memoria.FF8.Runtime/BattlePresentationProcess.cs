using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>Owns the native presentation process for one battle.</summary>
public sealed partial class BattlePresentationProcess
{
    internal void ValidateGuardianFormEntryResources(BattlePresentationProcess.HostedGuardianFormVisualOwner owner)
    {
        RequireAdmissionWindow();
        ValidateHostedGuardianFormEntryResources(owner);
    }

    internal BattlePresentationProcess.PreparedHostedGuardianFormChange PrepareGuardianFormChange(BattlePresentationProcess.HostedGuardianFormVisualChange change)
    {
        RequireAdmissionWindow();
        return PrepareHostedGuardianFormChange(change);
    }

    internal void ValidatePreparedGuardianFormChange(BattlePresentationProcess.PreparedHostedGuardianFormChange prepared)
    {
        RequireAdmissionWindow();
        ValidatePreparedHostedGuardianFormChange(prepared);
    }

    internal void CommitGuardianFormChange(BattlePresentationProcess.PreparedHostedGuardianFormChange prepared)
    {
        CommitHostedGuardianFormChange(prepared);
    }
    private readonly BattlePresentationResources resources;
    private readonly BattlePresentationServices? services;
    private bool advancing;
    private bool canonicalHandoff;

    public NativeBattleGraphicsState Graphics => NativeGraphics!;
    public BattleModelOwner? FindOriginalActor(NativeBattleModelSlot slot)
    {
        return Graphics.NativeBindings.FindOriginalOwner(slot, Graphics.ActorModels);
    }

    public NativeBattleEffectMemory Memory => NativeEffectMemory!;
    public NativeBattleCameraState Camera => NativeCamera!;
    public NativeBattleStageState Stage => NativeStage!;
    public NativeExamTimerState? Timer => NativeExamTimer;
    public bool EntryReady => Graphics.EntryReady;
    public bool HasActiveAction => ActiveActionToken is not null;
    public NativeBattleEffectPresentationStep LastStep { get; private set; } = new([], []);

    private long Frame => services?.ReadDisplayFrame().Frame ?? 0;
    private long FrameCallback => services?.ReadCallback() ?? NativeGraphics?.Callback ?? 0;

    internal ulong GlobalRandomCalls => NativeGlobalRandomChange!.Calls;
    internal NativeGlobalRandomChange? GlobalRandomChange => NativeGlobalRandomChange;
    internal NativeBattleModelCpuResources ModelCpuResources => NativeModelCpuResources!;
    internal NativeBattleModelTextureCache ModelTextureCache => NativeModelTextureCache!;
    internal (NativeBattleAiHeapMessage Message, string Text) ReadAiHeapMessage(BattleModelOwner owner, int modelId, NativeBattleAiHeapTextDirectory directory, int index)
    {
        RequireAdmissionWindow();
        return ReadNativeAiHeapMessage(owner, modelId, directory, index);
    }
    internal bool HasPendingModelLoad => NativeModelLoader?.Task is not null;
    internal long? ActiveActionToken => HostedActiveActionToken;
    internal long? LastStepActionToken { get; private set; }
    internal NativeBattleModelLifecycleServices? LastFrameServices { get; private set; }
    internal NativeBattleTriggerWidget? TriggerWidget => Graphics.TriggerWidget;
    internal NativeBattleTriggerReceipt? PendingTrigger => Graphics.PendingTrigger;
    internal NativeBattlePresentationCompletionRequested? PendingCompletion => PendingHostedCompletion;
    internal ushort? TriggerOptions => resources.TriggerOptions;





    internal void AppendDiagnosticPresentationBlockers(List<string> blockers)
    {
        if (HasActiveAction || PendingCompletion is not null)
        {
            blockers.Add("hosted-action-or-completion");
        }
        AppendDiagnosticGraphicsBlockers(blockers);
    }

    internal object ObserveDiagnosticPresentationOwners()
    {
        return new
        {
            ActiveActionToken,
            PendingCompletion,
            FileActiveCount = NativeFileIo?.ActiveCount,
            CameraMask = Camera.Mask,
            SoundFlags = Memory.SoundPublishedFlags,
            Owners = ObserveDiagnosticGraphicsOwners()
        };
    }

    internal long AccumulateElapsed(TimeSpan elapsed)
    {
        RequireIdle();
        RequireCompletionAcknowledged();
        return AccumulateHostedPresentationElapsed(elapsed);
    }

    internal void RequireCompletionAcknowledged()
    {
        RequireHostedCompletionAcknowledged();
    }

    internal long BeginAction(NativeBattleActionHeaderPreparation header, ImmutableArray<NativeBattleContactPacket> contacts)
    {
        RequireAdmissionWindow();
        return BeginHostedAction(header, contacts);
    }

    internal long BeginDiagnosticAction(NativeBattleActionHeaderPreparation header, ImmutableArray<NativeBattleContactPacket> contacts)
    {
        RequireAdmissionWindow();
        if (contacts.IsDefaultOrEmpty || contacts.Any(contact => contact.Primary.Amount6 != 0 || contact.Secondary is not null))
        {
            throw new InvalidDataException("External diagnostic invocation requires its zero-amount presentation contacts.");
        }
        PreparedHostedDispatch prepared = PrepareHostedDispatch([new(header, contacts)], dispatchAppended: true, freshActionPackets: true, provenance: BattlePresentationInvocationProvenance.ExternalDiagnostic);
        return PublishHostedAction(prepared);
    }

    internal BattlePresentationProcess.PreparedHostedDispatch PrepareAction(NativeBattleActionHeaderPreparation header, ImmutableArray<NativeBattleContactPacket> contacts)
    {
        RequireAdmissionWindow();
        return PrepareHostedAction(header, contacts);
    }

    internal void ValidatePreparedAction(BattlePresentationProcess.PreparedHostedDispatch prepared)
    {
        RequireAdmissionWindow();
        ValidatePreparedHostedAction(prepared);
    }

    internal long PublishAction(BattlePresentationProcess.PreparedHostedDispatch prepared)
    {
        RequireAdmissionWindow();
        return PublishHostedAction(prepared);
    }

    internal bool IsActionComplete(long token)
    {
        return IsHostedActionComplete(token);
    }

    internal BattlePresentationProcess.PreparedHostedDispatch PrepareDispatch(ImmutableArray<BattlePresentationProcess.HostedDispatchHeader> headers, BattleActionHeaderReference? headerReference = null, bool dispatchAppended = false, BattlePresentationProcess.HostedDispatchCompletion? completion = null, BattlePresentationProcess.PreparedHostedCompletionAck? acknowledgment = null, BattlePresentationProcess.HostedDispatchControls? controls = null, bool publishEventHeaders = false, ImmutableArray<BattlePresentationProcess.HostedModelLoadAdmission> allocations = default, NativeBattleDuelSelectionReached? duelSelection = null, BattlePresentationProcess.HostedRenzokukenFinisherReplacement? renzokukenFinisher = null, BattlePresentationProcess.PreparedHostedGuardianFormChange? guardianForm = null)
    {
        RequireAdmissionWindow();
        return PrepareHostedDispatch(headers, headerReference, dispatchAppended, completion, acknowledgment, controls: controls, publishEventHeaders: publishEventHeaders, allocations: allocations, duelSelection: duelSelection, renzokukenFinisher: renzokukenFinisher, guardianForm: guardianForm);
    }

    internal BattleModelRoster PlannedModelRoster => HostedPlannedModelRoster;

    internal bool IsControlPrefixRetired => HostedControlPrefixRetired;
    internal NativeBattleDuelSelectionReached? PendingDuelSelection => PendingHostedDuelSelection;
    internal NativeBattleRenzokukenFinisherReached? PendingRenzokukenFinisher => PendingHostedRenzokukenFinisher;
    internal NativeBattleActionPacketsState ActionPackets => NativeActionPackets;

    internal void AppendScriptMessage(BattleEvent message, byte tag, byte priority)
    {
        RequireAdmissionWindow();
        AppendHostedScriptMessage(message, tag, priority);
    }

    internal void ValidatePreparedDispatch(BattlePresentationProcess.PreparedHostedDispatch prepared)
    {
        RequireAdmissionWindow();
        ValidatePreparedHostedDispatch(prepared);
    }

    internal BattlePresentationProcess.HostedEncounterLoadCheckpoint? PrepareEncounterLoad(BattleEncounterAllocationState? batch, BattlePresentationProcess.PreparedHostedDispatch dispatch)
    {
        RequireAdmissionWindow();
        return PrepareHostedEncounterLoad(batch, dispatch);
    }

    internal void CommitDispatch(BattlePresentationProcess.PreparedHostedDispatch prepared, BattlePresentationProcess.HostedEncounterLoadCheckpoint? encounterLoad = null)
    {
        PublishHostedEncounterLoad(encounterLoad);
        CommitHostedDispatch(prepared);
    }

    internal void CompleteEncounterLoad()
    {
        CompleteHostedEncounterLoad();
    }

    internal bool RollbackEncounterLoad()
    {
        return RollbackHostedEncounterLoad();
    }

    internal void RefreshRolledBackEncounter()
    {
        LastStep = new([], []);
        LastStepActionToken = null;
        LastFrameServices = null;
        BattlePresentationServices host = services ?? throw new InvalidOperationException("A rolled-back battle requires its bound logical services.");
        NativeExamTimer = host.ReadTimer();
        RefreshRolledBackHostedEncounter();
    }

    internal BattlePresentationProcess.PreparedHostedDispatch PrepareCompletion(uint callback, BattleModelOwner owner, string actorId)
    {
        RequireAdmissionWindow();
        return PrepareHostedCompletion(callback, owner, actorId);
    }

    internal long PublishCompletion(BattlePresentationProcess.PreparedHostedDispatch prepared)
    {
        RequireAdmissionWindow();
        return PublishHostedCompletion(prepared);
    }

    internal void ValidatePreparedCompletion(BattlePresentationProcess.PreparedHostedDispatch prepared)
    {
        RequireAdmissionWindow();
        ValidatePreparedHostedCompletion(prepared);
    }

    internal bool IsCompletionQueued(long token)
    {
        return IsHostedCompletionQueued(token);
    }

    internal bool IsCompletionAcknowledged(long token)
    {
        return IsHostedCompletionAcknowledged(token);
    }

    internal BattlePresentationProcess.PreparedHostedCompletionAck PrepareCompletionAck(long token, bool resetCounts)
    {
        RequireAdmissionWindow();
        return PrepareHostedCompletionAck(token, resetCounts);
    }

    internal void ValidatePreparedCompletionAck(BattlePresentationProcess.PreparedHostedCompletionAck prepared)
    {
        RequireAdmissionWindow();
        ValidatePreparedHostedCompletionAck(prepared);
    }

    internal void PublishCompletionAck(BattlePresentationProcess.PreparedHostedCompletionAck prepared)
    {
        RequireAdmissionWindow();
        PublishHostedCompletionAck(prepared);
    }

    internal void PressGunblade(long token, BattleModelOwner actor, NativeBattleTriggerWidget widget)
    {
        RequireIdle();
        PressHostedGunblade(token, actor, widget);
    }

    internal BattlePresentationProcess.PreparedHostedContactReplacement PrepareRecomputedContacts(long token, ImmutableArray<NativeBattleContactPacket> contacts, ImmutableArray<BattlePresentationProcess.HostedDispatchHeader> append, NativeBattleTriggerReceipt? trigger = null, BattlePresentationProcess.HostedGuardianBoostRequest? guardian = null, ImmutableArray<BattlePresentationProcess.HostedModelLoadAdmission> allocations = default, BattlePresentationProcess.PreparedHostedGuardianFormChange? guardianForm = null, ImmutableArray<NativeBattleContactPacket> routed = default)
    {
        RequireAdmissionWindow();
        return PrepareHostedContactReplacement(token, contacts, trigger, append, guardian, allocations, guardianForm, routed);
    }

    internal void ValidatePreparedRecomputedContacts(BattlePresentationProcess.PreparedHostedContactReplacement prepared)
    {
        RequireAdmissionWindow();
        ValidatePreparedHostedContactReplacement(prepared);
    }

    internal void CommitRecomputedContacts(BattlePresentationProcess.PreparedHostedContactReplacement prepared)
    {
        CommitHostedContactReplacement(prepared);
    }

    internal void SetActionMenu(bool open)
    {
        RequireIdle();
        SetNativeActionMenu(open);
    }

    internal NativeBattleModelLifecycleServices Advance(Action beforeCore, Action<NativeBattleModelLifecycleServices, long?> afterGraphics)
    {
        BattlePresentationServices host = services ?? throw new InvalidOperationException("A battle frame requires its bound logical services.");
        RequireIdle();
        RequireCompletionAcknowledged();
        if (PendingDuelSelection is not null || PendingRenzokukenFinisher is not null)
        {
            throw new InvalidOperationException("A reached timed-input selection must commit before the next presentation callback.");
        }
        advancing = true;
        try
        {
            ImportSharedState(host.ReadSharedState());
            SetGunbladeMode(host.ReadGunbladeMode());
            long? actionToken = ActiveActionToken;
            BattleModelRoster? rosterAtReceipts = null;
            NativeBattleModelLifecycleServices frame = AdvanceHostedBattlePresentationFrame(host.ReadTimer(), DispatchBeforeCore, DispatchClockPoll, DispatchClockReady, DispatchAfterGraphics, DispatchRenzokukenUi, DispatchShotWidget, DispatchDuelWidget);
            PublishPreparedPresentation(frame, rosterAtReceipts);
            LastStep = ReadAndClearHostedPresentationFrameOutputs();
            LastStepActionToken = actionToken;
            LastFrameServices = frame;
            host.FrameEnded(this);
            return frame;

            void DispatchBeforeCore()
            {
                DispatchCanonical(beforeCore);
                actionToken = ActiveActionToken;
            }

            void DispatchAfterGraphics(NativeBattleModelLifecycleServices currentServices)
            {
                rosterAtReceipts = PlannedModelRoster;
                try
                {
                    DispatchCanonical(() => afterGraphics(currentServices, actionToken));
                }
                catch
                {
                    ClearHostedPresentationFrameOutputs();
                    throw;
                }
            }

            void DispatchShotWidget(NativeBattleShotWidgetUpdated update)
            {
                DispatchCanonical(() => host.PublishShotWidget(update));
            }

            void DispatchDuelWidget(NativeBattleDuelWidgetUpdated update)
            {
                DispatchCanonical(() => host.PublishDuelWidget(update));
            }

            void DispatchRenzokukenUi(ImmutableArray<NativeBattleRenzokukenWidgetStepped> steps, long epoch)
            {
                DispatchCanonical(() => host.PublishRenzokukenUi(steps, epoch));
            }

            void DispatchCanonical(Action callback)
            {
                RequireSameHostedTimerClock(NativeExamTimer, host.ReadTimer());
                host.PublishTimer(NativeExamTimer);
                canonicalHandoff = true;
                try
                {
                    DispatchSharedState(callback);
                }
                finally
                {
                    canonicalHandoff = false;
                    NativeExamTimer = host.ReadTimer();
                    SetGunbladeMode(host.ReadGunbladeMode());
                }
            }

            void DispatchClockReady()
            {
                DispatchSharedState(host.ActivateClock);
            }

            void DispatchClockPoll(NativeBattleUiServices ui)
            {
                DispatchSharedState(() => host.PollClock(ui));
            }

            void DispatchSharedState(Action callback)
            {
                host.PublishSharedState(ObserveSharedState());
                try
                {
                    callback();
                }
                finally
                {
                    ImportSharedState(host.ReadSharedState());
                }
            }
        }
        finally
        {
            host.PublishSharedState(ObserveSharedState());
            advancing = false;
        }
    }

    private void RequireAdmissionWindow()
    {
        if (advancing && !canonicalHandoff)
        {
            throw new InvalidOperationException("Hosted admission requires an idle process or a canonical frame handoff.");
        }
    }

    private void RequireIdle()
    {
        if (advancing)
        {
            throw new InvalidOperationException("A hosted presentation frame is already advancing.");
        }
    }
}
