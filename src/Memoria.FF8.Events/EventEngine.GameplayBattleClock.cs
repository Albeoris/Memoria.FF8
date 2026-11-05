using Memoria.FF8.Runtime;
using System.Text.Json;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    public long GetCompletedBattleCallbackCount()
    {
        long allocationClock = Scene.NativeFieldInitialization?.TimerClockCallsAtAllocation ?? throw new NotSupportedException("Battle callbacks require their current field allocation clock.");
        return GetCompletedBattleCallbackCount(Scene, allocationClock);
    }

    private static long GetCompletedBattleCallbackCount(SceneEventState scene, long allocationClock)
    {
        ValidateGameplayBattleCallbackBase(scene);
        long completed = scene.NativeBattleCompletions?.Where(completion => completion.Backend == NativeBattleBackend.Interactive && completion.ReturnFrame is not null && completion.BattleClockCallsAtEntry >= allocationClock).Sum(completion => (long)completion.EmulatedCombatCallbacks) ?? 0;
        return checked((scene.GameplayBattleCallbackBase ?? 0) + completed);
    }

    private static void ValidateGameplayBattleCallbackBase(SceneEventState scene)
    {
        long baseline = scene.GameplayBattleCallbackBase ?? 0;
        long? current = scene.FieldCallbackClock?.BattleCallbacks;
        bool semantic = scene.NativeFieldInitialization is { Origin: NativeFieldInitializationOrigin.GameplayContinuation };
        if (baseline < 0 || baseline != 0 && (!semantic || current is null || current < baseline) || scene.GameplayBattleCallbackBase is not null && semantic && scene.NativeBattleCompletions is null && current is not null && current != baseline)
        {
            throw new InvalidDataException("Gameplay battle callback baseline lost its semantic allocation or current counter authority.");
        }
    }

    private void RestoreMissingGameplayBattleCallbackBase(Checkpoint saved, JsonElement snapshot)
    {
        SceneEventState scene = saved.Scene ?? throw new InvalidDataException("Saved field scene is missing.");
        if (snapshot.GetProperty("Scene").TryGetProperty(nameof(SceneEventState.GameplayBattleCallbackBase), out _))
        {
            if (scene.GameplayBattleCallbackBase is null)
            {
                throw new InvalidDataException("An explicit battle callback baseline cannot be unknown.");
            }
            return;
        }
        if (scene.NativeFieldInitialization is not { Origin: NativeFieldInitializationOrigin.GameplayContinuation } initialization)
        {
            return;
        }
        if (scene.FieldCallbackClock?.BattleCallbacks is not long counter)
        {
            scene.GameplayBattleCallbackBase = 0;
            return;
        }
        long allocationClock = initialization.TimerClockCallsAtAllocation ?? throw new InvalidDataException("Historical semantic battle time requires its known allocation clock.");
        if (scene.NativeBattleCompletions?.Any(completion => completion is null) == true)
        {
            throw new InvalidDataException("Historical semantic battle time contains an absent completion owner.");
        }
        NativeBattleCompletion[] current = scene.NativeBattleCompletions?.Where(completion => completion.Backend == NativeBattleBackend.Interactive && completion.BattleClockCallsAtEntry >= allocationClock && completion.Abandonment is null).ToArray() ?? [];
        long represented = current.Where(completion => completion.ReturnFrame is not null).Sum(completion => (long)completion.EmulatedCombatCallbacks);
        NativeBattleCompletion[] pending = current.Where(completion => completion.ReturnFrame is null).ToArray();
        if (pending.Length > 1)
        {
            throw new InvalidDataException("Historical semantic battle time has competing pending owners.");
        }
        if (pending.Length == 1)
        {
            NativeBattleCompletion request = pending[0];
            long active;
            if (services is GameSession { State.Battle: { } battle, State.PendingBattleEncounter: { ScriptedOwner: { } owner } occurrence } && battle.Definition.NativeExamTimerAtEntry?.ClockCalls == request.BattleClockCallsAtEntry && occurrence.SourceFrame == request.Frame && occurrence.SourceFieldId == request.FieldId && occurrence.SourceHash == request.ProgramHash && occurrence.Formation == request.Encounter && owner.FieldId == request.FieldId && owner.ProgramHash == request.ProgramHash && owner.ObjectId == request.ObjectId && owner.ScriptId == request.ScriptId && owner.Operation == request.Operation && owner.Flags == request.Flags)
            {
                active = battle.NativeGraphics is null ? battle.Frame : battle.NativeHostCallbacks ?? throw new InvalidDataException("Historical semantic battle time lacks its active callback count.");
            }
            else if (saved.Frame == request.Frame && saved.ExamTimer?.ClockCalls == request.BattleClockCallsAtEntry)
            {
                active = 0;
            }
            else
            {
                throw new InvalidDataException("Historical semantic battle time requires its matching active battle callback owner.");
            }
            represented = checked(represented + active);
        }
        if (represented < 0 || represented > counter)
        {
            throw new InvalidDataException("Historical semantic battle callbacks exceed their saved aggregate counter.");
        }
        scene.GameplayBattleCallbackBase = counter - represented;
    }
}
