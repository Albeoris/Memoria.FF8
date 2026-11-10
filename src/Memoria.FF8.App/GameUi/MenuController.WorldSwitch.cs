namespace Memoria.FF8.GameUi;

public sealed partial class MenuController
{
    private Func<string?>? worldSwitchUnavailable;
    private Action? switchWorld;

    public bool WorldSwitchAvailable => worldSwitchUnavailable is not null && worldSwitchUnavailable() is null;
    public string WorldSwitchHelp => worldSwitchUnavailable is null
        ? "World switching is unavailable in this menu."
        : worldSwitchUnavailable() ?? "Continue the FFVII story. Each world retains its own progress.";

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
            SetSystemMessage(() => reason ?? "World switching is unavailable in this menu.");
        }
        else
        {
            switchWorld?.Invoke();
        }
        return true;
    }
}
