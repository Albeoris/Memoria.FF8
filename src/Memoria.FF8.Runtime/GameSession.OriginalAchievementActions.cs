namespace Memoria.FF8.Runtime;

public sealed partial class GameSession
{
    private GameState StageOriginalAchievementAction(GameState candidate, OriginalAchievementActionKind kind, string eventId, string contextId, uint? squallExperience = null, BattleNativeDrawContext? stockDraw = null)
    {
        OriginalAchievementActionEvent fact = new(new(eventId), new(contextId + "/actions/" + originalAchievementActionBranch), kind, squallExperience, CaptureAchievementOwner?.Invoke(), stockDraw);
        fact.Validate();
        foreach (OriginalAchievementActionEvent prior in candidate.PendingOriginalAchievementActions)
        {
            if (prior.ContextId != fact.ContextId || prior.EventId != fact.EventId)
            {
                continue;
            }
            if (prior.Kind != fact.Kind || prior.SquallExperience != fact.SquallExperience || prior.StockDraw != fact.StockDraw)
            {
                throw new InvalidDataException("Accepted original achievement operation contradicts its immutable receipt.");
            }
            return candidate;
        }
        return candidate with { PendingOriginalAchievementActions = candidate.PendingOriginalAchievementActions.Add(fact) };
    }

    private GameState StageOriginalAchievementDraws(GameState candidate, BattleSnapshot? owned)
    {
        if (owned is null || candidate.PendingBattleEncounter is not { } encounter)
        {
            return candidate;
        }
        BattleOriginalStockDrawOperation[] operations = owned.OriginalStockDrawOperations ?? [];
        BattleOriginalStockDrawOperation[] previous = State.PendingBattleEncounter?.OccurrenceId == encounter.OccurrenceId ? State.Battle?.OriginalStockDrawOperations ?? [] : [];
        OriginalAchievementDrawCursor cursor = candidate.OriginalAchievementDrawCursor ?? new(encounter.OccurrenceId, previous.Length);
        if (cursor.OccurrenceId != encounter.OccurrenceId || cursor.Operations > operations.Length || cursor.Operations < 0)
        {
            throw new InvalidDataException("Accepted Stock Draw history moved behind its published encounter cursor.");
        }
        for (int index = 0; index < operations.Length; index++)
        {
            BattleOriginalStockDrawOperation operation = operations[index] ?? throw new InvalidDataException("Accepted Stock Draw history contains an empty receipt.");
            operation.Validate();
            if (operation.Ordinal != index + 1)
            {
                throw new InvalidDataException("Accepted Stock Draw history requires contiguous operation ordinals.");
            }
            if (index < cursor.Operations)
            {
                if (index < previous.Length && previous[index] != operation)
                {
                    throw new InvalidDataException("Accepted Stock Draw receipt changed behind its published encounter cursor.");
                }
                continue;
            }
            candidate = StageOriginalAchievementAction(candidate, OriginalAchievementActionKind.StockDraw, "stock-draw/" + operation.Ordinal.ToString(System.Globalization.CultureInfo.InvariantCulture), encounter.OccurrenceId, stockDraw: operation.Result);
        }
        candidate = candidate with { OriginalAchievementDrawCursor = new(encounter.OccurrenceId, operations.Length) };
        candidate = StageOriginalMaximumHitPoints(candidate, owned);
        return StageOriginalAchievementBattleState(candidate, owned);
    }

    private GameState StageOriginalMaximumHitPoints(GameState candidate, BattleSnapshot owned)
    {
        if (candidate.PendingBattleEncounter is not { } encounter || owned.DebugOutcome is not null || owned.Definition.EncounterId != encounter.Formation)
        {
            return candidate;
        }
        foreach (BattleCombatantSnapshot actor in owned.Combatants)
        {
            if (actor.NativeParticipant is not { Active: true, Suspended: false, ActorIndex: >= 0 and < 3, MaximumHitPoints: 9999 })
            {
                continue;
            }
            BattleCombatantDefinition? definition = owned.Definition.Combatants.SingleOrDefault(source => source.Id == actor.Id);
            if (definition is not { IsPlayer: true, MagicRecordId: >= 0 and < 11, NativePlayerStatRules: not null })
            {
                continue;
            }
            if (State.Battle?.Combatants.SingleOrDefault(previous => previous.Id == actor.Id)?.NativeParticipant is { Active: true, Suspended: false, MaximumHitPoints: 9999 })
            {
                continue;
            }
            candidate = StageOriginalAchievementAction(candidate, OriginalAchievementActionKind.MaximumHitPoints, "maximum-hp/" + definition.MagicRecordId.Value.ToString(System.Globalization.CultureInfo.InvariantCulture), encounter.OccurrenceId);
        }
        return candidate;
    }

    public void AcceptOriginalAchievementAction(OriginalAchievementActionKind kind, string eventId, string contextId, uint? squallExperience = null, BattleNativeDrawContext? stockDraw = null)
    {
        State = StageOriginalAchievementAction(State, kind, eventId, contextId, squallExperience, stockDraw);
    }

    public void AcknowledgeOriginalAchievementActions(IReadOnlyList<OriginalAchievementActionEvent> facts)
    {
        HashSet<OriginalAchievementActionEvent> committed = facts.ToHashSet();
        State = State with { PendingOriginalAchievementActions = [.. State.PendingOriginalAchievementActions.Where(item => !committed.Contains(item))] };
    }

    private static void ValidateOriginalAchievementActions(GameState state)
    {
        if (state.PendingOriginalAchievementActions.IsDefault || state.PendingOriginalAchievementActions.Select(fact => (fact.ContextId, fact.EventId)).Distinct().Count() != state.PendingOriginalAchievementActions.Length)
        {
            throw new InvalidDataException("Original achievement outbox contains invalid or duplicate accepted operations.");
        }
        foreach (OriginalAchievementActionEvent fact in state.PendingOriginalAchievementActions)
        {
            fact.Validate();
        }
        if (state.OriginalAchievementDrawCursor is { } cursor && (cursor.Operations < 0 || string.IsNullOrWhiteSpace(cursor.OccurrenceId) || state.PendingBattleEncounter?.OccurrenceId != cursor.OccurrenceId))
        {
            throw new InvalidDataException("Original achievement Draw cursor has no matching encounter owner.");
        }
    }
}
