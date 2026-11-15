namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    public bool CanQueueNativeScanInput => expandedPresentationProcess?.CanQueueScanInput ?? CanQueueLocalScanInput;

    private bool CanQueueLocalScanInput => NativeHostCallbacks is not null && NativeGraphics is { UiServices.PauseDE9: false } graphics && NativeEffectMemory?.Scan is { ControlPulse: null } scan && NativeBattleScanInputControl.IsReady(graphics.OriginalEffect, scan);

    public void QueueNativeScanInput(BattleScanButton button)
    {
        if (expandedPresentationProcess is { } process)
        {
            process.QueueScanInput(button);
            return;
        }
        if (!Enum.IsDefined(button))
        {
            throw new ArgumentOutOfRangeException(nameof(button));
        }
        if (!CanQueueNativeScanInput)
        {
            throw new InvalidOperationException("Scan input requires an active initialized native Scan viewer/result consumer at its confirmation boundary and no pending input pulse.");
        }
        _ = NativeInputConfiguration.InvertPulse(NativeBattleScanInputControl.MappedButtons(button), definition.NativeSavedOptions ?? throw new NotSupportedException("Native Scan input requires its captured saved mapping options."), definition.NativeInputButtonMap);
        NativeBattleEffectMemory memory = NativeEffectMemory!;
        NativeEffectMemory = memory with { Scan = memory.Scan! with { ControlPulse = new(button) } };
    }

    public void PublishNativeScanMappedButtons(ushort mappedButtons)
    {
        if (expandedPresentationProcess is { } process)
        {
            process.PublishScanMappedButtons(mappedButtons);
            return;
        }
        if (NativeEffectMemory?.Scan is not { } scan)
        {
            return;
        }
        ushort raw = NativeInputConfiguration.InvertPulse(mappedButtons, definition.NativeSavedOptions ?? throw new NotSupportedException("Native Scan input requires its saved mapping options."), definition.NativeInputButtonMap);
        NativeEffectMemory = NativeEffectMemory with { Scan = scan with { PhysicalButtons1CD01FC = raw } };
    }

    private ushort? PublishNativeScanUiButtons()
    {
        PublishNativeScanInputFrame();
        if (NativeEffectMemory?.Scan is not { } scan)
        {
            return null;
        }
        uint options = definition.NativeSavedOptions ?? throw new NotSupportedException("Native4A84E0 input requires its saved mapping options.");
        return NativeInputConfiguration.Project(scan.Input.Input.Read49ED30(0, 0), options, definition.NativeInputButtonMap);
    }

    private void PublishNativeScanInputFrame()
    {
        if (NativeEffectMemory?.Scan is not { } scan)
        {
            return;
        }
        NativeBattleScanInputPublicationState state = scan.Input;
        uint physicalButtons = scan.PhysicalButtons1CD01FC;
        if (scan.ControlPulse is { } pulse)
        {
            NativeBattleScanControlSample sample = NativeBattleScanInputControl.Sample(pulse, NativeHostCallbacks ?? throw new NotSupportedException("Queued Scan input requires its accepted native host callback owner."));
            scan = scan with { ControlPulse = sample.Pending };
            if (sample.MappedButtons != 0)
            {
                physicalButtons |= NativeInputConfiguration.InvertPulse(sample.MappedButtons, definition.NativeSavedOptions ?? throw new NotSupportedException("Native Scan input requires its captured saved mapping options."), definition.NativeInputButtonMap);
            }
        }
        if (!scan.InputInitialized)
        {
            state = NativeBattleScanInputPublication.Initialize49F540(state, ReadPhysical);
            NativeBattleScanConfiguredInputStep configured = NativeBattleScanInputConfiguration.Configure4B9460(state, scan.Mouse1D2B2E0);
            state = configured.State;
            scan = scan with { Mouse1D2B2E0 = configured.Mouse16 };
        }
        if (!scan.Text.Initialized)
        {
            NativeBattleScanTextProcess text = scan.Text;
            NativeBattleScanTextWindows windows = NativeBattleScanTextInitialization.InitializeBackendStores4B7170(text.Windows);
            NativeBattleScanTextInitializationResult initialized = NativeBattleScanTextInitialization.InitializeTextStores4A0880(windows, text.Globals1D2B524);
            //These are the owned window stores; system-font presentation does not consume49F640's numeric font records.
            scan = scan with { Text = text with { Windows = initialized.Windows, Globals1D2B524 = initialized.Globals1D2B524, SelectedWindow1D2B51C = initialized.SelectedWindow1D2B51C, Initialized = true } };
        }
        state = NativeBattleScanInputPublication.Publish49E9C0(state, ReadPhysical);
        NativeEffectMemory = NativeEffectMemory with { Scan = scan with { Input = state, InputInitialized = true } };

        uint ReadPhysical(int device) => device == 0 ? physicalButtons : throw new NotSupportedException("Native PC Scan input polls its actual physical device zero.");
    }
}

public sealed partial class ExpandedBattlePresentationProcess
{
    internal bool CanQueueScanInput => presentation.CanQueueNativeScanInput;

    internal void QueueScanInput(BattleScanButton button)
    {
        RequireIdle();
        presentation.QueueNativeScanInput(button);
    }

    internal void PublishScanMappedButtons(ushort mappedButtons)
    {
        RequireIdle();
        presentation.PublishNativeScanMappedButtons(mappedButtons);
    }
}
