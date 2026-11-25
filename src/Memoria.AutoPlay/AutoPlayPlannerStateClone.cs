namespace Memoria.AutoPlay;

/// <summary>Copies the persisted planner graph without sharing mutable collection ownership.</summary>
public static class AutoPlayPlannerStateClone
{
    public static AutoPlayPlannerState<TDeferral> Clone<TDeferral>(AutoPlayPlannerState<TDeferral> state, Func<TDeferral, TDeferral> cloneDeferral) where TDeferral : class, IAutoPlayParticipantPolicyDeferral
    {
        return new()
        {
            Visits = state.Visits.Select(visit => new AutoPlayVisitCoverage<TDeferral> { Visit = visit.Visit, Events = visit.Events.Select(item => CloneEvent(item, cloneDeferral)).ToList() }).ToList(),
            Choices = state.Choices.Select(CloneChoice).ToList(),
            NavigationVisits = state.NavigationVisits.Select(visit => visit with { Events = visit.Events.Select(CloneEventDefinition).ToArray(), RouteRediscovery = new(visit.RouteRediscovery) }).ToList(),
            NavigationRoutes = state.NavigationRoutes.Select(CloneNavigationRoute).ToList(),
            NavigationRetirements = state.NavigationRetirements.Select(item => item with { Route = CloneNavigationRoute(item.Route) }).ToList()
        };
    }

    private static AutoPlayEventCoverage<TDeferral> CloneEvent<TDeferral>(AutoPlayEventCoverage<TDeferral> item, Func<TDeferral, TDeferral> cloneDeferral) where TDeferral : class, IAutoPlayParticipantPolicyDeferral
    {
        return new()
        {
            Event = CloneEventDefinition(item.Event),
            Available = item.Available,
            Completed = item.Completed,
            BlockedReason = item.BlockedReason,
            BlockKind = item.BlockKind,
            WalkingSearchRevision = item.WalkingSearchRevision,
            Conditions = item.Conditions is null ? null : new() { ConsumedEntries = new(item.Conditions.ConsumedEntries), ConsumedExits = new(item.Conditions.ConsumedExits), ActiveEntry = item.Conditions.ActiveEntry },
            ObservedStoryChanged = item.ObservedStoryChanged,
            ObservedConditionSignature = item.ObservedConditionSignature,
            Routes = item.Routes.Select(CloneRoute).ToList(),
            RetiredRoutes = item.RetiredRoutes.Select(route => route with { Route = CloneRoute(route.Route), ExecutedPrefix = route.ExecutedPrefix.ToArray() }).ToList(),
            RouteNeedsRediscovery = item.RouteNeedsRediscovery,
            GlobalRouteRediscovery = item.GlobalRouteRediscovery,
            UnavailableAttempts = item.UnavailableAttempts.ToList(),
            Unavailable = item.Unavailable,
            UnavailableVariants = new(item.UnavailableVariants),
            AvailabilityFingerprint = item.AvailabilityFingerprint,
            CompletedAvailabilityFingerprints = new(item.CompletedAvailabilityFingerprints),
            ParticipantPolicyDeferrals = item.ParticipantPolicyDeferrals.Select(cloneDeferral).ToList(),
            PolicyDeferredVariants = new(item.PolicyDeferredVariants),
            PolicyDeferred = item.PolicyDeferred
        };
    }

    private static AutoPlayEvent CloneEventDefinition(AutoPlayEvent item)
    {
        return item with { Variants = item.Variants?.ToArray() };
    }

    private static AutoPlayEventRoute CloneRoute(AutoPlayEventRoute route)
    {
        return route with { Selections = route.Selections?.ToArray() };
    }

    private static AutoPlayNavigationRoute CloneNavigationRoute(AutoPlayNavigationRoute route)
    {
        return route with { Route = CloneRoute(route.Route) };
    }

    private static AutoPlayInputSearchContext? CloneContext(AutoPlayInputSearchContext? context)
    {
        if (context is null)
        {
            return null;
        }
        return new()
        {
            Visit = context.Visit,
            Navigation = context.Navigation,
            NavigationSource = context.NavigationSource,
            Event = context.Event,
            Field = context.Field,
            Story = context.Story,
            ObjectId = context.ObjectId,
            Conditions = context.Conditions,
            BeforeCheckpoint = context.BeforeCheckpoint,
            Target = context.Target,
            AbsenceGuard = context.AbsenceGuard,
            Availability = context.Availability,
            ObservedControlLoss = context.ObservedControlLoss,
            SceneChoices = context.SceneChoices.ToList()
        };
    }

    private static AutoPlayChoiceCoverage CloneChoice(AutoPlayChoiceCoverage choice)
    {
        return new()
        {
            Key = choice.Key,
            OptionCount = choice.OptionCount,
            FirstOption = choice.FirstOption,
            BeforeCheckpoint = choice.BeforeCheckpoint,
            InputCheckpointPrepared = choice.InputCheckpointPrepared,
            SupersededInputDomain = choice.SupersededInputDomain,
            ReplayDeferredReason = choice.ReplayDeferredReason,
            ContinuationCheckpoint = choice.ContinuationCheckpoint,
            CompletedOptions = choice.CompletedOptions.ToList(),
            ActiveOption = choice.ActiveOption,
            Finished = choice.Finished,
            BeforeSelections = choice.BeforeSelections?.ToList(),
            BeforeContext = CloneContext(choice.BeforeContext),
            PolicyDeferredOptions = new(choice.PolicyDeferredOptions),
            PolicyExhausted = choice.PolicyExhausted,
            PolicyScope = choice.PolicyScope,
            PolicyCheckpoints = choice.PolicyCheckpoints.ToDictionary(item => item.Key, item => item.Value with { BeforeSelections = item.Value.BeforeSelections?.ToArray(), BeforeContext = CloneContext(item.Value.BeforeContext) })
        };
    }
}
