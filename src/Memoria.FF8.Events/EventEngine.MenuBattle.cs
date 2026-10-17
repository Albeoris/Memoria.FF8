using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed record NativeMenuBattleOwner(int ItemId, ushort Formation, ushort Flags, SceneKind SourceScene, int SourceFieldId, long SourceFrame, string SourceHash, long AllocationClockCalls, long CallbackBase, int Callbacks, long BattleClockCallsAtEntry, byte? Result = null, long? ReturnFrame = null, NativeBattleDefeatReturnBranch? DefeatBranch = null)
{
    public NativeBattleAbandonment? Abandonment { get; init; }
}

public sealed partial class EventEngine
{
    public NativeMenuBattleOwner? PendingMenuBattle => Scene.NativeMenuBattle is { Result: null, ReturnFrame: null, Abandonment: null } pending ? pending : null;

    public void ValidateNativeMenuBattleEntry(int itemId, ushort formation, SceneKind sourceScene, long sourceFrame, string sourceHash, ushort flags)
    {
        if (itemId is < 1 or > byte.MaxValue || sourceScene is not (SceneKind.Field or SceneKind.WorldMap) || sourceFrame < 1 || sourceHash is not { Length: 64 } || !sourceHash.All(Uri.IsHexDigit) || PendingMenuBattle is not null || PendingHostedBattle is not null || HasPendingRandomBattle?.Invoke() == true || NativeRuntimeProfile is not { IsVerified: true } || services.ExamTimer is null || services.WorldProcess is null)
        {
            throw new NotSupportedException("Native menu battle requires its actual item, physical scene, process clock and uncontested entry.");
        }
        if (Scene.NativeBattleFlagWrites is not { Count: > 0 } writes || writes[^1].ActiveFlags != flags)
        {
            throw new NotSupportedException("Native menu battle preserves the actual active battle flags.");
        }
        if (sourceScene == SceneKind.Field && (sourceFrame != Frame || sourceHash != programHash || Scene.NativeFieldInitialization is not { Stage: NativeFieldInitializationStage.Completed } || Scene.FieldCallbackClock is null))
        {
            throw new NotSupportedException("Native menu battle requires its actual initialized field source.");
        }
        if (partyMenu is { } menu && (sourceScene != SceneKind.Field || menu.Kind != NativeFieldMenuKind.Normal || menu.Phase != NativePartyMenuPhase.Active || services.CharacterVitals is null || services.Economy is null || services.ShotAvailability is null || services.SalaryPresentation is null || services.VibrationProcess is null))
        {
            throw new NotSupportedException("Native menu battle requires the active normal menu and its actual teardown owners.");
        }
        if (services.IsMoviePlaying || PendingTripleTriad is not null || Scene.NativeTutorial is not null || Scene.NativeMenuTutorial is not null || sourceScene == SceneKind.Field && partyMenu is null && (!CanAcceptFieldMovement || !Scene.MenuEnabled || CurrentDialogue is not null))
        {
            throw new NotSupportedException("Native menu battle requires an available physical Item menu context.");
        }
        if ((partyMenu?.TimerDisplayBeforeMenu ?? services.ExamTimer.DisplayEnabled))
        {
            throw new InvalidOperationException("Magical Lamp is unavailable while the retained native timer display is enabled.");
        }
    }

    public void BeginNativeMenuBattle(int itemId, ushort formation, SceneKind sourceScene, long sourceFrame, string sourceHash, ushort flags)
    {
        ValidateNativeMenuBattleEntry(itemId, formation, sourceScene, sourceFrame, sourceHash, flags);
        if (partyMenu is { } menu)
        {
            //4F81F0/4C0CF0 exit through the actual menu teardown before4706B0 tests result4.
            partyMenu = menu with { Phase = NativePartyMenuPhase.Closing, NativeReturnResult = 4 };
            while (partyMenu!.Phase != NativePartyMenuPhase.FieldReentry)
            {
                StepPartyMenu();
            }
            Fiber owner = fibers.Single(fiber => fiber.Sequence == menu.OwnerSequence);
            owner.WaitReason = WaitReason.None;
            partyMenu = null;
            ConsumeNativeFieldRequestPacket();
        }
        long allocation = Scene.NativeFieldInitialization?.TimerClockCallsAtAllocation ?? 0;
        long callbackBase = sourceScene == SceneKind.Field ? Scene.FieldCallbackClock!.MenuBattleCallbacks : 0;
        Scene.NativeMenuBattle = new(itemId, formation, flags, sourceScene, FieldId, sourceFrame, sourceHash, allocation, callbackBase, 0, services.ExamTimer!.ClockCalls);
    }

    public void AdvanceMenuBattleCallbacks(int callbacks)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(callbacks);
        NativeMenuBattleOwner owner = PendingMenuBattle ?? throw new InvalidOperationException("No native menu battle owns combat callbacks.");
        NativeExamTimerState timer = services.ExamTimer!.AdvanceCallbacks(NativeTimerCallbackKind.Battle, callbacks);
        NativeWorldProcess process = services.WorldProcess!.AdvanceMainLoopCallbacks(callbacks);
        services.SetExamTimer(timer);
        services.SetWorldProcess(process);
        if (owner.SourceScene == SceneKind.Field)
        {
            NativeFieldCallbackClock clock = Scene.FieldCallbackClock!;
            Scene.FieldCallbackClock = clock with { MenuBattleCallbacks = checked(clock.MenuBattleCallbacks + callbacks) };
        }
        Scene.NativeMenuBattle = owner with { Callbacks = checked(owner.Callbacks + callbacks) };
    }

    public void ReturnFromNativeMenuBattle(int callbacks, byte result, Action publishOutcome)
    {
        NativeMenuBattleOwner owner = PendingMenuBattle ?? throw new InvalidOperationException("No native menu battle owns physical scene return.");
        if (callbacks != owner.Callbacks || !SupportsNativeFieldReturnResult(result) || owner.SourceScene == SceneKind.Field && Scene.FieldCallbackClock?.MenuBattleCallbacks != owner.CallbackBase + callbacks)
        {
            throw new InvalidDataException("Native menu battle return disagrees with its actual callback owner or return branch.");
        }
        publishOutcome();
        Scene.NativeMenuBattle = owner with { Result = result, ReturnFrame = owner.SourceScene == SceneKind.Field ? Frame : owner.SourceFrame, DefeatBranch = result == 1 ? ReadNativeDefeatReturnBranch4706B0() : null };
        Scene.NativeBattleResult = result;
    }

    private static void ValidateNativeMenuBattleCheckpoint(Checkpoint saved)
    {
        if (saved.Scene.NativeMenuBattle is not { } menu)
        {
            return;
        }
        if (menu.Abandonment is { } abandoned)
        {
            abandoned.Validate();
            if (menu.Result is not null || menu.ReturnFrame is not null || abandoned.Encounter.ScriptedOwner is not null || abandoned.Encounter.SourceScene != menu.SourceScene || abandoned.Encounter.SourceFieldId != menu.SourceFieldId || abandoned.Encounter.SourceHash != menu.SourceHash || abandoned.Encounter.SourceFrame != menu.SourceFrame || abandoned.Encounter.Formation != menu.Formation || abandoned.EntryClockCalls != menu.BattleClockCallsAtEntry || abandoned.CombatCallbacks != menu.Callbacks)
            {
                throw new InvalidDataException("Abandoned menu battle changed its actual physical entry or callback owner.");
            }
        }
        bool currentField = menu.SourceScene == SceneKind.Field && menu.SourceFieldId == saved.FieldId && menu.SourceHash == saved.ProgramHash && menu.AllocationClockCalls == saved.Scene.NativeFieldInitialization?.TimerClockCallsAtAllocation;
        if (menu.ItemId is < 1 or > byte.MaxValue || menu.SourceScene is not (SceneKind.Field or SceneKind.WorldMap) || menu.SourceFrame < 1 || menu.SourceHash is not { Length: 64 } || !menu.SourceHash.All(Uri.IsHexDigit) || menu.Callbacks < 0 || menu.CallbackBase < 0 || menu.AllocationClockCalls < 0 || menu.BattleClockCallsAtEntry < menu.AllocationClockCalls || saved.ExamTimer is null || menu.BattleClockCallsAtEntry + 4L * menu.Callbacks > saved.ExamTimer.ClockCalls || (menu.Result is null) != (menu.ReturnFrame is null) || menu.Result is not (null or 2 or 4) && (menu.Result != 1 || menu.DefeatBranch is not { ResumePreviousScene: true }) || menu.Result != 1 && menu.DefeatBranch is not null || menu.Result is null && menu.Abandonment is null && menu.SourceScene == SceneKind.Field && (!currentField || menu.SourceFrame > saved.Frame) || currentField && saved.Scene.FieldCallbackClock?.MenuBattleCallbacks != menu.CallbackBase + menu.Callbacks)
        {
            throw new InvalidDataException("Saved menu battle lost its actual physical entry, callback or return owner.");
        }
    }
}
