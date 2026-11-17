namespace Memoria.FF8.Runtime;

public sealed partial class BattlePresentationProcess
{
    internal bool IsPartyReplacementVitalsHidden(string actorId)
    {
        foreach (HostedControlPacket control in hostedControlPackets)
        {
            if (!control.Retired && control.PartyReplacement is { } descriptor && (descriptor.OutgoingActorId == actorId || descriptor.IncomingActorId == actorId))
            {
                return true;
            }
        }
        return false;
    }

    internal void RefreshPartyRosterUi()
    {
        RequireAdmissionWindow();
        NativeBattleGraphicsState graphics = RequireNativeGraphics();
        NativeBattleUiServices ui = graphics.UiServices ?? throw new InvalidOperationException("Party roster changes require active battle windows.");
        NativeGraphics = graphics with { UiServices = ui.SetFlags(0x400) };
    }

    private NativeBattleGraphicsState ReachPartyReplacement(NativeBattleGraphicsState graphics, int slot, NativeBattleDescriptorPacket packet)
    {
        PartyReplacementDescriptor descriptor = hostedControlPackets.SingleOrDefault(control => !control.Retired && control.Slot == slot && control.Serial == packet.Serial && control.Callback == packet.Argument)?.PartyReplacement
            ?? throw new InvalidDataException("The party replacement descriptor has no current typed reservation.");
        NativeBattleModelLifecycleServices frame = hostedFrameServices ?? throw new InvalidOperationException("Party replacement requires its current presentation callback.");
        if (descriptor.Stage == PartyReplacementStage.Reveal)
        {
            graphics = SetPartyReplacementVitals(graphics, descriptor.Change.Slot, true);
            NativeBattleUiServices ui = graphics.UiServices ?? throw new InvalidOperationException("Party replacement requires its active battle windows.");
            graphics = graphics with { UiServices = ui.SetFlags(0x400) };
        }
        frame.EmitPartyReplacement(new(descriptor));
        return graphics;
    }

    private static NativeBattleGraphicsState SetPartyReplacementVitals(NativeBattleGraphicsState graphics, BattleRosterSlot slot, bool visible)
    {
        if (slot.Side != BattleSide.Allies)
        {
            throw new InvalidDataException("Party replacement requires an ally roster slot.");
        }
        if (NativeBattleSlots.TryEncode(slot) is not int original)
        {
            return graphics;
        }
        NativeBattleUiServices ui = graphics.UiServices ?? throw new InvalidOperationException("Party replacement requires its active battle windows.");
        NativeBattleUiWindows windows = ui.Windows ?? throw new InvalidOperationException("Party replacement requires the initialized vitals rows.");
        return graphics with { UiServices = ui with { Windows = windows.RequestVitalsVisibility4B1830(original, visible) } };
    }
}
