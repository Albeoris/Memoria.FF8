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
    private Func<bool>? hasOutcomeContext;
    private Func<bool>? canCompleteOutcome;

    internal void ConnectInstantOutcomes(Func<bool> hasContext, Func<bool> canComplete, Action<bool> complete)
    {
        hasOutcomeContext = hasContext;
        canCompleteOutcome = canComplete;
        _completeActiveBattle = complete;
    }

    internal IReadOnlyList<MenuEntry> InstantOutcomeEntries()
    {
        if (!(hasOutcomeContext?.Invoke() ?? _hasActiveBattle?.Invoke() ?? false))
        {
            return [];
        }
        bool enabled = canCompleteOutcome?.Invoke() ?? _hasActiveBattle?.Invoke() == true;
        return [new("instant-victory", SystemText("ui.instantoutcomeentries.entry.label.instant.victory", "Instant victory"), SystemText("ui.instantoutcomeentries.entry.help.win.the.current.battle.or.triple.triad.match", "Win the current battle or Triple Triad match. Available only on your turn."), Action: new("battle-debug-outcome", Value: "Victory"), Enabled: enabled),
            new("instant-defeat", SystemText("ui.instantoutcomeentries.entry.label.instant.defeat", "Instant defeat"), SystemText("ui.instantoutcomeentries.entry.help.lose.the.current.battle.or.triple.triad.match", "Lose the current battle or Triple Triad match. Available only on your turn."), Action: new("battle-debug-outcome", Value: "Defeat"), Enabled: enabled)];
    }

    internal void ConnectBattleSettings(Func<bool> hasActiveBattle, Action<bool> completeActiveBattle)
    {
        _hasActiveBattle = hasActiveBattle;
        _completeActiveBattle = completeActiveBattle;
    }

    internal IReadOnlyList<MenuEntry> BattleSettingsEntries()
    {
        return [new MenuEntry("battle-laboratory", SystemText("ui.battlesettingsentries.entry.label.battle.laboratory", "Battle laboratory…"), SystemText("ui.battlesettingsentries.entry.help.edit.an.isolated.scenario.start.launches.it.explicitly", "Edit an isolated scenario; Start launches it explicitly."), Action: new("open-battle-debug"), Enabled: BattleDebugAvailable),
            .. HostSettingsEntries("Battle policy", "Random encounters")];
    }

    internal IReadOnlyList<string> BattleSettingsDetails() => [battleSource?.Invoke() ?? (_hasActiveBattle?.Invoke() == true ? SystemText("ui.battlesettingsdetails.details.active.battle", "Active battle") : SystemText("ui.battlesettingsdetails.details.no.active.battle", "No active battle")), FormatSystemText("ui.battlesettingsdetails.details.encounter.execution.template", "Encounter execution: {0}", (HostSettingValue("Battle policy") == "Instant victory" ? SystemText("ui.battlesettingsdetails.details.automatic.victory", "Automatic victory") : SystemText("ui.battlesettingsdetails.details.full.combat", "Full combat"))), SystemText("ui.battlesettingsdetails.details.applies.to.the.current.game.at.title.it", "Applies to the current game; at Title it is the new-game default."), SystemText("ui.battlesettingsdetails.details.save.normally.to.retain.current.game.changes", "Save normally to retain current-game changes."), SelectedEntry?.Help ?? ""];
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
            return new MenuEntry(key, key == "Battle policy" ? FormatSystemText("ui.hostsettingsentries.entry.label.encounter.execution.template", "Encounter execution   {0}", (value == "Instant victory" ? SystemText("ui.hostsettingsentries.entry.label.automatic.victory", "Automatic victory") : value)) : $"{key}   {value}", setting.Description, Action: new MenuAction("host-config", key, Value: setting.Values[next]), Enabled: _writePreferences is not null);
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
            "Voice volume" => preferences.VoiceVolume + "%",
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
            "Triple Triad width" => preferences.TripleTriad.Width.ToString(),
            "Triple Triad height" => preferences.TripleTriad.Height.ToString(),
            "Triple Triad hand size" => preferences.TripleTriad.HandSize.ToString(),
            "Triple Triad Random redraws" => preferences.TripleTriad.RandomRedraws.ToString(),
            "Triple Triad assistance" => preferences.TripleTriad.Assistance.ToString(),
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
            if (_completeActiveBattle is null || !(canCompleteOutcome?.Invoke() ?? _hasActiveBattle?.Invoke() == true))
            {
                SetSystemMessage(() => SystemText("ui.executehostsettingsaction.message.instant.outcomes.are.available.only.on.your.turn", "Instant outcomes are available only on your turn in battle or Triple Triad."));
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
            SetSystemMessage(() => SystemText("ui.executehostsettingsaction.message.local.settings.are.not.connected.to.this.menu", "Local settings are not connected to this menu host."));
            return true;
        }
        if (action.SubjectId is not ("Battle policy" or "Random encounters"))
        {
            SetSystemMessage(() => SystemText("ui.executehostsettingsaction.message.this.preference.has.its.canonical.editor.in.config", "This preference has its canonical editor in Config."));
            return true;
        }
        if (action.SubjectId == "Battle policy" && action.Value == "Instant victory" && (encounterPolicyHasConsequence?.Invoke() ?? (_hasActiveBattle?.Invoke() == true || hasPendingEncounter?.Invoke() == true)))
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
