using Memoria.AutoPlay;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7AutoPlayPolicy
{
    private bool IsCurrentPersistentGoalEligible(AutoPlayVisit visit, AutoPlayEvent target)
    {
        return State.DestinationField is not null || !session.HasCurrentPersistentNoEffectEntry(visit.Key, target.Id);
    }

    private bool HasCurrentPersistentNavigationGoal()
    {
        if (State.DestinationField is not null || State.NavigationGoal is not { EventId: not null } goal)
        {
            return true;
        }
        var source = State.Coverage.NavigationVisits.SingleOrDefault(visit => visit.Key == goal.NavigationKey);
        return source is null || !session.HasCurrentPersistentNoEffectEntry(source.VisitKey, goal.EventId);
    }

    private void RetireCurrentPersistentNoEffectGoal()
    {
        if (State.Active is { Accepted: true } || State.Choice is not null || State.PendingRestore is not null || HasCurrentPersistentNavigationGoal())
        {
            return;
        }
        CancelNativePlanning("current-persistent-entry-has-no-effect");
        State.NativeRoute = null;
        State.Active = null;
        State.NavigationGoal = null;
        State.TravelCommitment = null;
        State.Status = "Selecting a native goal whose current persistent guard permits an effect";
        Publish();
    }
}
