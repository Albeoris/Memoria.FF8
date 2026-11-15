using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Memoria.AutoPlay;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7AutoPlayPolicy
{
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
        Ff7Actor player = actors.Single(actor => actor.PlayerControlled);
        bool inside = player.X4096 >= failure.MinX && player.X4096 <= failure.MaxX && player.Y4096 >= failure.MinY && player.Y4096 <= failure.MaxY && (failure.MinZ is not int minZ || player.Z4096 >= minZ) && (failure.MaxZ is not int maxZ || player.Z4096 <= maxZ);
        StringBuilder witness = new($"{physical}|movement-region:{inside}");
        foreach (Ff7Actor actor in actors.Where(actor => !actor.PlayerControlled && actor.CollisionEnabled && (blockingEntities is not { Count: > 0 } || blockingEntities.Contains(actor.Entity))).OrderBy(actor => actor.Entity))
        {
            witness.Append($"|occupancy:{actor.Entity}:{actor.Model}:{actor.X4096}:{actor.Y4096}:{actor.Z4096}:{actor.Triangle}:{actor.CollisionRadius}");
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(witness.ToString())));
    }
}
