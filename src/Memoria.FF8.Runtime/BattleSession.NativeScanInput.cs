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
        }
        state = NativeBattleScanInputPublication.Publish49E9C0(state, ReadPhysical);
        NativeEffectMemory = NativeEffectMemory with { Scan = scan with { Input = state, InputInitialized = true } };

        uint ReadPhysical(int device) => device == 0 ? scan.PhysicalButtons1CD01FC : throw new NotSupportedException("Native PC Scan input polls its actual physical device zero.");
    }
}
