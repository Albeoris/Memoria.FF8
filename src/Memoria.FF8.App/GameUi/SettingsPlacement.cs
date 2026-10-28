namespace Memoria.FF8.GameUi;

internal enum SettingsAudience
{
    Player,
    Developer,
    Fixture,
    Excluded
}
internal sealed record SettingsPage(string Label, string Help, string[] Items);
internal sealed record SettingPlacement(string Identity, string LegacyKey, SettingsAudience Audience, string Page, string Group, int Order, string Target, string Scope, string Kind = "scalar")
{
    internal string DefaultSource => Target.StartsWith("ControlsProfile", StringComparison.Ordinal) ? "ControlsProfile.CreateDefault" : Audience is SettingsAudience.Fixture or SettingsAudience.Excluded ? "MenuSettings fixture" : "UiPreferences / GameplayPreferences defaults";
    internal string DomainSource => "MenuSettings.Definitions:" + LegacyKey;
    internal string CommandOwner => LegacyKey == "Quick saves" ? "PlayerSaveStore.CommitQuickSaveCount" : LegacyKey == "Vibration" ? "ControlsProfile" : "Existing config command provider";
    internal string Application => LegacyKey == "Graphics backend" ? "Requires restart; applies on next start" : LegacyKey == "Quick saves" ? "Staged; persist then prune after exact-count review" : Scope.StartsWith("Game", StringComparison.Ordinal) ? "Session; normal game save; owner checks may defer/reject" : "Immediate global preference";
}

/// <summary>Explicit placement adapts legacy command keys without changing persistence owners.</summary>
internal static class SettingsPlacement
{
    internal static readonly IReadOnlyDictionary<string, SettingsPage> Pages = new Dictionary<string, SettingsPage>(StringComparer.Ordinal)
    {
        ["config-graphics"] = new("Graphics", "Presentation preferences shared across saves.",
            ["Anti-aliasing", "Graphics backend", "Window mode", "V-Sync", "config-frame-limits", "Show FPS"]),
        ["config-frame-limits"] = new("Frame rate limits", "Presentation caps; simulation speed is unchanged. V-Sync can limit output further.",
            ["Field FPS", "World FPS", "Battle FPS", "Menu FPS"]),
        ["config-audio"] = new("Audio", "Output levels shared across saves.",
            ["Music volume", "Effect volume", "Voice volume"]),
        ["config-controls"] = new("Controls", "Player bindings and controller preferences.",
            ["config-keyboard", "config-controller", "config-controller-settings"]),
        ["config-interface"] = new("Interface", "Interface style, fonts and battle cursor memory.",
            ["UI presentation", "config-text", "Cursor"]),
        ["config-text"] = new("Text and fonts", "Menus: design pixels multiplied by text scale. Dialogue: field pixels.",
            ["Text scale", "UI font", "UI font size", "Dialogue font", "Dialogue font size", "Field message"]),
        ["config-gameplay"] = new("Gameplay", "Game rules apply to the current game; save normally to retain them. Title edits new-game defaults or session choices.",
            ["config-combat", "config-magic", "config-exploration", "config-chocobo"]),
        ["config-chocobo"] = new("Chocobo World", "Choose a reward provider; acquired rewards remain recorded when switching modes.", ["Chocobo World"]),
        ["config-combat"] = new("Combat", "Rules for this game; Title sets new-game defaults.",
            ["Difficulty", "Battle ATB mode", "Limit once per turn", "Selphie card Slots", "Gunblade trigger"]),
        ["config-magic"] = new("Magic and Guardian Forces", "Changes can require review or remain pending until a safe boundary. Save the game to retain game rules.",
            ["Draw once per spell", "Shared magic stock", "Infinite magic", "Smart Attack", "GF Transformations"]),
        ["config-triple-triad"] = new("Triple Triad", "Rules apply to this game; Title sets new-game defaults. Rare-card markers are shared across saves.",
            [.. UiPreferences.TripleTriadSettingKeys, "Rare card markers"]),
        ["config-exploration"] = new("Exploration and saving", "Exploration item rules and rotating quick-save retention.",
            ["Card Mod once", "Weapon magazine required", "Weapon upgrade order", "Quick saves"])
    };

    internal static readonly IReadOnlyList<SettingPlacement> All = Create();

    private static IReadOnlyList<SettingPlacement> Create()
    {
        Dictionary<string, string> identities = new(StringComparer.Ordinal)
        {
            ["Show FPS"] = "frame-counter",
            ["Window mode"] = "window-mode",
            ["V-Sync"] = "vsync",
            ["Graphics backend"] = "graphics-backend",
            ["Anti-aliasing"] = "msaa",
            ["Field FPS"] = "field-cap",
            ["World FPS"] = "world-cap",
            ["Battle FPS"] = "battle-cap",
            ["Menu FPS"] = "menu-cap",
            ["Sound"] = "native-sound",
            ["Controller"] = "native-controller",
            ["Quick saves"] = "quick-save-retention",
            ["Cursor"] = "cursor-memory",
            ["ATB"] = "native-atb",
            ["Limit once per turn"] = "limit-once-per-turn",
            ["Selphie card Slots"] = "selphie-card-slots",
            ["Card Mod once"] = "card-mod-once",
            ["Weapon magazine required"] = "weapon-magazine-required",
            ["Weapon upgrade order"] = "weapon-upgrade-order",
            ["Draw once per spell"] = "draw-once-per-spell",
            ["Chocobo World"] = "chocobo-world",
            ["Difficulty"] = "difficulty",
            ["Battle ATB mode"] = "atb-mode",
            ["Shared magic stock"] = "shared-magic",
            ["Infinite magic"] = "infinite-magic",
            ["Smart Attack"] = "smart-attack",
            ["GF Transformations"] = "guardian-forms",
            ["Gunblade trigger"] = "gunblade",
            ["Battle policy"] = "encounter-execution",
            ["Triple Triad width"] = "triple-triad-width",
            ["Triple Triad height"] = "triple-triad-height",
            ["Triple Triad blocked cells"] = "triple-triad-blocked-cells",
            ["Triple Triad hand size"] = "triple-triad-hand-size",
            ["Triple Triad Random redraws"] = "triple-triad-random-redraws",
            ["Triple Triad assistance"] = "triple-triad-assistance",
            ["Triple Triad modified Same/Plus"] = "triple-triad-modified-same-plus",
            ["Rare card markers"] = "rare-card-markers",
            ["Random encounters"] = "random-encounters",
            ["Battle speed"] = "native-battle-speed",
            ["Battle message"] = "native-battle-message",
            ["Field message"] = "dialogue-speed",
            ["Scan"] = "native-scan",
            ["Camera movement"] = "native-camera",
            ["Battle control"] = "native-battle-control",
            ["Vibration"] = "controller-vibration",
            ["Music volume"] = "music-volume",
            ["Effect volume"] = "effects-volume",
            ["Voice volume"] = "voice-volume",
            ["UI presentation"] = "interface-style",
            ["Text scale"] = "menu-scale",
            ["UI font"] = "menu-font",
            ["UI font size"] = "menu-font-size",
            ["Dialogue font"] = "dialogue-font",
            ["Dialogue font size"] = "dialogue-font-size"
        };
        List<SettingPlacement> result = [];
        foreach (MenuSetting setting in MenuSettings.Definitions)
        {
            if (!identities.TryGetValue(setting.Id, out string? identity))
            {
                throw new InvalidOperationException($"Setting '{setting.Id}' requires an explicit disposition.");
            }
            var placement = Pages.FirstOrDefault(page => page.Value.Items.Contains(setting.Id));
            SettingsAudience audience = setting.Id switch
            {
                "Sound" or "Controller" => SettingsAudience.Excluded,
                "ATB" or "Battle speed" or "Battle message" or "Scan" or "Camera movement" or "Battle control" => SettingsAudience.Fixture,
                "Battle policy" or "Random encounters" => SettingsAudience.Developer,
                _ => SettingsAudience.Player
            };
            string page = placement.Key ?? (setting.Id == "Vibration" ? "config-controller-settings" : audience == SettingsAudience.Developer ? "debug-battle" : "");
            if (audience == SettingsAudience.Player && page.Length == 0)
            {
                throw new InvalidOperationException($"Player setting '{setting.Id}' has no canonical page.");
            }
            string scope = setting.Id switch
            {
                _ when UiPreferences.TripleTriadSettingKeys.Contains(setting.Id) => "Game / Title default",
                "Shared magic stock" or "Infinite magic" or "Smart Attack" => "Game / Title session",
                "Chocobo World" or "Weapon magazine required" or "Weapon upgrade order" or "Card Mod once" or "Draw once per spell" or "Limit once per turn" or "Selphie card Slots" or "Difficulty" or "Battle ATB mode" or "GF Transformations" or "Gunblade trigger" or "Battle policy" or "Random encounters" => "Game / Title default",
                _ => audience is SettingsAudience.Excluded or SettingsAudience.Fixture ? "Fixture" : "Global"
            };
            string target = setting.Id switch
            {
                "Show FPS" => "UiPreferences.ShowFps",
                "Window mode" => "UiPreferences.BorderlessWindow",
                "V-Sync" => "UiPreferences.VSync",
                "Graphics backend" => "UiPreferences.Graphics.Backend",
                "Anti-aliasing" => "UiPreferences.Graphics.MsaaSamples",
                "Field FPS" => "UiPreferences.FieldFps",
                "World FPS" => "UiPreferences.WorldFps",
                "Battle FPS" => "UiPreferences.BattleFps",
                "Menu FPS" => "UiPreferences.MenuFps",
                "Quick saves" => "UiPreferences.QuickSaveCount",
                "Cursor" => "UiPreferences.CursorMemory",
                "Limit once per turn" => "GameplayPreferences.LimitOncePerTurn",
                "Selphie card Slots" => "GameplayPreferences.SelphieCardSlots",
                "Card Mod once" => "GameplayPreferences.CardModOnce",
                "Weapon magazine required" => "GameplayPreferences.WeaponMagazineRequired",
                "Weapon upgrade order" => "GameplayPreferences.WeaponUpgradeOrder",
                "Draw once per spell" => "GameplayPreferences.DrawOncePerSpell",
                "Chocobo World" => "GameplayPreferences.ChocoboWorldMode",
                "Difficulty" => "GameplayPreferences.Difficulty",
                "Battle ATB mode" => "GameplayPreferences.AtbMode",
                "Shared magic stock" => "GameplayPreferences.SharedMagicStock",
                "Infinite magic" => "GameplayPreferences.InfiniteMagic",
                "Smart Attack" => "GameplayPreferences.SmartAttack",
                "GF Transformations" => "GameplayPreferences.GuardianFormsEnabled",
                "Gunblade trigger" => "GameplayPreferences.GunbladeMode",
                "Battle policy" => "BattlePolicy.FullBattlePlaythrough",
                "Triple Triad width" => "GameplayPreferences.TripleTriad.Width",
                "Triple Triad height" => "GameplayPreferences.TripleTriad.Height",
                "Triple Triad blocked cells" => "GameplayPreferences.TripleTriad.BlockedCellCount",
                "Triple Triad hand size" => "GameplayPreferences.TripleTriad.HandSize",
                "Triple Triad Random redraws" => "GameplayPreferences.TripleTriad.RandomRedraws",
                "Triple Triad assistance" => "GameplayPreferences.TripleTriad.Assistance",
                "Triple Triad modified Same/Plus" => "GameplayPreferences.TripleTriad.UseModifiedSamePlus",
                "Rare card markers" => "UiPreferences.RareCardMarkers",
                "Random encounters" => "RandomEncounters.Enabled",
                "Field message" => "UiPreferences.FieldMessageSpeed",
                "Vibration" => "ControlsProfile.Controller.VibrationEnabled",
                "Music volume" => "UiPreferences.MusicVolume",
                "Effect volume" => "UiPreferences.SoundVolume",
                "Voice volume" => "UiPreferences.VoiceVolume",
                "UI presentation" => "UiPreferences.MenuPresentation",
                "Text scale" => "UiPreferences.TextScale",
                "UI font" => "UiPreferences.CurrentPresentationFont",
                "UI font size" => "UiPreferences.UiFontSize",
                "Dialogue font" => "UiPreferences.DialogueFontFamily",
                "Dialogue font size" => "UiPreferences.DialogueFontSize",
                _ when audience is SettingsAudience.Fixture or SettingsAudience.Excluded => "Fixture." + identity,
                _ => throw new InvalidOperationException($"Setting '{setting.Id}' has no explicit command owner.")
            };
            result.Add(new(identity, setting.Id, audience, page, page == "config-text" ? setting.Id.StartsWith("Dialogue", StringComparison.Ordinal) || setting.Id == "Field message" ? "Dialogue" : "Menus" : page == "config-exploration" ? setting.Id == "Quick saves" ? "Saving" : "Exploration" : "", placement.Value is null ? 0 : Array.IndexOf(placement.Value.Items, setting.Id), target, scope));
        }
        return result;
    }
}
