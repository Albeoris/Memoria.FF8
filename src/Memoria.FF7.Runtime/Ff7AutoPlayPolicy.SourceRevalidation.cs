using System.Security.Cryptography;
using System.Text.Json;
using Memoria.AutoPlay;

namespace Memoria.FF7.Runtime;

internal sealed record Ff7NativeSourceRevalidation(string NavigationKey, string EventId, string Condition, string ReceiptKey);

public sealed partial class Ff7AutoPlayPolicy
{
    internal const int LocalSourceProverRevision = 1;

    internal static Ff7NativeSourceRevalidation[] ReadPendingSourceRevalidations(Ff7AutoPlayState state, Ff7AutoPlayObservation observation)
    {
        AutoPlayVisitCoverage<Ff7AutoPlayDeferral>? aggregate = state.Coverage.Visits.SingleOrDefault(visit => visit.Visit.Key == observation.VisitId);
        bool HasPending(AutoPlayEventCoverage<Ff7AutoPlayDeferral> item, string? condition) => item.Available && !item.PolicyDeferred && item.BlockedReason is null && (condition is not null ? item.Conditions?.HasExecutedEntry(condition) != true : !item.Completed);
        if (aggregate is null || !observation.Events.Any(target => target.AnalysisComplete && aggregate.Events.Any(item => item.Event.Id == target.Id && HasPending(item, target.ConditionFingerprint))))
        {
            return [];
        }
        List<Ff7NativeSourceRevalidation> pending = [];
        foreach (AutoPlayNavigationVisit source in state.Coverage.NavigationVisits.Where(visit => visit.VisitKey == observation.VisitId && visit.Key != observation.NavigationKey))
        {
            foreach (Ff7AutoPlayTarget target in observation.Events.Where(target => target.AnalysisComplete && target.Kind is >= Ff7AutoPlayTargetKind.LineTalk and <= Ff7AutoPlayTargetKind.LineLeave))
            {
                AutoPlayEvent? observed = source.Events.SingleOrDefault(item => item.Id == target.Id && item.ConditionSignature == target.ConditionFingerprint);
                if (observed is null || !state.NavigationGuards.Values.Any(guard => guard.NavigationKey == source.Key && guard.EventId == target.Id && guard.MovementFailure is not null) || ReadRetainedNavigationAvailability(state, source, observed)?.Unavailable != true)
                {
                    continue;
                }
                string key = ReadSourceRevalidationKey(state, source.Key, target.Id, target.ConditionFingerprint);
                if (!state.SourceRevalidationAttempts.Contains(key))
                {
                    pending.Add(new(source.Key, target.Id, target.ConditionFingerprint, key));
                }
            }
        }
        return pending.OrderBy(item => item.NavigationKey == state.NavigationGoal?.NavigationKey && state.NavigationGoal.EventId is null ? 0 : 1).ThenBy(item => aggregate.Events.Single(coverage => coverage.Event.Id == item.EventId).Conditions?.HasExecutedEntry(item.Condition) == true ? 1 : 0).ToArray();
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
        var facts = state.NavigationGuards.Where(entry => entry.Value.NavigationKey == navigationKey && entry.Value.EventId == eventId && entry.Value.MovementFailure is not null).OrderBy(entry => entry.Key, StringComparer.Ordinal).Select(entry => new { Guard = entry.Key, entry.Value.Entity, entry.Value.SourceTriangle, entry.Value.MovementFailure, entry.Value.BlockingEntities, entry.Value.SpatialBoundaryTriangles, entry.Value.MovementSourceInside, Fact = retained.GetValueOrDefault(entry.Key) ?? entry.Value.ObservedFingerprint }).ToArray();
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(new { Source = navigationKey, Event = eventId, Condition = condition, Revision = LocalSourceProverRevision, Facts = facts });
        return Convert.ToHexString(SHA256.HashData(bytes));
    }

    internal static void RecordSourceRevalidationAttempt(Ff7AutoPlayState state, string navigationKey, string eventId, string condition)
    {
        state.SourceRevalidationAttempts.Add(ReadSourceRevalidationKey(state, navigationKey, eventId, condition));
    }
}
