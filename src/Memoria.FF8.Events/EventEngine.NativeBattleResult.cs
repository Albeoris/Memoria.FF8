using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    public void BindNativeFreshDefeatOutcome(NativeBattleAbandonment abandoned)
    {
        abandoned.Validate();
        if (services.ExamTimer is null || abandoned.ExitClockCalls > services.ExamTimer.ClockCalls)
        {
            throw new InvalidDataException("Fresh defeat allocation requires its retained physical result writer and actual process clock.");
        }
        byte? result = ReadLatestNativeBattleResult(Scene, abandonedReplacement: abandoned);
        Scene.NativeAbandonedBattleOutcome = abandoned;
        Scene.NativeBattleResult = result;
    }

    public void RequireNativeWorldBattleOutcome(NativeWorldBattleOutcome outcome)
    {
        outcome.Validate();
        if (services.ExamTimer?.ClockCalls != outcome.ExitClockCalls || outcome.Result == 1 && outcome.DefeatBranch != ReadNativeDefeatReturnBranch4706B0())
        {
            throw new InvalidDataException("World outcome no longer matches its actual return branch or executed clock.");
        }
        _ = ReadLatestNativeBattleResult(Scene, outcome);
    }

    public void PublishNativeWorldBattleOutcome(NativeWorldBattleOutcome outcome)
    {
        RequireNativeWorldBattleOutcome(outcome);
        Scene.NativeWorldBattleOutcome = outcome;
        Scene.NativeBattleResult = ReadLatestNativeBattleResult(Scene);
    }

    private static byte? ReadLatestNativeBattleResult(SceneEventState scene, NativeWorldBattleOutcome? worldReplacement = null, NativeBattleAbandonment? abandonedReplacement = null)
    {
        List<(long? Entry, byte Result)> writers = [];
        NativeBattleCompletion? scripted = scene.NativeBattleCompletions?.LastOrDefault(item => !item.Suppressed && (item.ReturnFrame is not null || item.Abandonment is not null));
        if (scripted is not null)
        {
            writers.Add((scripted.BattleClockCallsAtEntry, scripted.Abandonment is null ? scripted.Result!.Value : (byte)1));
        }
        if (scene.NativeRandomFieldBattle is { } random && (random.Result is not null || random.Abandonment is not null))
        {
            writers.Add((random.BattleClockCallsAtEntry, random.Abandonment is null ? random.Result!.Value : (byte)1));
        }
        if (scene.NativeMenuBattle is { } menu && (menu.Result is not null || menu.Abandonment is not null))
        {
            writers.Add((menu.BattleClockCallsAtEntry, menu.Abandonment is null ? menu.Result!.Value : (byte)1));
        }
        if ((abandonedReplacement ?? scene.NativeAbandonedBattleOutcome) is { } abandoned)
        {
            writers.Add((abandoned.EntryClockCalls, 1));
        }
        if ((worldReplacement ?? scene.NativeWorldBattleOutcome) is { } world)
        {
            writers.Add((world.EntryClockCalls, world.Result));
        }
        if (writers.Count == 0)
        {
            return null;
        }
        if (writers.Any(item => item.Entry is null))
        {
            byte[] results = writers.Select(item => item.Result).Distinct().ToArray();
            return results.Length == 1 ? results[0] : throw new NotSupportedException("Mixed native outcome history requires explicit actual battle-entry clock provenance.");
        }
        long latest = writers.Max(item => item.Entry!.Value);
        byte[] latestResults = writers.Where(item => item.Entry == latest).Select(item => item.Result).Distinct().ToArray();
        return latestResults.Length == 1 ? latestResults[0] : throw new InvalidDataException("Distinct native results cannot share one actual battle-entry clock.");
    }
}
