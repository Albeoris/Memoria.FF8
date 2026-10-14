using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private void CompleteNativeMenuFieldTail(NativeFieldFadeState fieldFade, NativeVibrationProcessState vibration)
    {
        NativeWorldControlState controls = services.WorldControls ?? throw new NotSupportedException("Menu52B4F0(2) requires its complete eventDWORD104 owner.");
        NativeCharacterVitals vitals = services.CharacterVitals ?? throw new NotSupportedException("Menu52B4F0(2) requires actual495EF0 ability inputs.");
        NativeSeeDState seeD = services.SeeD ?? throw new NotSupportedException("Menu52B4F0(2) requires actual salary WORDs.");
        NativeExamTimerState timer = services.ExamTimer ?? throw new NotSupportedException("Menu52B4F0(2) requires actual timer UI ownership.");
        NativeSalaryPresentation salary = services.SalaryPresentation ?? throw new NotSupportedException("Menu52B4F0(2) requires actual salary UI ownership.");
        if (!services.CanStoreNativeEventOwners || !services.CanStoreNativeVariableValues || Scene.MusicProcess is null || variables.Keys.Any(address => address is 85 or 88) || services.NativeVariableValues?.Any(pair => pair.Key <= 88 && pair.Key + pair.Value.Width > 88 && (pair.Key != 88 || pair.Value.Width != 1)) == true)
        {
            throw new NotSupportedException("Menu52B4F0(2) requires owned music and unaliased entry/source BYTEs.");
        }
        RequireKnownZeroMenuPartyAbilityFlags(vitals);
        ushort? drawPointMask = PrepareMenuDrawPointDisplayMask();
        Dictionary<int, NativeMemoryValue> nativeMemory = services.NativeVariableValues is { } retainedMemory ? new(retainedMemory) : [];
        nativeMemory[88] = new NativeMemoryValue(1, NativeScalarValue.Exact(0));
        NativeMemoryValue.Validate(nativeMemory, new Dictionary<int, int>(variables));
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
        services.SetNativeEventOwners(controls, seeD: seeD, timer: timer, salaryPresentation: salary, vibrationProcess: vibration, fieldFade: fieldFade, nativeVariableValues: nativeMemory);
        Scene.MusicProcess = Scene.MusicProcess with { Complete214 = 1 };
        //52B4F0's common tail calls4747A0 only when B240 is nonzero;
        //it preserves B240/241/242 and never calls the eight-anchor reset474750.
        Scene.NativeDrawPointDisplayMask = drawPointMask;
        //52B9F0 writes only primary mode/leader/flags; ratios/offsets and other seven records remain.
        Scene.BackgroundScrollPositions.Remove(0);
        Scene.ActorBackgroundScroll = null;
        Scene.NormalBackgroundScrollCenter = null;
        //495EF0 cleared1CFF6D8; the proved absent78..82 branch produced typed BYTE88 above.
    }

    private void StepScriptMenuFieldReturn(NativePartyMenuState current)
    {
        NativeFieldCallbackClock clock = Scene.FieldCallbackClock ?? throw new NotSupportedException("Native kind6 requires its current callback ledger.");
        clock.Validate(Frame);
        if (current.Phase == NativePartyMenuPhase.FieldReentry)
        {
            NativeExamTimerState timer = (services.ExamTimer ?? throw new NotSupportedException("Native kind6 requires its retained process clock.")).AdvanceCallbacks(NativeTimerCallbackKind.NormalField, 1);
            NativeWorldProcess process = (services.WorldProcess ?? throw new NotSupportedException("Native kind6 requires its actual outer4022B0 process.")).AdvanceMainLoopCallbacks(1);
            NativeVibrationProcessState vibration = (services.VibrationProcess ?? throw new NotSupportedException("Native kind6 requires its actual queue.")).StopAll().ClearControllerRequests();
            NativeFieldFadeState fade = (services.FieldFade ?? throw new NotSupportedException("Native kind6 requires its actual physical fade registers.")).ResetPhysicalForLoader();
            if ((current.NativeReturnResult & 1) != 0)
            {
                //4706B0 executes52D110/52D050, but script entry main-mode10 still selects kind6.
                RefreshParty();
            }
            services.SetNativePartyMenuOwners(services.CharacterVitals!, services.Economy!, services.ShotAvailability!, services.SalaryPresentation!, timer, vibrationProcess: vibration, worldProcess: process, fieldFade: fade);
            //471F70 kind6 first callback returns before4767B0. VM/animation Frame does not advance.
            Scene.FieldCallbackClock = clock with { FieldLoaderCallbacks = checked(clock.FieldLoaderCallbacks + 1) };
            partyMenu = current with { Phase = NativePartyMenuPhase.FieldResumeInitialization };
            return;
        }
        NativeFieldActorAnimation[] animations = PrepareNativeFieldAnimations(Scene.NativeActorObjectIds.Where(objectId => (actors[objectId].NativePartyFlags ?? throw new NotSupportedException("Kind6 animation restore requires executed actor DWORD352 mask4.")).AutoAnimation4).ToArray());
        NativeFieldFadeState restoredFade = (services.FieldFade ?? throw new NotSupportedException("Native52B9F0 requires saved/physical fade words.")).ResumeKind6();
        //4767B0's first resumed callback clears4905 and marks port0 pending before529FF0/VM.
        NativeVibrationProcessState resumedController = (services.VibrationProcess ?? throw new NotSupportedException("Native4767B0 resume requires its actual controller owner.")).MarkControllerPending(0);
        CompleteNativeMenuFieldTail(restoredFade.SetMapFadeDisabled(false), resumedController);
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
