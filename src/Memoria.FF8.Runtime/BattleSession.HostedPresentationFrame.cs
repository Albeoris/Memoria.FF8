using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
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
        presentationContacts.Clear();
        presentationSounds.Clear();
        presentationSequences.Clear();
        presentationActivations.Clear();
        return step;
    }

    private NativeBattleModelLifecycleServices AdvanceHostedBattlePresentation(BattleSession logicalOwner, NativeExamTimerState? timerObservation)
    {
        if (!hostedBattlePresentation || NativeGraphics is not { PresentationRoster: { } roster } graphics)
        {
            throw new InvalidOperationException("Hosted battle frames require their initialized graphics process and roster.");
        }
        NativeBattleModelLifecycleServices services = logicalOwner.CaptureExpandedModelLifecycleServices(roster);
        services.Validate(roster, graphics);

        NativeExamTimer = graphics.Callback == 0
            ? timerObservation?.SetDisplayIntensity4A6CB0(0).SetDisplayEnabled4A6CC0(false)
            : timerObservation;
        BeginNativePcUiFrame();
        BeginNativeFrameStack();
        try
        {
            for (int pre = 0; pre < 3; pre++)
            {
                AdvanceHostedUiPublication();
            }
            nativePreCoreUiStack = false;

            if (NativeGraphics is { EntryLifecycle: 3 } entered)
            {
                NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Native47CCB0 case3 requires its retained action-phase process byte.");
                NativeGraphics = entered with { EntryLifecycle = 4, EntryReady = true };
                NativeEffectMemory = memory with { ActionPhaseDEA = 1 };
            }

            PumpNativeFileIo482590();
            AdvanceNativeGraphicsFrameCore(services);
            if (NativeGraphics is { UiServices: { } ui } published && (published.Flags & 4) != 0)
            {
                uint duration = definition.NativeGraphics!.Rules.UiPublicationDuration ?? throw new NotSupportedException("Native500900 publication requires its original B8A3E4 duration DWORD.");
                NativeGraphics = published with { UiServices = ui.QueuePublication(duration) };
            }
            AdvanceHostedUiPublication();
            PublishNativePcUiFrame();
            if (NativeExamTimer is { } timer && (timerObservation is null || !timer.HasSameClock(timerObservation)))
            {
                throw new InvalidDataException("Hosted timer display changed its authoritative clock.");
            }
        }
        finally
        {
            EndNativeFrameStack();
        }
        return services;
    }

    private void AdvanceHostedUiPublication()
    {
        if (NativeGraphics is { UiServices: { } ui } graphics)
        {
            NativeGraphics = graphics with { UiServices = ui.AdvancePublication() };
        }
    }
}
