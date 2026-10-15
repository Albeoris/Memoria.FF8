namespace Memoria.FF8.GameUi;

public sealed partial class MenuController
{
    private Func<UiPreferences>? _readPreferences;
    private Action<MenuAction>? _writePreferences;
    private bool IsHostSettingsScreen => ScreenId is "debug-ui" or "debug-audio";
    private bool UseHostControls => ScreenId == "debug-controls" || _history.Contains("debug-controls");

    internal void ConnectPreferences(Func<UiPreferences> read, Action<MenuAction> write)
    {
        _readPreferences = read;
        _writePreferences = write;
    }

    internal IReadOnlyList<MenuEntry> HostSettingsEntries(params string[] keys)
    {
        return keys.Select(key =>
        {
            MenuSetting setting = MenuSettings.Definitions.Single(setting => setting.Id == key);
            string value = HostSettingValue(key);
            int next = (Array.IndexOf(setting.Values, value) + 1) % setting.Values.Length;
            return new MenuEntry(key, $"{key}   {value}", setting.Description, Action: new MenuAction("host-config", key, Value: setting.Values[next]), Enabled: _writePreferences is not null);
        }).ToArray();
    }

    private string HostSettingValue(string key)
    {
        UiPreferences preferences = _readPreferences?.Invoke() ?? new UiPreferences();
        return key switch
        {
            "Text scale" => preferences.TextScale + "%",
            "Music volume" => preferences.MusicVolume + "%",
            "Effect volume" => preferences.SoundVolume + "%",
            "Initiative bar" => preferences.ShowInitiative ? "On" : "Off",
            _ => throw new NotSupportedException($"Unknown local setting '{key}'.")
        };
    }

    private bool ExecuteHostSettingsAction(MenuAction action)
    {
        if (action.Kind != "host-config")
        {
            return false;
        }
        if (_writePreferences is null)
        {
            Message = "Local settings are not connected to this menu host.";
            return true;
        }
        _writePreferences(action with { Kind = "config" });
        Message = "Local setting saved.";
        return true;
    }
}
