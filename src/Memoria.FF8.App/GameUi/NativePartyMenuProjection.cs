using Memoria.FF8.Events;

namespace Memoria.FF8.GameUi;

/// <summary>Focusable actions for the native party panes; the event engine validates exchanges.</summary>
public static class NativePartyMenuProjection
{
    public static IReadOnlyList<PlayerAction> Inputs(NativePartyMenuState menu)
    {
        if (menu.Kind != NativeFieldMenuKind.PartySelection || menu.Phase != NativePartyMenuPhase.Active || menu.TemporaryCombatParty is not { Length: 3 } || menu.TemporaryReserveParty is not { Length: 8 })
        {
            return [];
        }
        List<PlayerAction> inputs = Enumerable.Range(0, 3).Select(slot => new PlayerAction(PlayerActionKind.SelectPartySlot, slot)).ToList();
        if (menu.SelectedPartySlot is not null)
        {
            byte source = menu.TemporaryCombatParty[menu.SelectedPartySlot.Value];
            if (source == 255 || (menu.LockedMask & 1 << source) == 0)
            {
                inputs.AddRange(menu.TemporaryReserveParty.Distinct().Select(row => new PlayerAction(PlayerActionKind.SelectPartyMember, row)));
            }
        }
        else if (menu.TemporaryCombatParty.All(row => row != 255))
        {
            inputs.Add(new PlayerAction(PlayerActionKind.Confirm));
        }
        return inputs;
    }
}
