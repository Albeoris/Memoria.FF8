using Memoria.AutoPlay;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7AutoPlayPolicy
{
    private void RetainDestinationGoal(AutoPlayPlanStep next, string navigationKey, string condition)
    {
        RetainDestinationGoal(State, next, navigationKey, condition);
    }

    internal static void RetainDestinationGoal(Ff7AutoPlayState state, AutoPlayPlanStep next, string navigationKey, string condition)
    {
        AutoPlayTravelGoal? retained = state.DestinationField is not null && state.NavigationGoal is { EventId: not null } goal && goal.EventId == next.Event.Id && goal.NavigationKey == navigationKey && (goal.Conditions is null || goal.Conditions == condition) ? goal : null;
        state.NavigationGoal = next.Goal ?? retained;
    }

    internal static string? ResolveFieldDestinationGoal(Ff7AutoPlayState state, Ff7AutoPlayObservation observation, Func<ushort, int[]>? readConnections = null, Func<Ff7AutoPlayTarget, bool>? canSelect = null)
    {
        if (state.DestinationField is not ushort destination)
        {
            return null;
        }
        Ff7AutoPlayTarget[] candidates = observation.Events.Where(target => target.AnalysisComplete && (target.Kind == Ff7AutoPlayTargetKind.Gateway || target.ChangesField) && (target.DestinationField == destination || target.PossibleDestinationFields.Contains(destination) || target.SpatialDestinations?.Contains(destination) == true) && CanRequestFieldTravel(state, observation, target) && canSelect?.Invoke(target) != false).ToArray();
        Ff7AutoPlayTarget? selected = candidates.FirstOrDefault(target => state.NavigationGoal is { } retained && retained.NavigationKey == observation.NavigationKey && retained.EventId == target.Id)
            ?? candidates.FirstOrDefault(target => target.DestinationField == destination)
            ?? candidates.FirstOrDefault();
        bool limited = false;
        if (selected is null && readConnections is not null)
        {
            (selected, limited) = FindFieldDestinationAdvice(state, observation, destination, readConnections, canSelect);
        }
        if (selected is not null)
        {
            state.NavigationGoal = new(observation.NavigationKey, selected.Id, selected.ConditionFingerprint);
            AutoPlayEventCoverage<Ff7AutoPlayDeferral>? coverage = state.Coverage.Visits.SingleOrDefault(visit => visit.Visit.Key == observation.VisitId)?.Events.SingleOrDefault(item => item.Event.Id == selected.Id);
            if (coverage is { Completed: true } && coverage.Conditions?.HasExecutedEntry(selected.ConditionFingerprint) == true)
            {
                // Explicit travel intent rearms the route without erasing native consumption or component receipts.
                coverage.RouteNeedsRediscovery = true;
                coverage.GlobalRouteRediscovery = true;
            }
        }
        else if (readConnections is not null)
        {
            state.NavigationGoal = null;
            return limited ? $"Native field connectivity advice to field {destination} reached its bounded search limit." : $"No currently admitted native field connectivity route to field {destination} is established from this navigation component.";
        }
        return null;
    }

    private static (Ff7AutoPlayTarget? Target, bool Limited) FindFieldDestinationAdvice(Ff7AutoPlayState state, Ff7AutoPlayObservation observation, ushort destination, Func<ushort, int[]> readConnections, Func<Ff7AutoPlayTarget, bool>? canSelect)
    {
        const int maximumFields = 128;
        Queue<(ushort Field, Ff7AutoPlayTarget First)> pending = new();
        int current = ReadVisitField(observation.VisitId);
        HashSet<ushort> visited = current > 64 && current < short.MaxValue ? [(ushort)current] : [];
        foreach (Ff7AutoPlayTarget target in observation.Events.Where(target => target.AnalysisComplete && (target.Kind == Ff7AutoPlayTargetKind.Gateway || target.ChangesField) && CanRequestFieldTravel(state, observation, target) && canSelect?.Invoke(target) != false))
        {
            foreach (int field in TargetDestinations(target).Where(field => field > 64 && field < short.MaxValue && field != current))
            {
                pending.Enqueue(((ushort)field, target));
            }
        }
        while (pending.Count != 0 && visited.Count < maximumFields)
        {
            var step = pending.Dequeue();
            if (!visited.Add(step.Field))
            {
                continue;
            }
            if (step.Field == destination)
            {
                return (step.First, false);
            }
            IEnumerable<int> recorded = state.Coverage.Visits.Where(visit => ReadVisitField(visit.Visit.Key) == step.Field).SelectMany(visit => visit.Events).SelectMany(item => item.Routes).Select(route => ReadVisitField(route.DestinationVisitKey));
            foreach (int field in readConnections(step.Field).Concat(recorded).Where(field => field > 64 && field < short.MaxValue).Distinct())
            {
                pending.Enqueue(((ushort)field, step.First));
            }
        }
        return (null, pending.Count != 0);
    }

    internal static bool IsDestinationPlan(Ff7AutoPlayState state, AutoPlayPlanStep? next, string navigationKey)
    {
        return state.DestinationField is null || state.NavigationGoal is { } goal && next is not null && (next.Goal == goal || goal.NavigationKey == navigationKey && next.Event.Id == goal.EventId && next.Event.ConditionSignature == goal.Conditions);
    }

    private static IEnumerable<int> TargetDestinations(Ff7AutoPlayTarget target)
    {
        return (target.DestinationField is int field ? new[] { field } : []).Concat(target.PossibleDestinationFields).Concat(target.SpatialDestinations ?? []).Distinct();
    }

    private static bool CanRequestFieldTravel(Ff7AutoPlayState state, Ff7AutoPlayObservation observation, Ff7AutoPlayTarget target)
    {
        AutoPlayEventCoverage<Ff7AutoPlayDeferral>? coverage = state.Coverage.Visits.SingleOrDefault(visit => visit.Visit.Key == observation.VisitId)?.Events.SingleOrDefault(item => item.Event.Id == target.Id);
        if (coverage is not { Completed: true } || coverage.Conditions?.HasExecutedEntry(target.ConditionFingerprint) != true)
        {
            return true;
        }
        // Possible branches are advice; repeat travel requires an actual transfer under this same entry condition.
        return coverage.Routes.Any(route => route.ConditionSignature == target.ConditionFingerprint && ReadVisitField(route.DestinationVisitKey) > 64 && ReadVisitField(route.DestinationVisitKey) != ReadVisitField(observation.VisitId));
    }

    private static int ReadVisitField(string visit)
    {
        string[] parts = visit.Split(':', 3);
        return parts.Length == 3 && parts[0] == "ff7" && int.TryParse(parts[1], out int field) ? field : -1;
    }

    /// <summary>Sets a planning destination without changing native execution or selecting physical inputs.</summary>
    public void SetDestinationField(ushort? destination)
    {
        if (destination is ushort field && !session.IsKnownAutoPlayDestination(field))
        {
            throw new ArgumentOutOfRangeException(nameof(destination), "An autoplay destination must identify an ordinary native field.");
        }
        if (State.Active is { Accepted: true } || State.Choice is not null || State.PendingRestore is not null)
        {
            throw new InvalidOperationException("The current native event must return before changing its planning destination.");
        }
        State.DestinationField = destination;
        State.Active = null;
        State.NavigationGoal = null;
        worldModeCatalog.Clear();
        worldModeQueue.Clear();
        worldModeCandidatesObserved = false;
        State.Status = destination is null ? "Resuming native frontier exploration" : $"Planning a native route to field {destination}";
        Publish();
    }
}
