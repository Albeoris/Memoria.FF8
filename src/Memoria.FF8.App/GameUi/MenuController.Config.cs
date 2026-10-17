using System.Globalization;
using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

public sealed partial class MenuController
{
    private static readonly (string Id, string Label, string Help)[] ConfigCategories =
    [
        ("gameplay", "Gameplay", "Battle rules and assistance. Gameplay options belong to this save; Title sets defaults for a new game."),
        ("display", "Display", "V-Sync and scene presentation limits. Shared by every save on this installation."),
        ("audio", "Audio", "Music and sound-effect output. Shared by every save on this installation."),
        ("interface", "Interface", "Text, fonts, cursors and dialogue presentation. Shared by every save on this installation."),
        ("controls", "Controls", "Keyboard and controller mappings, dead zones and vibration. Shared by every save on this installation.")
    ];
    private bool IsConfigSettingsScreen => ScreenId is "config" or "config-gameplay" or "config-display" or "config-audio" or "config-interface" or "config-controls" or "new-game-options";

    private static string ConfigCategory(string id) => id switch
    {
        "V-Sync" or "Field FPS" or "Battle FPS" or "Menu FPS" => "display",
        "Music volume" or "Effect volume" or "Sound" => "audio",
        "Controller" or "Vibration" => "controls",
        "Quick saves" or "Cursor" or "Field message" or "Rare card markers" or "Text scale" or "UI font" or "UI font size" or "Dialogue font" or "Dialogue font size" => "interface",
        _ => "gameplay"
    };

    private int ConfigRowsPerPage => Math.Max(5, (int)(495 / (44 * TextScale)));

    internal IReadOnlyList<MenuEntry> ConfigEntries()
    {
        if (ScreenId == "config")
        {
            return ConfigCategories.Select(category => new MenuEntry("config-" + category.Id, category.Label, category.Help, "config-" + category.Id)).ToArray();
        }
        List<MenuEntry> entries = [];
        foreach (MenuSetting setting in MenuSettings.Definitions)
        {
            if (ScreenId == "new-game-options" ? setting.Id is not ("Battle ATB mode" or "Rare card markers") : ConfigCategory(setting.Id) != ScreenId["config-".Length..])
            {
                continue;
            }
            if (setting.Id is "Sound" or "Controller")
            {
                continue;
            }
            if (!IsDemo && !Data.SupportedActions.Contains("config") && !Data.SupportedActions.Contains("config:" + setting.Id))
            {
                continue;
            }
            string value = Data.Settings.GetValueOrDefault(setting.Id, setting.Values[0]);
            int next = (Array.IndexOf(setting.Values, value) + 1) % setting.Values.Length;
            entries.Add(new MenuEntry(setting.Id, setting.Id, setting.Description, Action: new MenuAction("config", setting.Id, Value: setting.Values[next])));
        }
        if (ScreenId == "config-controls")
        {
            entries.Add(new MenuEntry("config-controller", "Controller mappings", "Assign controller actions. Bindings are saved globally for every game save.", "config-controller"));
            entries.Add(new MenuEntry("config-keyboard", "Keyboard mappings", "Assign keyboard actions and combinations. Bindings are saved globally for every game save.", "config-keyboard"));
        }
        if (ScreenId == "new-game-options")
        {
            entries.Add(new MenuEntry("begin", "Begin", "Start a new game with the selected ATB mode and rare card markers. More settings are available in Config.", Action: new MenuAction("new-game-start")));
        }
        return entries;
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
        string closest = setting.Values.MinBy(candidate => MathF.Abs(float.Parse(candidate.TrimEnd('%'), CultureInfo.InvariantCulture) - value))!;
        Execute(new MenuAction("config", setting.Id, Value: closest));
    }

    private UiElement BuildConfigRow(MenuEntry entry, int index)
    {
        MenuSetting? setting = MenuSettings.Definitions.FirstOrDefault(candidate => candidate.Id == entry.Id);
        if (setting is null)
        {
            return new UiButton
            {
                Id = entry.Id,
                Text = entry.Label,
                FontSize = 24 * TextScale,
                PointerSize = 20 * TextScale,
                DrawBackground = false,
                HighlightSelection = false,
                FocusChanged = focused => SelectConfigRow(focused, index),
                Click = () => Activate(index)
            };
        }
        string value = Data.Settings.GetValueOrDefault(setting.Id, setting.Values[0]);
        UiElement row;
        if (TryConfigNumbers(setting, out float[] numbers))
        {
            row = new UiNumericSlider
            {
                Text = setting.Id == "Battle ATB mode" ? "ATB mode" : setting.Id,
                Max = numbers[^1],
                Min = numbers[0],
                Step = numbers.Zip(numbers.Skip(1), (left, right) => right - left).Min(),
                Value = float.TryParse(value.TrimEnd('%'), CultureInfo.InvariantCulture, out float number) ? number : numbers[0],
                ValueFormat = setting.Values[0].EndsWith('%') ? "0'%'" : "0",
                ValueWidth = 66 * TextScale,
                LabelWidthRatio = 0.45f,
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
                Text = setting.Id == "Battle ATB mode" ? "ATB mode" : setting.Id,
                Items = setting.Values,
                SelectedIndex = Math.Max(0, Array.IndexOf(setting.Values, value)),
                LabelWidthRatio = 0.45f,
                FontSize = 24 * TextScale,
                PointerSize = 20 * TextScale,
                TextInset = 35 * TextScale,
                SelectionChanged = selected => Execute(new MenuAction("config", setting.Id, Value: setting.Values[selected]))
            };
        }
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
