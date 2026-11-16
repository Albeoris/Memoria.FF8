using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7AutoPlayController
{
    private void ObserveProgress(string progress)
    {
        if (readPlanningProgress?.Invoke() is string planning)
        {
            progress += ":planning:" + planning;
        }
        if (progressWatchdog.Observe(progress))
        {
            deadline.Reset();
        }
        if (deadline.Expired)
        {
            throw new TimeoutException("FFVII autoplay made no native scene, dialogue or battle progress within its idle deadline.");
        }
    }

    internal static string WorldProgress(Ff7WorldProcess world)
    {
        return $"world:{world.WorldId}:{world.MovementEnabled}:{world.ControlLock}:{world.ControlledModel}:{world.MainState}:{world.FadeAlpha}:{world.FadeDisplayedAlpha}:"
            + string.Join(";", world.Entities.Select(entity => $"{entity.Model},{entity.X},{entity.Y},{entity.Z},{entity.TerrainResolved}"))
            + ":scripts:" + WorldScriptProgress(world.Vm.System)
            + string.Join(";", world.Vm.Models.Select(WorldScriptProgress))
            + ":dialogues:" + string.Join(";", world.Dialogues.Select(dialogue => $"{dialogue.RequestId},{dialogue.TokenPosition},{dialogue.WaitTicks},{dialogue.Completed}"));
    }

    private static string WorldScriptProgress(WorldEntitySnapshot entity)
    {
        return $"{entity.Model},{entity.ProgramCounter},{entity.MoveCounter},{entity.Priority},{entity.ScriptDisabled},{entity.CalledModel},{entity.Detached}:"
            + string.Join('/', entity.Calls.Select(call => $"{call.ProgramCounter},{call.MoveCounter},{call.Priority}"));
    }
}

/// <summary>Bounds repeated native work even when a host suspends the wall-clock deadline between input batches.</summary>
internal sealed class Ff7AutoPlayProgressWatchdog
{
    internal const int IdleStepLimit = 1800;
    private readonly HashSet<string> recentProgress = [];
    private readonly Queue<string> progressOrder = [];
    private int idleSteps;

    public void Reset()
    {
        recentProgress.Clear();
        progressOrder.Clear();
        idleSteps = 0;
    }

    public bool Observe(string progress)
    {
        if (recentProgress.Add(progress))
        {
            progressOrder.Enqueue(progress);
            if (progressOrder.Count > IdleStepLimit)
            {
                recentProgress.Remove(progressOrder.Dequeue());
            }
            idleSteps = 0;
            return true;
        }
        if (++idleSteps >= IdleStepLimit)
        {
            throw new TimeoutException($"FFVII autoplay made no native scene, dialogue or battle progress for {IdleStepLimit} consecutive steps.");
        }
        return false;
    }
}
