using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private void RequestGuestSelectionMenu(Fiber fiber, int argument, byte flags)
    {
        partyMenu = new NativePartyMenuState(FieldId, programHash, NativeRuntimeProfile!.ExecutableHash, Frame, fiber.ObjectId, fiber.Script.Id, fiber.Position - 1, fiber.Sequence, NativePartyMenuPhase.Requested, 0, null, 0, 0, 0, null, NativeFieldMenuKind.GuestSelection, flags, 0, null, null, null, null, null, JunctionRawArgument: argument);
        fiber.WaitReason = WaitReason.NativePartyMenu;
    }

    private static byte GuestMenuEligible(NativeCharacterVitals vitals)
    {
        byte result = 1;
        for (int row = 1; row < 8; row++)
        {
            if ((vitals.Records[row].MemberFlags & 8) != 0)
            {
                result |= checked((byte)(1 << row));
            }
        }
        return result;
    }

    private void StepGuestSelectionOpening(NativePartyMenuState current)
    {
        var prepared = PreparePartyMenuEntry(NativeFieldMenuKind.GuestSelection);
        byte eligible = GuestMenuEligible(prepared.Vitals);
        byte[] selection = [0, 255, 255];
        PublishPartyMenuCallback(prepared.Vitals, prepared.Economy, prepared.Shots, prepared.Salary, clearControllerRequests: true);
        partyMenu = current with { Phase = NativePartyMenuPhase.Active, Callbacks = checked(current.Callbacks + 1), TemporaryCombatParty = selection, TemporaryReserveParty = CreatePartyMenuReserve(selection, eligible), EligibleMask = eligible, LockedMask = 1, NativeReturnResult = 3 };
    }

    private bool ConfirmGuestSelectionMenu()
    {
        if (partyMenu is not { Kind: NativeFieldMenuKind.GuestSelection, Phase: NativePartyMenuPhase.Active, TemporaryCombatParty: not null } current || current.SelectedPartySlot is not null)
        {
            return false;
        }
        for (int slot = 0; slot < 3; slot++)
        {
            if ((current.NativeEntrySelection & 1 << slot) != 0 && current.TemporaryCombatParty[slot] == 255)
            {
                return false;
            }
        }
        NativeJunctionResult completed = services.CharacterVitals!.CompleteGuestSelectionMenu(current.TemporaryCombatParty, current.NativeEntrySelection);
        PublishPartyMenuCallback(completed.Vitals, services.Economy!, services.ShotAvailability!, services.SalaryPresentation!.Advance(1), advanceVibration: true);
        Scene.Party.Clear();
        Scene.Party.AddRange(completed.FieldParty.Select(identity => (int)identity));
        partyMenu = current with { Phase = NativePartyMenuPhase.Closing, Callbacks = checked(current.Callbacks + 1) };
        return true;
    }

    private void ValidateGuestSelectionCheckpoint(Checkpoint saved, NativePartyMenuState menu, IReadOnlyDictionary<long, Fiber> bySequence)
    {
        bool opening = menu.Phase is NativePartyMenuPhase.Requested or NativePartyMenuPhase.Entering;
        bool active = menu.Phase == NativePartyMenuPhase.Active;
        if (menu.FieldId != saved.FieldId || menu.ProgramHash != saved.ProgramHash || menu.ExecutableHash != NativeRuntimeProfile?.ExecutableHash || menu.Frame != saved.Frame || !Enum.IsDefined(menu.Phase) || menu.NativeEntrySelection > 7 || menu.JunctionRawArgument is not int argument || (argument & 3) != 3 || menu.NativeReturnResult != (opening ? 0 : 3) || !bySequence.TryGetValue(menu.OwnerSequence, out Fiber? owner) || owner.Complete || owner.ObjectId != menu.ObjectId || owner.Script.Id != menu.ScriptId || owner.WaitReason != WaitReason.NativePartyMenu || owner.Position != menu.Operation + 1 || menu.Operation < owner.Script.Start || menu.Operation >= owner.Script.End || program!.Operations[menu.Operation].Opcode != Jsm.Opcode.JUNCTION || saved.Scene.FieldCallbackClock is null || menu.Callbacks < 0 || menu.Callbacks > saved.Scene.FieldCallbackClock.MenuCallbacks || saved.CharacterVitals is null || saved.Economy is null || saved.ShotAvailability is null || saved.SalaryPresentation is null || saved.ExamTimer is null)
        {
            throw new InvalidDataException("Saved guest selection changed its exact JUNCTION request, source, menu result or native owner.");
        }
        if (menu.ShopRawArgument is not null || menu.Shop is not null || menu.WeaponShop is not null || menu.SavePanel is not null || menu.ForcedFieldMask != 0)
        {
            throw new InvalidDataException("Guest selection contains unrelated menu owners.");
        }
        if (opening)
        {
            if (menu.Callbacks != (menu.Phase == NativePartyMenuPhase.Requested ? 0 : 1) || menu.TemporaryCombatParty is not null || menu.TemporaryReserveParty is not null || menu.SelectedPartySlot is not null || menu.EligibleMask != 0 || menu.LockedMask != 0 || !saved.CharacterVitals.DreamEnabled)
            {
                throw new InvalidDataException("Guest selection was constructed before its actual menu callback.");
            }
        }
        else
        {
            byte eligible = GuestMenuEligible(saved.CharacterVitals);
            if (menu.Callbacks < 2 || menu.TemporaryCombatParty is not { Length: 3 } party || party[0] != 0 || menu.TemporaryReserveParty is not { Length: 8 } reserve || menu.EligibleMask != eligible || menu.LockedMask != 1 || menu.SelectedPartySlot is int selected && (selected is < 1 or > 2 || (menu.NativeEntrySelection & 1 << selected) == 0) || !active && menu.SelectedPartySlot is not null || saved.CharacterVitals.DreamEnabled == active)
            {
                throw new InvalidDataException("Guest selection lost its physical panes, eligibility, selection or dream-mode transition.");
            }
            byte[] actual = party.Concat(reserve).Where(row => row != 255).Order().ToArray();
            byte[] expected = Enumerable.Range(0, 8).Where(row => (eligible & 1 << row) != 0).Select(row => (byte)row).ToArray();
            if (!actual.SequenceEqual(expected))
            {
                throw new InvalidDataException("Guest selection duplicated, lost or invented an eligible physical row.");
            }
            if (!active)
            {
                NativeJunctionResult mapped = saved.CharacterVitals.CompleteGuestSelectionMenu(party, menu.NativeEntrySelection);
                if (!mapped.FieldParty.Select(identity => (int)identity).SequenceEqual(saved.Scene.Party) || !mapped.Vitals.CombatParty.SequenceEqual(saved.CharacterVitals.CombatParty) || !mapped.Vitals.Records.Select(row => row.CharacterId).SequenceEqual(saved.CharacterVitals.Records.Select(row => row.CharacterId)))
                {
                    throw new InvalidDataException("Completed guest selection disagrees with its native committed mapping.");
                }
            }
        }
        if ((menu.TimerDisplayBeforeMenu is null) != (menu.Phase == NativePartyMenuPhase.Requested) || menu.Phase != NativePartyMenuPhase.Requested && (saved.NativeFieldLocation is not NativeFieldLocation location || location.FieldId != menu.FieldId || location.ProgramHash != menu.ProgramHash || location.ExecutableHash != menu.ExecutableHash || location.Frame != menu.Frame || location.ObjectId != menu.ObjectId || location.ScriptId != menu.ScriptId || location.Operation != menu.Operation))
        {
            throw new InvalidDataException("Guest menu lost its actual field capture or cached timer display.");
        }
        if (menu.Phase is not (NativePartyMenuPhase.Requested or NativePartyMenuPhase.FieldReentry or NativePartyMenuPhase.FieldResumeInitialization) && saved.ExamTimer.DisplayEnabled || menu.Phase is NativePartyMenuPhase.FieldReentry or NativePartyMenuPhase.FieldResumeInitialization && saved.ExamTimer.DisplayEnabled != menu.TimerDisplayBeforeMenu || menu.Phase == NativePartyMenuPhase.FieldResumeInitialization && saved.Scene.FieldCallbackClock.FieldLoaderCallbacks <= 0)
        {
            throw new InvalidDataException("Guest menu changed its clock, timer or native kind6 resume.");
        }
    }
}
