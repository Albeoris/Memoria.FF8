using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleModelLifecycleServices? hostedFrameServices;

    internal long AccumulateHostedPresentationElapsed(TimeSpan elapsed)
    {
        if (!hostedBattlePresentation || NativeGraphics is not { } graphics)
        {
            throw new InvalidOperationException("Hosted cadence requires its initialized presentation process.");
        }
        NativeBattleGraphicsPresentationStep step = NativeBattleGraphicsPresentationClock.Advance(graphics.PresentationSubticks, graphics.PresentationHalfSubtick, elapsed);
        NativeGraphics = graphics with { PresentationSubticks = step.Subticks, PresentationHalfSubtick = step.HalfSubtick };
        return step.Callbacks;
    }

    internal NativeBattleEffectPresentationStep ReadAndClearHostedPresentationFrameOutputs()
    {
        if (!hostedBattlePresentation)
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

    internal NativeBattleModelLifecycleServices AdvanceHostedBattlePresentationFrame(BattleSession logicalOwner, NativeExamTimerState? timerObservation, Action? beforeCore = null, Action<NativeBattleUiServices, bool>? clockPoll = null, Action? clockReady = null, Action<NativeBattleModelLifecycleServices>? afterGraphics = null)
    {
        RequireHostedCompletionAcknowledged();
        if (hostedFrameServices is not null || nativePreRootStack is not null)
        {
            throw new InvalidOperationException("A hosted battle frame is already advancing.");
        }
        if (!hostedBattlePresentation || NativeGraphics is not { } graphics)
        {
            throw new InvalidOperationException("Hosted battle frames require their initialized graphics process and roster.");
        }
        BattleModelRoster roster = logicalOwner.ReadBattlePresentationRoster();
        NativeBattleModelLifecycleServices services = logicalOwner.CaptureModelLifecycleServices(roster);
        services.Validate(roster, graphics);

        hostedPreparationEpoch = checked(hostedPreparationEpoch + 1);
        NativeExamTimer = graphics.Callback == 0
            ? timerObservation?.SetDisplayIntensity4A6CB0(0).SetDisplayEnabled4A6CC0(false)
            : timerObservation;
        BeginNativePcUiFrame();
        BeginNativeFrameStack();
        try
        {
            BeginNativeHostPause();
            if (beforeCore is null)
            {
                hostedFrameServices = services;
            }
            for (int pre = 0; pre < 3; pre++)
            {
                AdvanceHostedUiPublication(clockPoll, postCore: false);
            }
            nativePreCoreUiStack = false;
            if (NativeGraphics!.UiServices?.PauseDE9 != true)
            {
                if (beforeCore is not null)
                {
                    beforeCore();
                    roster = logicalOwner.ReadBattlePresentationRoster();
                    services = logicalOwner.CaptureModelLifecycleServices(roster);
                    services.Validate(roster, NativeGraphics!);
                    hostedFrameServices = services;
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
                    DispatchNativeScriptTasks();
                }
                PumpNativeFileIo482590();
                AdvanceNativeGraphicsFrameCore(services);
                if (hostedTerminal is not null)
                {
                    AdvanceNativeOutcomeCountdown47CCB0();
                    ObserveHostedTerminalDrain(services);
                }
                if (afterGraphics is not null)
                {
                    hostedFrameServices = null;
                    try
                    {
                        afterGraphics(services);
                    }
                    finally
                    {
                        hostedFrameServices = services;
                    }
                }
                if (NativeGraphics is { UiServices: { } ui } published && (published.Flags & 4) != 0)
                {
                    uint duration = definition.NativeGraphics!.Rules.UiPublicationDuration ?? throw new NotSupportedException("Native500900 publication requires its original B8A3E4 duration DWORD.");
                    NativeGraphics = published with { UiServices = ui.QueuePublication(duration) };
                }
            }
            AdvanceHostedUiPublication(clockPoll, postCore: true);
            CompleteNativeHostPause();
            PublishNativePcUiFrame();
            RequireSameHostedTimerClock(NativeExamTimer, clockPoll is null ? timerObservation : logicalOwner.NativeExamTimer);
        }
        finally
        {
            hostedFrameServices = null;
            EndNativeFrameStack();
        }
        return services;
    }

    private void AdvanceHostedUiPublication(Action<NativeBattleUiServices, bool>? clockPoll, bool postCore)
    {
        if (NativeGraphics is { UiServices: { } ui } graphics)
        {
            ui = ui.AdvancePublication().ApplySelect4A8C50();
            NativeGraphics = graphics with { UiServices = ui with { HeldButtons16 = hostedGuardianBoostMappedButtons } };
            if (!ui.PauseDE9 && (ui.Flags942 & 0x40) != 0)
            {
                AdvanceNativeTriggerWidget();
                AdvanceNativeGuardianBoost();
            }
            clockPoll?.Invoke(NativeGraphics!.UiServices!, postCore);
        }
    }
}
