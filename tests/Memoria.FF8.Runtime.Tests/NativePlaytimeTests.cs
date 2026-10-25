namespace Memoria.FF8.Runtime.Tests;

public sealed class NativePlaytimeTests
{
    [Fact]
    public void Native_playtime_uses_its_own_fraction_and_preserves_it_when_New_Game_clears_saved_seconds()
    {
        NativePlaytimeState before = NativePlaytimeState.FromColdProcess().AdvanceTo(59);
        NativePlaytimeState after = before.AdvanceTo(60);

        Assert.Equal(0u, before.Seconds);
        Assert.Equal(129269u, before.Phase);
        Assert.Equal(1u, after.Seconds);
        Assert.Equal(388u, after.Phase);
        Assert.Equal(new NativePlaytimeState(0, 388, 60), after.ClearSavedStateForNewGame());
        Assert.Equal(0u, new NativePlaytimeState(uint.MaxValue, 131071, 0).AdvanceTo(1).Seconds);
        Assert.Throws<InvalidDataException>(() => after.AdvanceTo(59));
    }

    [Fact]
    public void Session_uses_only_admitted_timer_calls_restore_preserves_owners_and_New_Game_resets_them()
    {
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        session.SetExamTimer(session.ExamTimer!.AdvanceCallbacks(NativeTimerCallbackKind.NormalField, 30));
        NativePlaytimeState playtime = session.State.Playtime!;

        Assert.Equal(new NativePlaytimeState(1, 388, 60), playtime);
        session.SetExamTimer(session.ExamTimer!.AdvanceCallbacks(NativeTimerCallbackKind.Battle, 100, paused: true));
        Assert.Equal(playtime, session.State.Playtime);
        GameSession restored = new(session.State);
        restored.SetExamTimer(restored.ExamTimer!.AdvanceCallbacks(NativeTimerCallbackKind.Battle, 15));
        Assert.Equal(playtime.AdvanceTo(120), restored.State.Playtime);
        session.Restore(restored.State);
        Assert.Equal(restored.State.Playtime, session.State.Playtime);
        Assert.Equal(new NativeBattleUiRandomState(1), playtime.CreateBattleUiRandom());
        session.StartNewGame();
        Assert.Equal(NativePlaytimeState.FromColdProcess(), session.State.Playtime);
        Assert.Equal(0, session.ExamTimer!.ClockCalls);
    }

    [Fact]
    public void Old_unknown_playtime_remains_unknown_and_inconsistent_owned_call_counts_are_rejected()
    {
        GameSession legacy = new(new GameState { ExamTimer = NativeExamTimerState.FromColdProcess() });
        legacy.SetExamTimer(legacy.ExamTimer!.AdvanceCallbacks(NativeTimerCallbackKind.NormalField, 30));

        Assert.Null(legacy.State.Playtime);
        Assert.Throws<InvalidDataException>(() => new GameSession(legacy.State with { Playtime = NativePlaytimeState.FromColdProcess() }));
    }
}
