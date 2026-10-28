using System.Globalization;
using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

public sealed partial class MenuController
{
    internal static readonly (string Id, string Label, string Help)[] ConfigCategories =
    [
        ("graphics", "Graphics", "Rendering quality, presentation limits and frame-rate counter."),
        ("audio", "Audio", "Music, sound effects and dialogue voices. Shared across saves for this user."),
        ("controls", "Controls", "Player bindings and controller settings."),
        ("interface", "Interface", "Interface style, text and battle cursor memory."),
        ("gameplay", "Gameplay", "Combat, magic, exploration and saving."),
        ("triple-triad", "Triple Triad", "Board dimensions, cards, Random redraws and move assistance for this game."),
        ("mods", "Mods", "Installed packages, requested enable state and resource status.")
    ];
    private bool IsConfigSettingsScreen => ScreenId == "config" || SettingsPlacement.Pages.ContainsKey(ScreenId) || ScreenId == "new-game-options";
    private int ConfigRowsPerPage => Math.Max(1, (int)((ScreenId == "new-game-options" ? 315 : ScreenId == "config-text" ? 300 : 495) / (44 * TextScale)));

    private string ConfigLabel(string id) => LocalizeSettingLabel(id, id switch
    {
        "Triple Triad width" => "Board width",
        "Triple Triad height" => "Board height",
        "Triple Triad blocked cells" => "Blocked cells",
        "Triple Triad hand size" => "Hand size",
        "Triple Triad Random redraws" => "Random redraws",
        "Triple Triad assistance" => "Move assistance",
        "Triple Triad modified Same/Plus" => "Elemental Same/Plus",
        "Infinite magic" => "Limited magic",
        "Show FPS" => "Frame-rate counter",
        "Field FPS" => "Field and movies",
        "World FPS" => "World map",
        "Battle FPS" => "Battle",
        "Menu FPS" => "Menus and title",
        "Effect volume" => "Sound effects volume",
        "Quick saves" => "Quick saves to keep",
        "Cursor" => "Battle cursor memory",
        "UI presentation" => "Interface style",
        "Text scale" => "Menu text scale",
        "UI font" => "Menu font family",
        "UI font size" => "Menu font size",
        "Dialogue font" => "Dialogue font family",
        "Field message" => "Dialogue text speed",
        "Battle ATB mode" => "ATB mode",
        _ => MagicUiText.SettingLabel(id)
    });

    internal IReadOnlyList<MenuEntry> ConfigEntries()
    {
        if (ScreenId == "config")
        {
            return ConfigCategories.Select(category => new MenuEntry("config-" + category.Id, SystemText("config." + category.Id, category.Label), SystemText("config." + category.Id + ".help", category.Help), category.Id == "mods" ? "mods" : "config-" + category.Id, Enabled: category.Id != "mods" || _screens.ContainsKey("mods"))).ToArray();
        }
        bool newGame = ScreenId == "new-game-options";
        string[] ids = newGame ? NewGameSettings.Ids : SettingsPlacement.Pages[ScreenId].Items;
        List<MenuEntry> entries = [];
        foreach (string id in ids)
        {
            if (SettingsPlacement.Pages.TryGetValue(id, out var child))
            {
                entries.Add(new MenuEntry(id, SystemText("screen." + id + ".title", child.Label) + "…", SystemText("screen." + id + ".help", child.Help), id));
                continue;
            }
            if (id is "config-keyboard" or "config-controller" or "config-controller-settings")
            {
                string label = id == "config-keyboard" ? SystemText("ui.configentries.label.keyboard.bindings", "Keyboard bindings") : id == "config-controller" ? SystemText("ui.configentries.label.controller.bindings", "Controller bindings") : SystemText("ui.configentries.label.controller.settings", "Controller settings");
                entries.Add(new MenuEntry(id, label + "…", SystemText("ui.configentries.entry.help.player.controls.shared.across.saves.for.this.user", "Player controls shared across saves for this user."), id));
                continue;
            }
            MenuSetting setting = ResolveConfigSetting(MenuSettings.Definitions.Single(setting => setting.Id == id));
            if (!IsDemo && !Data.SupportedActions.Contains("config") && !Data.SupportedActions.Contains("config:" + setting.Id))
            {
                continue;
            }
            string value = ConfigValue(setting.Id);
            int next = (Array.IndexOf(setting.Values, value) + 1) % setting.Values.Length;
            string? unavailableReason = setting.Id switch
            {
                "Chocobo World" => Data.ChocoboWorldUnavailableReason,
                "GF Transformations" when Data.GuardianFormsPreferenceLocked => newGame ? SystemText("ui.configentries.unavailablereason.gf.transformations.is.unavailable.until.the.battle.ends", "GF Transformations is unavailable until the battle ends.") : SystemText("ui.configentries.unavailablereason.gf.transformations.is.unavailable.until.the.active.or", "GF Transformations is unavailable until the active or pending battle ends."),
                "Limit once per turn" when Data.LimitOncePerTurnPreferenceLocked => SystemText("ui.configentries.unavailablereason.limit.once.per.turn.is.unavailable.until.the", "Limit once per turn is unavailable until the active or pending battle ends."),
                "Selphie card Slots" when Data.SelphieCardSlotsPreferenceLocked => SystemText("ui.configentries.unavailablereason.selphie.card.slots.is.unavailable.until.the.active", "Selphie card Slots is unavailable until the active or pending battle ends."),
                "Draw once per spell" when Data.DrawOncePerSpellPreferenceLocked => SystemText("ui.configentries.unavailablereason.draw.once.per.spell.is.unavailable.until.the", "Draw once per spell is unavailable until the active or pending battle ends."),
                "Shared magic stock" or "Infinite magic" or "Smart Attack" => Data.MagicPreferencesUnavailableReason,
                _ => null
            };
            if (NewGameSettings.IsLocked(setting.Id, Enum.Parse<Memoria.FF8.Runtime.BattleDifficulty>(ConfigValue("Difficulty"))))
            {
                unavailableReason = SystemText("ui.configentries.unavailablereason.hardcore.is.permanent.and.requires.every.optional.gameplay", "Hardcore is permanent and requires every optional gameplay rule to stay On.");
            }
            bool enabled = unavailableReason is null;
            string help = unavailableReason ?? (setting.Id is "UI font" or "Dialogue font" ? FontDisplayValue(setting.Id) + ". " + LocalizeSettingHelp(setting.Id, setting.Description) : LocalizeSettingHelp(setting.Id, setting.Description));
            if (setting.Id == "Graphics backend")
            {
                Memoria.FF8.Platform.GraphicsBackend backend = Memoria.FF8.Platform.GraphicsBackendSelection.Parse(value);
                help = (Memoria.FF8.Platform.GraphicsBackendSelection.UnavailableReason(backend, Memoria.FF8.Platform.HostPlatforms.Current) is string reason ? reason + " " : "") + help;
            }
            MenuAction action = setting.Id == "Quick saves" && !newGame ? new("quick-save-open") : setting.Id is "UI font" or "Dialogue font" ? new("font-picker-open", setting.Id) : new("config", setting.Id, Value: setting.Values[next]);
            entries.Add(new MenuEntry(setting.Id, newGame ? LocalizeSettingLabel(setting.Id, MagicUiText.SettingLabel(setting.Id)) : ConfigLabel(setting.Id), help, Action: action, Enabled: enabled));
        }
        if (ScreenId == "config-chocobo")
        {
            entries.AddRange(ChocoboWorldActions());
        }
        if (newGame)
        {
            entries.Add(new MenuEntry("begin", SystemText("ui.configentries.entry.label.begin", "Begin"), MagicText("SettingBeginHelp"), Action: new MenuAction("new-game-start")));
        }
        return entries;
    }

    private string ConfigValue(string id) => id == "UI font" ? (IsFixtureContext ? demoFontPreferences : _readPreferences?.Invoke() ?? demoFontPreferences).GetUiFontFamily(Presentation) : Data.Settings.GetValueOrDefault(id, MenuSettings.Definitions.Single(setting => setting.Id == id).Values[0]);

    private MenuSetting ResolveConfigSetting(MenuSetting setting)
    {
        if (!UiPreferences.TripleTriadSettingKeys.Contains(setting.Id))
        {
            return setting;
        }
        bool hardcore = ConfigValue("Difficulty") == nameof(Memoria.FF8.Runtime.BattleDifficulty.Hardcore);
        int width = int.TryParse(Data.Settings.GetValueOrDefault("Triple Triad width"), CultureInfo.InvariantCulture, out int configuredWidth) ? configuredWidth : 3;
        int height = int.TryParse(Data.Settings.GetValueOrDefault("Triple Triad height"), CultureInfo.InvariantCulture, out int configuredHeight) ? configuredHeight : 3;
        if (hardcore)
        {
            width = Math.Max(4, width);
            height = Math.Max(4, height);
        }
        int blocked = int.TryParse(Data.Settings.GetValueOrDefault("Triple Triad blocked cells"), CultureInfo.InvariantCulture, out int configuredBlocked) ? configuredBlocked : 0;
        blocked = Math.Clamp(blocked, hardcore ? 2 : 0, width * height / 3);
        string[] values = setting.Id switch
        {
            "Triple Triad width" or "Triple Triad height" when hardcore => setting.Values.Where(value => int.Parse(value, CultureInfo.InvariantCulture) >= 4).ToArray(),
            "Triple Triad blocked cells" => setting.Values.Where(value => int.Parse(value, CultureInfo.InvariantCulture) >= (hardcore ? 2 : 0) && int.Parse(value, CultureInfo.InvariantCulture) <= width * height / 3).ToArray(),
            "Triple Triad hand size" => setting.Values.Where(value => int.Parse(value, CultureInfo.InvariantCulture) >= (width * height - blocked + 1) / 2).ToArray(),
            "Triple Triad Random redraws" when hardcore => setting.Values.Take(2).ToArray(),
            "Triple Triad assistance" when hardcore => setting.Values.Take(2).ToArray(),
            _ => setting.Values
        };
        return setting with { Values = values };
    }

    private static bool TryConfigNumbers(MenuSetting setting, out float[] numbers)
    {
        List<float> values = [];
        foreach (string value in setting.Values)
        {
            if (!float.TryParse(value.TrimEnd('%'), NumberStyles.Float, CultureInfo.InvariantCulture, out float number))
            {
                numbers = [];
                return false;
            }
            values.Add(number);
        }
        numbers = values.Distinct().Order().ToArray();
        return numbers.Length > 1;
    }

    private void SetConfigValue(MenuSetting setting, float value)
    {
        if (setting.Id is "Music volume" or "Effect volume" or "Voice volume")
        {
            Execute(new MenuAction("config", setting.Id, Value: Math.Clamp((int)MathF.Round(value), 0, 100).ToString(CultureInfo.InvariantCulture) + "%"));
            return;
        }
        string closest = setting.Values.MinBy(candidate => MathF.Abs(float.Parse(candidate.TrimEnd('%'), CultureInfo.InvariantCulture) - value))!;
        Execute(new MenuAction("config", setting.Id, Value: closest));
    }

    private UiElement BuildConfigRow(MenuEntry entry, int index)
    {
        if (entry.Id is "Quick saves" or "Graphics backend")
        {
            return new UiActionSettingRow
            {
                Id = entry.Id,
                Text = entry.Label,
                Value = ConfigValue(entry.Id) + "…",
                FontSize = 24 * TextScale,
                PointerSize = 20 * TextScale,
                TextInset = 35 * TextScale,
                LabelWidthRatio = 0.52f,
                ColumnGap = 12 * TextScale,
                Enabled = entry.Enabled,
                FocusChanged = focused => SelectConfigRow(focused, index),
                Click = () => Activate(index)
            };
        }
        MenuSetting? setting = MenuSettings.Definitions.FirstOrDefault(candidate => candidate.Id == entry.Id);
        if (setting is null || entry.Id is "UI font" or "Dialogue font")
        {
            return new UiButton
            {
                Id = entry.Id,
                Text = entry.Label + (entry.Id is "UI font" or "Dialogue font" ? " · " + FontDisplayValue(entry.Id) : ""),
                FontSize = 24 * TextScale,
                PointerSize = 20 * TextScale,
                DrawBackground = false,
                HighlightSelection = false,
                FocusChanged = focused => SelectConfigRow(focused, index),
                Click = () => Activate(index)
            };
        }
        setting = ResolveConfigSetting(setting);
        string value = ConfigValue(setting.Id);
        UiElement row;
        if (TryConfigNumbers(setting, out float[] numbers))
        {
            row = new UiNumericSlider
            {
                Text = ScreenId == "new-game-options" ? setting.Id == "Battle ATB mode" ? SystemText("ui.buildconfigrow.text.atb.mode", "ATB mode") : LocalizeSettingLabel(setting.Id, MagicUiText.SettingLabel(setting.Id)) : ConfigLabel(setting.Id),
                Max = numbers[^1],
                Min = numbers[0],
                Step = setting.Id is "Music volume" or "Effect volume" or "Voice volume" ? 1 : numbers.Zip(numbers.Skip(1), (left, right) => right - left).Min(),
                Value = float.TryParse(value.TrimEnd('%'), CultureInfo.InvariantCulture, out float number) ? number : numbers[0],
                ValueFormat = setting.Values[0].EndsWith('%') ? "0'%'" : "0",
                ValueWidth = 66 * TextScale,
                LabelWidthRatio = 0.52f,
                ColumnGap = 12 * TextScale,
                FontSize = 24 * TextScale,
                PointerSize = 20 * TextScale,
                TextInset = 35 * TextScale,
                TrackHeight = 22 * TextScale,
                ValueChanged = number => SetConfigValue(setting, number)
            };
        }
        else
        {
            row = new UiChoiceRow
            {
                Text = ScreenId == "new-game-options" ? setting.Id == "Battle ATB mode" ? SystemText("ui.buildconfigrow.text.atb.mode", "ATB mode") : LocalizeSettingLabel(setting.Id, MagicUiText.SettingLabel(setting.Id)) : ConfigLabel(setting.Id),
                Items = Enumerable.Range(0, setting.Values.Length).Select(index => LocalizeSettingValue(setting, index)).ToArray(),
                ReadOnly = !entry.Enabled,
                OptionGap = 12 * TextScale,
                SelectedIndex = Math.Max(0, Array.IndexOf(setting.Values, value)),
                LabelWidthRatio = setting.Id == "Chocobo World" ? 0.27f : 0.52f,
                ColumnGap = 12 * TextScale,
                FontSize = 24 * TextScale,
                PointerSize = 20 * TextScale,
                TextInset = 35 * TextScale,
                SelectionChanged = selected => Execute(new MenuAction("config", setting.Id, Value: setting.Values[selected]))
            };
        }
        row.Enabled = row is UiChoiceRow || entry.Enabled;
        row.Id = entry.Id;
        row.FocusChanged = focused => SelectConfigRow(focused, index);
        return row;
    }

    private void SelectConfigRow(bool focused, int index)
    {
        if (focused && SelectedIndex != index)
        {
            Select(index, Entries.Count);
        }
    }
}
