using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattlePresentationProcess
{
    private NativeBattleDuelSelectionReached? pendingHostedDuelSelection;

    private void EmitExpandedHostedDuelReadiness(NativeBattleGraphicsState graphics, uint callback)
    {
        NativeBattleModelLifecycleServices services = hostedFrameServices ?? throw new InvalidOperationException("Hosted Duel readiness requires its frame facts.");
        int slot = graphics.Descriptors.Queue[0];
        byte serial = graphics.Descriptors.Packets[slot].Serial;
        if (services.Duel is not { } duel || duel.Poll.ReadinessCallback != callback ||
            !hostedControlPackets.Any(control => !control.Retired && control.Slot == slot && control.Serial == serial && control.Command == 10 &&
                control.Callback == callback && ReferenceEquals(control.DuelPoll, duel.Poll)))
        {
            throw new InvalidDataException("Native Duel readiness does not identify the current hosted widget and move.");
        }
        services.Emit(new NativeBattleDuelReadinessReached(duel, callback));
    }

    internal NativeBattleDuelSelectionReached? PendingHostedDuelSelection => pendingHostedDuelSelection;

    private NativeBattleGraphicsState RunExpandedHostedDuelSelection(NativeBattleGraphicsState graphics)
    {
        NativeBattleModelLifecycleServices services = hostedFrameServices ?? throw new InvalidOperationException("Hosted Duel polling requires its current frame facts.");
        if (services.Duel is not { } frame || frame.Widget.Selection1D28DEF is not byte selection || pendingHostedDuelSelection is not null)
        {
            throw new InvalidDataException("Native Duel polling requires its current unconsumed widget selection.");
        }
        int slot = graphics.Descriptors.Queue[0];
        NativeBattleDescriptorPacket packet = graphics.Descriptors.Packets[slot];
        int tracked = -1;
        for (int index = 0; index < hostedControlPackets.Length; index++)
        {
            HostedControlPacket control = hostedControlPackets[index];
            if (!control.Retired && control.Slot == slot && control.Serial == packet.Serial && control is { Command: 10, Callback: 0x4852B0 } && ReferenceEquals(control.DuelPoll, frame.Poll))
            {
                tracked = index;
                break;
            }
        }
        if (tracked < 0)
        {
            throw new InvalidDataException("Native Duel poll lost its original queued control occurrence.");
        }
        if (selection == 254)
        {
            (NativeBattleDescriptorState descriptors, int next) = graphics.Descriptors.Enqueue(10, 128, 0x4852B0);
            hostedControlPackets = hostedControlPackets.SetItem(tracked, new(next, descriptors.Packets[next].Serial, 10, 0x4852B0) { DuelPoll = frame.Poll });
            return graphics with { Descriptors = descriptors };
        }
        NativeBattleDuelSelectionReached receipt = new(frame, slot, packet.Serial, lastHostedActionCompleted);
        pendingHostedDuelSelection = receipt;
        services.Emit(receipt);
        return graphics;
    }
}
