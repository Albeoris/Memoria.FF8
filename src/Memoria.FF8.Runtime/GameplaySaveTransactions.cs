using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Committed abandonment fact, excluding its former VM wait and next-operation pointer.</summary>
public sealed record GameplaySaveAbandonedBattle([property: JsonRequired] string OccurrenceId, [property: JsonRequired] ushort DefeatFlags, [property: JsonRequired] long EntryClockCalls, [property: JsonRequired] long ExitClockCalls, [property: JsonRequired] int CombatCallbacks);

public sealed record GameplaySaveTransactions
{
    [JsonRequired]
    public required string[] AppliedBattleResults { get; init; }
    [JsonRequired]
    public required NativeBattleStatistics? BattleStatistics { get; init; }
    [JsonRequired]
    public required NativeBattleVariableState? BattleVariables { get; init; }
    [JsonRequired]
    public required GameplaySaveAbandonedBattle[]? AbandonedBattles { get; init; }
    [JsonRequired]
    public required ImmutableArray<EnemyDefeatedEvent> PendingDefeats { get; init; }
    [JsonRequired]
    public required ImmutableArray<BattleKillTotalObservedEvent> PendingKillObservations { get; init; }
    [JsonRequired]
    public required ImmutableArray<OriginalAchievementObservedEvent> PendingOriginalObservations { get; init; }
    [JsonRequired]
    public required ImmutableArray<OriginalAchievementActionEvent> PendingOriginalActions { get; init; }
    [JsonRequired]
    public required OriginalAchievementDrawCursor? DrawCursor { get; init; }
    [JsonRequired]
    public required string? MutationContext { get; init; }
    [JsonRequired]
    public required long MutationSequence { get; init; }
    [JsonRequired]
    public required OriginalAchievementObservedEvent? LastOriginalObservation { get; init; }
    [JsonRequired]
    public required BattleAchievementCursor? BattleCursor { get; init; }

    internal static GameplaySaveTransactions Capture(GameState state)
    {
        foreach (NativeBattleAbandonment abandoned in state.AbandonedNativeBattles ?? [])
        {
            abandoned.Validate();
        }
        return new()
        {
            AppliedBattleResults = state.AppliedBattleResultIds,
            BattleStatistics = state.BattleStatistics,
            BattleVariables = state.BattleVariables,
            AbandonedBattles = state.AbandonedNativeBattles?.Select(value => new GameplaySaveAbandonedBattle(value.Encounter.OccurrenceId, value.Branch.PersistentFlags182, value.EntryClockCalls, value.ExitClockCalls, value.CombatCallbacks)).ToArray(),
            PendingDefeats = state.PendingAchievementFacts,
            PendingKillObservations = state.PendingAchievementObservations,
            PendingOriginalObservations = state.PendingOriginalAchievementObservations,
            PendingOriginalActions = state.PendingOriginalAchievementActions,
            DrawCursor = state.OriginalAchievementDrawCursor,
            MutationContext = state.OriginalAchievementMutationContext,
            MutationSequence = state.OriginalAchievementMutationSequence,
            LastOriginalObservation = state.LastOriginalAchievementObservation,
            BattleCursor = state.AchievementCursor
        };
    }

    public void Validate()
    {
        if (AppliedBattleResults is null || AppliedBattleResults.Any(string.IsNullOrWhiteSpace) || AppliedBattleResults.Distinct(StringComparer.Ordinal).Count() != AppliedBattleResults.Length || PendingDefeats.IsDefault || PendingKillObservations.IsDefault || PendingOriginalObservations.IsDefault || PendingOriginalActions.IsDefault || MutationSequence < 0 || DrawCursor is { } cursor && (string.IsNullOrWhiteSpace(cursor.OccurrenceId) || cursor.Operations < 0) || BattleCursor is { } battle && (string.IsNullOrWhiteSpace(battle.OccurrenceId) || battle.Allocations.IsDefault))
        {
            throw new InvalidDataException("Gameplay transaction receipts are malformed.");
        }
        BattleVariables?.Validate();
        foreach (GameplaySaveAbandonedBattle abandoned in AbandonedBattles ?? [])
        {
            if (abandoned is null || !AppliedBattleResults.Contains(abandoned.OccurrenceId, StringComparer.Ordinal) || abandoned.EntryClockCalls < 0 || abandoned.CombatCallbacks < 0 || abandoned.ExitClockCalls != abandoned.EntryClockCalls + 4L * abandoned.CombatCallbacks)
            {
                throw new InvalidDataException("Gameplay abandonment receipt has no matching committed result.");
            }
        }
        foreach (IAchievementEvent fact in PendingDefeats.Cast<IAchievementEvent>().Concat(PendingKillObservations.Cast<IAchievementEvent>()).Concat(PendingOriginalObservations.Cast<IAchievementEvent>()).Concat(PendingOriginalActions.Cast<IAchievementEvent>()))
        {
            if (string.IsNullOrWhiteSpace(fact.EventId.Value) || string.IsNullOrWhiteSpace(fact.ContextId.Value))
            {
                throw new InvalidDataException("Gameplay achievement outbox fact has no operation identity.");
            }
            fact.Owner?.Validate();
        }
        foreach (OriginalAchievementActionEvent fact in PendingOriginalActions)
        {
            fact.Validate();
        }
    }
}
