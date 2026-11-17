using Memoria.AutoPlay;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7AutoPlayPolicy
{
    private void RetainDestinationGoal(AutoPlayPlanStep next, string navigationKey, string condition)
    {
        AutoPlayTravelGoal? retained = State.DestinationField is not null && State.NavigationGoal is { EventId: not null } goal && goal.EventId == next.Event.Id && goal.NavigationKey == navigationKey && (goal.Conditions is null || goal.Conditions == condition) ? goal : null;
        State.NavigationGoal = next.Goal ?? retained;
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
