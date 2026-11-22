using Memoria.AutoPlay;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7AutoPlayPolicy
{
    internal static Ff7AutoPlayActiveEvent? ReadSourceActivation(Ff7AutoPlayActiveEvent active, Ff7NativeTickObservation observation)
    {
        return ReadSourceActivation(active, observation.SourceActivation, observation.Generation);
    }

    internal static Ff7AutoPlayActiveEvent? ReadSourceActivation(Ff7AutoPlayActiveEvent active, Ff7AutoPlayActiveEvent? tracked, long generation)
    {
        if (tracked is not { ExecutionProvenance: { } provenance } || active.ExecutionProvenance is not { } original || generation != active.Generation || provenance.RootDispatchTick != original.RootDispatchTick || !Ff7AutoPlayExecutionOwners.Matches(tracked, active.Generation, original.ProgramIdentity) || tracked.VisitId != active.VisitId || tracked.NavigationKey != active.NavigationKey || tracked.EventId != active.EventId || tracked.ConditionFingerprint != active.ConditionFingerprint || tracked.Kind != active.Kind || tracked.Entity != active.Entity || tracked.Script != active.Script)
        {
            return null;
        }
        return active with { ExecutionProvenance = provenance };
    }

    public void SetSourceGoal(string? eventId)
    {
        if (State.Active is { Accepted: true } || State.Choice is not null || State.PendingRestore is not null)
        {
            throw new InvalidOperationException("The current native event must return before changing its planning destination.");
        }
        if (string.IsNullOrWhiteSpace(eventId))
        {
            State.SourceGoal = null;
            State.NavigationGoal = null;
            State.Active = null;
            State.Status = "Explicit native source goal cleared";
            Publish();
            return;
        }
        if (session.World is not null)
        {
            throw new InvalidOperationException("An explicit native source goal requires a current field interaction.");
        }
        Ff7AutoPlayObservation observation = ObservePlanner();
        if (!observation.CanAcceptInput)
        {
            throw new InvalidOperationException("The current native event must return before changing its planning destination.");
        }
        Ff7AutoPlayTarget target = observation.Events.SingleOrDefault(target => target.Id == eventId) ?? throw new ArgumentException("The source goal must identify an observed current native interaction.", nameof(eventId));
        if ((uint)target.Entity >= session.Field.Scripts.Entities.Count || (uint)target.Script >= session.Field.Scripts.Entities[target.Entity].ScriptOffsets.Count)
        {
            throw new ArgumentException("The source goal requires an allocated native script entry.", nameof(eventId));
        }
        Ff7AutoPlaySourceGoal goal = CreateSourceGoal(observation, target, session.Field.Scripts.Entities[target.Entity].ScriptOffsets[target.Script]);
        CancelNativePlanning();
        State.NativeRoute = null;
        State.NativeRevalidation = null;
        State.TravelCommitment = null;
        State.DestinationField = null;
        State.SourceGoal = goal;
        State.Active = null;
        State.NavigationGoal = new(goal.NavigationKey, goal.EventId, goal.ConditionFingerprint);
        planner.MarkRouteNeedsRediscovery(goal.VisitId, goal.EventId);
        State.Status = $"Planning the explicit native source {goal.EventId}";
        Publish();
    }

    internal static Ff7AutoPlaySourceGoal CreateSourceGoal(Ff7AutoPlayObservation observation, Ff7AutoPlayTarget target, int entry)
    {
        if (!observation.CanAcceptInput || !observation.Events.Contains(target) || !target.AnalysisComplete || target.Kind is < Ff7AutoPlayTargetKind.ActorTalk or > Ff7AutoPlayTargetKind.LineLeave || target.Entity is < 0 or > 255 || target.Script is < 0 or > 31 || entry is < 0 or > 65535 || string.IsNullOrEmpty(target.ConditionFingerprint))
        {
            throw new ArgumentException("The source goal requires a current complete native field interaction.");
        }
        return new(observation.VisitId, observation.ProgramIdentity, observation.Generation, observation.NavigationKey, target.Id, target.ConditionFingerprint, target.Kind, target.Entity, target.Script, entry) { EntryGuardFingerprint = target.EntryGuardFingerprint };
    }

    internal static bool MatchesSourceGoal(Ff7AutoPlaySourceGoal goal, Ff7AutoPlayObservation observation, Ff7AutoPlayTarget target)
    {
        return !goal.Completed && observation.CanAcceptInput && observation.Generation == goal.Generation && observation.VisitId == goal.VisitId && observation.ProgramIdentity == goal.ProgramIdentity && observation.NavigationKey == goal.NavigationKey && observation.Events.Contains(target) && target.AnalysisComplete && target.Id == goal.EventId && (target.ConditionFingerprint == goal.ExecutionConditionFingerprint || goal.AcceptedEntry is null && goal.EntryGuardFingerprint is not null && target.EntryGuardFingerprint == goal.EntryGuardFingerprint) && target.Kind == goal.Kind && target.Entity == goal.Entity && target.Script == goal.Script;
    }

    private static bool OwnsLocalSourceGoal(Ff7AutoPlayState state, Ff7AutoPlayObservation observation, Ff7AutoPlayActiveEvent? active)
    {
        return state.SourceGoal is { Completed: false } goal && active is { Accepted: false } && goal.VisitId == observation.VisitId && goal.Generation == observation.Generation && goal.NavigationKey == observation.NavigationKey && active.VisitId == goal.VisitId && active.Generation == goal.Generation && active.NavigationKey == goal.NavigationKey && active.EventId == goal.EventId && active.ConditionFingerprint == goal.ExecutionConditionFingerprint;
    }

    internal static bool IsSourceGoalPlan(Ff7AutoPlayState state, AutoPlayPlanStep? next)
    {
        if (state.SourceGoal is not { Completed: false } goal)
        {
            return true;
        }
        if (next is null)
        {
            return false;
        }
        return next.Goal is { } planned ? planned == new AutoPlayTravelGoal(goal.NavigationKey, goal.EventId, goal.ExecutionConditionFingerprint) : !next.IsTravel && next.VisitKey == goal.VisitId && next.Event.Id == goal.EventId && next.Event.ConditionSignature == goal.ExecutionConditionFingerprint;
    }

    internal static bool CompleteSourceGoal(Ff7AutoPlayState state, Ff7AutoPlayActiveEvent active)
    {
        if (state.SourceGoal is not { Completed: false } goal || !active.Accepted || !active.CompletionRecorded || active.VisitId != goal.VisitId || active.NavigationKey != goal.NavigationKey || active.EventId != goal.EventId || active.ConditionFingerprint != goal.ExecutionConditionFingerprint || active.Kind != goal.Kind || active.Entity != goal.Entity || active.Script != goal.Script || !Ff7AutoPlayExecutionOwners.Matches(active, goal.Generation, goal.ProgramIdentity) || active.ExecutionProvenance is not { } provenance || !(active.Returned && provenance.RootReturned || active.TransitionOwned && provenance.AcceptedTransition is not null && provenance.AcceptedTransitionTick >= provenance.RootDispatchTick) || provenance.Owners.Any(owner => owner.Entity == goal.Entity && owner.Priority == 1 && owner.ParentEntity == -1 && owner.Entry != goal.Entry) || !(goal.AcceptedEntry is int accepted ? accepted == goal.Entry : provenance.Owners.Any(owner => owner.Entity == goal.Entity && owner.Priority == 1 && owner.ParentEntity == -1 && owner.Entry == goal.Entry)))
        {
            return false;
        }
        state.SourceGoal = goal with { Completed = true };
        return true;
    }

    internal static Ff7AutoPlaySourceGoal? BindSourceGoalAdmission(Ff7AutoPlaySourceGoal goal, Ff7AutoPlayObservation observation, Ff7AutoPlayTarget target, Ff7AutoPlayActiveEvent active, int entry)
    {
        if (goal.Completed || goal.AcceptedEntry is not null || observation.Generation != goal.Generation || observation.ProgramIdentity != goal.ProgramIdentity || observation.VisitId != goal.VisitId || observation.NavigationKey != goal.NavigationKey || !observation.Events.Contains(target) || !target.AnalysisComplete || entry != goal.Entry || target.Id != goal.EventId || target.Kind != goal.Kind || target.Entity != goal.Entity || target.Script != goal.Script || !(target.ConditionFingerprint == goal.ExecutionConditionFingerprint || goal.EntryGuardFingerprint is not null && target.EntryGuardFingerprint == goal.EntryGuardFingerprint) || !active.Accepted || active.ConditionFingerprint != target.ConditionFingerprint || active.EventId != goal.EventId || active.Entity != goal.Entity || active.Script != goal.Script || active.Kind != goal.Kind || active.VisitId != goal.VisitId || active.NavigationKey != goal.NavigationKey || !Ff7AutoPlayExecutionOwners.Matches(active, goal.Generation, goal.ProgramIdentity) || active.ExecutionProvenance is not { } provenance || !provenance.Owners.Any(owner => owner.Entity == goal.Entity && owner.Priority == 1 && owner.ParentEntity == -1 && owner.Entry == entry))
        {
            return null;
        }
        return goal with { AcceptedEntry = entry, CurrentConditionFingerprint = target.ConditionFingerprint == goal.ConditionFingerprint ? null : target.ConditionFingerprint };
    }

    private void ValidateSourceGoal(Ff7AutoPlayObservation observation)
    {
        if (State.SourceGoal is not { Completed: false } goal || State.Active is { Accepted: true })
        {
            return;
        }
        Ff7AutoPlayTarget? target = observation.Events.SingleOrDefault(target => target.Id == goal.EventId);
        if (target is null || !MatchesSourceGoal(goal, observation, target) || session.Field.Scripts.Entities[target.Entity].ScriptOffsets[target.Script] != goal.Entry)
        {
            throw new InvalidOperationException("The explicit native source changed before admission; select its current observed source again.");
        }
        State.SourceGoal = goal with { CurrentConditionFingerprint = target.ConditionFingerprint == goal.ConditionFingerprint ? null : target.ConditionFingerprint };
        State.NavigationGoal = new(goal.NavigationKey, goal.EventId, target.ConditionFingerprint);
    }
}
