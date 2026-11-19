using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattlePresentationProcess
{
    private NativeBattleModelLifecycleServices? hostedFrameServices;

    internal long AccumulateHostedPresentationElapsed(TimeSpan elapsed)
    {
        if (!HasBattleEntry || NativeGraphics is not { } graphics)
        {
            throw new InvalidOperationException("Hosted cadence requires its initialized presentation process.");
        }
        BattlePresentationCallbackStep step = BattlePresentationTiming.Advance(graphics.PresentationSubticks, graphics.PresentationHalfSubtick, elapsed);
        NativeGraphics = graphics with { PresentationSubticks = step.Subticks, PresentationHalfSubtick = step.HalfSubtick };
        return step.Callbacks;
    }

    internal NativeBattleEffectPresentationStep ReadAndClearHostedPresentationFrameOutputs()
    {
        if (!HasBattleEntry)
        {
            throw new InvalidOperationException("Only a hosted battle process owns these frame outputs.");
        }
        NativeBattleEffectPresentationStep step = new(presentationContacts.ToImmutableArray(), presentationSounds.ToImmutableArray())
        {
            Sequences = presentationSequences.ToImmutableArray(),
            Activations = presentationActivations.ToImmutableArray()
        };
        ClearHostedPresentationFrameOutputs();
        return step;
    }

    internal void ClearHostedPresentationFrameOutputs()
    {
        presentationContacts.Clear();
        presentationSounds.Clear();
        presentationSequences.Clear();
        presentationActivations.Clear();
    }

    internal NativeBattleModelLifecycleServices AdvanceHostedBattlePresentationFrame(NativeExamTimerState? timerObservation, Action? beforeCore = null, Action<NativeBattleUiServices>? clockPoll = null, Action? clockReady = null, Action<NativeBattleModelLifecycleServices>? afterGraphics = null,
        Action<ImmutableArray<NativeBattleRenzokukenWidgetStepped>, long>? renzokukenUi = null, Action<NativeBattleShotWidgetUpdated>? shotUi = null, Action<NativeBattleDuelWidgetUpdated>? duelUi = null)
    {
        RequireHostedCompletionAcknowledged();
        if (hostedFrameServices is not null || nativePreRootStack is not null)
        {
            throw new InvalidOperationException("A hosted battle frame is already advancing.");
        }
        if (!HasBattleEntry || NativeGraphics is not { } graphics)
        {
            throw new InvalidOperationException("Hosted battle frames require their initialized graphics process and roster.");
        }
        HostedModelRegistry? registry = hostedModelRegistry;
        BattleModelRoster roster = HostedPlannedModelRoster;
        BattlePresentationServices host = services ?? throw new InvalidOperationException("A battle frame requires its bound logical services.");
        NativeBattleModelLifecycleServices frameServices = host.CaptureModels(roster, registry);
        frameServices.Validate(roster, graphics);

        hostedPreparationEpoch = checked(hostedPreparationEpoch + 1);
        NativeExamTimer = graphics.Callback == 0
            ? timerObservation?.SetDisplayIntensity4A6CB0(0).SetDisplayEnabled4A6CC0(false)
            : timerObservation;
        BeginNativePcUiFrame();
        BeginNativeFrameStack();
        try
        {
            BeginNativeHostPause();
            if (frameServices.GuardianForm is { Finishing: true } && NativeGraphics?.GuardianFormPresentation is { SkipRequested: true } && HostedGuardianBoost is { Phase: not 6 })
            {
                SettleGuardianFormSkipBoost();
            }
            if (beforeCore is null)
            {
                hostedFrameServices = frameServices;
            }
            BeginHostedRenzokukenUi(frameServices.Renzokuken);
            for (int pre = 0; pre < BattlePresentationTiming.NativeUiServicePollsPerCallback - 1; pre++)
            {
                AdvanceHostedUiPublication(host.PublishVitals, clockPoll, shotUi, duelUi);
            }
            nativePreCoreUiStack = false;
            DrainHostedRenzokukenUi(renzokukenUi);
            if (NativeGraphics!.UiServices?.PauseDE9 != true)
            {
                if (beforeCore is not null)
                {
                    beforeCore();
                    registry = hostedModelRegistry;
                    roster = HostedPlannedModelRoster;
                    frameServices = host.CaptureModels(roster, registry);
                    frameServices.Validate(roster, NativeGraphics!);
                    hostedFrameServices = frameServices;
                }

                if (NativeGraphics is { EntryLifecycle: 3 } entered)
                {
                    NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Native47CCB0 case3 requires its retained action-phase process byte.");
                    NativeGraphics = entered with { EntryLifecycle = 4, EntryReady = true };
                    NativeEffectMemory = memory with { ActionPhaseDEA = 1 };
                    clockReady?.Invoke();
                }

                if (hostedTerminal is not null)
                {
                    AdvanceTerminalScriptTasks();
                }
                NativeGraphics = SynchronizeLimitPresentationScopes(NativeGraphics!);
                PumpNativeFileIo482590();
                AdvanceNativeGraphicsFrameCore(frameServices);
                if (hostedTerminal is not null)
                {
                    AdvanceNativeOutcomeCountdown47CCB0();
                    ObserveHostedTerminalDrain(frameServices);
                }
                if (afterGraphics is not null)
                {
                    hostedFrameServices = null;
                    try
                    {
                        afterGraphics(frameServices);
                    }
                    finally
                    {
                        hostedFrameServices = frameServices;
                    }
                }
                if (NativeGraphics is { UiServices: { } ui } published && (published.Flags & 4) != 0)
                {
                    uint duration = this.resources.NativeGraphics!.Rules.UiPublicationDuration ?? throw new NotSupportedException("Native500900 publication requires its original B8A3E4 duration DWORD.");
                    NativeGraphics = published with { UiServices = ui.QueuePublication(duration) };
                }
            }
            BeginHostedRenzokukenUi(host.ReadRenzokuken());
            AdvanceHostedUiPublication(host.PublishVitals, clockPoll, shotUi, duelUi);
            DrainHostedRenzokukenUi(renzokukenUi);
            AdvanceNativeScanText537A30();
            PublishNativeScanInputFrame();
            CompleteNativeHostPause();
            PublishNativePcUiFrame();
            NativeExamTimerState.RequireSameClock(NativeExamTimer, clockPoll is null ? timerObservation : host.ReadTimer());
        }
        finally
        {
            hostedFrameServices = null;
            hostedRenzokukenUiFrame = null;
            hostedRenzokukenUiSteps = null;
            EndNativeFrameStack();
        }
        return frameServices;
    }

    private void AdvanceHostedUiPublication(Action publishVitals, Action<NativeBattleUiServices>? clockPoll, Action<NativeBattleShotWidgetUpdated>? shotUi, Action<NativeBattleDuelWidgetUpdated>? duelUi)
    {
        if (NativeGraphics is { UiServices: { } ui } graphics)
        {
            ui = ui.AdvancePublication().ApplySelect4A8C50();
            if (!ui.PauseDE9)
            {
                ui = ui.DispatchCommands4AD400();
            }
            ushort scanButtons = PublishNativeScanUiButtons() ?? 0;
            NativeGraphics = graphics with { UiServices = ui with { HeldButtons16 = (ushort)(scanButtons | hostedGuardianBoostMappedButtons) } };
            if (!ui.PauseDE9 && (ui.Flags942 & 0x40) != 0)
            {
                AdvanceNativeUiWindowCallbacks(NativeGraphics.UiServices!, publishVitals);
                if (shotUi is not null)
                {
                    AdvanceNativeShotWindow(shotUi);
                }
                if (duelUi is not null)
                {
                    AdvanceNativeDuelWindow(duelUi);
                }
            }
            clockPoll?.Invoke(NativeGraphics!.UiServices!);
            if (!ui.PauseDE9)
            {
                NativeGraphics = NativeGraphics! with { UiServices = NativeGraphics.UiServices!.QueueVitalsOpening4A84E0() };
            }
        }
    }
}
