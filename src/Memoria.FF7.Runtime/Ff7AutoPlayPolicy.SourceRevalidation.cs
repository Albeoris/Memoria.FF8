using System.Security.Cryptography;
using System.Text.Json;
using Memoria.AutoPlay;

namespace Memoria.FF7.Runtime;

internal sealed record Ff7NativeSourceRevalidation(string NavigationKey, string EventId, string Condition, string ReceiptKey);

public sealed partial class Ff7AutoPlayPolicy
{
    internal const int LocalSourceProverRevision = 7;

    internal static Ff7NativeSourceRevalidation[] ReadPendingSourceRevalidations(Ff7AutoPlayState state, Ff7AutoPlayObservation observation)
    {
        // Existing guards record heuristic movement observations, not a native
        // source impossibility certificate. They cannot create maintenance goals.
        return [];
    }

    internal static bool HasSourceRevalidationRefusal(Ff7AutoPlayState state, string navigationKey, string eventId, string condition)
    {
        return false;
    }

    internal static AutoPlayPlanStep? SelectSourceRefreshPlan(Ff7AutoPlayState state, AutoPlayPlanner<Ff7AutoPlayDeferral> sharedPlanner, Ff7AutoPlayObservation observation, Ff7NativeSourceRevalidation debt, Func<AutoPlayVisit, bool>? needsRefresh, Func<AutoPlayNavigationVisit, AutoPlayEvent, AutoPlayEventAvailability?> readAvailability, IReadOnlySet<string>? avoidFirstEvents = null)
    {
        Ff7NativeSourceRevalidation[] pending = state.DestinationField is null ? ReadPendingSourceRevalidations(state, observation) : [];
        if (!pending.Contains(debt))
        {
            return null;
        }
        AutoPlayPlanStep? plan = sharedPlanner.PlanNavigation(observation.NavigationKey, needsRefresh: visit => pending.Any(item => item.NavigationKey == visit.Key) || needsRefresh?.Invoke(visit) == true, refreshNavigationKey: debt.NavigationKey, allowRemoteConditionRoutes: true, avoidFirstEvents: avoidFirstEvents, preferredGoal: new(debt.NavigationKey), readAvailability: readAvailability);
        return plan;
    }

    internal static void RetainSourceRevalidationTask(Ff7AutoPlayState state, AutoPlayPlanStep plan, Ff7NativeSourceRevalidation? debt)
    {
        if (debt is not null && plan.RefreshVisitKey == debt.NavigationKey && plan.Goal is { EventId: null } goal && goal.NavigationKey == debt.NavigationKey)
        {
            state.NativeRevalidation = new(debt.NavigationKey, debt.EventId, debt.Condition, debt.ReceiptKey);
        }
    }

    internal static bool HasArrivedSourceRevalidation(Ff7AutoPlayState state, Ff7AutoPlayObservation observation)
    {
        return state.NativeRevalidation is { ArrivalAcknowledged: true } task && task.NavigationKey == observation.NavigationKey && observation.Events.Any(target => target.Id == task.EventId && target.ConditionFingerprint == task.Condition && target.AnalysisComplete);
    }

    internal static bool CancelReleasedSourceRevalidation(Ff7AutoPlayState state, Ff7AutoPlayObservation observation, IReadOnlyDictionary<string, string> actualFacts)
    {
        if (!HasArrivedSourceRevalidation(state, observation))
        {
            return false;
        }
        Ff7AutoPlaySourceRevalidationTask task = state.NativeRevalidation!;
        AutoPlayNavigationVisit source = state.Coverage.NavigationVisits.Single(visit => visit.Key == observation.NavigationKey);
        AutoPlayEvent target = source.Events.Single(item => item.Id == task.EventId && item.ConditionSignature == task.Condition);
        if (ReadComponentAvailability(state, source, target, actualFacts).Unavailable)
        {
            return false;
        }
        state.NativeRevalidation = null;
        return true;
    }

    internal static void CompleteSourceRevalidation(Ff7AutoPlayState state, string navigationKey, string eventId, string condition)
    {
        RecordSourceRevalidationAttempt(state, navigationKey, eventId, condition);
        if (state.NativeRevalidation is { ArrivalAcknowledged: true } task && task.NavigationKey == navigationKey && task.EventId == eventId && task.Condition == condition)
        {
            state.NativeRevalidation = null;
        }
    }

    internal static string ReadSourceRevalidationKey(Ff7AutoPlayState state, string navigationKey, string eventId, string condition)
    {
        AutoPlayNavigationVisit source = state.Coverage.NavigationVisits.Single(visit => visit.Key == navigationKey);
        Dictionary<string, string> retained = ReadRetainedNavigationGuardFacts(state, source);
        var facts = state.NavigationGuards.Where(entry => entry.Value.NavigationKey == navigationKey && entry.Value.EventId == eventId && !IsUncertifiedMovementAdvice(entry.Key, entry.Value)).OrderBy(entry => entry.Key, StringComparer.Ordinal).Select(entry => new { Guard = entry.Key, entry.Value.Entity, entry.Value.SourceTriangle, entry.Value.MovementFailure, entry.Value.BlockingEntities, entry.Value.SpatialBoundaryTriangles, entry.Value.MovementSourceInside, Fact = retained.GetValueOrDefault(entry.Key) ?? entry.Value.ObservedFingerprint }).ToArray();
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(new { Source = navigationKey, Event = eventId, Condition = condition, Revision = LocalSourceProverRevision, Facts = facts });
        return Convert.ToHexString(SHA256.HashData(bytes));
    }

    internal static void RecordSourceRevalidationAttempt(Ff7AutoPlayState state, string navigationKey, string eventId, string condition)
    {
        state.SourceRevalidationAttempts.Add(ReadSourceRevalidationKey(state, navigationKey, eventId, condition));
    }
}
