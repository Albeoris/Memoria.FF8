namespace Memoria.FF8.GameUi;

internal sealed record AutoPlayMenuState(bool Running, bool Paused, string Status, string? Error = null, string? ReproductionPath = null, string? ResumeCommand = null);

public sealed partial class MenuController
{
    private Func<AutoPlayMenuState>? _readAutoPlay;
    private Action<string>? _autoPlayCommand;
    private AutoPlayMenuState? _presentedAutoPlay;

    internal void ConnectAutoPlay(Func<AutoPlayMenuState> read, Action<string> command)
    {
        _readAutoPlay = read;
        _autoPlayCommand = command;
        _view = null;
    }

    internal bool AutoPlayAvailable => _autoPlayCommand is not null;

    private AutoPlayMenuState CurrentAutoPlay => _readAutoPlay?.Invoke() ?? new AutoPlayMenuState(false, false, "Automatic play is not connected to this menu host.");

    internal IReadOnlyList<MenuEntry> AutoPlayEntries()
    {
        AutoPlayMenuState state = CurrentAutoPlay;
        return [
            new MenuEntry("autoplay-start", "Start a new game", "Start New Game with seed 1. Replaces the current session.", Action: new MenuAction("autoplay", Value: "start"), Enabled: AutoPlayAvailable && !state.Running),
            new MenuEntry("autoplay-pause", state.Paused ? "Resume" : "Pause", "Pause automatic play or continue the paused run.", Action: new MenuAction("autoplay", Value: state.Paused ? "resume" : "pause"), Enabled: AutoPlayAvailable && state.Running),
            new MenuEntry("autoplay-stop", "Stop", "Stop automatic play and return control to the player.", Action: new MenuAction("autoplay", Value: "stop"), Enabled: AutoPlayAvailable && state.Running),
            new MenuEntry("autoplay-load", "Replay saved reproduction", "Restore the standalone snapshot and automatic decisions saved immediately before the failure, then repeat the failing action.", Action: new MenuAction("autoplay", Value: "load-reproduction"), Enabled: AutoPlayAvailable && !state.Running && state.ReproductionPath is not null)
        ];
    }

    internal IReadOnlyList<string> AutoPlayDetails()
    {
        AutoPlayMenuState state = CurrentAutoPlay;
        List<string> details = [state.Status, "Automatic play advances without external input.", "Dialogues advance quickly; movies are skipped."];
        if (state.Error is not null)
        {
            details.Add("Error: " + state.Error);
        }
        if (state.ReproductionPath is not null)
        {
            details.Add("Reproduction: " + state.ReproductionPath);
        }
        if (state.ResumeCommand is not null)
        {
            details.Add("Resume: " + state.ResumeCommand);
        }
        return details;
    }

    private void RefreshAutoPlayView()
    {
        if (ScreenId == "debug-autoplay" && _presentedAutoPlay != CurrentAutoPlay)
        {
            _presentedAutoPlay = CurrentAutoPlay;
            _view = null;
        }
    }

    private bool ExecuteAutoPlayAction(MenuAction action)
    {
        if (action.Kind != "autoplay")
        {
            return false;
        }
        MenuEntry? entry = AutoPlayEntries().FirstOrDefault(entry => entry.Action?.Value == action.Value);
        if (entry?.Enabled != true)
        {
            Message = "This automatic play action is unavailable in the current state.";
            return true;
        }
        _autoPlayCommand!(action.Value);
        _presentedAutoPlay = null;
        return true;
    }
}
