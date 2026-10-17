using Memoria.FF8.Events;

namespace Memoria.FF8;

internal sealed partial class GameWorld
{
    internal static PlayerAction PlanAutoPlayPartyInput(NativePartyMenuState menu)
    {
        if (menu.Phase != NativePartyMenuPhase.Active)
        {
            return new PlayerAction(PlayerActionKind.Wait);
        }
        if (menu.Kind is NativeFieldMenuKind.Normal or NativeFieldMenuKind.Save or NativeFieldMenuKind.Shop or NativeFieldMenuKind.WeaponShop)
        {
            return new PlayerAction(PlayerActionKind.Cancel);
        }
        if (menu.Kind is not (NativeFieldMenuKind.PartySelection or NativeFieldMenuKind.GuestSelection))
        {
            throw new NotSupportedException($"Automatic play does not support native menu kind {menu.Kind}.");
        }
        if (menu.TemporaryCombatParty is not { Length: 3 } party || menu.TemporaryReserveParty is not { Length: 8 } reserve)
        {
            throw new InvalidDataException("Active native party selection requires its exact combat and reserve panes.");
        }
        if (menu.SelectedPartySlot is int selected)
        {
            if ((uint)selected >= party.Length)
            {
                throw new InvalidDataException("Native party selection contains an invalid selected slot.");
            }
            if (party[selected] != 255)
            {
                return new PlayerAction(PlayerActionKind.Cancel);
            }
            if (menu.Kind == NativeFieldMenuKind.GuestSelection && (selected == 0 || (menu.NativeEntrySelection & 1 << selected) == 0))
            {
                throw new InvalidDataException("Native guest selection contains an unavailable selected slot.");
            }
            int candidate = reserve.Where(row => row < 8 && (menu.EligibleMask & 1 << row) != 0 && (menu.LockedMask & 1 << row) == 0 && !party.Contains(row)).Select(row => (int)row).Order().FirstOrDefault(-1);
            if (candidate < 0)
            {
                throw new NotSupportedException("Native party selection has an empty required slot and no eligible unlocked reserve member.");
            }
            return new PlayerAction(PlayerActionKind.SelectPartyMember, candidate);
        }
        for (int slot = 0; slot < party.Length; slot++)
        {
            bool required = menu.Kind == NativeFieldMenuKind.PartySelection || (menu.NativeEntrySelection & 1 << slot) != 0;
            if (required && party[slot] == 255)
            {
                return new PlayerAction(PlayerActionKind.SelectPartySlot, slot);
            }
        }
        return new PlayerAction(PlayerActionKind.Confirm);
    }
}
