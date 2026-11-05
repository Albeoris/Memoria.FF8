using System.Collections.Immutable;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    public bool HasSupportedGameplayScriptMenu
    {
        get
        {
            try
            {
                return CaptureGameplayScriptMenu() is not null;
            }
            catch (Exception exception) when (exception is InvalidDataException or NotSupportedException)
            {
                return false;
            }
        }
    }

    private GameplaySaveScriptMenu? CaptureGameplayScriptMenu()
    {
        if (partyMenu is not { } menu)
        {
            return null;
        }
        if (menu.Kind is not (NativeFieldMenuKind.Normal or NativeFieldMenuKind.Save) || menu.Phase is NativePartyMenuPhase.Requested or NativePartyMenuPhase.Entering || menu.ShopRawArgument is not null || menu.Shop is not null || menu.WeaponShop is not null || menu.JunctionRawArgument is not null || menu.TemporaryReserveParty is not null || menu.SelectedPartySlot is not null || menu.TimerDisplayBeforeMenu is null || services.NativeFieldLocation is not { } location || services.CharacterVitals is not { } vitals || services.Economy is null || services.ShotAvailability is null || services.SalaryPresentation is null || services.ExamTimer is null || services.VibrationProcess is null || services.WorldProcess is null || services.FieldFade is null)
        {
            throw new NotSupportedException("Gameplay script-menu continuation supports settled Normal/Save panels with known canonical owners and close state.");
        }
        ValidateGameplayMenuOwner(menu);
        location.Validate();
        if (menu.Frame != Frame || menu.FieldId != FieldId || menu.ProgramHash != programHash || menu.ExecutableHash != NativeRuntimeProfile?.ExecutableHash || location.FieldId != FieldId || location.Frame != menu.Frame || location.ObjectId != menu.ObjectId || location.ScriptId != menu.ScriptId || location.Operation != menu.Operation || location.ProgramHash != programHash || location.ExecutableHash != NativeRuntimeProfile?.ExecutableHash)
        {
            throw new InvalidDataException("Gameplay script-menu entry location and waiting owner disagree.");
        }
        ValidateSaveMenuCheckpoint(menu);
        if (menu.Kind == NativeFieldMenuKind.Normal && (menu.TemporaryCombatParty is null || !menu.TemporaryCombatParty.SequenceEqual(vitals.CombatParty)))
        {
            throw new NotSupportedException("Gameplay normal-menu continuation requires committed canonical party edits.");
        }
        (byte eligible, byte locked, byte forced) = GameplayMenuMasks(vitals, menu.Kind);
        bool fieldResume = menu.Phase is NativePartyMenuPhase.FieldReentry or NativePartyMenuPhase.FieldResumeInitialization;
        if (menu.EligibleMask != eligible || menu.LockedMask != locked || menu.ForcedFieldMask != forced || services.ExamTimer.DisplayEnabled != (fieldResume && menu.TimerDisplayBeforeMenu.Value))
        {
            throw new InvalidDataException("Gameplay script-menu membership masks or timer display contradict their canonical owners.");
        }
        GameplaySaveScriptMenu result = new(1, menu.Kind == NativeFieldMenuKind.Normal ? GameplaySaveScriptMenuKind.Normal : GameplaySaveScriptMenuKind.Save, MenuPhase(menu.Phase), menu.OwnerSequence, menu.ObjectId, menu.ScriptId, menu.Operation, menu.Callbacks, menu.NativeEntrySelection, menu.NativeReturnResult, menu.TimerDisplayBeforeMenu.Value, location.PreviousFieldId, location.Slots.ToArray(), menu.SavePanel?.Fade70);
        if (menu.Kind == NativeFieldMenuKind.Normal && (menu.NativeReturnResult > 1 || menu.NativeReturnResult == 1 && (menu.NativeEntrySelection & 2) == 0) || menu.Kind == NativeFieldMenuKind.Save && menu.NativeReturnResult != 0)
        {
            throw new NotSupportedException("Gameplay script-menu result requires a separately supported native return contract.");
        }
        return result;
    }

    private void ValidateGameplayMenuOwner(NativePartyMenuState menu)
    {
        Fiber? owner = fibers.SingleOrDefault(fiber => fiber.Sequence == menu.OwnerSequence);
        Jsm.Opcode expected = menu.Kind == NativeFieldMenuKind.Normal ? Jsm.Opcode.MENUNORMAL : Jsm.Opcode.MENUSAVE;
        if (program is null || owner is null || owner.ObjectId != menu.ObjectId || owner.Script.Id != menu.ScriptId || owner.Complete || owner.Initialization || owner.WaitReason != WaitReason.NativePartyMenu || owner.Position != menu.Operation + 1 || !owner.Script.ContainsOperation(menu.Operation) || program.Operations[menu.Operation].Opcode != expected || fibers.Count(fiber => fiber.WaitReason == WaitReason.NativePartyMenu) != 1)
        {
            throw new InvalidDataException("Gameplay script-menu continuation requires its unique waiting MENUNORMAL/MENUSAVE owner.");
        }
    }

    private void RestoreGameplayScriptMenu(GameplaySaveScriptMenu? saved)
    {
        if (saved is null)
        {
            return;
        }
        if (partyMenu is not null || NativeRuntimeProfile is not { IsVerified: true } || services is not GameSession session || !services.CanStoreNativePartyMenuOwners || services.CharacterVitals is not { } vitals || services.Economy is null || services.ShotAvailability is null || services.SalaryPresentation is null || services.ExamTimer is null || services.VibrationProcess is null || services.WorldProcess is null || services.FieldFade is null)
        {
            throw new NotSupportedException("Gameplay script-menu restoration requires fresh canonical menu and return owners.");
        }
        bool fieldResume = saved.Phase is GameplaySaveScriptMenuPhase.FieldReentry or GameplaySaveScriptMenuPhase.FieldResumeInitialization;
        if (services.ExamTimer.DisplayEnabled != (fieldResume && saved.TimerDisplayBeforeMenu))
        {
            throw new InvalidDataException("Gameplay script-menu timer display contradicts its current close phase.");
        }
        NativeFieldMenuKind kind = saved.Kind == GameplaySaveScriptMenuKind.Normal ? NativeFieldMenuKind.Normal : NativeFieldMenuKind.Save;
        if (saved.PendingPartyRefresh)
        {
            int[] entryParty = saved.EntryLocation.Select(slot => slot.RuntimeBinding == 255 ? 255 : actors[Scene.NativeActorObjectIds[slot.RuntimeBinding!.Value]].CharacterId).ToArray();
            byte[] currentParty = vitals.PreparePartyMenuFieldSlots(entryParty, vitals.CombatParty);
            if (!Scene.Party.SequenceEqual(currentParty.Select(row => (int)row)))
            {
                throw new InvalidDataException("Pending script-menu party refresh contradicts the canonical committed combat selection.");
            }
        }
        (byte eligible, byte locked, byte forced) = GameplayMenuMasks(vitals, kind);
        NativeSavePanelState? panel = kind == NativeFieldMenuKind.Save ? new(saved.Phase == GameplaySaveScriptMenuPhase.Active ? 15 : saved.Phase == GameplaySaveScriptMenuPhase.Closing && saved.SaveFade == 4096 ? 82 : 83, saved.SaveFade!.Value) : null;
        NativePartyMenuState restored = new(FieldId, programHash, NativeRuntimeProfile.ExecutableHash, Frame, saved.ObjectId, saved.ScriptId, saved.Operation, saved.OwnerSequence, NativeMenuPhase(saved.Phase), saved.Callbacks, kind == NativeFieldMenuKind.Normal ? vitals.CombatParty.ToArray() : null, eligible, locked, forced, saved.TimerDisplayBeforeMenu, kind, saved.EntrySelection, saved.ReturnResult, null, null, null, null, null, panel);
        ValidateGameplayMenuOwner(restored);
        ValidateSaveMenuCheckpoint(restored);
        NativeFieldLocation location = new(checked((ushort)FieldId), saved.PreviousField, 1, saved.EntryLocation.ToImmutableArray(), programHash, NativeRuntimeProfile.ExecutableHash, Frame, saved.ObjectId, saved.ScriptId, saved.Operation) { Origin = NativeFieldLocationOrigin.GameplayContinuation };
        location.Validate();
        session.RestoreGameplayMenuLocation(location);
        PublishNativeScriptMenuRequest(kind == NativeFieldMenuKind.Save ? (ushort)24 : (ushort)0);
        partyMenu = restored;
    }

    private static (byte Eligible, byte Locked, byte Forced) GameplayMenuMasks(NativeCharacterVitals vitals, NativeFieldMenuKind kind)
    {
        byte eligible = 0;
        byte locked = 0;
        byte forced = 0;
        if (kind == NativeFieldMenuKind.Normal)
        {
            for (int row = 0; row < 8; row++)
            {
                ushort flags = vitals.Records[row].MemberFlags;
                if ((flags & 1) != 0)
                {
                    eligible |= checked((byte)(1 << row));
                    if ((flags & 6) != 0)
                    {
                        locked |= checked((byte)(1 << row));
                    }
                    if ((flags & 4) != 0)
                    {
                        forced |= checked((byte)(1 << row));
                    }
                }
            }
        }
        return (eligible, locked, forced);
    }

    private static GameplaySaveScriptMenuPhase MenuPhase(NativePartyMenuPhase phase)
    {
        return phase switch
        {
            NativePartyMenuPhase.Active => GameplaySaveScriptMenuPhase.Active,
            NativePartyMenuPhase.Closing => GameplaySaveScriptMenuPhase.Closing,
            NativePartyMenuPhase.Teardown => GameplaySaveScriptMenuPhase.Teardown,
            NativePartyMenuPhase.Restoring => GameplaySaveScriptMenuPhase.Restoring,
            NativePartyMenuPhase.FieldReentry => GameplaySaveScriptMenuPhase.FieldReentry,
            NativePartyMenuPhase.FieldResumeInitialization => GameplaySaveScriptMenuPhase.FieldResumeInitialization,
            _ => throw new NotSupportedException("Gameplay script-menu opening is not an admitted settled panel.")
        };
    }

    private static NativePartyMenuPhase NativeMenuPhase(GameplaySaveScriptMenuPhase phase)
    {
        return phase switch
        {
            GameplaySaveScriptMenuPhase.Active => NativePartyMenuPhase.Active,
            GameplaySaveScriptMenuPhase.Closing => NativePartyMenuPhase.Closing,
            GameplaySaveScriptMenuPhase.Teardown => NativePartyMenuPhase.Teardown,
            GameplaySaveScriptMenuPhase.Restoring => NativePartyMenuPhase.Restoring,
            GameplaySaveScriptMenuPhase.FieldReentry => NativePartyMenuPhase.FieldReentry,
            GameplaySaveScriptMenuPhase.FieldResumeInitialization => NativePartyMenuPhase.FieldResumeInitialization,
            _ => throw new InvalidDataException("Unsupported gameplay script-menu phase.")
        };
    }
}
