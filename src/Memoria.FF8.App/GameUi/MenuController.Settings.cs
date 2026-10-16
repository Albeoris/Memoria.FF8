namespace Memoria.FF8.GameUi;

public sealed partial class MenuController
{
    private Func<UiPreferences>? _readPreferences;
    private Action<MenuAction>? _writePreferences;
    private bool IsHostSettingsScreen => ScreenId is "debug-ui" or "debug-audio" or "debug-battle";
    private bool UseHostControls => ScreenId == "debug-controls" || _history.Contains("debug-controls");
    private Func<bool>? _hasActiveBattle;
    private Action<bool>? _completeActiveBattle;

    internal void ConnectBattleSettings(Func<bool> hasActiveBattle, Action<bool> completeActiveBattle)
    {
        _hasActiveBattle = hasActiveBattle;
        _completeActiveBattle = completeActiveBattle;
    }

    internal IReadOnlyList<MenuEntry> BattleSettingsEntries()
    {
        bool active = _hasActiveBattle?.Invoke() == true;
        return [.. HostSettingsEntries("Battle policy", "Random encounters", "Battle ATB mode", "Gunblade trigger"),
            new MenuEntry("instant-victory", "Instant victory", "Immediately defeat all enemies in the current game battle or laboratory battle.", Action: new MenuAction("battle-debug-outcome", Value: "Victory"), Enabled: active),
            new MenuEntry("instant-defeat", "Instant defeat", "Immediately defeat the party in the current game battle or laboratory battle.", Action: new MenuAction("battle-debug-outcome", Value: "Defeat"), Enabled: active)];
    }

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
            "UI font" => preferences.UiFontFamily,
            "UI font size" => preferences.UiFontSize.ToString(),
            "Dialogue font" => preferences.DialogueFontFamily,
            "Dialogue font size" => preferences.DialogueFontSize.ToString(),
            "Music volume" => preferences.MusicVolume + "%",
            "Effect volume" => preferences.SoundVolume + "%",
            "Cursor" => preferences.CursorMemory ? "Memory" : "Initial",
            "Field message" => preferences.FieldMessageSpeed.ToString(),
            "Vibration" => CurrentControls.VibrationEnabled ? "On" : "Off",
            "Battle ATB mode" => preferences.AtbMode.ToString(),
            "Gunblade trigger" => preferences.GunbladeMode.ToString(),
            "Battle policy" => preferences.BattlePolicyLabel,
            "Rare card markers" => preferences.RareCardMarkers ? "On" : "Off",
            "Random encounters" => preferences.RandomEncounters ? "On" : "Off",
            _ => throw new NotSupportedException($"Unknown local setting '{key}'.")
        };
    }

    private bool ExecuteHostSettingsAction(MenuAction action)
    {
        if (action.Kind == "battle-debug-outcome")
        {
            if (_completeActiveBattle is null || _hasActiveBattle?.Invoke() != true)
            {
                Message = "An active game battle or laboratory battle is required.";
                return true;
            }
            if (action.Value is not ("Victory" or "Defeat"))
            {
                throw new ArgumentException("Unsupported debug battle outcome.");
            }
            _completeActiveBattle(action.Value == "Victory");
            Message = "Battle ended with " + action.Value.ToLowerInvariant() + ".";
            return true;
        }
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
