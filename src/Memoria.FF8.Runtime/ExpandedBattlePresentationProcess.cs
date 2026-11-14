using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>Owns the native presentation process for one expanded battle.</summary>
public sealed class ExpandedBattlePresentationProcess
{
    private readonly BattleSession logicalOwner;
    private readonly BattleSession presentation;
    private bool advancing;

    public ExpandedBattlePresentationProcess(BattleSession logicalOwner, BattleDefinition presentationResources)
    {
        ArgumentNullException.ThrowIfNull(logicalOwner);
        ArgumentNullException.ThrowIfNull(presentationResources);
        this.logicalOwner = logicalOwner;
        presentation = BattleSession.CreateHostedBattlePresentation(logicalOwner, presentationResources);
        presentation.ValidateHostedPresentationRandomOwner(logicalOwner);
    }

    public NativeBattleGraphicsState Graphics => presentation.NativeGraphics!;
    public NativeBattleEffectMemory Memory => presentation.NativeEffectMemory!;
    public NativeBattleCameraState Camera => presentation.NativeCamera!;
    public NativeBattleStageState Stage => presentation.NativeStage!;
    public NativeExamTimerState? Timer => presentation.NativeExamTimer;
    public bool EntryReady => Graphics.EntryReady;
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
        return presentation.BeginHostedAction(header, contacts);
    }

    internal BattleSession.PreparedHostedAction PrepareAction(NativeBattleActionHeaderPreparation header, ImmutableArray<NativeBattleContactPacket> contacts)
    {
        return presentation.PrepareHostedAction(header, contacts);
    }

    internal void ValidatePreparedAction(BattleSession.PreparedHostedAction prepared)
    {
        presentation.ValidatePreparedHostedAction(prepared);
    }

    internal long PublishAction(BattleSession.PreparedHostedAction prepared)
    {
        return presentation.PublishHostedAction(prepared);
    }

    internal bool IsActionComplete(long token)
    {
        return presentation.IsHostedActionComplete(token);
    }

    internal BattleSession.PreparedHostedCompletion PrepareCompletion(uint callback, int row, string actorId)
    {
        RequireIdle();
        return presentation.PrepareHostedCompletion(callback, row, actorId);
    }

    internal long PublishCompletion(BattleSession.PreparedHostedCompletion prepared)
    {
        RequireIdle();
        return presentation.PublishHostedCompletion(prepared);
    }

    internal void ValidatePreparedCompletion(BattleSession.PreparedHostedCompletion prepared)
    {
        RequireIdle();
        presentation.ValidatePreparedHostedCompletion(prepared);
    }

    internal bool IsCompletionAcknowledged(long token)
    {
        return presentation.IsHostedCompletionAcknowledged(token);
    }

    internal BattleSession.PreparedHostedCompletionAck PrepareCompletionAck(long token, bool resetCounts)
    {
        RequireIdle();
        return presentation.PrepareHostedCompletionAck(token, resetCounts);
    }

    internal void ValidatePreparedCompletionAck(BattleSession.PreparedHostedCompletionAck prepared)
    {
        RequireIdle();
        presentation.ValidatePreparedHostedCompletionAck(prepared);
    }

    internal void PublishCompletionAck(BattleSession.PreparedHostedCompletionAck prepared)
    {
        RequireIdle();
        presentation.PublishHostedCompletionAck(prepared);
    }

    internal void PressGunblade(long token, byte actor, NativeBattleTriggerWidget widget)
    {
        RequireIdle();
        presentation.PressHostedGunblade(token, actor, widget);
    }

    internal BattleSession.PreparedHostedContactReplacement PrepareGunbladeContact(long token, NativeBattleContactPacket packet, NativeBattleTriggerReceipt receipt)
    {
        return presentation.PrepareHostedContactReplacement(token, [packet], receipt);
    }

    internal void ValidateGunbladeContact(BattleSession.PreparedHostedContactReplacement prepared)
    {
        presentation.ValidatePreparedHostedContactReplacement(prepared);
    }

    internal void CommitGunbladeContact(BattleSession.PreparedHostedContactReplacement prepared)
    {
        presentation.CommitHostedContactReplacement(prepared);
    }

    internal void SetActionMenu(bool open)
    {
        RequireIdle();
        presentation.SetNativeActionMenu(open);
    }

    internal NativeBattleModelLifecycleServices Advance(BattleSession logicalOwner, NativeExamTimerState? timerObservation = null, Action? beforeCore = null)
    {
        ArgumentNullException.ThrowIfNull(logicalOwner);
        RequireLogicalOwner(logicalOwner);
        RequireIdle();
        RequireCompletionAcknowledged();

        advancing = true;
        try
        {
            LastPostUiVitalsAdmitted = false;
            presentation.ImportHostedPresentationRandom(logicalOwner);
            presentation.SetGunbladeMode(logicalOwner.GunbladeMode);
            try
            {
                long? actionToken = ActiveActionToken;
                bool attached = ReferenceEquals(logicalOwner.ExpandedPresentationProcess, this);
                NativeBattleModelLifecycleServices services = presentation.AdvanceHostedBattlePresentationFrame(logicalOwner, timerObservation ?? logicalOwner.NativeExamTimer, beforeCore is null ? null : DispatchBeforeCore, attached ? DispatchClockPoll : null, attached ? logicalOwner.ActivateExpandedHostedClock : null);
                presentation.RefreshHostedPreparedPresentation(logicalOwner, services);
                LastStep = presentation.ReadAndClearHostedPresentationFrameOutputs();
                LastStepActionToken = actionToken;
                LastFrameServices = services;
                return services;

                void DispatchBeforeCore()
                {
                    if (attached)
                    {
                        BattleSession.RequireSameHostedTimerClock(presentation.NativeExamTimer, logicalOwner.NativeExamTimer);
                        logicalOwner.CopyHostedPresentationTimerFrom(presentation);
                    }
                    presentation.ExportHostedPresentationRandom(logicalOwner);
                    try
                    {
                        beforeCore();
                        actionToken = ActiveActionToken;
                    }
                    finally
                    {
                        if (attached)
                        {
                            presentation.CopyHostedPresentationTimerFrom(logicalOwner);
                        }
                        presentation.CopyHostedPresentationRandomFrom(logicalOwner);
                        presentation.SetGunbladeMode(logicalOwner.GunbladeMode);
                    }
                }

                void DispatchClockPoll(NativeBattleUiServices ui, bool postCore)
                {
                    if (postCore)
                    {
                        LastPostUiVitalsAdmitted = !ui.PauseDE9 && (ui.Flags942 & 0x40) != 0;
                    }
                    presentation.ExportHostedPresentationRandom(logicalOwner);
                    try
                    {
                        logicalOwner.AdvanceExpandedUiAtbService(ui);
                    }
                    finally
                    {
                        presentation.CopyHostedPresentationRandomFrom(logicalOwner);
                    }
                }
            }
            finally
            {
                presentation.ExportHostedPresentationRandom(logicalOwner);
            }
        }
        finally
        {
            advancing = false;
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
