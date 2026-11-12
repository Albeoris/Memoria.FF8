using System.Reflection;
using Memoria.FF8.Runtime;
using Microsoft.Xna.Framework;

namespace Memoria.FF8.App.Tests;

public sealed class FocusPauseTests
{
    [Fact]
    public void Hidden_world_transfer_can_draw_and_activation_keeps_movies_paused()
    {
        using RecreateGame game = CreateGame();
        WorldTransferPresentation transfer = new(null!, incoming: true, NoOperation, NoOperation, NoOperation, NoOperation);
        FieldInfo field = typeof(RecreateGame).GetField("worldTransfer", BindingFlags.Instance | BindingFlags.NonPublic)!;
        field.SetValue(game, transfer);
        try
        {
            game.SetWindowActive(false);
            Assert.True((bool)Invoke(game, "BeginDraw")!);
            game.SetWindowActive(true);
            Assert.True(Get<bool>(game, "uiMovieSuspended"));
        }
        finally
        {
            field.SetValue(game, null);
        }
    }

    private static void NoOperation()
    {
    }

    [Fact]
    public void Closing_the_window_cannot_resume_disposed_outputs()
    {
        RecreateGame game = CreateGame();
        game.SetWindowActive(false);
        game.Dispose();
        game.SetWindowActive(true);
        game.SetWindowActive(false);
    }

    [Fact]
    public void Initial_window_activity_is_sampled_before_the_first_update()
    {
        using RecreateGame game = CreateGame();
        Assert.False(Get<bool>(game, "windowActivityInitialized"));
        bool paused = (bool)Invoke(game, "UpdateFocusPause")!;
        Assert.True(Get<bool>(game, "windowActivityInitialized"));
        Assert.Equal(!game.IsActive, paused);
        Assert.Equal(!game.IsActive, Get<bool>(game, "focusPaused"));
        Assert.Equal(0, Get<int>(game, "updateFrames"));
    }

    [Fact]
    public void Windows_background_window_ignores_input_even_when_monogame_reports_active()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Requires the Windows foreground window contract.");
        using RecreateGame game = CreateGame();
        object platform = typeof(Game).GetField("Platform", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(game)!;
        platform.GetType().GetProperty(nameof(Game.IsActive))!.SetValue(platform, true);

        Assert.True(game.IsActive);
        Assert.False(Get<bool>(game, "focusPaused"));
        Assert.False(Get<HostPhysicalInput>(game, "physicalInput").CanRead);
    }

    [Fact]
    public void Background_host_does_not_update_or_draw_and_resume_discards_elapsed_time()
    {
        using RecreateGame game = CreateGame();
        PresentationTiming timing = Get<PresentationTiming>(game, "presentationTiming");
        timing.Accumulate(PresentationTiming.SimulationStep);
        Assert.True(timing.TryStep());
        TimeSpan initial = timing.SimulationTotal;
        timing.Accumulate(TimeSpan.FromMilliseconds(200));

        game.SetWindowActive(false);
        for (int frame = 0; frame < 3; frame++)
        {
            Invoke(game, "Update", new GameTime(TimeSpan.FromHours(1), TimeSpan.FromSeconds(30)));
            Assert.False((bool)Invoke(game, "BeginDraw")!);
        }
        Assert.Equal(0, Get<int>(game, "updateFrames"));
        Assert.Equal(initial, timing.SimulationTotal);
        Assert.False(timing.TryStep());

        game.SetWindowActive(true);
        Invoke(game, "Update", new GameTime(TimeSpan.FromHours(1), TimeSpan.FromSeconds(30)));
        Assert.Equal(0, Get<int>(game, "updateFrames"));
        Assert.Equal(initial, timing.SimulationTotal);
        Assert.False(timing.TryStep());
        timing.Accumulate(PresentationTiming.SimulationStep);
        Assert.True(timing.TryStep());
        Assert.False(timing.TryStep());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Focus_pause_is_idempotent_and_does_not_clear_manual_pause(bool manuallyPaused)
    {
        using RecreateGame game = CreateGame();
        typeof(RecreateGame).GetField("userPaused", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(game, manuallyPaused);
        game.SetWindowActive(false);
        game.SetWindowActive(false);
        Assert.True(Get<bool>(game, "uiMovieSuspended"));
        game.SetWindowActive(true);
        game.SetWindowActive(true);
        Assert.Equal(manuallyPaused, Get<bool>(game, "userPaused"));
        Assert.Equal(manuallyPaused, Get<bool>(game, "uiMovieSuspended"));
    }

    [Fact]
    public void Explicit_control_host_can_continue_without_window_focus()
    {
        using RecreateGame game = CreateGame(controlDirectory: "controlled-host");
        PresentationTiming timing = Get<PresentationTiming>(game, "presentationTiming");
        timing.Accumulate(PresentationTiming.SimulationStep);
        game.SetWindowActive(false);
        game.SetWindowActive(true);
        game.SetWindowActive(false);
        Assert.False(Get<bool>(game, "focusPaused"));
        Assert.False((bool)Invoke(game, "UpdateFocusPause")!);
        Assert.False(Get<bool>(game, "discardFocusElapsed"));
        Assert.Equal(TimeSpan.Zero, game.InactiveSleepTime);
        Assert.True(timing.TryStep());
    }

    private static RecreateGame CreateGame(string? controlDirectory = null)
    {
        LaunchOptions options = new() { MuteAudio = true, ControlDirectory = controlDirectory, GameDirectory = AppContext.BaseDirectory, SettingsDirectory = AppContext.BaseDirectory };
        return new RecreateGame(options, new GameSession(new GameState()));
    }

    private static T Get<T>(RecreateGame game, string name)
    {
        return (T)typeof(RecreateGame).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(game)!;
    }

    private static object? Invoke(RecreateGame game, string name, params object[] arguments)
    {
        return typeof(RecreateGame).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(game, arguments);
    }
}
