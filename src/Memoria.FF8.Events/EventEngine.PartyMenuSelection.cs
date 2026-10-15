using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private static byte[] CreatePartyMenuReserve(IReadOnlyList<byte> party, byte eligible)
    {
        //4CB997..4CBA0A fills eight FF cells, then appends eligible nonparty rows.
        byte[] reserve = Enumerable.Repeat((byte)255, 8).ToArray();
        int slot = 0;
        for (byte row = 0; row < 8; row++)
        {
            if ((eligible & 1 << row) != 0 && !party.Contains(row))
            {
                reserve[slot++] = row;
            }
        }
        return reserve;
    }

    private void PublishPartyMenuSelection(NativePartyMenuState next)
    {
        PublishPartyMenuCallback(services.CharacterVitals!, services.Economy!, services.ShotAvailability!, services.SalaryPresentation!.Advance(1), advanceVibration: true);
        partyMenu = next;
    }

    private bool SelectPartyMenuSlot(int slot)
    {
        if (partyMenu is not { Phase: NativePartyMenuPhase.Active, Kind: NativeFieldMenuKind.PartySelection, TemporaryCombatParty: not null } current || (uint)slot >= 3)
        {
            return false;
        }
        byte[] party = current.TemporaryCombatParty.ToArray();
        int? selected = slot;
        if (current.SelectedPartySlot is int origin)
        {
            //4CBE0A permits locked rows to move within the same party pane.
            (party[origin], party[slot]) = (party[slot], party[origin]);
            selected = null;
        }
        NativePartyMenuState next = current with { TemporaryCombatParty = party, SelectedPartySlot = selected, Callbacks = checked(current.Callbacks + 1) };
        PublishPartyMenuSelection(next);
        return true;
    }

    private bool SelectPartyMenuMember(int character)
    {
        if (partyMenu is not { Phase: NativePartyMenuPhase.Active, Kind: NativeFieldMenuKind.PartySelection, TemporaryReserveParty: not null } current || (uint)character >= 8 && character != 255)
        {
            return false;
        }
        int reserveSlot = Array.IndexOf(current.TemporaryReserveParty, (byte)character);
        if (reserveSlot < 0)
        {
            return false;
        }
        int slot = current.SelectedPartySlot ?? Array.IndexOf(current.TemporaryCombatParty!, (byte)255);
        if (slot < 0)
        {
            throw new NotSupportedException("Replacing an occupied party-menu cell requires an explicit SelectPartySlot input.");
        }
        byte old = current.TemporaryCombatParty![slot];
        if (old != 255 && (current.LockedMask & 1 << old) != 0 || character != 255 && (current.LockedMask & 1 << character) != 0)
        {
            return false;
        }
        byte[] party = current.TemporaryCombatParty.ToArray();
        byte[] reserve = current.TemporaryReserveParty.ToArray();
        //4CBE1A..4CBE3E exchanges the two cells and4CBE70 clears the marker.
        (party[slot], reserve[reserveSlot]) = (reserve[reserveSlot], party[slot]);
        NativePartyMenuState next = current with { TemporaryCombatParty = party, TemporaryReserveParty = reserve, SelectedPartySlot = null, Callbacks = checked(current.Callbacks + 1) };
        PublishPartyMenuSelection(next);
        return true;
    }

    private bool CancelPartyMenuSelection()
    {
        if (partyMenu is not { Phase: NativePartyMenuPhase.Active, Kind: NativeFieldMenuKind.PartySelection, SelectedPartySlot: not null } current)
        {
            return false;
        }
        //4CC04F..4CC061 restores the selection cursor and clears pending4F only.
        PublishPartyMenuSelection(current with { SelectedPartySlot = null, Callbacks = checked(current.Callbacks + 1) });
        return true;
    }

    private static void ValidatePartyMenuSelectionCheckpoint(NativePartyMenuState menu)
    {
        if (menu.Kind != NativeFieldMenuKind.PartySelection || menu.Phase is NativePartyMenuPhase.Requested or NativePartyMenuPhase.Entering)
        {
            if (menu.TemporaryReserveParty is not null || menu.SelectedPartySlot is not null)
            {
                throw new InvalidDataException("Native party selection appeared outside its actual constructor.");
            }
            return;
        }
        if (menu.TemporaryCombatParty is not { Length: 3 } || menu.TemporaryReserveParty is not { Length: 8 } || menu.SelectedPartySlot is int slot && (uint)slot >= 3 || menu.Phase != NativePartyMenuPhase.Active && menu.SelectedPartySlot is not null)
        {
            throw new InvalidDataException("Saved native party selection lost its exact pane or pending cell.");
        }
        byte[] rows = menu.TemporaryCombatParty.Concat(menu.TemporaryReserveParty).Where(row => row != 255).ToArray();
        byte[] eligible = Enumerable.Range(0, 8).Where(row => (menu.EligibleMask & 1 << row) != 0).Select(row => (byte)row).ToArray();
        if (!rows.Order().SequenceEqual(eligible))
        {
            throw new InvalidDataException("Saved native party panes duplicated, discarded or invented an eligible character.");
        }
    }
}
