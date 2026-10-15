namespace Memoria.FF8.Runtime;

public sealed partial class GameSession
{
    private static void ValidateBattleFactsCheckpoint(GameState state)
    {
        state.PendingBattleEncounter?.Validate();
        if (state.AppliedBattleResultIds is null || state.AppliedBattleResultIds.Any(id => string.IsNullOrWhiteSpace(id) || id.Length > 160) || state.AppliedBattleResultIds.Distinct(StringComparer.Ordinal).Count() != state.AppliedBattleResultIds.Length || state.PendingBattleEncounter is { } pending && (state.AppliedBattleResultIds.Contains(pending.OccurrenceId, StringComparer.Ordinal) || pending.SourceScene != state.Scene || pending.SourceFieldId != state.FieldId))
        {
            throw new InvalidDataException("Battle fact checkpoint has duplicate receipts or a contradictory current encounter owner.");
        }
    }

    public void SetPendingBattleEncounter(ReplayBattleEncounter? encounter)
    {
        encounter?.Validate();
        if (encounter is not null && State.AppliedBattleResultIds.Contains(encounter.OccurrenceId, StringComparer.Ordinal))
        {
            throw new InvalidOperationException("A completed encounter occurrence cannot become pending again.");
        }
        State = State with { PendingBattleEncounter = encounter };
    }

    public void CommitBattleResult(ReplayBattleEncounter encounter, NativeCharacterVitals vitals, NativeEconomy economy)
    {
        encounter.Validate();
        vitals.Validate();
        economy.Validate();
        if (State.PendingBattleEncounter != encounter || State.AppliedBattleResultIds.Contains(encounter.OccurrenceId, StringComparer.Ordinal))
        {
            throw new InvalidOperationException("Battle result requires its unapplied current encounter occurrence.");
        }
        GameState candidate = State with { CharacterVitals = vitals.DeepCopy(), Economy = economy.DeepCopy(), AppliedBattleResultIds = [.. State.AppliedBattleResultIds, encounter.OccurrenceId], PendingBattleEncounter = null };
        ValidateLiveEventMemoryOwners(candidate, State.EventMemory);
        State = candidate;
    }

    public void SetRandomEncounters(RandomEncounterSessionState state)
    {
        state.Validate();
        State = State with { RandomEncounters = state };
    }
    public void SetFullBattlePlaythrough(bool enabled)
    {
        if (State.Battle is not null)
        {
            throw new InvalidOperationException("Battle policy cannot change during combat.");
        }
        State = State with { FullBattlePlaythrough = enabled ? true : null };
    }

    public void SetBattle(BattleSnapshot? snapshot)
    {
        BattleSnapshot? owned = snapshot is null ? null : BattleSession.Restore(snapshot).Capture();
        State = State with { Battle = owned };
    }
}
