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

    internal static void ResolveFieldDestinationGoal(Ff7AutoPlayState state, Ff7AutoPlayObservation observation)
    {
        if (state.DestinationField is not ushort destination)
        {
            return;
        }
        Ff7AutoPlayTarget[] candidates = observation.Events.Where(target => target.AnalysisComplete && (target.Kind == Ff7AutoPlayTargetKind.Gateway || target.ChangesField) && (target.DestinationField == destination || target.PossibleDestinationFields.Contains(destination) || target.SpatialDestinations?.Contains(destination) == true)).ToArray();
        Ff7AutoPlayTarget? selected = candidates.FirstOrDefault(target => state.NavigationGoal is { } retained && retained.NavigationKey == observation.NavigationKey && retained.EventId == target.Id)
            ?? candidates.FirstOrDefault(target => target.DestinationField == destination)
            ?? candidates.FirstOrDefault();
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
