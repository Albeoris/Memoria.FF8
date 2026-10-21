using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record BattleTurnAdvanceRequest(
    [property: JsonRequired] ImmutableArray<string> EnemyActorIds,
    [property: JsonRequired] ImmutableArray<string> ReadyPlayerIds,
    [property: JsonRequired] int RemainingFrames);

public sealed partial class BattleSession
{
    private BattleTurnAdvanceRequest? turnAdvance;

    private void StopTurnAdvance()
    {
        turnAdvance = null;
    }

    private void AwaitAdvancedEnemyTurn(HashSet<string> waiting, int remainingFrames)
    {
        turnAdvance = null;
        if (!CanExplicitlyAdvanceTurn || HasPendingTurn || HasGuardianForceTimeBoundary || combatants.Any(actor => actor.IsPlayer && actor.IsReady && waiting.Contains(actor.Id)))
        {
            return;
        }
        ImmutableArray<string> enemies = combatants.Where(actor => !actor.IsPlayer && actor.IsReady && waiting.Contains(actor.Id)).Select(actor => actor.Id).ToImmutableArray();
        if (!enemies.IsEmpty)
        {
            turnAdvance = new(enemies, combatants.Where(actor => actor.IsPlayer && actor.IsReady).Select(actor => actor.Id).ToImmutableArray(), remainingFrames);
        }
    }

    private bool ContinueTurnAdvance()
    {
        if (turnAdvance is not { } request)
        {
            return false;
        }
        if (Mode != BattleAtbMode.TurnBased || Outcome != BattleOutcome.Active || HasPendingNativeOutcome || HasGuardianForceTimeBoundary || combatants.Any(actor => actor.IsPlayer && actor.IsReady && !request.ReadyPlayerIds.Contains(actor.Id)))
        {
            StopTurnAdvance();
            return false;
        }
        // An empty AI decision still owns its ordinary completion callback and post-action effects.
        // Resume only after those owners release the normal clock, on a later presentation tick.
        if (!CanAdvanceTurnBasedTime || request.EnemyActorIds.Any(id => byId[id].IsReady))
        {
            return false;
        }
        if (request.RemainingFrames == 0)
        {
            throw new TimeoutException("Advance Turn exhausted its logical time budget without combat activity.");
        }
        int advanced = AdvanceTurn(request.RemainingFrames);
        if (advanced == 0)
        {
            StopTurnAdvance();
        }
        return advanced != 0;
    }

    private void RestoreTurnAdvance(BattleTurnAdvanceRequest? request)
    {
        if (request is not null && (Mode != BattleAtbMode.TurnBased || Outcome != BattleOutcome.Active || request.RemainingFrames is < 0 or > MaximumAdvanceFrames || request.EnemyActorIds.IsDefaultOrEmpty || request.ReadyPlayerIds.IsDefault || request.EnemyActorIds.Distinct(StringComparer.Ordinal).Count() != request.EnemyActorIds.Length || request.ReadyPlayerIds.Distinct(StringComparer.Ordinal).Count() != request.ReadyPlayerIds.Length || request.EnemyActorIds.Any(id => id is null || !byId.TryGetValue(id, out BattleCombatant? actor) || actor.IsPlayer) || request.ReadyPlayerIds.Any(id => id is null || !byId.TryGetValue(id, out BattleCombatant? actor) || !actor.IsPlayer)))
        {
            throw new InvalidDataException("Invalid pending Advance Turn request.");
        }
        turnAdvance = request;
    }
}
