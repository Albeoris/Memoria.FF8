using System.Reflection;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class RecreateGameSessionLifecycleTests
{
    [Fact]
    public void ReplacingPlayerSessionRebindsSessionOwnersWithoutMergingAbandonedReceipts()
    {
        GameSession abandoned = new(new GameState
        {
            PendingAchievementObservations = [new(new("kills/1"), new("battle-results/abandoned"), 1, BattleOutcome.Victory, BattleRewardPolicy.NativeCombat, 0)],
            PendingOriginalAchievementObservations = [OriginalAchievementObservedEvent.Create(new() { MaximumGil = true }, null)],
            PendingOriginalAchievementActions = [new(new("card-entry"), new("abandoned"), OriginalAchievementActionKind.CardPlayed)]
        });
        using RecreateGame game = new(new LaunchOptions { MuteAudio = true, AchievementsLocalOnly = true }, abandoned);
        MovieClock previousMovieClock = Get<MovieClock>(game, "movieClock");
        previousMovieClock.AdvanceFrame = (_, _) => throw new InvalidOperationException("Abandoned callback must not be reused.");
        AchievementService achievements = Get<AchievementService>(game, "achievements");
        Assert.NotNull(abandoned.CaptureAchievementOwner);
        AutoPlayController autoPlay = new(abandoned, () => throw new InvalidOperationException("World loading is unnecessary for session replacement."), _ =>
        {
        }, Path.GetTempPath());
        Set(game, "autoPlay", autoPlay);
        GameState selectedState = new() { Scene = SceneKind.Field, FieldId = 180, FieldName = "bghoke_2" };
        GameSession selected = new(selectedState);
        GameState selectedBeforeReplacement = selected.State;

        Replace(game, selected);

        Assert.Same(selected, Get<GameSession>(game, "session"));
        Assert.Same(selectedBeforeReplacement, selected.State);
        Assert.Empty(selected.State.PendingAchievementObservations);
        Assert.Empty(selected.State.PendingOriginalAchievementObservations);
        Assert.Empty(selected.State.PendingOriginalAchievementActions);
        Assert.Single(abandoned.State.PendingAchievementObservations);
        Assert.Single(abandoned.State.PendingOriginalAchievementObservations);
        Assert.Single(abandoned.State.PendingOriginalAchievementActions);
        Assert.Null(abandoned.CaptureAchievementOwner);
        Assert.NotNull(selected.CaptureAchievementOwner);
        Assert.Same(achievements, selected.CaptureAchievementOwner!.Target);
        Assert.Same(achievements, Get<AchievementService>(game, "achievements"));
        MovieClock replacementMovieClock = Get<MovieClock>(game, "movieClock");
        Assert.NotSame(previousMovieClock, replacementMovieClock);
        Assert.Same(selected, Get<GameSession>(replacementMovieClock, "session"));
        Assert.Null(replacementMovieClock.AdvanceFrame);
        Assert.Same(selected, Get<GameSession>(autoPlay, "session"));
    }

    [Fact]
    public void ReplacingPlayerSessionResetsHostTimingInputAndPauseWhilePreservingGlobalPreferences()
    {
        using RecreateGame game = new(new LaunchOptions { MuteAudio = true, AchievementsLocalOnly = true }, GameSession.CreateColdProcess());
        GameplayClock previousClock = Get<GameplayClock>(game, "gameplayClock");
        previousClock.ToggleSpeed();
        previousClock.Advance(TimeSpan.FromSeconds(2), SceneKind.Field);
        KeyboardInput previousKeyboard = Get<KeyboardInput>(game, "keyboard");
        ControlsProfile controls = previousKeyboard.Profile.WithControllerSettings(0.35f, 0.3f, 0.7f, vibrationEnabled: false);
        previousKeyboard.Profile = controls;
        previousKeyboard.ControllerGeneration = 7;
        UiPreferences preferences = Get<UiPreferences>(game, "uiPreferences") with { MusicVolume = 31, ShowFps = true };
        Set(game, "uiPreferences", preferences);
        Set(game, "userPaused", true);
        Set(game, "capturingControls", true);

        Replace(game, new GameSession(new GameState()));

        GameplayClock replacementClock = Get<GameplayClock>(game, "gameplayClock");
        Assert.NotSame(previousClock, replacementClock);
        Assert.Equal(1, replacementClock.SpeedMultiplier);
        Assert.Equal(TimeSpan.Zero, replacementClock.Total);
        KeyboardInput replacementKeyboard = Get<KeyboardInput>(game, "keyboard");
        Assert.NotSame(previousKeyboard, replacementKeyboard);
        Assert.Same(controls, replacementKeyboard.Profile);
        Assert.Equal(0, replacementKeyboard.ControllerGeneration);
        Assert.Same(preferences, Get<UiPreferences>(game, "uiPreferences"));
        Assert.False(Get<bool>(game, "userPaused"));
        Assert.False(Get<bool>(game, "capturingControls"));
    }

    private static void Replace(RecreateGame game, GameSession replacement)
    {
        typeof(RecreateGame).GetMethod("ReplacePlayerSession", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(game, [replacement]);
    }

    private static T Get<T>(object owner, string name)
    {
        return (T)owner.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(owner)!;
    }

    private static void Set(RecreateGame game, string name, object value)
    {
        typeof(RecreateGame).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(game, value);
    }
}
