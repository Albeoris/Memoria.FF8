using Albeoris.Games.FF8.Jsm;
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
    FieldReentry
}

/// <summary>Executed521D40 request and the4CB880 temporary party; the minimal interactive panel does not claim original raster parity.</summary>
public sealed record NativePartyMenuState([property: JsonRequired] int FieldId, [property: JsonRequired] string ProgramHash, [property: JsonRequired] string ExecutableHash, [property: JsonRequired] long Frame, [property: JsonRequired] int ObjectId, [property: JsonRequired] int ScriptId, [property: JsonRequired] int Operation, [property: JsonRequired] long OwnerSequence, [property: JsonRequired] NativePartyMenuPhase Phase, [property: JsonRequired] long Callbacks, [property: JsonRequired] byte[]? TemporaryCombatParty, [property: JsonRequired] byte EligibleMask, [property: JsonRequired] byte LockedMask, [property: JsonRequired] byte ForcedFieldMask, [property: JsonRequired] bool? TimerDisplayBeforeMenu)
{
    [JsonIgnore]
    public int NativeMode => 5;
    [JsonIgnore]
    public int NativeMenuId => 1;
    [JsonIgnore]
    public byte NativeEntrySelection => 2;
}

public sealed partial class EventEngine
{
    private NativePartyMenuState? partyMenu;
    public NativePartyMenuState? PartyMenu => partyMenu is NativePartyMenuState current ? current with { TemporaryCombatParty = current.TemporaryCombatParty?.ToArray() } : null;
    public IReadOnlyList<byte> PartyMenuCandidates => partyMenu is { Phase: NativePartyMenuPhase.Active } current ? Enumerable.Range(0, 8).Where(row => (current.EligibleMask & 1 << row) != 0 && (current.LockedMask & 1 << row) == 0 && !current.TemporaryCombatParty!.Contains((byte)row)).Select(row => (byte)row).ToArray() : [];

    private string? UnsupportedPartyMenuRequest()
    {
        if (NativeRuntimeProfile is not { IsVerified: true } || partyMenu is not null || services.IsMoviePlaying || !services.CanStoreNativePartyMenuOwners || services.CharacterVitals is null || services.Economy is null || services.ShotAvailability is null || services.SalaryPresentation is null || services.ExamTimer is null)
        {
            return "MENUPHS requires the verified native request, actual character/economy/shot/UI/clock owners and no active menu or movie";
        }
        try
        {
            _ = PreparePartyMenuEntry();
            return null;
        }
        catch (Exception exception) when (exception is NotSupportedException or InvalidDataException)
        {
            return exception.Message;
        }
    }

    private (NativeCharacterVitals Vitals, NativeEconomy Economy, NativeShotAvailabilityState Shots, NativeSalaryPresentation Salary) PreparePartyMenuEntry()
    {
        NativeCharacterVitals vitals = services.CharacterVitals!.EnterNormalPartyMenu();
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

    private void RequestPartyMenu(Fiber fiber)
    {
        partyMenu = new NativePartyMenuState(FieldId, programHash, NativeRuntimeProfile!.ExecutableHash, Frame, fiber.ObjectId, fiber.Script.Id, fiber.Position - 1, fiber.Sequence, NativePartyMenuPhase.Requested, 0, null, 0, 0, 0, null);
        fiber.WaitReason = WaitReason.NativePartyMenu;
    }

    private void PublishPartyMenuCallback(NativeCharacterVitals vitals, NativeEconomy economy, NativeShotAvailabilityState shots, NativeSalaryPresentation salary, bool? timerDisplay = null)
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
        services.SetNativePartyMenuOwners(vitals, economy, shots, salary, timer);
        Scene.FieldCallbackClock = clock with { MenuCallbacks = menuCallbacks };
    }

    private void StepPartyMenu()
    {
        NativePartyMenuState current = partyMenu!;
        if (current.Phase == NativePartyMenuPhase.FieldReentry)
        {
            throw new NotSupportedException("Confirmed native MENUPHS requires4706B0/52D110/471F70/52B4F0(3) field reentry; the loader, actor flags and bindings are unproved, so no field VM, pose or rendering consumer resumes.");
        }
        if (current.Phase == NativePartyMenuPhase.Requested)
        {
            bool timerDisplay = services.ExamTimer!.DisplayEnabled;
            PublishPartyMenuCallback(services.CharacterVitals!, services.Economy!, services.ShotAvailability!, services.SalaryPresentation!.Reset(), false);
            partyMenu = current with { Phase = NativePartyMenuPhase.Entering, Callbacks = 1, TimerDisplayBeforeMenu = timerDisplay };
        }
        else if (current.Phase == NativePartyMenuPhase.Entering)
        {
            var prepared = PreparePartyMenuEntry();
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
            PublishPartyMenuCallback(prepared.Vitals, prepared.Economy, prepared.Shots, prepared.Salary);
            partyMenu = current with { Phase = NativePartyMenuPhase.Active, Callbacks = checked(current.Callbacks + 1), TemporaryCombatParty = prepared.Vitals.CombatParty.ToArray(), EligibleMask = eligible, LockedMask = locked, ForcedFieldMask = forced };
        }
        else
        {
            NativeSalaryPresentation salary = current.Phase == NativePartyMenuPhase.Active ? services.SalaryPresentation!.Advance(1) : services.SalaryPresentation!;
            bool? timerDisplay = current.Phase == NativePartyMenuPhase.Restoring ? current.TimerDisplayBeforeMenu : null;
            PublishPartyMenuCallback(services.CharacterVitals!, services.Economy!, services.ShotAvailability!, salary, timerDisplay);
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

    private bool SelectPartyMenuMember(int character)
    {
        if (partyMenu is not { Phase: NativePartyMenuPhase.Active } current || (uint)character >= 8 || !PartyMenuCandidates.Contains((byte)character))
        {
            return false;
        }
        int slot = Array.IndexOf(current.TemporaryCombatParty!, (byte)255);
        if (slot < 0)
        {
            throw new NotSupportedException("Replacing an occupied party-menu slot requires an explicit slot selection; no automatic member removal is implemented.");
        }
        byte[] selected = current.TemporaryCombatParty!.ToArray();
        selected[slot] = (byte)character;
        StepPartyMenu();
        partyMenu = partyMenu! with { TemporaryCombatParty = selected };
        return true;
    }

    private bool ConfirmPartyMenu()
    {
        if (partyMenu is not { Phase: NativePartyMenuPhase.Active } current)
        {
            return false;
        }
        if (current.TemporaryCombatParty!.Any(row => row == 255))
        {
            return false;
        }
        NativeCharacterVitals vitals = services.CharacterVitals!;
        byte[] fieldParty = vitals.PreparePartyMenuFieldSlots(Scene.Party, current.TemporaryCombatParty!);
        NativeCharacterVitals next = (vitals with { CombatParty = current.TemporaryCombatParty!.ToArray() }).RebuildParty();
        PublishPartyMenuCallback(next, services.Economy!, services.ShotAvailability!, services.SalaryPresentation!.Advance(1));
        Scene.Party.Clear();
        Scene.Party.AddRange(fieldParty.Select(value => (int)value));
        partyMenu = current with { Phase = NativePartyMenuPhase.Closing, Callbacks = checked(current.Callbacks + 1) };
        // Native4ABF10 sets the menu-local result bit1. The next menu callback observes completion and exits without49A970.
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
        if (menu.FieldId != saved.FieldId || menu.ProgramHash != saved.ProgramHash || menu.ExecutableHash != NativeRuntimeProfile?.ExecutableHash || menu.Frame != saved.Frame || menu.Callbacks < 0 || saved.Scene.FieldCallbackClock is null || menu.Callbacks > saved.Scene.FieldCallbackClock.MenuCallbacks || !Enum.IsDefined(menu.Phase) || !bySequence.TryGetValue(menu.OwnerSequence, out Fiber? owner) || owner.ObjectId != menu.ObjectId || owner.Script.Id != menu.ScriptId || menu.Operation < owner.Script.Start || menu.Operation >= owner.Script.End || program!.Operations[menu.Operation].Opcode != Jsm.Opcode.MENUPHS || (owner.Complete || owner.Position != menu.Operation + 1 || owner.WaitReason != WaitReason.NativePartyMenu))
        {
            throw new InvalidDataException("Saved party menu changed its original request, owner or suspended field frame.");
        }
        if (menu.Phase is NativePartyMenuPhase.Requested or NativePartyMenuPhase.Entering ? menu.Callbacks != (menu.Phase == NativePartyMenuPhase.Requested ? 0 : 1) || menu.TemporaryCombatParty is not null || menu.EligibleMask != 0 || menu.LockedMask != 0 || menu.ForcedFieldMask != 0 || (menu.TimerDisplayBeforeMenu is null) != (menu.Phase == NativePartyMenuPhase.Requested) : menu.Callbacks < 2 || menu.TimerDisplayBeforeMenu is null || menu.TemporaryCombatParty is not { Length: 3 } || menu.TemporaryCombatParty.Any(row => row != 255 && row >= 8) || menu.TemporaryCombatParty.Where(row => row != 255).Distinct().Count() != menu.TemporaryCombatParty.Count(row => row != 255) || (menu.LockedMask & ~menu.EligibleMask) != 0 || (menu.ForcedFieldMask & ~menu.LockedMask) != 0 || saved.CharacterVitals is not { DreamEnabled: false })
        {
            throw new InvalidDataException("Saved party menu lacks its actual native entry/temporary selection.");
        }
        if (menu.Phase is not (NativePartyMenuPhase.Requested or NativePartyMenuPhase.Entering))
        {
            byte eligible = 0;
            byte locked = 0;
            byte forced = 0;
            for (int row = 0; row < 8; row++)
            {
                NativeCharacterVital record = saved.CharacterVitals!.Records[row];
                if (record.CharacterId != row)
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
            if (menu.Phase is NativePartyMenuPhase.Closing or NativePartyMenuPhase.Teardown or NativePartyMenuPhase.Restoring or NativePartyMenuPhase.FieldReentry && (!menu.TemporaryCombatParty.SequenceEqual(saved.CharacterVitals.CombatParty) || menu.TemporaryCombatParty.Contains((byte)255)))
            {
                throw new InvalidDataException("Completed party menu differs from its confirmed saved combat party.");
            }
        }
        if (menu.Phase is not (NativePartyMenuPhase.Requested or NativePartyMenuPhase.FieldReentry) && saved.ExamTimer?.DisplayEnabled != false || menu.Phase == NativePartyMenuPhase.FieldReentry && saved.ExamTimer?.DisplayEnabled != menu.TimerDisplayBeforeMenu)
        {
            throw new InvalidDataException("Saved party menu did not preserve its native cached timer display disable/restore.");
        }
        if (menu.Phase != NativePartyMenuPhase.FieldReentry && (saved.CharacterVitals is null || saved.Economy is null || saved.ShotAvailability is null || saved.SalaryPresentation is null || saved.ExamTimer is null))
        {
            throw new InvalidDataException("Saved active party menu has unknown native entry owners.");
        }
    }
}
