using Memoria.FF8.Events;

namespace Memoria.FF8.GameUi;

/// <summary>Focusable actions for the native party panes; the event engine validates exchanges.</summary>
public static class NativePartyMenuProjection
{
    public static bool HasPanes(NativePartyMenuState menu)
    {
        return menu is { Kind: NativeFieldMenuKind.PartySelection or NativeFieldMenuKind.GuestSelection, TemporaryCombatParty.Length: 3, TemporaryReserveParty.Length: 8 };
    }

    public static IReadOnlyList<PlayerAction> Inputs(NativePartyMenuState menu)
    {
        if (!HasPanes(menu) || menu.Phase != NativePartyMenuPhase.Active)
        {
            return [];
        }
        bool guest = menu.Kind == NativeFieldMenuKind.GuestSelection;
        List<PlayerAction> inputs = Enumerable.Range(0, 3).Where(slot => !guest || slot != 0 && (menu.NativeEntrySelection & 1 << slot) != 0).Select(slot => new PlayerAction(PlayerActionKind.SelectPartySlot, slot)).ToList();
        if (menu.SelectedPartySlot is not null)
        {
            byte source = menu.TemporaryCombatParty![menu.SelectedPartySlot.Value];
            if (source == 255 || (menu.LockedMask & 1 << source) == 0)
            {
                inputs.AddRange(menu.TemporaryReserveParty!.Distinct().Where(row => !guest || (row == 255 ? source != 255 : (menu.LockedMask & 1 << row) == 0)).Select(row => new PlayerAction(PlayerActionKind.SelectPartyMember, row)));
            }
        }
        else if (Enumerable.Range(0, 3).All(slot => guest && (menu.NativeEntrySelection & 1 << slot) == 0 || menu.TemporaryCombatParty![slot] != 255))
        {
            inputs.Add(new PlayerAction(PlayerActionKind.Confirm));
        }
        return inputs;
    }
}
