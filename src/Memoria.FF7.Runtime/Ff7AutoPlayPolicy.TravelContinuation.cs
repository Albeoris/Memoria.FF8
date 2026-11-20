using Memoria.AutoPlay;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7AutoPlayPolicy
{
    internal static bool CanRefreshOwnedActorTarget(Ff7AutoPlayState state, Ff7AutoPlayObservation observation, Ff7AutoPlayTarget target, Ff7Actor? actor, AutoPlayEventAvailability? availability, bool freshContactApproach = false)
    {
        if (state.TravelCommitment is not { Goal: { EventId: not null } goal } commitment || state.NavigationGoal != goal || state.DestinationField != commitment.DestinationField || observation.NavigationKey != commitment.NavigationKey || !observation.CanAcceptInput || !state.Coverage.NavigationVisits.Any(visit => visit.Key == goal.NavigationKey && visit.VisitKey == observation.VisitId) || target.Id != goal.EventId || target.Id != $"actor:{target.Entity}:script:{target.Script}" || target.Kind != Ff7AutoPlayTargetKind.ActorTalk || !target.AnalysisComplete || actor is not { EntityBound: true, PlayerControlled: false, TalkEnabled: true } || actor.Entity != target.Entity || availability is null || availability.Unavailable)
        {
            return false;
        }
        var coverage = state.Coverage.Visits.SingleOrDefault(visit => visit.Visit.Key == observation.VisitId)?.Events.SingleOrDefault(item => item.Event.Id == target.Id);
        return coverage is { Available: true, BlockedReason: null, PolicyDeferred: false } && coverage.Event.ConditionSignature == target.ConditionFingerprint && (freshContactApproach || coverage.Conditions?.HasExecutedEntry(target.ConditionFingerprint) != true);
    }

    internal static bool CanPreserveUnchangedOwnedTarget(Ff7AutoPlayState state, Ff7AutoPlayObservation observation, Ff7AutoPlayTarget target, AutoPlayEventAvailability? availability)
    {
        if (target.Kind == Ff7AutoPlayTargetKind.ActorTalk || !target.AnalysisComplete || !observation.CanAcceptInput || !observation.Events.Contains(target) || state.NavigationGoal is not { EventId: not null, Conditions: not null } goal || target.Id != goal.EventId || target.ConditionFingerprint != goal.Conditions || availability?.Unavailable == true || !CanContinueOwnedTravel(state, observation))
        {
            return false;
        }
        var coverage = state.Coverage.Visits.SingleOrDefault(visit => visit.Visit.Key == observation.VisitId)?.Events.SingleOrDefault(item => item.Event.Id == target.Id);
        return coverage is { Available: true, BlockedReason: null, PolicyDeferred: false } && coverage.Event.ConditionSignature == target.ConditionFingerprint;
    }

    private void RefreshOwnedTargetIntent(Ff7AutoPlayObservation observation, Ff7AutoplayDriver driver, IReadOnlyDictionary<string, string> physicalGuards)
    {
        if (State.TravelCommitment is not { Goal: { EventId: not null } goal } commitment || State.NavigationGoal != goal || State.DestinationField != commitment.DestinationField || State.NativeRoute is not null || State.Choice is not null || State.PendingRestore is not null || !observation.CanAcceptInput || !State.Coverage.NavigationVisits.Any(visit => visit.Key == goal.NavigationKey && visit.VisitKey == observation.VisitId))
        {
            return;
        }
        if (observation.NavigationKey != commitment.NavigationKey)
        {
            State.TravelCommitment = null;
            return;
        }
        if (State.Active is { } active && (active.EventId != goal.EventId || active.VisitId != observation.VisitId || active.Generation != observation.Generation || active.Accepted || active.Returned || active.TransitionOwned || active.CompletionRecorded || active.Kind != Ff7AutoPlayTargetKind.ActorTalk || active.TravelRoute is not null))
        {
            return;
        }
        Ff7AutoPlayTarget? target = observation.Events.SingleOrDefault(item => item.Id == goal.EventId && item.AnalysisComplete);
        if (target is null)
        {
            State.TravelCommitment = null;
            return;
        }
        AutoPlayNavigationVisit current = State.Coverage.NavigationVisits.Single(visit => visit.Key == observation.NavigationKey);
        AutoPlayEvent currentEvent = current.Events.Single(item => item.Id == target.Id);
        var coverage = State.Coverage.Visits.Single(visit => visit.Visit.Key == observation.VisitId).Events.Single(item => item.Event.Id == target.Id);
        var facts = new Dictionary<string, IReadOnlyDictionary<string, string>?> { [observation.NavigationKey] = physicalGuards };
        AutoPlayEventAvailability? availability = ReadNavigationAvailability(current, currentEvent, observation, driver, facts);
        if (target.Kind != Ff7AutoPlayTargetKind.ActorTalk)
        {
            if (!CanPreserveUnchangedOwnedTarget(State, observation, target, availability))
            {
                State.TravelCommitment = null;
            }
            return;
        }
        if (!session.Host.Process.Actors.Any(actor => actor.EntityBound && actor.Entity == target.Entity && !actor.PlayerControlled && actor.TalkEnabled))
        {
            State.TravelCommitment = null;
            return;
        }
        bool consumedIntermediate = coverage.Conditions?.HasExecutedEntry(target.ConditionFingerprint) == true;
        bool freshContactApproach = consumedIntermediate && ReadFreshTalkContact(driver, observation, target) is not null;
        if (!CanRefreshOwnedActorTarget(State, observation, target, session.Host.Process.Actors.SingleOrDefault(actor => actor.EntityBound && actor.Entity == target.Entity), availability, freshContactApproach))
        {
            State.TravelCommitment = null;
            return;
        }
        AutoPlayTravelGoal fresh = new(observation.NavigationKey, target.Id, target.ConditionFingerprint);
        if (State.Active is { } planned && (planned.ConditionFingerprint != fresh.Conditions || planned.NavigationKey != fresh.NavigationKey))
        {
            if (coverage.Conditions?.ActiveEntry == planned.ConditionFingerprint)
            {
                coverage.Conditions.ActiveEntry = null;
            }
            planner.BeginEvent(observation.VisitId, target.Id);
            State.Active = planned with { ConditionFingerprint = target.ConditionFingerprint, NavigationKey = observation.NavigationKey, NavigationProbeKey = target.MayChangeNavigation ? NavigationProbeKey(observation.NavigationKey, target) : null, EntryAvailabilityFingerprint = availability!.Fingerprint, EntryGuardFingerprints = physicalGuards.Where(entry => State.NavigationGuards.TryGetValue(entry.Key, out var guard) && guard.NavigationKey == observation.NavigationKey && guard.EventId == target.Id).ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal) };
        }
        if (freshContactApproach)
        {
            return;
        }
        if (fresh != goal)
        {
            CancelNativePlanning();
        }
        State.NavigationGoal = fresh;
        State.TravelCommitment = commitment with { Goal = fresh, NavigationKey = observation.NavigationKey };
    }

    internal static bool IsOwnedTravelArrival(Ff7AutoPlayState state, Ff7AutoPlayActiveEvent active, Ff7AutoPlayObservation observation, bool changedField, bool sourceGoalPending = false)
    {
        if (state.NavigationGoal is not { } goal || goal.EventId is null && !sourceGoalPending || state.TravelCommitment is { } previous && (previous.Goal != state.NavigationGoal || previous.DestinationField != state.DestinationField) || active.TravelRoute is not { } route || active.NavigationKey is not string source || route.DestinationVisitKey != observation.VisitId || !(active.Accepted && (active.Returned || active.TransitionOwned) || changedField && active.Kind == Ff7AutoPlayTargetKind.Gateway && observation.VisitId.StartsWith($"ff7:{active.DestinationField}:", StringComparison.Ordinal)))
        {
            return false;
        }
        bool targetArrival = state.TravelCommitment is { } commitment && commitment.Goal == state.NavigationGoal && commitment.DestinationField == state.DestinationField && commitment.NavigationKey == source && state.Coverage.NavigationVisits.Any(visit => visit.Key == commitment.Goal.NavigationKey && visit.VisitKey == observation.VisitId);
        return state.Coverage.NavigationRoutes.Any(known => known.SourceKey == source && (known.DestinationKey == observation.NavigationKey || targetArrival) && known.EventId == active.EventId && known.Route.TargetId == active.EventId && known.Route.ConditionSignature == active.ConditionFingerprint && known.Route.DestinationVisitKey == route.DestinationVisitKey);
    }

    internal static bool CanContinueOwnedTravel(Ff7AutoPlayState state, Ff7AutoPlayObservation observation, bool sourceGoalPending = false)
    {
        if (state.TravelCommitment is not { Goal: { } } commitment || string.IsNullOrEmpty(commitment.NavigationKey) || state.NavigationGoal != commitment.Goal || state.DestinationField != commitment.DestinationField || observation.NavigationKey != commitment.NavigationKey || state.NativeRevalidation is not null || state.Choice is not null || state.PendingRestore is not null)
        {
            return false;
        }
        AutoPlayNavigationVisit? source = state.Coverage.NavigationVisits.SingleOrDefault(visit => visit.Key == commitment.Goal.NavigationKey);
        if (commitment.Goal.EventId is null)
        {
            return sourceGoalPending && commitment.Goal.Conditions is null && source is not null && observation.NavigationKey != commitment.Goal.NavigationKey;
        }
        if (commitment.Goal.Conditions is null)
        {
            return false;
        }
        AutoPlayEvent? target = source?.Events.SingleOrDefault(item => item.Id == commitment.Goal.EventId && item.ConditionSignature == commitment.Goal.Conditions);
        var coverage = source is null ? null : state.Coverage.Visits.SingleOrDefault(visit => visit.Visit.Key == source.VisitKey)?.Events.SingleOrDefault(item => item.Event.Id == commitment.Goal.EventId);
        return target is not null && coverage is not null && coverage.Conditions?.HasExecutedEntry(commitment.Goal.Conditions) != true;
    }

    private bool IsPendingOwnedSourceGoal(AutoPlayTravelGoal? goal, Ff7AutoPlayObservation observation)
    {
        if (goal is not { EventId: null, Conditions: null } || goal.NavigationKey == observation.NavigationKey)
        {
            return false;
        }
        AutoPlayNavigationVisit? source = State.Coverage.NavigationVisits.SingleOrDefault(visit => visit.Key == goal.NavigationKey);
        AutoPlayVisitCoverage<Ff7AutoPlayDeferral>? visit = source is null ? null : State.Coverage.Visits.SingleOrDefault(visit => visit.Visit.Key == source.VisitKey);
        return source is not null && visit is not null && NeedsVisitRefresh(new(source.Key, visit.Visit.Region));
    }

    internal static bool MatchesOwnedTravelPlan(AutoPlayTravelGoal goal, AutoPlayPlanStep? candidate, string currentNavigationKey)
    {
        return candidate is not null && (candidate.Goal == goal || goal.EventId is not null && !candidate.IsTravel && currentNavigationKey == goal.NavigationKey && candidate.Event.Id == goal.EventId && candidate.Event.ConditionSignature == goal.Conditions);
    }

    private AutoPlayPlanStep? ContinueOwnedTravel(Ff7AutoPlayObservation observation, Func<AutoPlayNavigationVisit, AutoPlayEvent, AutoPlayEventAvailability?> availability)
    {
        if (!CanContinueOwnedTravel(State, observation, IsPendingOwnedSourceGoal(State.NavigationGoal, observation)))
        {
            State.TravelCommitment = null;
            return null;
        }
        Ff7AutoPlayTravelCommitment commitment = State.TravelCommitment!;
        AutoPlayPlanStep? plan = planner.PlanNavigation(observation.NavigationKey, needsRefresh: NeedsVisitRefresh, preferredGoal: commitment.Goal, readAvailability: availability, strongPreferredGoal: true);
        bool Matches(AutoPlayPlanStep? candidate) => MatchesOwnedTravelPlan(commitment.Goal, candidate, observation.NavigationKey);
        if (!Matches(plan))
        {
            plan = planner.PlanNavigation(observation.NavigationKey, needsRefresh: NeedsVisitRefresh, allowRemoteConditionRoutes: true, preferredGoal: commitment.Goal, readAvailability: availability, strongPreferredGoal: true);
        }
        if (!Matches(plan) || !IsDestinationPlan(State, plan, observation.NavigationKey))
        {
            State.TravelCommitment = null;
            return null;
        }
        return plan! with { Goal = commitment.Goal };
    }
}
