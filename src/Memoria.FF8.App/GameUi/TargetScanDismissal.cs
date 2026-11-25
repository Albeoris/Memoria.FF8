using Memoria.FF8.Runtime;

namespace Memoria.FF8.GameUi;

internal sealed class TargetScanDismissal
{
    private static readonly ushort confirm = NativeBattleScanInputControl.MappedButtons(BattleScanButton.Confirm);
    private bool requested;

    public ushort Sample(ushort buttons, bool visible, bool exiting, InputFrame input, bool pointerPressed)
    {
        if (!visible)
        {
            requested = false;
            return buttons;
        }

        // The results task and the viewer consume confirm on separate callbacks.
        requested = !exiting && (requested || input.Confirm || input.Cancel || pointerPressed);
        return (ushort)((buttons & ~confirm) | (requested ? confirm : 0));
    }

    public void Reset()
    {
        requested = false;
    }
}
