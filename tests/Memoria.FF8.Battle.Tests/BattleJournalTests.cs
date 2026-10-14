using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Battle.Tests;

public sealed class BattleJournalTests
{
    [Fact]
    public void IdleFieldBattleTimingRunRetainsEveryDispatchInPortableCapsule()
    {
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        for (int frame = 0; frame < 120; frame++)
        {
            session.RecordReplayEntry(Tick(1));
        }
        ReplayJournalEntry run = Assert.Single(session.ReplayJournal!.Entries);
        Assert.Equal(120, run.BattleInput!.Frames);
        Assert.Equal(120, run.Advanced);
        GameState restored = JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State))!;
        ReplayJournalEntry portable = Assert.Single(restored.ReplayJournal!.Entries);
        Assert.Equal(run.BattleInput, portable.BattleInput);
        Assert.Equal(120, portable.Advanced);
        Assert.Equal(19, portable.Operation);
    }

    [Fact]
    public void PlayerCommandModeAndTimingBoundFenceBattleCadence()
    {
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        session.RecordReplayEntry(Tick(2));
        session.RecordReplayEntry(Tick(2) with { BattleInput = new BattleInput(BattleInputKind.Attack, ActorId: "party-0", TargetId: "enemy-0"), Advanced = 0 });
        session.RecordReplayEntry(Tick(3));
        session.RecordReplayEntry(Tick(3) with { BattleInput = new BattleInput(BattleInputKind.SetMode, Mode: BattleAtbMode.TurnBased), Advanced = 0 });
        session.RecordReplayEntry(Tick(30000));
        session.RecordReplayEntry(Tick(1));
        Assert.Equal(6, session.ReplayJournal!.EntryCount);
        Assert.Equal(new[] { BattleInputKind.Tick, BattleInputKind.Attack, BattleInputKind.Tick, BattleInputKind.SetMode, BattleInputKind.Tick, BattleInputKind.Tick }, session.ReplayJournal.Entries.Select(entry => entry.BattleInput!.Kind));
        Assert.Equal(30000, session.ReplayJournal.Entries[^2].BattleInput!.Frames);
        Assert.Equal(1, session.ReplayJournal.Entries[^1].BattleInput!.Frames);
    }

    private static ReplayJournalEntry Tick(int frames)
    {
        return new ReplayJournalEntry(ReplayEntryKind.BattleInput, SceneKind.Field, 74, 10) { BattleInput = new BattleInput(BattleInputKind.Tick, frames), Advanced = frames, Trigger = 9, ResultTrigger = 9, ResultFrame = 10, ResultScene = SceneKind.Field, ResultFieldId = 74, ObjectId = 2, ScriptId = 8, Operation = 19 };
    }
}
