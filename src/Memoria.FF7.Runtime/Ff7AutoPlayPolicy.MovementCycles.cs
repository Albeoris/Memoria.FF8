using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Memoria.AutoPlay;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7AutoPlayPolicy
{
    internal static void RecordFailedAdmission(Ff7AutoPlayState state, AutoPlayPlanner<Ff7AutoPlayDeferral> planner, Ff7AutoPlayActiveEvent active, IReadOnlyDictionary<string, string> currentFacts)
    {
        var coverage = state.Coverage.Visits.Single(visit => visit.Visit.Key == active.VisitId).Events.Single(item => item.Event.Id == active.EventId);
        if (active.Accepted || active.Returned || active.TransitionOwned || active.CompletionRecorded || active.EntryGuardFingerprints is null || coverage.Event.ConditionSignature != active.ConditionFingerprint)
        {
            return;
        }
        foreach (var entry in active.EntryGuardFingerprints)
        {
            if (!state.NavigationGuards.TryGetValue(entry.Key, out var guard) || guard.VisitId != active.VisitId || guard.NavigationKey != active.NavigationKey || guard.EventId != active.EventId || !currentFacts.TryGetValue(entry.Key, out string? current))
            {
                continue;
            }
            if (coverage.UnavailableAttempts.Any(attempt => attempt.GuardKey == entry.Key && attempt.GuardFingerprint == current && attempt.SourceConditionSignature == active.ConditionFingerprint && (attempt.TargetId ?? active.EventId) == active.EventId))
            {
                planner.MarkUnavailable(active.VisitId, active.EventId, new(active.ConditionFingerprint, active.VisitId, entry.Key, entry.Value, active.EventId));
            }
        }
    }

    internal static bool DeferUnavailableActiveNavigation(Ff7AutoPlayState state, Ff7AutoPlayObservation observation)
    {
        if (state.Active is not { Accepted: false } active || active.VisitId != observation.VisitId || active.Generation != observation.Generation)
        {
            return false;
        }
        Ff7AutoPlayTarget? target = observation.Events.SingleOrDefault(target => target.Id == active.EventId);
        AutoPlayEventCoverage<Ff7AutoPlayDeferral>? coverage = state.Coverage.Visits.SingleOrDefault(visit => visit.Visit.Key == observation.VisitId)?.Events.SingleOrDefault(item => item.Event.Id == active.EventId);
        if (target is null || coverage is null || coverage.Event.ConditionSignature != target.ConditionFingerprint || !coverage.UnavailableVariants.Contains(target.Id))
        {
            return false;
        }
        state.Active = null;
        state.Status = "Replanning after entering a deferred native approach region";
        return true;
    }

    internal static string ReadMovementFailureGuardKey(string navigationKey, string eventId, string kind, Ff7AutoPlayMovementFailure? failure, IReadOnlyList<int>? blockingEntities = null)
    {
        string key = navigationKey + ":" + eventId + ":" + kind;
        if (failure is null)
        {
            return key;
        }
        byte[] scope = JsonSerializer.SerializeToUtf8Bytes(new { Region = failure, Blockers = blockingEntities?.Distinct().Order().ToArray() ?? [] });
        return key + ":" + Convert.ToHexString(SHA256.HashData(scope));
    }

    internal static void RestoreUnscopedMovementFailures(Ff7AutoPlayState state)
    {
        foreach (var visit in state.Coverage.Visits)
        {
            foreach (var item in visit.Events.Where(item => item.BlockKind == AutoPlayBlockKind.Walking && item.BlockedReason == "Physical navigation repeated a bounded native position cycle."))
            {
                // Older observation caches recorded a local search failure as permanent execution.
                // They have no position witness with which to retain that optional deferral.
                item.BlockedReason = null;
                item.BlockKind = null;
                item.WalkingSearchRevision = null;
                if (!item.Completed && item.Event.ConditionSignature is string signature)
                {
                    item.Conditions?.ConsumedEntries.Remove(signature);
                }
            }
        }
    }

    internal static string ReadMovementFailureFingerprint(string physical, Ff7AutoPlayMovementFailure? failure, IReadOnlyList<Ff7Actor> actors, IReadOnlyList<int>? blockingEntities = null)
    {
        if (failure is null)
        {
            return physical;
        }
        StringBuilder witness = new($"{physical}|movement-entry-admission:2");
        foreach (Ff7Actor actor in actors.Where(actor => !actor.PlayerControlled && actor.CollisionEnabled && (blockingEntities is not { Count: > 0 } || blockingEntities.Contains(actor.Entity))).OrderBy(actor => actor.Entity))
        {
            witness.Append($"|occupancy:{actor.Entity}:{actor.Model}:{actor.X4096}:{actor.Y4096}:{actor.Z4096}:{actor.Triangle}:{actor.CollisionRadius}");
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(witness.ToString())));
    }
}
