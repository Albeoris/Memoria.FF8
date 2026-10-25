using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>The accepted allocation facts already observed in the current encounter.</summary>
public sealed record BattleAchievementCursor(string OccurrenceId, ImmutableArray<string> Allocations);

public sealed partial class GameSession
{
    // The process host supplies identity at acceptance, never when replaying a saved outbox.
    public Func<AchievementOwner?>? CaptureAchievementOwner { get; set; }

    /// <summary>Publishes facts after a live gameplay operation has completed successfully.</summary>
    public void AcceptBattleAchievementFacts(IReadOnlyList<BattleEnemyDefeat> defeats)
    {
        State = StageBattleAchievementFacts(State, defeats);
    }

    private GameState StageBattleAchievementFacts(GameState candidate, IReadOnlyList<BattleEnemyDefeat> defeats)
    {
        if (candidate.PendingBattleEncounter is not { } encounter)
        {
            return candidate;
        }
        BattleAchievementCursor cursor = candidate.AchievementCursor ?? LegacyAchievementCursor(candidate);
        if (cursor.OccurrenceId != encounter.OccurrenceId)
        {
            throw new InvalidDataException("Achievement facts disagree with the current encounter owner.");
        }
        HashSet<string> observed = cursor.Allocations.ToHashSet(StringComparer.Ordinal);
        observed.UnionWith(candidate.PendingAchievementFacts.Where(fact => fact.ContextId.Value == encounter.OccurrenceId).Select(fact => fact.EventId.Value));
        foreach (BattleEnemyDefeat defeat in defeats)
        {
            defeat.Validate();
        }
        if (defeats.GroupBy(defeat => defeat.AllocationKey, StringComparer.Ordinal).Any(group => group.Any(defeat => defeat != group.First())))
        {
            throw new InvalidDataException("Accepted defeat facts contradict one allocation's immutable facts.");
        }
        defeats = defeats.DistinctBy(defeat => defeat.AllocationKey).ToArray();
        if (defeats.All(defeat => defeat.AllocationId is null || observed.Contains(defeat.AllocationId)))
        {
            return candidate;
        }
        ImmutableArray<EnemyDefeatedEvent>.Builder pending = candidate.PendingAchievementFacts.ToBuilder();
        long? nativeKillTotal = ProjectAchievementKillTotal(candidate.CharacterVitals, defeats);
        foreach (BattleEnemyDefeat defeat in defeats)
        {
            // Older snapshots without allocation identity cannot fabricate historical events.
            if (defeat.AllocationId is null || !observed.Add(defeat.AllocationId))
            {
                continue;
            }
            EnemyDefeatedEvent fact = EnemyDefeatedEvent.FromBattle(encounter.OccurrenceId, defeat);
            pending.Add(fact with { Owner = CaptureAchievementOwner?.Invoke(), Facts = fact.Facts with { NativeKillTotal = nativeKillTotal } });
        }
        return candidate with { AchievementCursor = new(encounter.OccurrenceId, [.. observed.Order(StringComparer.Ordinal)]), PendingAchievementFacts = pending.ToImmutable() };
    }

    private static long? ProjectAchievementKillTotal(NativeCharacterVitals? vitals, IReadOnlyList<BattleEnemyDefeat> defeats)
    {
        if (vitals is null)
        {
            return null;
        }
        ushort[] characters = vitals.Records.Select(record => record.KillCount).ToArray();
        if (vitals.GuardianForces.Any(gf => gf.KillCount is null))
        {
            return null;
        }
        ushort[] guardianForces = vitals.GuardianForces.Select(gf => gf.KillCount!.Value).ToArray();
        foreach (BattleEnemyDefeat defeat in defeats)
        {
            if (defeat.Credit == BattleDefeatCredit.Character && defeat.KillerRow is byte row)
            {
                characters[row] = unchecked((ushort)(characters[row] + 1));
            }
            if (defeat.Credit == BattleDefeatCredit.GuardianForce && defeat.GuardianForceId is byte id)
            {
                guardianForces[id] = unchecked((ushort)(guardianForces[id] + 1));
            }
        }
        return characters.Sum(value => (long)value) + guardianForces.Sum(value => (long)value);
    }

    private static BattleAchievementCursor LegacyAchievementCursor(GameState state)
    {
        return new(state.PendingBattleEncounter!.OccurrenceId, [.. (state.Battle?.EnemyDefeats ?? []).Where(defeat => defeat.AllocationId is not null).Select(defeat => defeat.AllocationId!).Distinct(StringComparer.Ordinal)]);
    }

    private static GameState FinalizeBattleAchievementFacts(GameState state, string occurrenceId, IReadOnlyList<BattleEnemyDefeat> defeats, BattleOutcome? outcome, bool resultPresentationEligible)
    {
        HashSet<string> accepted = defeats.Where(defeat => defeat.AllocationId is not null).Select(defeat => defeat.AllocationId!).ToHashSet(StringComparer.Ordinal);
        return state with
        {
            PendingAchievementFacts = [.. state.PendingAchievementFacts
                .Where(fact => fact.ContextId.Value != occurrenceId || fact.Facts.AcceptedOutcome is not null || outcome is not null && accepted.Contains(fact.EventId.Value))
                .Select(fact => fact.ContextId.Value == occurrenceId && fact.Facts.AcceptedOutcome is null
                    ? fact with { Facts = fact.Facts with { AcceptedOutcome = outcome, ResultPresentationEligible = resultPresentationEligible } }
                    : fact)]
        };
    }

    private GameState StageBattleAchievementObservation(GameState candidate, string occurrenceId, BattleOutcome? outcome, byte? formationFlags, BattleRewardPolicy policy)
    {
        if (outcome is null || formationFlags is null)
        {
            return candidate;
        }
        long? total = ProjectAchievementKillTotal(candidate.CharacterVitals, []);
        string observationId = $"kills/{total?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "unknown"}/{(int)outcome.Value}/{formationFlags.Value}/{(int)policy}";
        NativeWorldEventFlags? worldFlags = candidate.SavedWorld?.WorldEventFlags116;
        BattleKillTotalObservedEvent observation = new(new(observationId), new("battle-results/" + occurrenceId), total, outcome.Value, policy, formationFlags.Value, CaptureAchievementOwner?.Invoke())
        {
            DiablosAcquired = candidate.CharacterVitals is { } vitals ? vitals.GuardianForces[5].Flags != 0 : null,
            UfoCompleted = worldFlags is null || (worldFlags.KnownMask & (1UL << 15)) == 0 ? null : (worldFlags.Value & (1UL << 15)) != 0
        };
        if (candidate.PendingAchievementObservations.Any(fact => fact.ContextId == observation.ContextId && fact.EventId == observation.EventId))
        {
            return candidate;
        }
        return candidate with { PendingAchievementObservations = candidate.PendingAchievementObservations.Add(observation) };
    }

    /// <summary>Acknowledges only a receipt whose profile transaction has durably completed.</summary>
    public void AcknowledgeAchievementFact(EnemyDefeatedEvent fact)
    {
        AcknowledgeAchievementFacts([fact]);
    }

    public void AcknowledgeAchievementFacts(IReadOnlyList<EnemyDefeatedEvent> facts)
    {
        HashSet<EnemyDefeatedEvent> committed = facts.ToHashSet();
        State = State with { PendingAchievementFacts = [.. State.PendingAchievementFacts.Where(item => !committed.Contains(item))] };
    }

    public void AcknowledgeAchievementObservations(IReadOnlyList<BattleKillTotalObservedEvent> facts)
    {
        HashSet<BattleKillTotalObservedEvent> committed = facts.ToHashSet();
        State = State with { PendingAchievementObservations = [.. State.PendingAchievementObservations.Where(item => !committed.Contains(item))] };
    }

    private static void ValidateAchievementCheckpoint(GameState state)
    {
        ValidateOriginalAchievementCheckpoint(state);
        if (state.PendingAchievementObservations.IsDefault || state.PendingAchievementObservations.Any(fact => string.IsNullOrWhiteSpace(fact.EventId.Value) || string.IsNullOrWhiteSpace(fact.ContextId.Value) || fact.NativeKillTotal < 0 || fact.Outcome == BattleOutcome.Active || !Enum.IsDefined(fact.Outcome) || !Enum.IsDefined(fact.RewardPolicy)) || state.PendingAchievementObservations.Select(fact => (fact.ContextId, fact.EventId)).Distinct().Count() != state.PendingAchievementObservations.Length)
        {
            throw new InvalidDataException("Achievement outbox contains invalid or duplicate completed-battle observations.");
        }
        if (state.PendingAchievementFacts.IsDefault || state.PendingAchievementFacts.Any(fact => string.IsNullOrWhiteSpace(fact.EventId.Value) || string.IsNullOrWhiteSpace(fact.ContextId.Value) || fact.EventId.Value != fact.Facts.AllocationId) || state.PendingAchievementFacts.Select(fact => (fact.ContextId, fact.EventId)).Distinct().Count() != state.PendingAchievementFacts.Length)
        {
            throw new InvalidDataException("Achievement outbox contains invalid or duplicate accepted facts.");
        }
        if (state.AchievementCursor is { } cursor && (cursor.Allocations.IsDefault || cursor.Allocations.Any(string.IsNullOrWhiteSpace) || cursor.Allocations.Distinct(StringComparer.Ordinal).Count() != cursor.Allocations.Length || state.PendingBattleEncounter is not { } encounter || encounter.OccurrenceId != cursor.OccurrenceId))
        {
            throw new InvalidDataException("Achievement cursor has no matching live encounter.");
        }
    }
}
