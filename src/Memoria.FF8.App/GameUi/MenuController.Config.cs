using System.Globalization;
using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

public sealed partial class MenuController
{
    private int ConfigRowsPerPage => Math.Max(5, (int)(495 / (44 * TextScale)));

    internal IReadOnlyList<MenuEntry> ConfigEntries()
    {
        List<MenuEntry> entries = [];
        foreach (MenuSetting setting in MenuSettings.Definitions)
        {
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
        entries.Add(new MenuEntry("config-controller", "Controller mappings", "Assign controller actions. Bindings are saved globally for every game save.", "config-controller"));
        entries.Add(new MenuEntry("config-keyboard", "Keyboard mappings", "Assign keyboard actions and combinations. Bindings are saved globally for every game save.", "config-keyboard"));
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
                Text = entry.Label + "  ›",
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
                Text = setting.Id,
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
                Text = setting.Id,
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
