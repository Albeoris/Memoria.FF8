namespace Memoria.FF8;

internal sealed record AutoPlayJourneyHop(string Source, string Destination, string EventId, string? Conditions, AutoPlayTravelGoal Goal, int WalkingEvidenceRevision = 0, int WalkingSearchRevision = 0);

internal sealed partial class AutoPlayController
{
    private const int MaximumJourneyHops = 8192;

    private Func<AutoPlayPlanStep, bool>? TravelAdmission(string navigation)
    {
        ResetJourneyOnStoryChange();
        if (run.JourneyHops.Count == 0)
        {
            return null;
        }
        Dictionary<string, int> walkingEvidence = run.WalkRejections.CountBy(item => item.Source.VisitKey, StringComparer.Ordinal).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        // A newly rejected approach can expose a different route on the next arrival.
        return step => step.Goal is not { } goal || step.Route is not { } route ||
            !run.JourneyHops.Contains(new(navigation, route.DestinationVisitKey, step.Event.Id, step.Event.ConditionSignature, goal, walkingEvidence.GetValueOrDefault(route.DestinationVisitKey), AutoPlayPlanner.CurrentWalkingSearchRevision));
    }

    private void RecordJourneyHop(string destination, string destinationNavigation)
    {
        int story = ResetJourneyOnStoryChange();
        if (run.ActiveStory != story || run.ActiveTravelRoute is null || run.ActiveTravelGoal is not { } goal || run.ActiveNavigationKey is not { } source ||
            source == destinationNavigation || run.ActiveEvent is not { } eventId)
        {
            return;
        }
        int walkingEvidence = run.WalkRejections.Count(item => item.Source.VisitKey == destination);
        var hop = new AutoPlayJourneyHop(source, destination, eventId, run.ActiveConditionSignature, goal, walkingEvidence, AutoPlayPlanner.CurrentWalkingSearchRevision);
        if (run.JourneyHops.Count >= MaximumJourneyHops && !run.JourneyHops.Contains(hop))
        {
            throw new NotSupportedException("Autoplay exhausted the bounded journey history without story progress.");
        }
        run.JourneyHops.Add(hop);
    }

    private int ResetJourneyOnStoryChange()
    {
        int story = session.State.Variables.GetValueOrDefault(256);
        if (run.TravelStory != story)
        {
            run.TravelStory = story;
            run.JourneyHops.Clear();
        }
        return story;
    }
}
