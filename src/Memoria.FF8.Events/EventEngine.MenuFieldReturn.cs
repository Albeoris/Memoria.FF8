using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    internal sealed record NativeFieldResumeTail(NativeWorldControlState Controls, NativeSeeDState SeeD, NativeExamTimerState Timer, NativeSalaryPresentation Salary, NativeVibrationProcessState Vibration, NativeFieldFadeState Fade, NativeEventMemory Memory, ushort? DrawPointMask, NativeMusicProcessState Music);

    private NativeFieldResumeTail PrepareNativeFieldResumeTail(NativeFieldFadeState fieldFade, NativeVibrationProcessState vibration, NativeMusicProcessState music)
    {
        NativeWorldControlState controls = services.WorldControls ?? throw new NotSupportedException("Menu52B4F0(2) requires its complete eventDWORD104 owner.");
        NativeCharacterVitals vitals = services.CharacterVitals ?? throw new NotSupportedException("Menu52B4F0(2) requires actual495EF0 ability inputs.");
        NativeSeeDState seeD = services.SeeD ?? throw new NotSupportedException("Menu52B4F0(2) requires actual salary WORDs.");
        NativeExamTimerState timer = services.ExamTimer ?? throw new NotSupportedException("Menu52B4F0(2) requires actual timer UI ownership.");
        NativeSalaryPresentation salary = services.SalaryPresentation ?? throw new NotSupportedException("Menu52B4F0(2) requires actual salary UI ownership.");
        if (!services.CanStoreNativeEventOwners || !services.CanStoreEventMemory || Scene.MusicProcess is null || EventMemory.Cells.Any(pair => pair.Key < 89 && (long)pair.Key + pair.Value.Width > 88 && (pair.Key != 88 || pair.Value.Width != 1)))
        {
            throw new NotSupportedException("Menu52B4F0(2) requires owned music and unaliased entry/source BYTEs.");
        }
        byte drawPointSource = (byte)((vitals.GetEncounterAbilityFlags() & 0x10) != 0 ? 1 : 0);
        ushort? drawPointMask = PrepareMenuDrawPointDisplayMask(drawPointSource);
        NativeEventMemory memory = EventMemory.Write(88, 1, NativeScalarValue.Exact(drawPointSource));
        if ((controls.EventFlags104 & 0x40) != 0)
        {
            if (!timer.TimerMemoryActivated || EventMemory.Overlaps(205, 2))
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
        return new(controls, seeD, timer, salary, vibration, fieldFade, memory, drawPointMask, music.Complete214 == 1 ? music : music with { Complete214 = 1 });
    }

    private void PublishNativeFieldResumeScene(NativeFieldResumeTail tail)
    {
        Scene.MusicProcess = tail.Music;
        //52B4F0's common tail calls4747A0 only when B240 is nonzero;
        //it preserves B240/241/242 and never calls the eight-anchor reset474750.
        Scene.NativeDrawPointDisplayMask = tail.DrawPointMask;
        //52B9F0 writes only primary mode/leader/flags; ratios/offsets and other seven records remain.
        Scene.BackgroundScrollPositions.Remove(0);
        Scene.ActorBackgroundScroll = null;
        Scene.NormalBackgroundScrollCenter = null;
        //495EF0 rebuilds the combat party ability flags before the tail publishes BYTE88.
    }

    private void CompleteNativeMenuFieldTail(NativeFieldFadeState fieldFade, NativeVibrationProcessState vibration)
    {
        NativeFieldResumeTail tail = PrepareNativeFieldResumeTail(fieldFade, vibration, Scene.MusicProcess ?? throw new NotSupportedException("Menu52B4F0(2) requires its physical music buffers."));
        services.SetNativeEventOwners(tail.Controls, seeD: tail.SeeD, timer: tail.Timer, salaryPresentation: tail.Salary, vibrationProcess: tail.Vibration, fieldFade: tail.Fade, eventMemory: tail.Memory);
        PublishNativeFieldResumeScene(tail);
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
        ConsumeNativeFieldRequestPacket();
        CompleteNativeMenuFieldTail(restoredFade.SetMapFadeDisabled(false), resumedController);
        foreach (int objectId in Scene.NativeActorObjectIds)
        {
            ActorState actor = actors[objectId];
            if (actor.NativePartyFlags is not { AutoAnimation4: true })
            {
                PublishNativeAnimation(actor, true);
            }
        }
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
}
