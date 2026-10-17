using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed record NativeRandomFieldBattleOwner(RandomEncounterRequest Request, long AllocationClockCalls, long CallbackBase, int Callbacks, ushort Flags, byte? Result, long? ReturnFrame, long? BattleClockCallsAtEntry = null, NativeBattleDefeatReturnBranch? DefeatBranch = null)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleAbandonment? Abandonment { get; init; }
}

public sealed partial class EventEngine
{
    public ushort BeginNativeRandomFieldBattle(RandomEncounterRequest request)
    {
        if (request.Scene != SceneKind.Field || request.FieldId != FieldId || request.SourceHash != programHash || request.Frame < 1 || request.Frame > Frame || PendingHostedBattle is not null || HasPendingRandomBattle?.Invoke() != true || Scene.NativeFieldInitialization is not { Stage: NativeFieldInitializationStage.Completed } || Scene.FieldCallbackClock is not { } clock)
        {
            throw new InvalidOperationException("Native random field entry requires its actual suspended encounter and initialized field.");
        }
        if (Scene.NativeRandomFieldBattle is { Result: null, Abandonment: null } existing)
        {
            if (existing.Request != request)
            {
                throw new InvalidDataException("Another random encounter already owns the suspended field.");
            }
            return existing.Flags;
        }
        //47CA90 changes the scene/formation request, preserving the active battle flags.
        ushort flags = Scene.NativeBattleFlagWrites is { Count: > 0 } writes ? writes[^1].ActiveFlags : throw new NotSupportedException("Native random field entry requires its observed active battle flags.");
        long allocationClock = Scene.NativeFieldInitialization!.TimerClockCallsAtAllocation ?? throw new NotSupportedException("Native random field entry requires its actual allocation clock.");
        Scene.NativeRandomFieldBattle = new(request, allocationClock, clock.RandomBattleCallbacks, 0, flags, null, null, services.ExamTimer?.ClockCalls ?? throw new NotSupportedException("Random battle entry requires its actual process clock."));
        return flags;
    }

    private long RequireNativeBattleFieldReloadEntryFrame()
    {
        if (PendingMenuBattle is { SourceScene: SceneKind.Field } menu)
        {
            if (PendingInteractiveBattle is not null || Scene.NativeRandomFieldBattle is { Result: null, Abandonment: null })
            {
                throw new InvalidDataException("Menu battle cannot share a field return owner.");
            }
            return menu.SourceFrame;
        }
        if (PendingInteractiveBattle is { } scripted)
        {
            if (Scene.NativeRandomFieldBattle is { Result: null, Abandonment: null })
            {
                throw new InvalidDataException("Scripted and random encounters cannot both own field return.");
            }
            return scripted.Frame;
        }
        return Scene.NativeRandomFieldBattle is { Result: null, ReturnFrame: null, Abandonment: null } random ? random.Request.Frame : throw new NotSupportedException("Native kind3 requires its actual suspended scripted or random field encounter.");
    }

    public void ReturnFromNativeRandomFieldBattle(int callbacks, byte result, Action publishOutcome)
    {
        NativeRandomFieldBattleOwner owner = Scene.NativeRandomFieldBattle is { Result: null, ReturnFrame: null, Abandonment: null } pending ? pending : throw new InvalidOperationException("No random encounter owns the suspended field.");
        if (callbacks != owner.Callbacks || !SupportsNativeFieldReturnResult(result) || Scene.FieldCallbackClock?.RandomBattleCallbacks != owner.CallbackBase + callbacks)
        {
            throw new InvalidDataException("Native random battle return disagrees with its actual callbacks or result.");
        }
        publishOutcome();
        Scene.NativeRandomFieldBattle = owner with { Result = result, ReturnFrame = Frame, DefeatBranch = result == 1 ? ReadNativeDefeatReturnBranch4706B0() : null };
        Scene.NativeBattleResult = result;
    }

    private static int CountPendingNativeFieldOwners(SceneEventState scene, long frame)
    {
        int scripted = scene.NativeBattleCompletions?.Count(completion => completion.Backend == NativeBattleBackend.Interactive && completion.ReturnFrame is null && completion.Result is null && completion.Abandonment is null && completion.Frame == frame) ?? 0;
        return scripted + (scene.NativeMenuBattle is { SourceScene: SceneKind.Field, Result: null, ReturnFrame: null, Abandonment: null } menu && menu.SourceFrame == frame ? 1 : 0) + (scene.NativeRandomFieldBattle is { Result: null, ReturnFrame: null, Abandonment: null } random && random.Request.Frame == frame ? 1 : 0);
    }

    private static void ValidateNativeRandomFieldBattleCheckpoint(Checkpoint saved)
    {
        if (saved.Scene.NativeRandomFieldBattle is not { } random)
        {
            return;
        }
        if (random.Abandonment is { } abandoned)
        {
            abandoned.Validate();
            if (random.Result is not null || random.ReturnFrame is not null || abandoned.Encounter.ScriptedOwner is not null || abandoned.Encounter.SourceScene != SceneKind.Field || abandoned.Encounter.SourceFieldId != random.Request.FieldId || abandoned.Encounter.SourceHash != random.Request.SourceHash || abandoned.Encounter.SourceFrame != random.Request.Frame || abandoned.Encounter.Formation != random.Request.Formation || abandoned.EntryClockCalls != random.BattleClockCallsAtEntry || abandoned.CombatCallbacks != random.Callbacks)
            {
                throw new InvalidDataException("Abandoned random encounter changed its original entry or executed callback interval.");
            }
        }
        if (random.BattleClockCallsAtEntry is long entry && (entry < random.AllocationClockCalls || saved.ExamTimer is null || entry + 4L * random.Callbacks > saved.ExamTimer.ClockCalls) || random.Result != 1 && random.DefeatBranch is not null)
        {
            throw new InvalidDataException("Random battle clock or defeat return provenance is contradictory.");
        }
        bool current = random.Request.FieldId == saved.FieldId && random.Request.SourceHash == saved.ProgramHash && random.AllocationClockCalls == saved.Scene.NativeFieldInitialization?.TimerClockCallsAtAllocation;
        if (random.Request.Scene != SceneKind.Field || random.Request.FieldId < 0 || random.Request.Frame < 0 || random.Request.SourceHash is not { Length: 64 } hash || !hash.All(Uri.IsHexDigit) || random.AllocationClockCalls < 0 || random.CallbackBase < 0 || random.Callbacks < 0 || (random.Result is null) != (random.ReturnFrame is null) || random.Result is not (null or 2 or 4) && (random.Result != 1 || random.DefeatBranch is not { ResumePreviousScene: true }) || random.Abandonment is null && random.Result is null && (!current || random.Request.Frame > saved.Frame) || current && saved.Scene.FieldCallbackClock?.RandomBattleCallbacks != random.CallbackBase + random.Callbacks || random.ReturnFrame is long returned && returned < random.Request.Frame)
        {
            throw new InvalidDataException("Saved random field battle lost its actual encounter, callback or return owner.");
        }
    }
}
