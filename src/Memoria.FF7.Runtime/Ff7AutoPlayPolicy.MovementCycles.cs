using System.Security.Cryptography;
using System.Text;
using Memoria.AutoPlay;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7AutoPlayPolicy
{
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

    internal static string ReadMovementFailureFingerprint(string physical, Ff7AutoPlayMovementFailure? failure, IReadOnlyList<Ff7Actor> actors)
    {
        if (failure is null)
        {
            return physical;
        }
        Ff7Actor player = actors.Single(actor => actor.PlayerControlled);
        bool inside = player.X4096 >= failure.MinX && player.X4096 <= failure.MaxX && player.Y4096 >= failure.MinY && player.Y4096 <= failure.MaxY;
        StringBuilder witness = new($"{physical}|movement-region:{inside}");
        foreach (Ff7Actor actor in actors.Where(actor => !actor.PlayerControlled && actor.CollisionEnabled).OrderBy(actor => actor.Entity))
        {
            witness.Append($"|occupancy:{actor.Entity}:{actor.Model}:{actor.X4096}:{actor.Y4096}:{actor.Z4096}:{actor.Triangle}:{actor.CollisionRadius}");
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(witness.ToString())));
    }
}
