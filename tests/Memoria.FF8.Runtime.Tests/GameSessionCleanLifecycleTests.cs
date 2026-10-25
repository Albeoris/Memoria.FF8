using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class GameSessionCleanLifecycleTests
{
    [Fact]
    public void NewGameDiscardsAllPreviousGameplayAndPendingAchievementState()
    {
        GameSession expected = GameSession.CreateColdProcess();
        expected.StartNewGame();
        GameSession session = ContaminatedSession("previous");
        session.ShowDialog("Previous dialogue");
        session.RequestMovie(1);
        session.BeginMovieAcceleration();

        session.StartNewGame();

        Assert.Equal(JsonSerializer.Serialize(expected.State), JsonSerializer.Serialize(session.State));
        Assert.Null(session.Dialog);
        session.RequestMovie(1);
        session.BeginMovieAcceleration();
        session.EndMovieAcceleration(null);
    }

    [Fact]
    public void NewGameFromAnUnspecifiedOrRestoredStateUsesColdInitialization()
    {
        GameSession expected = GameSession.CreateColdProcess();
        expected.StartNewGame();
        GameSession session = new(new GameState());

        session.StartNewGame();
        Assert.Equal(JsonSerializer.Serialize(expected.State), JsonSerializer.Serialize(session.State));

        session.Restore(ContaminatedSession("loaded").State);
        session.StartNewGame();
        Assert.Equal(JsonSerializer.Serialize(expected.State), JsonSerializer.Serialize(session.State));
    }

    [Fact]
    public void NewGameUsesExplicitColdMemoryOrTheFactorySeedInsteadOfLiveMemory()
    {
        NativeBattleRetainedMemory seed = NativeBattleRetainedMemory.FromColdProcess() with { EffectIoResult = 17 };
        GameSession session = GameSession.CreateColdProcess(seed);
        session.Restore(ContaminatedSession("loaded").State);

        session.StartNewGame();

        Assert.Same(seed, session.State.NativeBattleMemory);
        NativeBattleRetainedMemory replacement = seed with { EffectIoResult = 23 };
        session.StartNewGame(replacement);
        Assert.Same(replacement, session.State.NativeBattleMemory);
        session.StartNewGame();
        Assert.Same(seed, session.State.NativeBattleMemory);
    }

    [Fact]
    public void RestoreReplacesThePreviousBranchWithOnlyTheSelectedSaveAndClearsMovieAcceleration()
    {
        GameSession session = ContaminatedSession("abandoned");
        GameState selected = ContaminatedSession("selected").State;
        session.ShowDialog("Previous dialogue");
        session.RequestMovie(1);
        session.BeginMovieAcceleration();

        session.Restore(selected);

        Assert.Equal(JsonSerializer.Serialize(selected), JsonSerializer.Serialize(session.State));
        Assert.Equal("selected", Assert.Single(session.State.PendingAchievementFacts).ContextId.Value);
        Assert.Equal("selected", Assert.Single(session.State.PendingAchievementObservations).ContextId.Value);
        Assert.Equal("selected", Assert.Single(session.State.PendingOriginalAchievementActions).ContextId.Value);
        Assert.Null(session.Dialog);
        session.RequestMovie(1);
        session.BeginMovieAcceleration();
        session.EndMovieAcceleration(null);
        session.CompleteMovie();
        Assert.Equal(selected.Scene, session.State.Scene);
    }

    private static GameSession ContaminatedSession(string context)
    {
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        session.SetExamTimer(session.ExamTimer!.AdvanceCallbacks(NativeTimerCallbackKind.NormalField, 30));
        NativeEventRandomState random = session.EventRandom!.Draw(out _);
        EnemyDefeatedEvent fact = EnemyDefeatedEvent.FromBattle(context, new BattleEnemyDefeat(0, 1, BattleDefeatCredit.Character, 0, null, false, -1) { AllocationId = "defeat", NativeCommand = 1 });
        fact = fact with { Facts = fact.Facts with { AcceptedOutcome = BattleOutcome.Victory, ResultPresentationEligible = true } };
        return new GameSession(session.State with
        {
            X = 101,
            Y = 202,
            NativeFieldDebugRequested = true,
            NativeBattleMemory = NativeBattleRetainedMemory.FromColdProcess() with { EffectIoResult = -1 },
            CrtRandom = session.State.CrtRandom!.Draw(out _),
            MenuRandom = session.State.MenuRandom!.Advance(5),
            EventRandom = random,
            FieldRequestPacket = new() { Kind = 5, FieldId = 12, ExtraWord = 99 },
            SeeD = NativeSeeDState.CreateNewGame() with { RankPoints = 3100, SalaryMovementCounter = 1234 },
            PendingAchievementFacts = [fact],
            PendingAchievementObservations = [new(new("kills"), new(context), 500, BattleOutcome.Victory, BattleRewardPolicy.NativeCombat, 0)],
            PendingOriginalAchievementObservations = [OriginalAchievementObservedEvent.Create(new() { CardWins = context == "selected" ? (ushort)2 : (ushort)7 }, null)],
            PendingOriginalAchievementActions = [new(new("action"), new(context), OriginalAchievementActionKind.CardWin)],
            OriginalAchievementMutationContext = Guid.NewGuid().ToString("N"),
            OriginalAchievementMutationSequence = 7
        });
    }
}
