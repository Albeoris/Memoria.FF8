using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>Owns the native presentation process for one expanded battle.</summary>
public sealed partial class ExpandedBattlePresentationProcess
{
    internal void ValidateGuardianFormEntryResources(BattleSession.HostedGuardianFormVisualOwner owner)
    {
        RequireAdmissionWindow();
        presentation.ValidateHostedGuardianFormEntryResources(owner);
    }

    internal BattleSession.PreparedHostedGuardianFormChange PrepareGuardianFormChange(BattleSession.HostedGuardianFormVisualChange change)
    {
        RequireAdmissionWindow();
        return presentation.PrepareHostedGuardianFormChange(change);
    }

    internal void ValidatePreparedGuardianFormChange(BattleSession.PreparedHostedGuardianFormChange prepared)
    {
        RequireAdmissionWindow();
        presentation.ValidatePreparedHostedGuardianFormChange(prepared);
    }

    internal void CommitGuardianFormChange(BattleSession.PreparedHostedGuardianFormChange prepared)
    {
        presentation.CommitHostedGuardianFormChange(prepared);
    }
    internal bool CanSkipAttachedGuardianFormFinisher => presentation.CanSkipAttachedGuardianFormFinisher;
    internal bool SkippingAttachedGuardianFormFinisher => presentation.SkippingAttachedGuardianFormFinisher;

    internal bool RequestAttachedGuardianFormSkip(BattleSession.HostedGuardianFormFrameFacts facts)
    {
        RequireIdle();
        return presentation.RequestAttachedGuardianFormSkip(facts);
    }
    private readonly BattleSession logicalOwner;
    private readonly BattleSession presentation;
    private bool entryMusicPublished;
    private bool advancing;
    private bool canonicalHandoff;

    private readonly Func<BattleSession, BattleModelOwner, BattleRosterSlot, NativeBattleGraphicsBinding>? prepareModelBinding;

    public ExpandedBattlePresentationProcess(BattleSession logicalOwner, BattleDefinition presentationResources, Func<BattleSession, BattleModelOwner, BattleRosterSlot, NativeBattleGraphicsBinding>? prepareModelBinding = null)
    {
        ArgumentNullException.ThrowIfNull(logicalOwner);
        this.prepareModelBinding = prepareModelBinding;
        ArgumentNullException.ThrowIfNull(presentationResources);
        this.logicalOwner = logicalOwner;
        presentation = BattleSession.CreateHostedBattlePresentation(logicalOwner, presentationResources);
        presentation.ValidateHostedPresentationRandomOwner(logicalOwner);
    }

    internal NativeBattleGraphicsBinding PrepareModelBinding(BattleSession owner, BattleModelOwner actor, BattleRosterSlot slot)
    {
        return (prepareModelBinding ?? throw new NotSupportedException("The presentation process has no installed model resource provider."))(owner, actor, slot);
    }

    public NativeBattleGraphicsState Graphics => presentation.NativeGraphics!;
    public NativeBattleEffectMemory Memory => presentation.NativeEffectMemory!;
    public NativeBattleCameraState Camera => presentation.NativeCamera!;
    public NativeBattleStageState Stage => presentation.NativeStage!;
    public NativeExamTimerState? Timer => presentation.NativeExamTimer;
    public bool EntryReady => Graphics.EntryReady;
    public bool HasActiveAction => ActiveActionToken is not null;
    public NativeBattlePreparedPresentation? PreparedPresentation => presentation.PreparedPresentation;
    public NativeBattleEffectPresentationStep LastStep { get; private set; } = new([], []);

    internal ulong GlobalRandomCalls => presentation.NativeGlobalRandomChange!.Calls;
    internal NativeGlobalRandomChange? GlobalRandomChange => presentation.NativeGlobalRandomChange;
    internal NativeCrtRandomState? CurrentCrtRandom => presentation.CurrentCrtRandom;
    internal NativeBattleModelCpuResources ModelCpuResources => presentation.NativeModelCpuResources!;
    internal NativeBattleModelTextureCache ModelTextureCache => presentation.NativeModelTextureCache!;
    internal long? ActiveActionToken => presentation.HostedActiveActionToken;
    internal long? LastStepActionToken { get; private set; }
    internal NativeBattleModelLifecycleServices? LastFrameServices { get; private set; }
    internal bool LastPostUiVitalsAdmitted { get; private set; }
    internal NativeBattleTriggerWidget? TriggerWidget => Graphics.TriggerWidget;
    internal NativeBattleTriggerReceipt? PendingTrigger => Graphics.PendingTrigger;
    internal NativeBattlePresentationCompletionRequested? PendingCompletion => presentation.PendingHostedCompletion;
    internal ushort? TriggerOptions => presentation.HostedTriggerOptions;

    internal void RequireLogicalOwner(BattleSession owner)
    {
        if (!ReferenceEquals(logicalOwner, owner))
        {
            throw new InvalidOperationException("Hosted presentation requires its original logical battle owner.");
        }
    }

    internal NativeBattleMusicProcessState? ReadPendingEntryMusic()
    {
        return entryMusicPublished ? null : presentation.RequireHostedEntryMusic(logicalOwner);
    }

    internal void PublishEntryMusic(NativeBattleMusicProcessState? music)
    {
        if (music is not null)
        {
            logicalOwner.PublishHostedEntryMusic(music);
            entryMusicPublished = true;
        }
    }

    internal long AccumulateElapsed(TimeSpan elapsed)
    {
        RequireIdle();
        RequireCompletionAcknowledged();
        return presentation.AccumulateHostedPresentationElapsed(elapsed);
    }

    internal void RequireCompletionAcknowledged()
    {
        presentation.RequireHostedCompletionAcknowledged();
    }

    internal long BeginAction(NativeBattleActionHeaderPreparation header, ImmutableArray<NativeBattleContactPacket> contacts)
    {
        RequireAdmissionWindow();
        return presentation.BeginHostedAction(header, contacts);
    }

    internal BattleSession.PreparedHostedDispatch PrepareAction(NativeBattleActionHeaderPreparation header, ImmutableArray<NativeBattleContactPacket> contacts)
    {
        RequireAdmissionWindow();
        return presentation.PrepareHostedAction(header, contacts);
    }

    internal void ValidatePreparedAction(BattleSession.PreparedHostedDispatch prepared)
    {
        RequireAdmissionWindow();
        presentation.ValidatePreparedHostedAction(prepared);
    }

    internal long PublishAction(BattleSession.PreparedHostedDispatch prepared)
    {
        RequireAdmissionWindow();
        return presentation.PublishHostedAction(prepared);
    }

    internal bool IsActionComplete(long token)
    {
        return presentation.IsHostedActionComplete(token);
    }

    internal BattleSession.PreparedHostedDispatch PrepareDispatch(ImmutableArray<BattleSession.HostedDispatchHeader> headers, BattleActionHeaderReference? headerReference = null, bool dispatchAppended = false, BattleSession.HostedDispatchCompletion? completion = null, BattleSession.PreparedHostedCompletionAck? acknowledgment = null, BattleSession.HostedDispatchControls? controls = null, bool publishEventHeaders = false, ImmutableArray<BattleSession.HostedModelLoadAdmission> allocations = default, NativeBattleDuelSelectionReached? duelSelection = null, BattleSession.HostedRenzokukenFinisherReplacement? renzokukenFinisher = null, BattleSession.PreparedHostedGuardianFormChange? guardianForm = null)
    {
        RequireAdmissionWindow();
        return presentation.PrepareHostedDispatch(headers, headerReference, dispatchAppended, completion, acknowledgment, controls: controls, publishEventHeaders: publishEventHeaders, allocations: allocations, duelSelection: duelSelection, renzokukenFinisher: renzokukenFinisher, guardianForm: guardianForm);
    }

    internal BattleModelRoster PlannedModelRoster => presentation.HostedPlannedModelRoster;

    internal bool IsControlPrefixRetired => presentation.HostedControlPrefixRetired;
    internal NativeBattleDuelSelectionReached? PendingDuelSelection => presentation.PendingHostedDuelSelection;
    internal NativeBattleRenzokukenFinisherReached? PendingRenzokukenFinisher => presentation.PendingHostedRenzokukenFinisher;
    internal NativeBattleActionPacketsState ActionPackets => presentation.NativeActionPackets;

    internal void AppendScriptMessage(BattleEvent message, byte tag, byte priority)
    {
        RequireAdmissionWindow();
        presentation.AppendHostedScriptMessage(message, tag, priority);
    }

    internal void ValidatePreparedDispatch(BattleSession.PreparedHostedDispatch prepared)
    {
        RequireAdmissionWindow();
        presentation.ValidatePreparedHostedDispatch(prepared);
    }

    internal void CommitDispatch(BattleSession.PreparedHostedDispatch prepared)
    {
        presentation.CommitHostedDispatch(prepared);
    }

    internal BattleSession.PreparedHostedDispatch PrepareCompletion(uint callback, BattleModelOwner owner, string actorId)
    {
        RequireAdmissionWindow();
        return presentation.PrepareHostedCompletion(callback, owner, actorId);
    }

    internal long PublishCompletion(BattleSession.PreparedHostedDispatch prepared)
    {
        RequireAdmissionWindow();
        return presentation.PublishHostedCompletion(prepared);
    }

    internal void ValidatePreparedCompletion(BattleSession.PreparedHostedDispatch prepared)
    {
        RequireAdmissionWindow();
        presentation.ValidatePreparedHostedCompletion(prepared);
    }

    internal bool IsCompletionQueued(long token)
    {
        return presentation.IsHostedCompletionQueued(token);
    }

    internal bool IsCompletionAcknowledged(long token)
    {
        return presentation.IsHostedCompletionAcknowledged(token);
    }

    internal BattleSession.PreparedHostedCompletionAck PrepareCompletionAck(long token, bool resetCounts)
    {
        RequireAdmissionWindow();
        return presentation.PrepareHostedCompletionAck(token, resetCounts);
    }

    internal void ValidatePreparedCompletionAck(BattleSession.PreparedHostedCompletionAck prepared)
    {
        RequireAdmissionWindow();
        presentation.ValidatePreparedHostedCompletionAck(prepared);
    }

    internal void PublishCompletionAck(BattleSession.PreparedHostedCompletionAck prepared)
    {
        RequireAdmissionWindow();
        presentation.PublishHostedCompletionAck(prepared);
    }

    internal void PressGunblade(long token, BattleModelOwner actor, NativeBattleTriggerWidget widget)
    {
        RequireIdle();
        presentation.PressHostedGunblade(token, actor, widget);
    }

    internal BattleSession.PreparedHostedContactReplacement PrepareRecomputedContacts(long token, ImmutableArray<NativeBattleContactPacket> contacts, ImmutableArray<BattleSession.HostedDispatchHeader> append, NativeBattleTriggerReceipt? trigger = null, BattleSession.HostedGuardianBoostRequest? guardian = null, ImmutableArray<BattleSession.HostedModelLoadAdmission> allocations = default, BattleSession.PreparedHostedGuardianFormChange? guardianForm = null)
    {
        RequireAdmissionWindow();
        return presentation.PrepareHostedContactReplacement(token, contacts, trigger, append, guardian, allocations, guardianForm);
    }

    internal void ValidatePreparedRecomputedContacts(BattleSession.PreparedHostedContactReplacement prepared)
    {
        RequireAdmissionWindow();
        presentation.ValidatePreparedHostedContactReplacement(prepared);
    }

    internal void CommitRecomputedContacts(BattleSession.PreparedHostedContactReplacement prepared)
    {
        presentation.CommitHostedContactReplacement(prepared);
    }

    internal void SetActionMenu(bool open)
    {
        RequireIdle();
        presentation.SetNativeActionMenu(open);
    }

    internal NativeBattleModelLifecycleServices Advance(BattleSession logicalOwner, NativeExamTimerState? timerObservation = null, Action? beforeCore = null, Action<NativeBattleModelLifecycleServices, long?>? afterGraphics = null)
    {
        ArgumentNullException.ThrowIfNull(logicalOwner);
        RequireLogicalOwner(logicalOwner);
        RequireIdle();
        RequireCompletionAcknowledged();
        if (PendingDuelSelection is not null)
        {
            throw new InvalidOperationException("A reached hosted Duel selection must commit before its next graphics frame.");
        }
        if (PendingRenzokukenFinisher is not null)
        {
            throw new InvalidOperationException("A reached hosted Renzokuken finisher must commit before its next graphics frame.");
        }

        advancing = true;
        try
        {
            PublishEntryMusic(ReadPendingEntryMusic());
            LastPostUiVitalsAdmitted = false;
            presentation.ImportHostedPresentationSharedState(logicalOwner);
            presentation.SetGunbladeMode(logicalOwner.GunbladeMode);
            try
            {
                long? actionToken = ActiveActionToken;
                bool attached = ReferenceEquals(logicalOwner.ExpandedPresentationProcess, this);
                NativeBattleModelLifecycleServices services = presentation.AdvanceHostedBattlePresentationFrame(logicalOwner, timerObservation ?? logicalOwner.NativeExamTimer, beforeCore is null ? null : DispatchBeforeCore, attached ? DispatchClockPoll : null, attached ? DispatchClockReady : null, afterGraphics is null ? null : DispatchAfterGraphics, attached ? DispatchRenzokukenUi : null);
                presentation.RefreshHostedPreparedPresentation(logicalOwner, services);
                LastStep = presentation.ReadAndClearHostedPresentationFrameOutputs();
                LastStepActionToken = actionToken;
                LastFrameServices = services;
                return services;

                void DispatchBeforeCore()
                {
                    DispatchCanonical(beforeCore!);
                    actionToken = ActiveActionToken;
                }

                void DispatchAfterGraphics(NativeBattleModelLifecycleServices currentServices)
                {
                    try
                    {
                        DispatchCanonical(() => afterGraphics!(currentServices, actionToken));
                    }
                    catch
                    {
                        presentation.ClearHostedPresentationFrameOutputs();
                        throw;
                    }
                }

                void DispatchRenzokukenUi(ImmutableArray<NativeBattleRenzokukenWidgetStepped> steps, long epoch)
                {
                    DispatchCanonical(() => logicalOwner.ApplyExpandedHostedRenzokukenWidgetSteps(steps, epoch));
                }

                void DispatchCanonical(Action callback)
                {
                    if (attached)
                    {
                        BattleSession.RequireSameHostedTimerClock(presentation.NativeExamTimer, logicalOwner.NativeExamTimer);
                        logicalOwner.CopyHostedPresentationTimerFrom(presentation);
                    }
                    presentation.ExportHostedPresentationSharedState(logicalOwner);
                    canonicalHandoff = true;
                    try
                    {
                        callback();
                    }
                    finally
                    {
                        canonicalHandoff = false;
                        if (attached)
                        {
                            presentation.CopyHostedPresentationTimerFrom(logicalOwner);
                        }
                        presentation.CopyHostedPresentationSharedStateFrom(logicalOwner);
                        presentation.SetGunbladeMode(logicalOwner.GunbladeMode);
                    }
                }

                void DispatchClockReady()
                {
                    DispatchSharedState(logicalOwner.ActivateExpandedHostedClock);
                }

                void DispatchClockPoll(NativeBattleUiServices ui, bool postCore)
                {
                    if (postCore)
                    {
                        LastPostUiVitalsAdmitted = !ui.PauseDE9 && (ui.Flags942 & 0x40) != 0;
                    }
                    DispatchSharedState(() => logicalOwner.AdvanceExpandedUiAtbService(ui));
                }

                void DispatchSharedState(Action callback)
                {
                    presentation.ExportHostedPresentationSharedState(logicalOwner);
                    try
                    {
                        callback();
                    }
                    finally
                    {
                        presentation.CopyHostedPresentationSharedStateFrom(logicalOwner);
                    }
                }
            }
            finally
            {
                presentation.ExportHostedPresentationSharedState(logicalOwner);
            }
        }
        finally
        {
            advancing = false;
        }
    }

    internal NativeBattleRetainedMemory CaptureRetainedMemory(BattleSession owner, string? coldExecutableSha256)
    {
        RequireLogicalOwner(owner);
        RequireIdle();
        RequireCompletionAcknowledged();
        if (ActiveActionToken is not null)
        {
            throw new InvalidOperationException("The hosted action must complete before capturing retained field memory.");
        }
        return presentation.CaptureHostedRetainedMemory(owner, coldExecutableSha256);
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
