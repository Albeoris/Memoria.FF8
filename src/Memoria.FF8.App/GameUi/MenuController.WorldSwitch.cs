namespace Memoria.FF8.GameUi;

public sealed partial class MenuController
{
    private Func<string?>? worldSwitchUnavailable;
    private Action? switchWorld;

    public bool WorldSwitchAvailable => worldSwitchUnavailable is not null && worldSwitchUnavailable() is null;
    public string WorldSwitchHelp => worldSwitchUnavailable?.Invoke() ?? "Continue the FFVII story; each world retains its own progress.";

    public void ConnectWorldSwitch(Func<string?> unavailable, Action open)
    {
        worldSwitchUnavailable = unavailable;
        switchWorld = open;
        _view = null;
    }

    private bool ExecuteWorldSwitchAction(MenuAction action)
    {
        if (action.Kind != "switch-world")
        {
            return false;
        }
        string? reason = worldSwitchUnavailable?.Invoke();
        if (worldSwitchUnavailable is null || reason is not null)
        {
            SetSystemMessage(() => reason ?? "Configure the FFVII installation before switching worlds.");
        }
        else
        {
            switchWorld?.Invoke();
        }
        return true;
    }
}
