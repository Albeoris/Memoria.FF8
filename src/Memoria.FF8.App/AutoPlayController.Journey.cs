namespace Memoria.FF8;

internal sealed record AutoPlayJourneyHop(string Source, string Destination, string EventId, string? Conditions, AutoPlayTravelGoal Goal);

internal sealed partial class AutoPlayController
{
    private const int MaximumJourneyHops = 8192;

    private Func<AutoPlayPlanStep, bool>? TravelAdmission(string navigation)
    {
        ResetJourneyOnStoryChange();
        return run.JourneyHops.Count == 0 ? null : step => step.Goal is not { } goal || step.Route is not { } route ||
            !run.JourneyHops.Contains(new(navigation, route.DestinationVisitKey, step.Event.Id, step.Event.ConditionSignature, goal));
    }

    private void RecordJourneyHop(string destination, string destinationNavigation)
    {
        int story = ResetJourneyOnStoryChange();
        if (run.ActiveStory != story || run.ActiveTravelRoute is null || run.ActiveTravelGoal is not { } goal || run.ActiveNavigationKey is not { } source ||
            source == destinationNavigation || run.ActiveEvent is not { } eventId)
        {
            return;
        }
        var hop = new AutoPlayJourneyHop(source, destination, eventId, run.ActiveConditionSignature, goal);
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
