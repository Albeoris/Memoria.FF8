using Memoria.FF8.GameUi;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlayMenuTests
{
    [Theory]
    [InlineData("start")]
    [InlineData("load-reproduction")]
    public void ReplacingLiveGameRequiresExplicitConfirmationAndCancelPreservesIt(string command)
    {
        AutoPlayMenuState state = new(false, false, "Ready", ReproductionPath: "reproduction.json", HasLiveGame: true);
        List<string> commands = [];
        MenuController menus = new();
        menus.ConnectAutoPlay(() => state, commands.Add);
        menus.Open("debug-autoplay", true);
        menus.Execute(new MenuAction("autoplay", Value: command));
        Assert.Equal("autoplay-replace-confirm", menus.ScreenId);
        Assert.Empty(commands);
        menus.Navigate(MenuInput.Cancel);
        Assert.Equal("debug-autoplay", menus.ScreenId);
        Assert.Empty(commands);
        menus.Execute(new MenuAction("autoplay-confirm", Value: command));
        Assert.Empty(commands);
        menus.Execute(new MenuAction("autoplay", Value: command));
        menus.Execute(new MenuAction("autoplay-confirm", Value: command));
        Assert.Equal(new[] { command }, commands);
    }

    [Fact]
    public void AutomaticPlayActionsFollowHostStateAndCannotExecuteUnavailableCommands()
    {
        AutoPlayMenuState state = new(false, false, "Ready");
        List<string> commands = [];
        MenuController menus = new();
        menus.ConnectAutoPlay(() => state, commands.Add);
        menus.Open("debug-autoplay", true);
        Assert.True(menus.Entries.Single(entry => entry.Id == "autoplay-start").Enabled);
        menus.Execute(new MenuAction("autoplay", Value: "pause"));
        Assert.Empty(commands);
        menus.Execute(new MenuAction("autoplay", Value: "start"));
        Assert.Equal(new[] { "start" }, commands);
        state = new(true, false, "Running");
        Assert.False(menus.Entries.Single(entry => entry.Id == "autoplay-start").Enabled);
        menus.Execute(new MenuAction("autoplay", Value: "pause"));
        state = state with { Paused = true };
        Assert.Equal("Resume", menus.Entries.Single(entry => entry.Id == "autoplay-pause").Label);
        menus.Execute(new MenuAction("autoplay", Value: "resume"));
        menus.Execute(new MenuAction("autoplay", Value: "stop"));
        Assert.Equal(new[] { "start", "pause", "resume", "stop" }, commands);
    }

    [Fact]
    public void FailureDetailsRefreshAndSavedReproductionCanBeLoaded()
    {
        AutoPlayMenuState state = new(true, false, "Running");
        string? command = null;
        MenuController menus = new();
        menus.ConnectAutoPlay(() => state, value => command = value);
        menus.Open("debug-autoplay", true);
        var runningView = menus.BuildView();
        state = new(false, false, "Stopped", "Unsupported operation", "reproduction.json", "Memoria.FF8 --state reproduction.json");
        Assert.NotSame(runningView, menus.BuildView());
        Assert.Contains("Error: Unsupported operation", menus.CurrentScreen.Details(menus));
        Assert.Contains("Reproduction: reproduction.json", menus.CurrentScreen.Details(menus));
        Assert.Contains("Resume: Memoria.FF8 --state reproduction.json", menus.CurrentScreen.Details(menus));
        menus.Execute(new MenuAction("autoplay", Value: "load-reproduction"));
        Assert.Equal("load-reproduction", command);
    }

    [Fact]
    public void UnconnectedHostDisablesAutomaticPlay()
    {
        MenuController menus = new();
        menus.Open("debug-room", true);
        Assert.False(menus.Entries.Single(entry => entry.Id == "debug-autoplay").Enabled);
        menus.Open("debug-autoplay", true);
        Assert.All(menus.Entries, entry => Assert.False(entry.Enabled));
    }
}
