using Albeoris.Games.FF8.Jsm;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Events;

public enum NativePartyMenuPhase
{
    Requested,
    Entering,
    Active,
    Closing,
    Teardown,
    Restoring,
    FieldReentry,
    FieldResumeInitialization
}

public enum NativeFieldMenuKind
{
    Normal,
    PartySelection,
    Shop,
    WeaponShop
}

/// <summary>Executed521D40 request and the4CB880 temporary party; the minimal interactive panel does not claim original raster parity.</summary>
public sealed record NativePartyMenuState([property: JsonRequired] int FieldId, [property: JsonRequired] string ProgramHash, [property: JsonRequired] string ExecutableHash, [property: JsonRequired] long Frame, [property: JsonRequired] int ObjectId, [property: JsonRequired] int ScriptId, [property: JsonRequired] int Operation, [property: JsonRequired] long OwnerSequence, [property: JsonRequired] NativePartyMenuPhase Phase, [property: JsonRequired] long Callbacks, [property: JsonRequired] byte[]? TemporaryCombatParty, [property: JsonRequired] byte EligibleMask, [property: JsonRequired] byte LockedMask, [property: JsonRequired] byte ForcedFieldMask, [property: JsonRequired] bool? TimerDisplayBeforeMenu, [property: JsonRequired] NativeFieldMenuKind Kind, [property: JsonRequired] byte NativeEntrySelection, [property: JsonRequired] ushort NativeReturnResult, [property: JsonRequired] int? ShopRawArgument, [property: JsonRequired] NativeShopTransaction? Shop, [property: JsonRequired] NativeWeaponShopTransaction? WeaponShop, [property: JsonRequired] byte[]? TemporaryReserveParty, [property: JsonRequired] int? SelectedPartySlot)
{
    [JsonIgnore]
    public int NativeMode => 5;
    [JsonIgnore]
    public int NativeMenuId => Kind is NativeFieldMenuKind.Shop or NativeFieldMenuKind.WeaponShop ? 23 : Kind == NativeFieldMenuKind.PartySelection ? 1 : 0;
    [JsonIgnore]
    public int NativeFieldEntryKind => 6;
}

public sealed partial class EventEngine
{
    private NativePartyMenuState? partyMenu;
    public NativePartyMenuState? PartyMenu => partyMenu is NativePartyMenuState current ? current with { TemporaryCombatParty = current.TemporaryCombatParty?.ToArray(), TemporaryReserveParty = current.TemporaryReserveParty?.ToArray() } : null;
    public IReadOnlyList<byte> PartyMenuCandidates => partyMenu is { Phase: NativePartyMenuPhase.Active, Kind: NativeFieldMenuKind.PartySelection, TemporaryReserveParty: not null } current ? current.TemporaryReserveParty.Where(row => row == 255 ? current.SelectedPartySlot is not null : (current.LockedMask & 1 << row) == 0).Distinct().ToArray() : [];

    private string? UnsupportedPartyMenuRequest(Jsm.Opcode opcode)
    {
        if (NativeRuntimeProfile is not { IsVerified: true } || partyMenu is not null || services.IsMoviePlaying || !services.CanStoreNativePartyMenuOwners || services.CharacterVitals is null || services.Economy is null || services.ShotAvailability is null || services.SalaryPresentation is null || services.ExamTimer is null || services.VibrationProcess is null)
        {
            return "MENUPHS requires the verified native request, actual character/economy/shot/UI/clock owners and no active menu or movie";
        }
        try
        {
            if (opcode == Jsm.Opcode.MENUNORMAL && services.WorldControls is null)
            {
                return "MENUNORMAL requires its actual saved menu BYTE209 entry selection";
            }
            _ = PreparePartyMenuEntry(opcode == Jsm.Opcode.MENUNORMAL ? NativeFieldMenuKind.Normal : NativeFieldMenuKind.PartySelection);
            return null;
        }
        catch (Exception exception) when (exception is NotSupportedException or InvalidDataException)
        {
            return exception.Message;
        }
    }

    private (NativeCharacterVitals Vitals, NativeEconomy Economy, NativeShotAvailabilityState Shots, NativeSalaryPresentation Salary) PreparePartyMenuEntry(NativeFieldMenuKind kind)
    {
        NativeCharacterVitals vitals = kind == NativeFieldMenuKind.PartySelection ? services.CharacterVitals!.EnterNormalPartyMenu() : PrepareNormalMenuVitals();
        NativeEconomy economy = services.Economy! with { Gil = Math.Min(services.Economy!.Gil, NativeEconomy.GilCeiling), PastGil = Math.Min(services.Economy.PastGil, NativeEconomy.GilCeiling) };
        NativeShotAvailabilityState shots = services.ShotAvailability!.Discover(economy);
        NativeSalaryPresentation salary = services.SalaryPresentation!;
        salary.Validate();
        if (vitals.InitOutSha256 != shots.InitOutSha256 || vitals.KernelSha256 != shots.KernelSha256 || Scene.Party.Count != 3 || Scene.Party.Any(value => value != 255 && (uint)value >= 8))
        {
            throw new NotSupportedException("MENUPHS requires matching original owners and the three native field slots.");
        }
        return (vitals, economy, shots, salary);
    }

    private NativeCharacterVitals PrepareNormalMenuVitals()
    {
        NativeCharacterVitals vitals = services.CharacterVitals!.RebuildParty();
        //4C23D0 filters each existing slot through4AD030; an unchanged slot has exactly the eligible bit1.
        if (vitals.CombatParty.Any(row => row != 255 && (vitals.Records[row].MemberFlags & 1) == 0))
        {
            throw new NotSupportedException("Normal menu entry would filter an ineligible combat slot; its post-filter saved-stat projection requires a separate proved producer.");
        }
        return vitals;
    }

    private void RequestPartyMenu(Fiber fiber, Jsm.Opcode opcode)
    {
        NativeFieldMenuKind kind = opcode == Jsm.Opcode.MENUNORMAL ? NativeFieldMenuKind.Normal : NativeFieldMenuKind.PartySelection;
        byte selection = kind == NativeFieldMenuKind.Normal ? services.WorldControls!.Menu209 : (byte)2;
        partyMenu = new NativePartyMenuState(FieldId, programHash, NativeRuntimeProfile!.ExecutableHash, Frame, fiber.ObjectId, fiber.Script.Id, fiber.Position - 1, fiber.Sequence, NativePartyMenuPhase.Requested, 0, null, 0, 0, 0, null, kind, selection, 0, null, null, null, null, null);
        fiber.WaitReason = WaitReason.NativePartyMenu;
    }

    private NativeFieldLocation CaptureNativeMenuFieldLocation(NativePartyMenuState request)
    {
        ImmutableArray<NativeFieldLocationSlot>.Builder slots = ImmutableArray.CreateBuilder<NativeFieldLocationSlot>(3);
        for (int slot = 0; slot < 3; slot++)
        {
            byte? binding = Scene.FieldPartyNativeActorIds.Count == 3 ? checked((byte)Scene.FieldPartyNativeActorIds[slot]) : null;
            NativeFieldLocationPose? pose = null;
            if (binding is byte row && row < Scene.NativeActorObjectIds.Count)
            {
                ActorState actor = actors[Scene.NativeActorObjectIds[row]];
                FieldFixedPosition? position = ReadNativeScriptPosition(actor);
                if (position is not null && actor.WalkmeshTriangle is >= 0 and <= ushort.MaxValue && actor.Direction == MathF.Truncate(actor.Direction) && actor.Direction is >= 0 and <= byte.MaxValue)
                {
                    //470250 uses SAR12, not signed division toward zero.
                    pose = NativeFieldLocationPose.FromPhysicalWords(position.X, position.Y, (ushort)actor.WalkmeshTriangle, (byte)actor.Direction);
                }
            }
            slots.Add(new NativeFieldLocationSlot(binding, pose));
        }
        NativeFieldLocation location = new((ushort)FieldId, Scene.PreviousFieldWord84, 1, slots.MoveToImmutable(), programHash, NativeRuntimeProfile!.ExecutableHash, Frame, request.ObjectId, request.ScriptId, request.Operation);
        location.Validate();
        return location;
    }

    private void PublishPartyMenuCallback(NativeCharacterVitals vitals, NativeEconomy economy, NativeShotAvailabilityState shots, NativeSalaryPresentation salary, bool? timerDisplay = null, NativeFieldLocation? fieldLocation = null, bool advanceVibration = false, bool clearControllerRequests = false, bool markPrimaryControllerPending = false, NativeShopHistory? shopHistory = null, NativeMenuRandomState? menuRandom = null, NativeWeaponRecipes? weaponRecipes = null)
    {
        NativeFieldCallbackClock clock = Scene.FieldCallbackClock ?? throw new NotSupportedException("Party menu requires its actual field allocation clock history.");
        clock.Validate(Frame);
        long menuCallbacks = checked(clock.MenuCallbacks + 1);
        NativeExamTimerState timer = (services.ExamTimer ?? throw new NotSupportedException("Party menu has no process clock authority.")).AdvanceCallbacks(NativeTimerCallbackKind.Menu, 1);
        if (timerDisplay is bool display)
        {
            timer = timer with { DisplayEnabled = display };
        }
        // Native4A22C0 calls4701B0 once before497380. It replaces the field/world
        // callbacks and does not execute their4022B0 world-process producer.
        NativeVibrationProcessState? vibration = services.VibrationProcess;
        if (advanceVibration)
        {
            vibration = (vibration ?? throw new NotSupportedException("Active native party menu requires its49A970 vibration process owner.")).Advance(1);
        }
        if (clearControllerRequests)
        {
            vibration = (vibration ?? throw new NotSupportedException("Native party menu teardown requires its49E8E0 controller request owner.")).ClearControllerRequests();
        }
        if (markPrimaryControllerPending)
        {
            vibration = (vibration ?? throw new NotSupportedException("Native party menu entry requires its49E6D0 controller request owner.")).MarkControllerPending(0);
        }
        services.SetNativePartyMenuOwners(vitals, economy, shots, salary, timer, fieldLocation, vibration, shopHistory: shopHistory, menuRandom: menuRandom, weaponRecipes: weaponRecipes);
        Scene.FieldCallbackClock = clock with { MenuCallbacks = menuCallbacks };
    }

    private void StepPartyMenu()
    {
        NativePartyMenuState current = partyMenu!;
        if (current.Phase is NativePartyMenuPhase.FieldReentry or NativePartyMenuPhase.FieldResumeInitialization)
        {
            StepScriptMenuFieldReturn(current);
            return;
        }
        if (current.Phase == NativePartyMenuPhase.Requested)
        {
            NativeFieldLocation location = CaptureNativeMenuFieldLocation(current);
            bool timerDisplay = services.ExamTimer!.DisplayEnabled;
            PublishPartyMenuCallback(services.CharacterVitals!, services.Economy!, services.ShotAvailability!, services.SalaryPresentation!.Reset(), false, location, markPrimaryControllerPending: true);
            partyMenu = current with { Phase = NativePartyMenuPhase.Entering, Callbacks = 1, TimerDisplayBeforeMenu = timerDisplay };
        }
        else if (current.Phase == NativePartyMenuPhase.Entering)
        {
            if (current.Kind == NativeFieldMenuKind.WeaponShop)
            {
                var preparedWeapon = PrepareNativeWeaponShopEntry(current);
                NativePartyMenuState nextMenu = current with { Phase = NativePartyMenuPhase.Active, Callbacks = checked(current.Callbacks + 1), WeaponShop = preparedWeapon.Shop };
                PublishPartyMenuCallback(preparedWeapon.Vitals, preparedWeapon.Economy, services.ShotAvailability!, services.SalaryPresentation!, weaponRecipes: preparedWeapon.Recipes);
                partyMenu = nextMenu;
                return;
            }
            if (current.Kind == NativeFieldMenuKind.Shop)
            {
                var preparedShop = PrepareNativeShopEntry(current);
                PublishPartyMenuCallback(services.CharacterVitals!, preparedShop.Economy, services.ShotAvailability!, services.SalaryPresentation!, shopHistory: preparedShop.History, menuRandom: preparedShop.MenuRandom);
                partyMenu = current with { Phase = NativePartyMenuPhase.Active, Callbacks = checked(current.Callbacks + 1), Shop = preparedShop.Shop };
                return;
            }
            var prepared = PreparePartyMenuEntry(current.Kind);
            byte eligible = 0;
            byte locked = 0;
            byte forced = 0;
            for (int row = 0; row < 8; row++)
            {
                ushort flags = prepared.Vitals.Records[row].MemberFlags;
                if ((flags & 1) != 0)
                {
                    eligible |= (byte)(1 << row);
                    if ((flags & 6) != 0)
                    {
                        locked |= (byte)(1 << row);
                    }
                    if ((flags & 4) != 0)
                    {
                        forced |= (byte)(1 << row);
                    }
                }
            }
            PublishPartyMenuCallback(prepared.Vitals, prepared.Economy, prepared.Shots, prepared.Salary, clearControllerRequests: true);
            partyMenu = current with { Phase = NativePartyMenuPhase.Active, Callbacks = checked(current.Callbacks + 1), TemporaryCombatParty = prepared.Vitals.CombatParty.ToArray(), TemporaryReserveParty = current.Kind == NativeFieldMenuKind.PartySelection ? CreatePartyMenuReserve(prepared.Vitals.CombatParty, eligible) : null, EligibleMask = eligible, LockedMask = locked, ForcedFieldMask = forced };
        }
        else
        {
            NativeSalaryPresentation salary = current.Phase == NativePartyMenuPhase.Active ? services.SalaryPresentation!.Advance(1) : services.SalaryPresentation!;
            bool? timerDisplay = current.Phase == NativePartyMenuPhase.Restoring ? current.TimerDisplayBeforeMenu : null;
            PublishPartyMenuCallback(services.CharacterVitals!, services.Economy!, services.ShotAvailability!, salary, timerDisplay, advanceVibration: current.Phase == NativePartyMenuPhase.Active, clearControllerRequests: current.Phase == NativePartyMenuPhase.Teardown);
            NativePartyMenuPhase nextPhase = current.Phase switch
            {
                NativePartyMenuPhase.Closing => NativePartyMenuPhase.Teardown,
                NativePartyMenuPhase.Teardown => NativePartyMenuPhase.Restoring,
                NativePartyMenuPhase.Restoring => NativePartyMenuPhase.FieldReentry,
                _ => current.Phase
            };
            partyMenu = current with { Callbacks = checked(current.Callbacks + 1), Phase = nextPhase };
        }
    }

    private bool ConfirmPartyMenu()
    {
        if (partyMenu is not { Phase: NativePartyMenuPhase.Active, Kind: NativeFieldMenuKind.PartySelection } current)
        {
            return false;
        }
        if (current.SelectedPartySlot is not null || current.TemporaryCombatParty!.Any(row => row == 255))
        {
            return false;
        }
        NativeCharacterVitals vitals = services.CharacterVitals!;
        byte[] fieldParty = vitals.PreparePartyMenuFieldSlots(Scene.Party, current.TemporaryCombatParty!);
        NativeCharacterVitals next = (vitals with { CombatParty = current.TemporaryCombatParty!.ToArray() }).RebuildParty();
        PublishPartyMenuCallback(next, services.Economy!, services.ShotAvailability!, services.SalaryPresentation!.Advance(1), advanceVibration: true);
        Scene.Party.Clear();
        Scene.Party.AddRange(fieldParty.Select(value => (int)value));
        partyMenu = current with { Phase = NativePartyMenuPhase.Closing, Callbacks = checked(current.Callbacks + 1), NativeReturnResult = 1 };
        // Native4ABF10 sets the menu-local result bit1. The next menu callback observes completion and exits without49A970.
        return true;
    }

    private bool CloseNormalMenu()
    {
        if (partyMenu is not { Phase: NativePartyMenuPhase.Active, Kind: NativeFieldMenuKind.Normal } current)
        {
            return false;
        }
        //4C0CF0 root cancel closes without4ABF10. No menu feature or saved-party mutation is implied.
        StepPartyMenu();
        partyMenu = current with { Phase = NativePartyMenuPhase.Closing, Callbacks = checked(current.Callbacks + 1) };
        return true;
    }

    private void ValidatePartyMenuCheckpoint(Checkpoint saved, IReadOnlyDictionary<long, Fiber> bySequence)
    {
        NativePartyMenuState? menu = saved.PartyMenu;
        if (menu is null)
        {
            if (bySequence.Values.Any(fiber => fiber.WaitReason == WaitReason.NativePartyMenu))
            {
                throw new InvalidDataException("Saved party-menu wait has no executed request.");
            }
            return;
        }
        if (menu.FieldId != saved.FieldId || menu.ProgramHash != saved.ProgramHash || menu.ExecutableHash != NativeRuntimeProfile?.ExecutableHash || menu.Frame != saved.Frame || menu.Callbacks < 0 || saved.Scene.FieldCallbackClock is null || menu.Callbacks > saved.Scene.FieldCallbackClock.MenuCallbacks || !Enum.IsDefined(menu.Phase) || !Enum.IsDefined(menu.Kind) || menu.NativeReturnResult != (menu.Kind == NativeFieldMenuKind.PartySelection && menu.Phase is NativePartyMenuPhase.Closing or NativePartyMenuPhase.Teardown or NativePartyMenuPhase.Restoring or NativePartyMenuPhase.FieldReentry or NativePartyMenuPhase.FieldResumeInitialization ? 1 : 0) || menu.Kind == NativeFieldMenuKind.PartySelection && menu.NativeEntrySelection != 2 || !bySequence.TryGetValue(menu.OwnerSequence, out Fiber? owner) || owner.ObjectId != menu.ObjectId || owner.Script.Id != menu.ScriptId || menu.Operation < owner.Script.Start || menu.Operation >= owner.Script.End || program!.Operations[menu.Operation].Opcode != (menu.Kind switch { NativeFieldMenuKind.Normal => Jsm.Opcode.MENUNORMAL, NativeFieldMenuKind.PartySelection => Jsm.Opcode.MENUPHS, NativeFieldMenuKind.Shop or NativeFieldMenuKind.WeaponShop => Jsm.Opcode.MENUSHOP, _ => throw new InvalidDataException("Unsupported native menu kind.") }) || (owner.Complete || owner.Position != menu.Operation + 1 || owner.WaitReason != WaitReason.NativePartyMenu))
        {
            throw new InvalidDataException("Saved party menu changed its original request, owner or suspended field frame.");
        }
        if (menu.Phase is NativePartyMenuPhase.Requested or NativePartyMenuPhase.Entering ? menu.Callbacks != (menu.Phase == NativePartyMenuPhase.Requested ? 0 : 1) || menu.TemporaryCombatParty is not null || menu.EligibleMask != 0 || menu.LockedMask != 0 || menu.ForcedFieldMask != 0 || (menu.TimerDisplayBeforeMenu is null) != (menu.Phase == NativePartyMenuPhase.Requested) : menu.Kind is NativeFieldMenuKind.Shop or NativeFieldMenuKind.WeaponShop ? menu.Callbacks < 2 || menu.TimerDisplayBeforeMenu is null || saved.CharacterVitals is null : menu.Callbacks < 2 || menu.TimerDisplayBeforeMenu is null || menu.TemporaryCombatParty is not { Length: 3 } || menu.TemporaryCombatParty.Any(row => row != 255 && row >= 8) || menu.TemporaryCombatParty.Where(row => row != 255).Distinct().Count() != menu.TemporaryCombatParty.Count(row => row != 255) || (menu.LockedMask & ~menu.EligibleMask) != 0 || (menu.ForcedFieldMask & ~menu.LockedMask) != 0 || saved.CharacterVitals is null || menu.Kind == NativeFieldMenuKind.PartySelection && saved.CharacterVitals.DreamEnabled)
        {
            throw new InvalidDataException("Saved party menu lacks its actual native entry/temporary selection.");
        }
        ValidatePartyMenuSelectionCheckpoint(menu);
        if (menu.Kind is not (NativeFieldMenuKind.Shop or NativeFieldMenuKind.WeaponShop) && (menu.ShopRawArgument is not null || menu.Shop is not null || menu.WeaponShop is not null))
        {
            throw new InvalidDataException("Non-shop request contains an unrelated transaction or raw argument.");
        }
        if (menu.Kind == NativeFieldMenuKind.Shop)
        {
            if (menu.WeaponShop is not null)
            {
                throw new InvalidDataException("Ordinary shop contains an unrelated weapon constructor.");
            }
            ValidateNativeShopCheckpoint(saved, menu);
        }
        else if (menu.Kind == NativeFieldMenuKind.WeaponShop)
        {
            ValidateNativeWeaponShopCheckpoint(saved, menu);
        }
        else if (menu.Phase is not (NativePartyMenuPhase.Requested or NativePartyMenuPhase.Entering))
        {
            byte eligible = 0;
            byte locked = 0;
            byte forced = 0;
            for (int row = 0; row < 8; row++)
            {
                NativeCharacterVital record = saved.CharacterVitals!.Records[row];
                if (menu.Kind == NativeFieldMenuKind.PartySelection && record.CharacterId != row)
                {
                    throw new InvalidDataException("Saved party menu changed its native normal identity reset.");
                }
                if ((record.MemberFlags & 1) != 0)
                {
                    eligible |= (byte)(1 << row);
                    if ((record.MemberFlags & 6) != 0)
                    {
                        locked |= (byte)(1 << row);
                    }
                    if ((record.MemberFlags & 4) != 0)
                    {
                        forced |= (byte)(1 << row);
                    }
                }
            }
            if (menu.EligibleMask != eligible || menu.LockedMask != locked || menu.ForcedFieldMask != forced || menu.TemporaryCombatParty!.Any(row => row != 255 && (eligible & 1 << row) == 0) || saved.CharacterVitals!.CombatParty.Any(row => row != 255 && (locked & 1 << row) != 0 && !menu.TemporaryCombatParty.Contains(row)))
            {
                throw new InvalidDataException("Saved menu masks or locked members disagree with the canonical membership WORDs.");
            }
            if (menu.Phase is NativePartyMenuPhase.Closing or NativePartyMenuPhase.Teardown or NativePartyMenuPhase.Restoring or NativePartyMenuPhase.FieldReentry or NativePartyMenuPhase.FieldResumeInitialization && (!menu.TemporaryCombatParty.SequenceEqual(saved.CharacterVitals.CombatParty) || menu.Kind == NativeFieldMenuKind.PartySelection && menu.TemporaryCombatParty.Contains((byte)255)))
            {
                throw new InvalidDataException("Completed party menu differs from its confirmed saved combat party.");
            }
        }
        if (menu.Phase != NativePartyMenuPhase.Requested && (saved.NativeFieldLocation is not NativeFieldLocation location || location.FieldId != menu.FieldId || location.ProgramHash != menu.ProgramHash || location.ExecutableHash != menu.ExecutableHash || location.Frame != menu.Frame || location.ObjectId != menu.ObjectId || location.ScriptId != menu.ScriptId || location.Operation != menu.Operation))
        {
            throw new InvalidDataException("Saved menu lacks its actual470250 capture before the replacement menu callback.");
        }
        if (menu.Phase is not (NativePartyMenuPhase.Requested or NativePartyMenuPhase.FieldReentry or NativePartyMenuPhase.FieldResumeInitialization) && saved.ExamTimer?.DisplayEnabled != false || menu.Phase is NativePartyMenuPhase.FieldReentry or NativePartyMenuPhase.FieldResumeInitialization && saved.ExamTimer?.DisplayEnabled != menu.TimerDisplayBeforeMenu)
        {
            throw new InvalidDataException("Saved party menu did not preserve its native cached timer display disable/restore.");
        }
        if (menu.Phase == NativePartyMenuPhase.FieldResumeInitialization && saved.Scene.FieldCallbackClock.FieldLoaderCallbacks <= 0)
        {
            throw new InvalidDataException("Saved kind6 bootstrap lacks its actual clock-only callback.");
        }
        if (menu.Phase != NativePartyMenuPhase.FieldReentry && (saved.CharacterVitals is null || saved.Economy is null || saved.ShotAvailability is null || saved.SalaryPresentation is null || saved.ExamTimer is null))
        {
            throw new InvalidDataException("Saved active party menu has unknown native entry owners.");
        }
    }
}
