using Memoria.AutoPlay;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7AutoPlayPolicy
{
    private Ff7AutoPlayActiveEvent RefreshCompletedGatewayTravel(Ff7AutoPlayObservation observation, Ff7AutoPlayActiveEvent active)
    {
        if (active.TravelRoute is not { Selections: not null } route || active.TravelSelections is { } executed && route.Selections.SequenceEqual(executed))
        {
            return active;
        }
        int source = ReadVisitField(active.VisitId);
        bool nativeGateway = active.Kind == Ff7AutoPlayTargetKind.Gateway && source > 64 && source < short.MaxValue && active.DestinationField == session.MapId && session.Host.Process.PreviousMap == source && session.MatchesAutoPlayGateway((ushort)source, active.VisitId, active.EventId, active.ConditionFingerprint, session.MapId);
        bool liveChoice = State.Choice is not null || session.Host.Process.Dialogues.Any(dialogue => !dialogue.Completed && dialogue.FirstChoice is not null);
        return RefreshCompletedGatewayTravel(active, observation, nativeGateway, liveChoice);
    }

    internal static Ff7AutoPlayActiveEvent RefreshCompletedGatewayTravel(Ff7AutoPlayActiveEvent active, Ff7AutoPlayObservation observation, bool nativeGatewayProved, bool liveChoice)
    {
        if (active.TravelRoute is not { Selections: not null } route || active.TravelSelections is { } executed && route.Selections.SequenceEqual(executed))
        {
            return active;
        }
        if (!nativeGatewayProved || liveChoice || active.Kind != Ff7AutoPlayTargetKind.Gateway || active.Entity != -1 || active.Script != -1 || active.Generation == observation.Generation || active.VisitId == observation.VisitId || active.DestinationField != ReadVisitField(observation.VisitId) || active.TravelSelections is { Count: > 0 } || route.ConditionSignature != active.ConditionFingerprint || route.TargetId != active.EventId || route.DestinationVisitKey != observation.VisitId)
        {
            return active;
        }
        // This actual native transfer needed no incidental approach choices. Refresh only its selected advice, retaining the historical route and choice ledgers.
        return active with { TravelRoute = route with { Selections = [] }, TravelSelections = [] };
    }
}
