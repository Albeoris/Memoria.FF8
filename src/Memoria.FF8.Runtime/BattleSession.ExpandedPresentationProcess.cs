namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private BattlePresentationProcess? expandedPresentationProcess;
    private bool dispatchingExpandedPresentation;
    private Func<BattleSession, BattleModelOwner, BattleRosterSlot, NativeBattleGraphicsBinding>? preparePresentationModelBinding;

    private BattlePresentationSharedState ObserveBattlePresentationSharedState()
    {
        return new(nativeCrtRandom, nativeBattleUiRandom, nativeGlobalRandom, nativeGlobalRandomCalls, NativeMusicProcess, NativeGlobalSoundProcess, NativeVibrationProcess);
    }

    private void PublishBattlePresentationSharedState(BattlePresentationSharedState state)
    {
        nativeCrtRandom = state.CrtRandom;
        nativeBattleUiRandom = state.UiRandom;
        nativeGlobalRandom = state.GlobalRandom;
        nativeGlobalRandomCalls = state.GlobalRandomCalls;
        NativeMusicProcess = state.Music;
        NativeGlobalSoundProcess = state.Sound;
        NativeVibrationProcess = state.Vibration;
    }

    private NativeBattleGraphicsBinding PrepareModelBinding(BattleModelOwner actor, BattleRosterSlot slot)
    {
        return (preparePresentationModelBinding ?? throw new NotSupportedException("The battle has no installed model resource provider."))(this, actor, slot);
    }

    public BattlePresentationProcess? Presentation => expandedPresentationProcess;

    public void AttachPresentation(BattlePresentationResources resources, Func<BattleSession, BattleModelOwner, BattleRosterSlot, NativeBattleGraphicsBinding>? prepareModelBinding = null)
    {
        ArgumentNullException.ThrowIfNull(resources);
        if (expandedPresentationProcess is not null || Frame != 0 || NativeHostCallbacks != 0 || ActiveAction is not null)
        {
            throw new InvalidOperationException("Hosted presentation can attach only before the expanded logical battle's first callback.");
        }

        var (roster, aliases) = ReadBattlePresentationPacketBindings();
        BattlePresentationEntry entry = new(roster, aliases, CaptureHostedEntryFacts(roster), NativeEntryAdvantage, ResolvedNativeFlags, nativeEffectResourceResolver);
        BattlePresentationServices services = CreateBattlePresentationServices();
        BattlePresentationProcess process = new(resources, entry, services);
        NativeMusicProcess = process.ReadEntryMusic(NativeMusicProcess);
        this.preparePresentationModelBinding = prepareModelBinding;
        InitializeExpandedPartyPresentation();
        NativeExamTimer = definition.NativeExamTimerAtEntry;
        expandedPresentationProcess = process;
    }

    private BattlePresentationServices CreateBattlePresentationServices()
    {
        return new(CaptureHostedModelLifecycleServices, ObserveBattlePresentationSharedState, PublishBattlePresentationSharedState,
            () => NativeExamTimer, timer => NativeExamTimer = timer, () => GunbladeMode,
            AdvanceExpandedUiAtbService, ActivateExpandedHostedClock, AdvanceExpandedPresentedVitals4B0500, CaptureExpandedHostedRenzokukenFrame, ApplyExpandedHostedRenzokukenWidgetSteps,
            () => (Frame, GuardianAnimationSkipPending), () => NativeHostCallbacks ?? Frame, ReadPreparedActor, ReadPreparedScanActor, ReadPreparedScanExtra,
            () => DiagnosticEffectDispatched is not null, ObservePresentationEffect,
            ObservePresentationDescriptorQueued, ObservePresentationDescriptorRemoved, ObservePresentationFrameEnded, ReadPresentationStackVitals, MatchesDiagnosticVisualEntry, AdmitDiagnosticVisualEntry,
            ReadNativeShotWidgetFacts, ApplyNativeShotWidgetUpdate);
    }

    private void RequireExpandedPresentationDispatchIdle()
    {
        if (dispatchingExpandedPresentation)
        {
            throw new InvalidOperationException("An expanded battle presentation frame is already advancing.");
        }
    }

    private void DispatchExpandedPresentationFrame(BattlePresentationProcess process)
    {
        RequireExpandedPresentationDispatchIdle();
        if (process.TerminalFinished && Outcome != BattleOutcome.Active)
        {
            return;
        }
        process.RequireCompletionAcknowledged();
        dispatchingExpandedPresentation = true;
        try
        {
            AdvanceNativeHostTime();
            process.Advance(DispatchExpandedCore, ApplyExpandedHostedFrameReceipts);
            NativeExamTimerState? presentedTimer = process.Timer;
            RequireSameHostedTimerClock(presentedTimer, NativeExamTimer);
            NativeExamTimer = presentedTimer;

            void DispatchExpandedCore()
            {
                DispatchExpandedGuardianBoost(process);
                if (process.EntryReady && Outcome == BattleOutcome.Active && !HasPendingExpandedTerminal)
                {
                    DispatchTurnBasedFrameCore();
                }
            }
        }
        catch
        {
            AbortEncounterAllocation();
            throw;
        }
        finally
        {
            dispatchingExpandedPresentation = false;
        }
    }

    private (NativeBattlePartyPresentation Presentation, bool[] Guardian) ReadPresentationStackVitals()
    {
        var rows = new NativeBattlePartyPresentedVital?[NativeBattleSlots.AllyCount];
        bool[] guardian = new bool[NativeBattleSlots.AllyCount];
        foreach (BattleCombatant actor in allies)
        {
            if (actor.NativeActorIndex is int slot && slot < rows.Length)
            {
                rows[slot] = RequireExpandedPartyPresentedRow(expandedPartyPresentation ?? throw new InvalidOperationException("Presented vitals have no current owner."), actor.Id);
                guardian[slot] = guardianForceCasts.ContainsKey(actor.Id);
            }
        }
        return (new NativeBattlePartyPresentation(System.Collections.Immutable.ImmutableArray.CreateRange(rows)), guardian);
    }

    private void ObserveNativeUnknownStackCallback(uint callback, string phase)
    {
        Presentation?.ObserveLogicalStackCall(callback, phase);
    }
}
