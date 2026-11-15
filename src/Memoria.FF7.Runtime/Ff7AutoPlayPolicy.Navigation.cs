using Memoria.AutoPlay;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7AutoPlayPolicy
{
    private bool NeedsNavigationRefresh(AutoPlayVisit visit)
    {
        return NeedsNavigationRefresh(State, visit);
    }

    internal static bool NeedsNavigationRefresh(Ff7AutoPlayState state, AutoPlayVisit visit)
    {
        AutoPlayNavigationVisit? navigation = state.Coverage.NavigationVisits.SingleOrDefault(item => item.Key == visit.Key);
        if (navigation is null)
        {
            return false;
        }
        AutoPlayVisitCoverage<Ff7AutoPlayDeferral>? aggregate = state.Coverage.Visits.SingleOrDefault(item => item.Visit.Key == navigation.VisitKey);
        if (aggregate is null)
        {
            return false;
        }
        foreach (AutoPlayEvent observed in navigation.Events)
        {
            AutoPlayEventCoverage<Ff7AutoPlayDeferral>? item = aggregate.Events.SingleOrDefault(candidate => candidate.Event.Id == observed.Id);
            if (item is null || !item.Unavailable || item.Event.ConditionSignature != observed.ConditionSignature)
            {
                continue;
            }
            bool consumed = observed.ConditionSignature is string signature ? item.Conditions?.HasExecutedEntry(signature) == true : item.Completed;
            if (consumed)
            {
                continue;
            }
            bool attempted = item.UnavailableAttempts.Any(attempt => attempt.SourceConditionSignature == observed.ConditionSignature && state.NavigationGuards.TryGetValue(attempt.GuardKey, out Ff7AutoPlayNavigationGuard? guard) && guard.NavigationKey == navigation.Key && guard.VisitId == navigation.VisitKey && guard.EventId == observed.Id);
            if (!attempted)
            {
                return true;
            }
        }
        return false;
    }
}
