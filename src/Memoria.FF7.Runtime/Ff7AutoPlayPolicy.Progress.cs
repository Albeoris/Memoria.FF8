namespace Memoria.FF7.Runtime;

public sealed partial class Ff7AutoPlayPolicy
{
    internal const int FrontierIdleLimit = 7200;

    internal static void TrackFrontierProgress(Ff7AutoPlayState state, int story)
    {
        var events = state.Coverage.Visits.SelectMany(visit => visit.Events).ToArray();
        int routes = events.Sum(item => item.Routes.Select(route => route.DestinationVisitKey).Distinct(StringComparer.Ordinal).Count());
        int chunks = state.WorldVisits.Values.Sum(visit => visit.ObservedChunks.Count);
        string key = $"{story}:{state.Coverage.Visits.Count}:{events.Length}:{events.Count(item => item.Completed)}:{routes}:{chunks}";
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
