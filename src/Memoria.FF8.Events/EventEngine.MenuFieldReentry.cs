using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

/// <summary>Issued only after actual menu completion; native4706B0 selects471F70 kind10.</summary>
public sealed class NativeMenuFieldReentry
{
    internal NativeMenuFieldReentry(NativeFieldLocation location, int generation)
    {
        Location = location;
        Generation = generation;
    }

    public NativeFieldLocation Location { get; }
    internal int Generation { get; }
}

public sealed partial class EventEngine
{
    private NativeMenuFieldReentry? issuedMenuFieldReentry;
    private NativeMenuFieldReentry? initializingMenuFieldReentry;

    public NativeMenuFieldReentry PreparePartyMenuFieldReentry(byte nativeInfFlag8)
    {
        if (partyMenu is not { Phase: NativePartyMenuPhase.FieldReentry } menu || NativeRuntimeProfile is not { IsVerified: true } || services.NativeFieldLocation is not NativeFieldLocation location || location.FieldId != FieldId || location.ProgramHash != programHash || location.Frame != menu.Frame || location.Operation != menu.Operation)
        {
            throw new NotSupportedException("Native menu field reentry requires its executed request, confirmation and470250 capture.");
        }
        location.Validate();
        if (menu.NativeReturnResult != 1)
        {
            throw new NotSupportedException("Unchanged native normal menu returns kind6/52B4F0(2); retained animation/fade restoration must execute before its old VM resumes.");
        }
        if (nativeInfFlag8 != 0 && services.FieldFade?.MapFadeDisabled != 1)
        {
            throw new NotSupportedException("Menu reentry requires actual byte4905!=0 or INF[8]==0 for fresh52BC00; the alternate retained allocation is unproved.");
        }
        if (location.IncomingEntryWords is null)
        {
            throw new NotSupportedException("Menu kind10 requires its retained native incoming entry words;470250 saved current poses do not replace that packet.");
        }
        if (Scene.NativeSalaryMovementUnits is null || services.FieldShake is null || Scene.PreviousFieldWord84 != location.PreviousFieldId)
        {
            throw new NotSupportedException("Menu kind10 requires the actual retained scalar, shake process and previous-field WORD.");
        }
        NativeCharacterVitals vitals = services.CharacterVitals ?? throw new NotSupportedException("Menu reentry requires its confirmed character owner.");
        if (vitals.DreamEnabled || !vitals.CombatParty.SequenceEqual(menu.TemporaryCombatParty!))
        {
            throw new InvalidDataException("Menu reentry no longer has the actual confirmed combat party.");
        }
        RequireKnownZeroMenuPartyAbilityFlags(vitals);
        //52D110 clears party/trace flags and52D050 rebuilds bindings before fresh allocation.
        //The cleared physical records are then discarded by the proved INF[8]==0 branch.
        issuedMenuFieldReentry = new NativeMenuFieldReentry(location, generation);
        return issuedMenuFieldReentry;
    }

    private void ValidateMenuFieldReentry(NativeMenuFieldReentry? reentry, int fieldId, string hash, FieldTransition? arrival, int? previousFieldId)
    {
        if (reentry is null)
        {
            return;
        }
        if (!ReferenceEquals(reentry, issuedMenuFieldReentry) || reentry.Generation != generation || partyMenu?.Phase != NativePartyMenuPhase.FieldReentry || fieldId != FieldId || hash != programHash || arrival != reentry.Location.IncomingEntryWords || previousFieldId is not null)
        {
            throw new InvalidDataException("Menu reentry changed its actual same-field allocation authority or retained entry packet.");
        }
    }

    private void CompleteMenuFieldInitialization()
    {
        if (initializingMenuFieldReentry is null)
        {
            return;
        }
        CompleteNativeMenuFieldTail(true);
        initializingMenuFieldReentry = null;
    }

    private void CompleteNativeMenuFieldTail(bool fresh, NativeFieldFadeState? fieldFade = null)
    {
        NativeWorldControlState controls = services.WorldControls ?? throw new NotSupportedException("Menu52B4F0(3) requires its complete eventDWORD104 owner.");
        NativeCharacterVitals vitals = services.CharacterVitals ?? throw new NotSupportedException("Menu52B4F0(3) requires actual495EF0 ability inputs.");
        NativeSeeDState seeD = services.SeeD ?? throw new NotSupportedException("Menu52B4F0(3) requires actual salary WORDs.");
        NativeExamTimerState timer = services.ExamTimer ?? throw new NotSupportedException("Menu52B4F0(3) requires actual timer UI ownership.");
        NativeSalaryPresentation salary = services.SalaryPresentation ?? throw new NotSupportedException("Menu52B4F0(3) requires actual salary UI ownership.");
        if (!services.CanStoreNativeEventOwners || Scene.MusicProcess is null || Scene.DrawPointEnabled != 0 || variables.Keys.Any(address => address == 85) || services.NativeVariableValues?.Any(pair => pair.Key <= 88 && pair.Key + pair.Value.Width > 87) == true)
        {
            throw new NotSupportedException("Menu52B4F0(3) requires owned music, inactive draw-point dispatch and unaliased entry/source BYTEs.");
        }
        RequireKnownZeroMenuPartyAbilityFlags(vitals);
        if ((controls.EventFlags104 & 0x40) != 0)
        {
            if (!timer.TimerMemoryActivated || variables.Keys.Any(address => address is >= 202 and <= 206))
            {
                throw new NotSupportedException("Menu timer display reload requires its actual saved DISPTIMER coordinates.");
            }
            timer = timer with { DisplayEnabled = true };
        }
        if (!controls.SalaryDisplaySuppressed && unchecked((short)seeD.SalaryDisplayCountdown184) > 0)
        {
            NativeSalaryTable table = seeD.SalaryTable ?? throw new NotSupportedException("Menu rank-transition presentation requires the original salary table.");
            int oldRank = unchecked((short)seeD.PreviousRank186) / 100;
            int newRank = unchecked((short)seeD.RankPoints) / 100;
            salary = salary.StartPayment(oldRank, newRank, table.Pay(oldRank), table.Pay(newRank));
            seeD = seeD with { SalaryDisplayCountdown184 = 90 };
        }
        services.SetNativeEventOwners(controls, seeD: seeD, timer: timer, salaryPresentation: salary, fieldFade: fieldFade);
        Scene.MusicProcess = Scene.MusicProcess with { Complete214 = 1 };
        if (fresh)
        {
            //52B4F0(3) resets every background record after init and binding placement.
            Scene.BackgroundScrollLayers.Clear();
            InitializeNativeBackgroundScrollSettings();
            Scene.BackgroundScrollPositions.Clear();
        }
        else
        {
            //52B9F0 writes only primary mode/leader/flags; ratios/offsets and other seven records remain.
            Scene.BackgroundScrollPositions.Remove(0);
        }
        Scene.ActorBackgroundScroll = null;
        Scene.NormalBackgroundScrollCenter = null;
        //495EF0 cleared1CFF6D8; none of the actual equipped bytes enters495530's78..82 OR branch.
        variables[88] = 0;
    }

    private void StepUnchangedNormalMenuFieldReturn(NativePartyMenuState current)
    {
        NativeFieldCallbackClock clock = Scene.FieldCallbackClock ?? throw new NotSupportedException("Native kind6 requires its current callback ledger.");
        clock.Validate(Frame);
        if (current.Phase == NativePartyMenuPhase.FieldReentry)
        {
            NativeExamTimerState timer = (services.ExamTimer ?? throw new NotSupportedException("Native kind6 requires its retained process clock.")).AdvanceCallbacks(NativeTimerCallbackKind.NormalField, 1);
            NativeWorldProcess process = (services.WorldProcess ?? throw new NotSupportedException("Native kind6 requires its actual outer4022B0 process.")).AdvanceMainLoopCallbacks(1);
            NativeVibrationProcessState vibration = (services.VibrationProcess ?? throw new NotSupportedException("Native kind6 requires its actual queue.")).StopAll().ClearControllerRequests();
            NativeFieldFadeState fade = (services.FieldFade ?? throw new NotSupportedException("Native kind6 requires its actual physical fade registers.")).ResetPhysicalForLoader();
            services.SetNativePartyMenuOwners(services.CharacterVitals!, services.Economy!, services.ShotAvailability!, services.SalaryPresentation!, timer, vibrationProcess: vibration, worldProcess: process, fieldFade: fade);
            //471F70 kind6 first callback returns before4767B0. VM/animation Frame does not advance.
            Scene.FieldCallbackClock = clock with { FieldLoaderCallbacks = checked(clock.FieldLoaderCallbacks + 1) };
            partyMenu = current with { Phase = NativePartyMenuPhase.FieldResumeInitialization };
            return;
        }
        NativeFieldActorAnimation[] animations = PrepareNativeFieldAnimations(Scene.NativeActorObjectIds.Where(objectId => (actors[objectId].NativePartyFlags ?? throw new NotSupportedException("Kind6 animation restore requires executed actor DWORD352 mask4.")).AutoAnimation4).ToArray());
        NativeFieldFadeState restoredFade = (services.FieldFade ?? throw new NotSupportedException("Native52B9F0 requires saved/physical fade words.")).ResumeKind6();
        CompleteNativeMenuFieldTail(false, restoredFade);
        foreach (NativeFieldActorAnimation animation in animations)
        {
            SelectFullNativeAnimation(actors[animation.ObjectId], animation.Idle, true);
        }
        Fiber owner = fibers.Single(fiber => fiber.Sequence == current.OwnerSequence);
        owner.WaitReason = WaitReason.None;
        partyMenu = null;
        //52B9F0 does not reallocate or reset JSM slots. This next real4767B0 resumes the advanced issuing PC.
        StepCore();
    }

    private static void RequireKnownZeroMenuPartyAbilityFlags(NativeCharacterVitals vitals)
    {
        foreach (byte row in vitals.CombatParty.Where(row => row != 255))
        {
            uint abilities = vitals.Records[row].Junction.Abilities;
            for (int index = 0; index < 4; index++)
            {
                byte ability = (byte)(abilities >> (index * 8));
                if (ability is >= 78 and < 83)
                {
                    throw new NotSupportedException("Menu source BYTE88 consumes495530 party ability flags78..82 whose kernel projection is not yet owned.");
                }
            }
        }
    }
}
