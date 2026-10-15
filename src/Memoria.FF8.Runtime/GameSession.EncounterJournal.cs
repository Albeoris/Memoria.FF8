namespace Memoria.FF8.Runtime;

public sealed partial class GameSession
{
    public void CompactEncounterJournal()
    {
        GameState source = State;
        ReplayBattleEncounter pending = source.PendingBattleEncounter ?? throw new InvalidOperationException("Encounter compaction requires its current observed pending occurrence.");
        ReplayJournal journal = source.ReplayJournal ?? throw new NotSupportedException("Encounter compaction requires an authentic recorded New Game history.");
        if (journal.Endpoint is not { Kind: ReplayEntryKind.BattleEncounter, BattleEncounter: { } occurrence } || occurrence != pending)
        {
            throw new InvalidOperationException("Encounter compaction requires the matching occurrence as the latest committed fact.");
        }
        ReplayJournal next = journal.CompactActiveEncounterHistory();
        if (ReferenceEquals(next, journal))
        {
            return;
        }
        State = source with { ReplayJournal = next };
        ReplayJournalChanged?.Invoke(next);
    }
}
