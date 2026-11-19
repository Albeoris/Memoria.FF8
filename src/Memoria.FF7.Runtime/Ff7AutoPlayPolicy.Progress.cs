namespace Memoria.FF7.Runtime;

public sealed partial class Ff7AutoPlayPolicy
{
    internal const int FrontierIdleLimit = 7200;

    private void TrackControllableFrontierProgress(bool controllable, Ff7AutoplayDriver driver)
    {
        try
        {
            CommitFrontierInput(State, Story(), controllable, driver.PlanningPending, State.PendingRestore is not null);
        }
        finally
        {
            if (session.AutoPlayState is { } published)
            {
                published.FrontierProgressKey = State.FrontierProgressKey;
                published.FrontierIdleSteps = State.FrontierIdleSteps;
            }
        }
    }

    internal static void CommitFrontierInput(Ff7AutoPlayState state, int story, bool controllable, bool planningPending, bool pendingRestore)
    {
        if (!controllable || planningPending || pendingRestore)
        {
            return;
        }
        TrackFrontierProgress(state, story);
    }

    internal static void TrackFrontierProgress(Ff7AutoPlayState state, int story)
    {
        var events = state.Coverage.Visits.SelectMany(visit => visit.Events).ToArray();
        int routes = events.Sum(item => item.Routes.Select(route => route.DestinationVisitKey).Distinct(StringComparer.Ordinal).Count());
        int chunks = state.WorldVisits.Values.Sum(visit => visit.ObservedChunks.Count);
        int completedEntries = events.Sum(item => item.Conditions is { ConsumedEntries.Count: > 0 } conditions ? conditions.ConsumedEntries.Count : item.Completed ? 1 : 0);
        string key = $"{story}:{state.Coverage.Visits.Count}:{events.Length}:{completedEntries}:{routes}:{chunks}";
        if (state.FrontierProgressKey != key)
        {
            state.FrontierProgressKey = key;
            state.FrontierIdleSteps = 0;
            return;
        }
        if (++state.FrontierIdleSteps >= FrontierIdleLimit)
        {
            throw new TimeoutException($"FFVII autoplay repeated frontier work for {FrontierIdleLimit} controllable steps without new native events, routes or story progress.");
        }
    }
}
