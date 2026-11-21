using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

internal static class LightingEditorLayout
{
    internal const int VisibleLights = 11;
    internal static UiRect Header => new(12, 12, 1256, 58);
    internal static UiRect Sources => new(12, 82, 242, 556);
    internal static UiRect Properties => new(960, 82, 308, 556);
    internal static UiRect Footer => new(12, 648, 1256, 60);
    internal static UiRect Add => new(24, 122, 72, 28);
    internal static UiRect AddFill => new(102, 122, 76, 28);
    internal static UiRect AddSunlight => new(24, 156, 106, 28);
    internal static UiRect Delete => new(184, 122, 58, 28);
    internal static UiRect Move => new(268, 84, 110, 28);
    internal static UiRect Rotate => new(386, 84, 110, 28);
    internal static UiRect Orbit => new(504, 84, 192, 28);
    internal static UiRect Pivot => new(704, 84, 142, 28);
    internal static UiRect ResetView => new(854, 84, 94, 28);
    internal static UiRect Undo => new(736, 25, 76, 30);
    internal static UiRect Redo => new(820, 25, 76, 30);
    internal static UiRect Settings => new(904, 25, 100, 30);
    internal static UiRect Save => new(1012, 25, 102, 30);
    internal static UiRect Cancel => new(1122, 25, 132, 30);
    internal static UiRect Enabled => new(972, 156, 126, 26);
    internal static UiRect Primary => new(1106, 156, 148, 26);
    internal static UiRect Discard => new(435, 380, 195, 34);
    internal static UiRect Continue => new(646, 380, 195, 34);
    internal static UiRect Defaults => new(372, 608, 160, 32);
    internal static UiRect SettingsClose => new(760, 608, 140, 32);
    internal static UiRect CancelEdit => new(852, 610, 96, 28);
    internal static UiRect ColorSwatch => new(1212, 120, 42, 28);
    internal static UiRect ColorClose => new(802, 385, 112, 28);

    internal static UiRect ColorChoice(int index)
    {
        return new UiRect(618 + index % 6 * 51, 260 + index / 6 * 53, 42, 42);
    }

    internal static UiRect LightRow(int visibleIndex)
    {
        return new UiRect(24, 195 + visibleIndex * 31, 218, 28);
    }

    internal static UiRect Numeric(LightingProperty property)
    {
        return property switch
        {
            LightingProperty.Name => new(972, 120, 232, 28),
            LightingProperty.PositionX => new(972, 208, 88, 28),
            LightingProperty.PositionY => new(1069, 208, 88, 28),
            LightingProperty.PositionZ => new(1166, 208, 88, 28),
            LightingProperty.DirectionX => new(972, 260, 88, 28),
            LightingProperty.DirectionY => new(1069, 260, 88, 28),
            LightingProperty.DirectionZ => new(1166, 260, 88, 28),
            _ => new(1184, PropertyY(property), 70, 25)
        };
    }

    internal static UiRect Slider(LightingProperty property)
    {
        return new UiRect(1052, PropertyY(property), 120, 25);
    }

    internal static float PropertyY(LightingProperty property)
    {
        return 300 + ((int)property - (int)LightingProperty.Red) * 29 + (property >= LightingProperty.ShadowOpacity ? 20 : 0);
    }

    internal static UiRect ShortcutRow(int index)
    {
        return new UiRect(372, 155 + index * 31, 466, 27);
    }

    internal static UiRect ShortcutClear(int index)
    {
        return new UiRect(846, 155 + index * 31, 54, 27);
    }
}
