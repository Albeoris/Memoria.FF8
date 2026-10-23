namespace Memoria.FF8.GameUi;

public sealed partial class MenuController
{
    private Func<UiPreferences>? _readPreferences;
    private UiPreferences demoFontPreferences = new();
    private Action<MenuAction>? _writePreferences;
    private bool IsHostSettingsScreen => ScreenId == "debug-battle";
    private bool UseHostControls => !IsFixtureContext && IsControlsScreen;
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
        return [new MenuEntry("battle-laboratory", "Battle laboratory…", "Edit an isolated scenario; Start launches it explicitly.", Action: new("open-battle-debug"), Enabled: BattleDebugAvailable),
            .. HostSettingsEntries("Battle policy"),
            new MenuEntry("instant-victory", "Win current battle", active ? "Immediately win the displayed active battle." : "No active battle is available.", Action: new MenuAction("battle-debug-outcome", Value: "Victory"), Enabled: active),
            new MenuEntry("instant-defeat", "Lose current battle", active ? "Immediately lose the displayed active battle." : "No active battle is available.", Action: new MenuAction("battle-debug-outcome", Value: "Defeat"), Enabled: active)];
    }

    internal IReadOnlyList<string> BattleSettingsDetails() => [battleSource?.Invoke() ?? (_hasActiveBattle?.Invoke() == true ? "Active battle" : "No active battle"), "Encounter execution: " + (HostSettingValue("Battle policy") == "Instant victory" ? "Automatic victory" : "Full combat"), "Applies to the current game; at Title it is the new-game default.", "Save normally to retain current-game changes.", SelectedEntry?.Help ?? ""];
    private Func<string>? battleSource;
    private bool pendingEncounterPolicy;
    private Func<bool>? hasPendingEncounter;
    private Func<bool>? encounterPolicyHasConsequence;
    internal void ConnectEncounterPolicyConsequence(Func<bool> read) => encounterPolicyHasConsequence = read;
    internal void ConnectPendingEncounter(Func<bool> read) => hasPendingEncounter = read;
    internal void ConnectBattleSource(Func<string> read) => battleSource = read;

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
            return new MenuEntry(key, key == "Battle policy" ? "Encounter execution   " + (value == "Instant victory" ? "Automatic victory" : value) : $"{key}   {value}", setting.Description, Action: new MenuAction("host-config", key, Value: setting.Values[next]), Enabled: _writePreferences is not null);
        }).ToArray();
    }

    private string HostSettingValue(string key)
    {
        UiPreferences preferences = FontPreferences;
        return key switch
        {
            "Show FPS" => preferences.ShowFps ? "On" : "Off",
            "Quick saves" => preferences.QuickSaveCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "UI presentation" => preferences.MenuPresentation.ToString(),
            "Text scale" => preferences.TextScale + "%",
            "UI font" => preferences.GetUiFontFamily(Presentation),
            "UI font size" => preferences.UiFontSize.ToString(),
            "Dialogue font" => preferences.DialogueFontFamily,
            "Dialogue font size" => preferences.DialogueFontSize.ToString(),
            "Music volume" => preferences.MusicVolume + "%",
            "Effect volume" => preferences.SoundVolume + "%",
            "Cursor" => preferences.CursorMemory ? "Memory" : "Initial",
            "Field message" => preferences.FieldMessageSpeed.ToString(),
            "Vibration" => CurrentControls.VibrationEnabled ? "On" : "Off",
            "Limit once per turn" => preferences.LimitOncePerTurn ? "On" : "Off",
            "Selphie card Slots" => preferences.SelphieCardSlots ? "On" : "Off",
            "Card Mod once" => preferences.CardModOnce ? "On" : "Off",
            "Weapon magazine required" => preferences.WeaponMagazineRequired ? "On" : "Off",
            "Weapon upgrade order" => preferences.WeaponUpgradeOrder ? "On" : "Off",
            "Draw once per spell" => preferences.DrawOncePerSpell ? "On" : "Off",
            "Difficulty" => preferences.Difficulty.ToString(),
            "Battle ATB mode" => preferences.AtbMode == Memoria.FF8.Runtime.BattleAtbMode.Normal ? "Classic" : "Turn Based",
            "Gunblade trigger" => preferences.GunbladeMode.ToString(),
            "Battle policy" => preferences.BattlePolicyLabel,
            "Rare card markers" => preferences.RareCardMarkers ? "On" : "Off",
            "Random encounters" => preferences.RandomEncounters ? "On" : "Off",
            _ => throw new NotSupportedException($"Unknown local setting '{key}'.")
        };
    }

    private bool ExecuteHostSettingsAction(MenuAction action)
    {
        if (IsDemo && action.Kind == "demo-reset")
        {
            demoFontPreferences = new UiPreferences();
        }
        if (action.Kind == "encounter-policy-confirm")
        {
            if (ScreenId == "debug-policy-confirm" && pendingEncounterPolicy)
            {
                pendingEncounterPolicy = false;
                Back();
                _writePreferences?.Invoke(new("config", "Battle policy", Value: "Instant victory"));
                Message = "";
            }
            return true;
        }
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
            Message = "";
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
        if (action.SubjectId != "Battle policy")
        {
            Message = "This preference has its canonical editor in Config.";
            return true;
        }
        if (action.Value == "Instant victory" && (encounterPolicyHasConsequence?.Invoke() ?? (_hasActiveBattle?.Invoke() == true || hasPendingEncounter?.Invoke() == true)))
        {
            pendingEncounterPolicy = true;
            Push("debug-policy-confirm");
            return true;
        }
        _writePreferences(action with { Kind = "config" });
        Message = "";
        return true;
    }
}
