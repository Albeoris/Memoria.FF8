using System.Globalization;
using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

public sealed partial class MenuController
{
    internal static readonly (string Id, string Label, string Help)[] ConfigCategories =
    [
        ("graphics", "Graphics", "Rendering quality, presentation limits and frame-rate counter."),
        ("audio", "Audio", "Music and sound effects. Shared across saves for this user."),
        ("controls", "Controls", "Player bindings and controller settings."),
        ("interface", "Interface", "Interface style, text and cursor memory."),
        ("gameplay", "Gameplay", "Combat, magic, exploration and saving."),
        ("mods", "Mods", "Installed packages, requested enable state and resource status.")
    ];
    private bool IsConfigSettingsScreen => ScreenId == "config" || SettingsPlacement.Pages.ContainsKey(ScreenId) || ScreenId == "new-game-options";
    private int ConfigRowsPerPage => Math.Max(1, (int)((ScreenId == "new-game-options" ? 315 : ScreenId == "config-text" ? 300 : 495) / (44 * TextScale)));

    private static string ConfigLabel(string id) => id switch
    {
        "Show FPS" => "Frame-rate counter",
        "Field FPS" => "Field and movies",
        "World FPS" => "World map",
        "Battle FPS" => "Battle",
        "Menu FPS" => "Menus and title",
        "Effect volume" => "Sound effects volume",
        "Quick saves" => "Quick saves to keep",
        "Cursor" => "Cursor memory",
        "UI presentation" => "Interface style",
        "Text scale" => "Menu text scale",
        "UI font" => "Menu font family",
        "UI font size" => "Menu font size",
        "Dialogue font" => "Dialogue font family",
        "Field message" => "Dialogue text speed",
        "Battle ATB mode" => "ATB mode",
        _ => MagicUiText.SettingLabel(id)
    };

    internal IReadOnlyList<MenuEntry> ConfigEntries()
    {
        if (ScreenId == "config")
        {
            return ConfigCategories.Select(category => new MenuEntry("config-" + category.Id, category.Label, category.Help, category.Id == "mods" ? "mods" : "config-" + category.Id, Enabled: category.Id != "mods" || _screens.ContainsKey("mods"))).ToArray();
        }
        bool newGame = ScreenId == "new-game-options";
        string[] ids = newGame ? ["Difficulty", "Battle ATB mode", "Shared magic stock", "Infinite magic", "Smart Attack", "GF Transformations", "Rare card markers"] : SettingsPlacement.Pages[ScreenId].Items;
        List<MenuEntry> entries = [];
        foreach (string id in ids)
        {
            if (SettingsPlacement.Pages.TryGetValue(id, out var child))
            {
                entries.Add(new MenuEntry(id, child.Label + "…", child.Help, id));
                continue;
            }
            if (id is "config-keyboard" or "config-controller" or "config-controller-settings")
            {
                string label = id == "config-keyboard" ? "Keyboard bindings" : id == "config-controller" ? "Controller bindings" : "Controller settings";
                entries.Add(new MenuEntry(id, label + "…", "Player controls shared across saves for this user.", id));
                continue;
            }
            MenuSetting setting = MenuSettings.Definitions.Single(setting => setting.Id == id);
            if (!IsDemo && !Data.SupportedActions.Contains("config") && !Data.SupportedActions.Contains("config:" + setting.Id))
            {
                continue;
            }
            string value = ConfigValue(setting.Id);
            int next = (Array.IndexOf(setting.Values, value) + 1) % setting.Values.Length;
            bool enabled = setting.Id != "GF Transformations" || !Data.GuardianFormsPreferenceLocked;
            string help = enabled ? setting.Id is "UI font" or "Dialogue font" ? FontDisplayValue(setting.Id) + ". " + setting.Description : setting.Description : newGame ? "GF Transformations is unavailable until the battle ends." : "GF Transformations is unavailable until the active or pending battle ends.";
            MenuAction action = setting.Id == "Quick saves" && !newGame ? new("quick-save-open") : setting.Id is "UI font" or "Dialogue font" ? new("font-picker-open", setting.Id) : new("config", setting.Id, Value: setting.Values[next]);
            entries.Add(new MenuEntry(setting.Id, newGame ? MagicUiText.SettingLabel(setting.Id) : ConfigLabel(setting.Id), help, Action: action, Enabled: enabled));
        }
        if (newGame)
        {
            entries.Add(new MenuEntry("begin", "Begin", MagicUiText.Get("SettingBeginHelp"), Action: new MenuAction("new-game-start")));
        }
        return entries;
    }

    private string ConfigValue(string id) => id == "UI font" ? (IsFixtureContext ? demoFontPreferences : _readPreferences?.Invoke() ?? demoFontPreferences).GetUiFontFamily(Presentation) : Data.Settings.GetValueOrDefault(id, MenuSettings.Definitions.Single(setting => setting.Id == id).Values[0]);

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
        if (setting.Id is "Music volume" or "Effect volume")
        {
            Execute(new MenuAction("config", setting.Id, Value: Math.Clamp((int)MathF.Round(value), 0, 100).ToString(CultureInfo.InvariantCulture) + "%"));
            return;
        }
        string closest = setting.Values.MinBy(candidate => MathF.Abs(float.Parse(candidate.TrimEnd('%'), CultureInfo.InvariantCulture) - value))!;
        Execute(new MenuAction("config", setting.Id, Value: closest));
    }

    private UiElement BuildConfigRow(MenuEntry entry, int index)
    {
        MenuSetting? setting = MenuSettings.Definitions.FirstOrDefault(candidate => candidate.Id == entry.Id);
        if (setting is null || entry.Id is "Quick saves" or "UI font" or "Dialogue font")
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
        string value = ConfigValue(setting.Id);
        UiElement row;
        if (TryConfigNumbers(setting, out float[] numbers))
        {
            row = new UiNumericSlider
            {
                Text = ScreenId == "new-game-options" ? setting.Id == "Battle ATB mode" ? "ATB mode" : MagicUiText.SettingLabel(setting.Id) : ConfigLabel(setting.Id),
                Max = numbers[^1],
                Min = numbers[0],
                Step = setting.Id is "Music volume" or "Effect volume" ? 1 : numbers.Zip(numbers.Skip(1), (left, right) => right - left).Min(),
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
                Text = ScreenId == "new-game-options" ? setting.Id == "Battle ATB mode" ? "ATB mode" : MagicUiText.SettingLabel(setting.Id) : ConfigLabel(setting.Id),
                Items = setting.Values,
                OptionGap = 12 * TextScale,
                SelectedIndex = Math.Max(0, Array.IndexOf(setting.Values, value)),
                LabelWidthRatio = 0.52f,
                ColumnGap = 12 * TextScale,
                FontSize = 24 * TextScale,
                PointerSize = 20 * TextScale,
                TextInset = 35 * TextScale,
                SelectionChanged = selected => Execute(new MenuAction("config", setting.Id, Value: setting.Values[selected]))
            };
        }
        row.Enabled = entry.Enabled;
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
