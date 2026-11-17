using Memoria.AutoPlay;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7AutoPlayPolicy
{
    private static bool HasConsumedWorldEntry(AutoPlayEventCoverage<Ff7AutoPlayDeferral> coverage, string? condition)
    {
        if (!coverage.Completed)
        {
            return false;
        }
        if (condition is not null && coverage.Conditions is not null)
        {
            return coverage.Conditions.HasExecutedEntry(condition);
        }
        return coverage.ObservedConditionSignature is null || coverage.ObservedConditionSignature == condition;
    }

    internal static AutoPlayEventAvailability ReadWorldSemanticAvailability(AutoPlayEventCoverage<Ff7AutoPlayDeferral> coverage, AutoPlayEvent observed, AutoPlayEventAvailability physical)
    {
        // Pose-dependent failures still gate execution and recorded travel. Successful
        // completion belongs to the native predicate, geometry and source context.
        return physical with { Fingerprint = HasConsumedWorldEntry(coverage, observed.ConditionSignature) ? null : observed.ConditionSignature };
    }

    internal static bool RetireConsumedWorldFrontier(Ff7AutoPlayState state)
    {
        if (state.Choice is not null || state.PendingRestore is not null || state.Active is not { Kind: Ff7AutoPlayTargetKind.WorldMesh or Ff7AutoPlayTargetKind.WorldModel, Accepted: false, Returned: false, TransitionOwned: false, CompletionRecorded: false, TravelRoute: null } active || state.NavigationGoal is not null)
        {
            return false;
        }
        // Current semantic selections can be required topology probes even after entry
        // completion. Only the obsolete physical completion token needs retirement.
        if (active.EntryAvailabilityFingerprint is not string legacyAvailability || legacyAvailability == active.ConditionFingerprint)
        {
            return false;
        }
        AutoPlayEventCoverage<Ff7AutoPlayDeferral>? coverage = state.Coverage.Visits.SingleOrDefault(visit => visit.Visit.Key == active.VisitId)?.Events.SingleOrDefault(item => item.Event.Id == active.EventId);
        if (coverage is null || coverage.Event.ConditionSignature != active.ConditionFingerprint || coverage.RouteNeedsRediscovery || coverage.GlobalRouteRediscovery || !HasConsumedWorldEntry(coverage, active.ConditionFingerprint))
        {
            return false;
        }
        state.Active = null;
        return true;
    }
}
