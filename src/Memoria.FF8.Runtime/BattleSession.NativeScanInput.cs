namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    public void PublishNativeScanMappedButtons(ushort mappedButtons)
    {
        if (NativeEffectMemory?.Scan is not { } scan)
        {
            return;
        }
        ushort raw = NativeInputConfiguration.InvertPulse(mappedButtons, definition.NativeSavedOptions ?? throw new NotSupportedException("Native Scan input requires its saved mapping options."), definition.NativeInputButtonMap);
        NativeEffectMemory = NativeEffectMemory with { Scan = scan with { PhysicalButtons1CD01FC = raw } };
    }

    private void PublishNativeScanInputFrame()
    {
        if (NativeEffectMemory?.Scan is not { } scan)
        {
            return;
        }
        NativeBattleScanInputPublicationState state = scan.Input;
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

        uint ReadPhysical(int device) => device == 0 ? scan.PhysicalButtons1CD01FC : throw new NotSupportedException("Native PC Scan input polls its actual physical device zero.");
    }
}
