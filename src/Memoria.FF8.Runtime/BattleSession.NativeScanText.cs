namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private void AdvanceNativeScanText537A30()
    {
        if (NativeEffectMemory?.Scan is not { InputInitialized: true, Text.Initialized: true } process || NativeGraphics?.UiServices is not { } ui)
        {
            return;
        }
        uint options = definition.NativeSavedOptions ?? throw new NotSupportedException("Native Scan text input requires its saved input options.");
        NativeBattleScanInputBank bank = process.Input.Input;
        ushort held = NativeInputConfiguration.Project(bank.Read49ED30(0, 0), options, definition.NativeInputButtonMap);
        ushort edges = NativeInputConfiguration.Project(bank.Read49EDE0(0, 0), options, definition.NativeInputButtonMap);
        NativeBattleScanUiRepeatStep repeat = NativeBattleScanUiRepeat.Repeat49FDA0(process.Input, process.Text.Globals1D2B524, held);
        NativeBattleScanTextFrame frame = process.Text.ReadFrame(ui.Flags942) with { Globals1D2B524 = repeat.Globals1D2B524 };
        NativeBattleScanTextExpansionServices expansion = CreateNativeScanTextExpansionServices();
        NativeBattleScanTextServices services = new((current, source) => NativeBattleScanTextExpansion.Expand4B8B30(current, source, -1, expansion), expansion.ReadOriginalByte, (current, cue) =>
        {
            events.Add(new(Frame, "native-ui-sound", null, null, cue, "") { NativeUiSound = cue });
            return current;
        });
        for (byte index = 0; index < 8; index++)
        {
            NativeBattleScanByteOwner backend = frame.Windows.Backend(index);
            if (backend.ReadByte(0x34) == 0 || backend.ReadDword(0) == 0)
            {
                continue;
            }
            if (backend.ReadDword(0) != 0x4A0C00)
            {
                throw new NotSupportedException("Native Scan text update requires its actual backend callback.");
            }
            int row = backend.ReadByte(0x3A);
            if (frame.Windows.Row(row).ReadByte(0x16) != 0 && frame.Windows.Row(row).ReadDword(0x38) != 0)
            {
                throw new NotSupportedException("Native Scan text requires its reached extra update callback.");
            }
            frame = NativeBattleScanTextUpdate.Advance49FEB0(frame, row, held | ((uint)edges << 16), repeat.Value, services);
        }
        NativeBattleScanTextProcess text = process.Text.CaptureFrame(frame, out ushort flags);
        NativeEffectMemory = NativeEffectMemory with { Scan = process with { Text = text } };
        NativeGraphics = NativeGraphics with { UiServices = ui with { Flags942 = flags } };
    }
}
