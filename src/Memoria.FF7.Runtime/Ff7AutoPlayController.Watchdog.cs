using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7AutoPlayController
{
    private void ObserveProgress(string progress, Ff7SessionSave? observation = null)
    {
        bool advancedMovie = observation is not null && movieProgress.Observe(observation, session.Machine);
        if (observation is null)
        {
            movieProgress.Reset();
        }
        bool countIdleStep = semanticWatchdog.HasPendingNativeObservation;
        semanticWatchdog.Observe(progress, readFrontierProgress?.Invoke(), advancedMovie);
        progress += ":physical-planning:" + driver.PhysicalPlanningWork;
        if (readPlanningProgress?.Invoke() is string planning)
        {
            progress += ":planning:" + planning;
        }
        if (progressWatchdog.Observe(progress, advancedMovie, countIdleStep))
        {
            deadline.Reset();
        }
        if (deadline.Expired)
        {
            throw new TimeoutException("FFVII autoplay made no native scene, dialogue or battle progress within its idle deadline.");
        }
    }

    internal static string FieldProgress(Ff7SessionSave state)
    {
        return $"field:{state.FieldName}:{state.Vm.ProgramIdentity}:{state.Progress.CompletedBattles.Length}:{state.Process.Movie?.PreparedId}:{state.Process.Movie?.Frame}:{state.Process.CameraScroll?.Step}:{state.Process.CameraViewOffset?.Step}:"
            + string.Join(";", state.Vm.Entities.Select(entity => $"{entity.ProgramCounter},{entity.Priority},{entity.WaitCounter}"))
            + string.Join(";", state.Process.Actors.Select(actor => $"{actor.X4096},{actor.Y4096},{actor.Z4096},{actor.Triangle},{actor.Ladder?.Step},{actor.Jump?.Step}"))
            + ":dialogues:" + string.Join(";", state.Process.Dialogues.Select(DialogueProgress))
            + ":blocking-animations:" + string.Join(";", BlockingAnimationProgress(state))
            + ":owned-fade-waits:" + string.Join(";", OwnedFadeWaitProgress(state));
    }

    private static IEnumerable<string> OwnedFadeWaitProgress(Ff7SessionSave state)
    {
        if (state.Process.FadeState is not { Type: 1 or 2 or 5 or 6 or 7 or 8 or 9 or 10 or 11 or 12, Speed: > 0, Completed: false } fade)
        {
            yield break;
        }
        foreach (FieldHostRequest request in state.Vm.PendingRequests ?? [])
        {
            if (request.Command is not FieldFadeCommand { Wait: true } || (uint)request.Entity >= state.Vm.Entities.Length)
            {
                continue;
            }
            FieldEntitySnapshot owner = state.Vm.Entities[request.Entity];
            if (owner.Priority == request.Priority && owner.ProgramCounter == request.ProgramCounter)
            {
                yield return $"{request.Entity},{request.Priority},{request.ProgramCounter},{fade.Type},{fade.Position}";
            }
        }
    }

    private static IEnumerable<string> BlockingAnimationProgress(Ff7SessionSave state)
    {
        foreach (FieldHostRequest request in state.Vm.PendingRequests ?? [])
        {
            if (request.Command is not FieldAnimation { Blocking: true } animation || (uint)request.Entity >= state.Vm.Entities.Length)
            {
                continue;
            }
            FieldEntitySnapshot owner = state.Vm.Entities[request.Entity];
            if (owner.Priority != request.Priority || owner.ProgramCounter != request.ProgramCounter || !state.Process.AnimationRequests.Any(active => active.RequestId == request.Id && active.Entity == request.Entity))
            {
                continue;
            }
            Ff7Actor? actor = state.Process.Actors.LastOrDefault(actor => actor.EntityBound && actor.Entity == request.Entity && actor.Animation == animation.Animation);
            if (actor is not null)
            {
                yield return $"{request.Entity},{request.Priority},{request.ProgramCounter},{actor.Model},{actor.Animation},{actor.AnimationFrame16},{actor.AnimationStatus},{actor.AnimationStopped},{actor.AnimationFinished}";
            }
        }
    }

    private static string DialogueProgress(Ff7Dialogue dialogue)
    {
        return $"{dialogue.Window},{dialogue.Text},{dialogue.Page},{dialogue.FirstChoice},{dialogue.LastChoice},{dialogue.Selected},{dialogue.TokenPosition},{dialogue.WaitTicks},{dialogue.Completed},{dialogue.AwaitingInput}";
    }

    internal static string WorldProgress(Ff7WorldProcess world)
    {
        return $"world:{world.WorldId}:{world.Vm.ProgramIdentity}:{world.MovementEnabled}:{world.ControlLock}:{world.ControlledModel}:{world.MainState}:{world.FadeAlpha}:{world.FadeDisplayedAlpha}:"
            + string.Join(";", world.Entities.Select(entity => $"{entity.Model},{entity.X},{entity.Y},{entity.Z},{entity.TerrainResolved}"))
            + ":scripts:" + WorldScriptProgress(world.Vm.System)
            + string.Join(";", world.Vm.Models.Select(WorldScriptProgress))
            + ":dialogues:" + string.Join(";", world.Dialogues.Select(DialogueProgress));
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

    public bool Observe(string progress, bool advancedMovie = false, bool countIdleStep = true)
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
        if (advancedMovie)
        {
            idleSteps = 0;
            return true;
        }
        if (countIdleStep && ++idleSteps >= IdleStepLimit)
        {
            throw new TimeoutException($"FFVII autoplay made no native scene, dialogue or battle progress for {IdleStepLimit} consecutive steps.");
        }
        return false;
    }
}

/// <summary>Observes repeated semantic states once per successful native callback, independently of speculative work.</summary>
internal sealed class Ff7AutoPlaySemanticWatchdog
{
    private readonly Ff7AutoPlayProgressWatchdog progress = new();
    private bool observationPending = true;
    internal bool HasPendingNativeObservation => observationPending;

    public void Reset()
    {
        progress.Reset();
        observationPending = true;
    }

    public void NativeAdvanced()
    {
        observationPending = true;
    }

    public bool Observe(string nativeProgress, string? frontierProgress, bool advancedMovie = false)
    {
        if (!observationPending && !advancedMovie)
        {
            return false;
        }
        observationPending = false;
        return progress.Observe(nativeProgress + ":frontier:" + frontierProgress, advancedMovie);
    }
}

/// <summary>Recognizes actual frame advancement within an authenticated native movie owner, without treating owner allocation as progress.</summary>
internal sealed class Ff7AutoPlayMovieProgress
{
    private sealed record Owner(object Machine, long Generation, string Program, long Request, int Movie, int Entity, int Priority, int ProgramCounter);
    private Owner? owner;
    private int highestFrame;

    public void Reset()
    {
        owner = null;
        highestFrame = -1;
    }

    public bool Observe(Ff7SessionSave state, object machine)
    {
        Ff7FieldProcess process = state.Process;
        if (state.World is not null || process.Movie is not { Playing: true, Completed: false, RequestId: long id, Frame: >= 0 } movie || process.GlobalCommand != Ff7GlobalCommand.Movie || process.GlobalRequestId != id)
        {
            Reset();
            return false;
        }
        FieldHostRequest? request = (state.Vm.PendingRequests ?? []).SingleOrDefault(request => request.Id == id);
        if (request is not { Command: FieldMovieCommand { Operation: FieldMovieOperation.Play }, Priority: >= 0 and < 8 } || (uint)request.Entity >= state.Vm.Entities.Length || state.Vm.Entities[request.Entity].Priority != request.Priority || state.Vm.Entities[request.Entity].ProgramCounter != request.ProgramCounter)
        {
            Reset();
            return false;
        }
        Owner current = new(machine, state.FieldGeneration, state.Vm.ProgramIdentity, id, movie.PreparedId, request.Entity, request.Priority, request.ProgramCounter);
        if (owner != current)
        {
            owner = current;
            highestFrame = movie.Frame;
            return false;
        }
        if (movie.Frame <= highestFrame)
        {
            return false;
        }
        highestFrame = movie.Frame;
        return true;
    }
}
