using System.Numerics;
using Memoria.FF8.Mods;
using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

internal sealed class LightingEditorElement(LightingEditorController controller) : UiElement
{
    protected override void DrawSelf(IUiCanvas canvas, UiTheme theme)
    {
        controller.DrawEditor(canvas, theme);
    }
}

public sealed partial class LightingEditorController
{
    private static readonly UiColor PanelColor = new(24, 29, 38, 245);
    private static readonly UiColor InputColor = new(13, 18, 26);
    private static readonly UiColor AccentColor = new(79, 175, 219);
    private static readonly UiColor ErrorColor = new(255, 199, 118);
    private static readonly UiColor[] AxisColors = [new(241, 84, 88), new(100, 215, 118), new(90, 151, 255)];
    private static readonly UiColor[] ColorPalette = [new(255, 255, 255), new(255, 220, 170), new(255, 150, 55), new(255, 65, 65), new(255, 120, 190), new(175, 100, 255), new(95, 150, 255), new(80, 235, 255), new(110, 255, 125), new(255, 240, 85), new(128, 128, 128), new(0, 0, 0)];

    internal void DrawEditor(IUiCanvas canvas, UiTheme theme)
    {
        if (!OverlayVisible)
        {
            return;
        }
        DrawSceneGuides(canvas, theme);
        Panel(canvas, LightingEditorLayout.Header);
        Panel(canvas, LightingEditorLayout.Sources);
        Panel(canvas, LightingEditorLayout.Properties);
        Panel(canvas, LightingEditorLayout.Footer);
        Label(canvas, "FIELD LIGHTS  /  " + Draft.FieldName + (IsDirty ? "  * unsaved" : "  saved"), 20, new(26, 20, 700, 26), theme.Foreground);
        Label(canvas, ModName + "  •  Authoring preview  •  Simulation frozen", 13, new(26, 47, 700, 18), AccentColor);
        Button(canvas, theme, LightingEditorLayout.Undo, "Undo", false, History.CanUndo);
        Button(canvas, theme, LightingEditorLayout.Redo, "Redo", false, History.CanRedo);
        Button(canvas, theme, LightingEditorLayout.Settings, "Shortcuts");
        Button(canvas, theme, LightingEditorLayout.Save, "Save field", true);
        Button(canvas, theme, LightingEditorLayout.Cancel, "Cancel / Close");
        Button(canvas, theme, LightingEditorLayout.Move, "Move", ActiveTool == LightingTransformTool.Translate);
        Button(canvas, theme, LightingEditorLayout.Rotate, "Aim", ActiveTool == LightingTransformTool.Rotate);
        Button(canvas, theme, LightingEditorLayout.Orbit, "Orbit around scene pivot", ActiveTool == LightingTransformTool.Orbit);
        Button(canvas, theme, LightingEditorLayout.Pivot, "Pick scene pivot", PickingPivot);
        DrawSources(canvas, theme);
        DrawProperties(canvas, theme);
        string hints = Shortcuts.Describe(LightingEditorCommand.Translate) + " Move   " + Shortcuts.Describe(LightingEditorCommand.Rotate) + " Aim   " + Shortcuts.Describe(LightingEditorCommand.AxisX) + "/" + Shortcuts.Describe(LightingEditorCommand.AxisY) + "/" + Shortcuts.Describe(LightingEditorCommand.AxisZ) + " axes   " + Shortcuts.Describe(LightingEditorCommand.Confirm) + " Confirm   Right click cancel edit   " + Shortcuts.Describe(LightingEditorCommand.ToggleVisibility) + " Hide UI";
        Label(canvas, Message, 14, new(24, 656, 1232, 21), ErrorColor);
        Label(canvas, hints, 13, new(24, 682, 1232, 18), theme.Muted);
        if (transform is not null || propertyEdit is not null)
        {
            Button(canvas, theme, LightingEditorLayout.CancelEdit, "Cancel edit");
        }
        if (transform is { } active)
        {
            string constraint = active.Axis == LightingAxis.None ? active.Tool == LightingTransformTool.Translate ? "camera plane" : active.Tool == LightingTransformTool.Rotate ? "camera axis" : "world Z" : "world " + active.Axis;
            string numeric = active.Numeric.Length == 0 ? "mouse" : active.Numeric + (active.Tool == LightingTransformTool.Translate ? " units" : "°");
            Panel(canvas, new(270, 566, 675, 34));
            Label(canvas, active.Tool + "  /  " + constraint + "  /  " + numeric, 17, new(281, 574, 650, 24), theme.Foreground);
        }
        if (!string.IsNullOrEmpty(RendererStatus))
        {
            Panel(canvas, new(270, 120, 675, 42));
            Label(canvas, RendererStatus, 13, new(280, 128, 655, 28), ErrorColor);
        }
        if (SelectionLocationNotice is { } locationNotice)
        {
            Panel(canvas, new(270, 170, 675, 48));
            canvas.Text(locationNotice, 13, new(280, 176, 655, 38), ErrorColor);
        }
        if (ShortcutsVisible)
        {
            DrawShortcutDialog(canvas, theme);
        }
        if (ColorPickerVisible)
        {
            DrawColorPicker(canvas, theme);
        }
        if (DiscardConfirmation)
        {
            DrawDiscardDialog(canvas, theme);
        }
    }

    private void DrawSources(IUiCanvas canvas, UiTheme theme)
    {
        Label(canvas, "SCENE SOURCES", 16, new(24, 94, 216, 22), theme.Foreground);
        Button(canvas, theme, LightingEditorLayout.Add, "Add light");
        Button(canvas, theme, LightingEditorLayout.Delete, "Delete", false, Selected is not null);
        for (int row = 0; row < LightingEditorLayout.VisibleLights && ListOffset + row < Draft.Lights.Count; row++)
        {
            FieldSpotLight light = Draft.Lights[ListOffset + row];
            UiRect bounds = LightingEditorLayout.LightRow(row);
            canvas.Fill(bounds, light.Id == SelectedId ? new UiColor(39, 83, 108) : InputColor);
            canvas.Fill(new(bounds.X + 5, bounds.Y + 8, 10, 10), light.Enabled ? LightColor(light) : new UiColor(74, 74, 74));
            Label(canvas, light.Name, 14, new(bounds.X + 22, bounds.Y + 5, bounds.Width - 50, 20), light.Enabled ? theme.Foreground : theme.Muted);
            if (Draft.PrimaryLightId == light.Id)
            {
                Label(canvas, "P", 15, new(bounds.Right - 20, bounds.Y + 5, 16, 20), ErrorColor);
            }
        }
        if (Draft.Lights.Count == 0)
        {
            canvas.Text("No lamps in this field.\n\nPress " + Shortcuts.Describe(LightingEditorCommand.AddLight) + " or Add light to place a spotlight above the ground pivot.\n\nOnly placed, enabled lamps illuminate models.", 15, new(28, 178, 208, 200), theme.Muted);
        }
        Label(canvas, Draft.Lights.Count(light => light.Enabled) + " / 8 enabled  •  " + Draft.Lights.Count + " total", 13, new(24, 550, 218, 24), theme.Muted);
        Label(canvas, "P = Primary shadow source", 13, new(24, 575, 218, 23), theme.Muted);
        Label(canvas, "Mouse wheel scrolls sources", 12, new(24, 604, 218, 21), theme.Muted);
    }

    private void DrawProperties(IUiCanvas canvas, UiTheme theme)
    {
        Label(canvas, "SPOTLIGHT PROPERTIES", 16, new(972, 94, 282, 22), theme.Foreground);
        if (Selected is { } selected)
        {
            DrawNumeric(canvas, theme, LightingProperty.Name);
            canvas.Fill(LightingEditorLayout.ColorSwatch, LightColor(selected));
            Label(canvas, "RGB", 12, LightingEditorLayout.ColorSwatch.Inset(6), selected.Color.LengthSquared() > 1 ? InputColor : theme.Foreground);
            Button(canvas, theme, LightingEditorLayout.Enabled, selected.Enabled ? "✓ Enabled" : "Disabled", selected.Enabled);
            Button(canvas, theme, LightingEditorLayout.Primary, Draft.PrimaryLightId == selected.Id ? "✓ Primary shadow" : "Primary shadow", Draft.PrimaryLightId == selected.Id);
            Label(canvas, "Position: X / Y / Z (height)", 13, new(972, 188, 282, 20), theme.Muted);
            Label(canvas, "Outward aim: X / Y / Z (normalized)", 13, new(972, 240, 282, 20), theme.Muted);
            foreach (LightingProperty property in new[] { LightingProperty.PositionX, LightingProperty.PositionY, LightingProperty.PositionZ, LightingProperty.DirectionX, LightingProperty.DirectionY, LightingProperty.DirectionZ })
            {
                DrawNumeric(canvas, theme, property);
            }
            foreach (LightingProperty property in Enum.GetValues<LightingProperty>().Where(property => property is >= LightingProperty.Red and <= LightingProperty.OuterCone))
            {
                DrawSlider(canvas, theme, property);
            }
        }
        else
        {
            canvas.Text("Select a source from the list or click its world-space marker.\n\nMove changes position.\nAim changes beam direction.\nOrbit rotates both around the scene pivot.\n\nXY is ground; positive Z is height. Below-floor lamps are allowed.", 15, new(977, 130, 272, 290), theme.Muted);
        }
        Label(canvas, "GROUND SHADOW", 13, new(972, 501, 282, 20), AccentColor);
        DrawSlider(canvas, theme, LightingProperty.ShadowOpacity);
        DrawSlider(canvas, theme, LightingProperty.ShadowSoftness);
        Label(canvas, "RGB: sRGB 0–255 • strength: relative", 12, new(972, 588, 282, 19), theme.Muted);
        Label(canvas, "Drag sliders or click values and type.", 12, new(972, 610, 282, 19), theme.Muted);
    }

    private void DrawNumeric(IUiCanvas canvas, UiTheme theme, LightingProperty property)
    {
        UiRect bounds = LightingEditorLayout.Numeric(property);
        bool editing = EditingProperty == property;
        canvas.Fill(bounds, editing ? new UiColor(35, 66, 87) : InputColor);
        canvas.Line(bounds.X, bounds.Bottom - 1, bounds.Right, bounds.Bottom - 1, editing ? AccentColor : new UiColor(67, 77, 89), 1);
        string value = PropertyText(property);
        Label(canvas, value + (editing ? "|" : ""), 14, new(bounds.X + 5, bounds.Y + 4, bounds.Width - 10, bounds.Height - 5), editing && TextSelected ? ErrorColor : theme.Foreground);
    }

    private void DrawSlider(IUiCanvas canvas, UiTheme theme, LightingProperty property)
    {
        UiRect bounds = LightingEditorLayout.Slider(property);
        string name = property switch
        {
            LightingProperty.Red => "Red",
            LightingProperty.Green => "Green",
            LightingProperty.Blue => "Blue",
            LightingProperty.Intensity => "Intensity",
            LightingProperty.Range => "Range",
            LightingProperty.InnerCone => "Inner °",
            LightingProperty.OuterCone => "Outer °",
            LightingProperty.ShadowOpacity => "Opacity",
            LightingProperty.ShadowSoftness => "Softness",
            _ => property.ToString()
        };
        UiColor color = property switch
        {
            LightingProperty.Red => AxisColors[0],
            LightingProperty.Green => AxisColors[1],
            LightingProperty.Blue => AxisColors[2],
            _ => AccentColor
        };
        Label(canvas, name, 13, new(972, bounds.Y + 4, 76, 22), theme.Foreground);
        (float min, float max) = PropertyRange(property);
        float ratio = Math.Clamp((PropertyValue(property) - min) / Math.Max(0.0001f, max - min), 0, 1);
        canvas.Fill(new(bounds.X, bounds.Y + 8, bounds.Width, 9), InputColor);
        if (property is LightingProperty.Red or LightingProperty.Green or LightingProperty.Blue)
        {
            for (int index = 0; index < 24; index++)
            {
                float value = index / 23f;
                canvas.Fill(new(bounds.X + bounds.Width * index / 24, bounds.Y + 8, bounds.Width / 24 + 1, 9), new((byte)(color.R * value), (byte)(color.G * value), (byte)(color.B * value)));
            }
        }
        else
        {
            canvas.Fill(new(bounds.X, bounds.Y + 8, bounds.Width * ratio, 9), color);
        }
        canvas.Fill(new(bounds.X + bounds.Width * ratio - 3, bounds.Y + 3, 6, 19), theme.Foreground);
        DrawNumeric(canvas, theme, property);
    }

    private void DrawSceneGuides(IUiCanvas canvas, UiTheme theme)
    {
        if (project(Pivot) is { } pivotScreen)
        {
            canvas.Line(pivotScreen.X - 11, pivotScreen.Y, pivotScreen.X + 11, pivotScreen.Y, AccentColor, 2);
            canvas.Line(pivotScreen.X, pivotScreen.Y - 11, pivotScreen.X, pivotScreen.Y + 11, AccentColor, 2);
            Label(canvas, "scene pivot", 13, new(pivotScreen.X + 13, pivotScreen.Y + 3, 110, 22), AccentColor);
        }
        foreach (FieldSpotLight light in Draft.Lights)
        {
            if (project(light.Position) is not { } marker)
            {
                continue;
            }
            UiColor color = light.Enabled ? LightColor(light) : new UiColor(120, 120, 120);
            bool selected = light.Id == SelectedId;
            float radius = selected ? 10 : 7;
            canvas.Line(marker.X, marker.Y - radius, marker.X + radius, marker.Y, color, selected ? 3 : 2);
            canvas.Line(marker.X + radius, marker.Y, marker.X, marker.Y + radius, color, selected ? 3 : 2);
            canvas.Line(marker.X, marker.Y + radius, marker.X - radius, marker.Y, color, selected ? 3 : 2);
            canvas.Line(marker.X - radius, marker.Y, marker.X, marker.Y - radius, color, selected ? 3 : 2);
            Label(canvas, light.Name, 13, new(marker.X + 14, marker.Y - 18, 180, 22), color);
            if (selected)
            {
                DrawBeam(canvas, light, color);
            }
        }
        LightingGizmoSegment? hover = HitGizmo(pointer);
        foreach (LightingGizmoSegment segment in GizmoSegments())
        {
            UiColor color = AxisColors[(int)segment.Axis - 1];
            bool highlighted = ActiveAxis == segment.Axis || hover?.Axis == segment.Axis;
            canvas.Line(segment.Start.X, segment.Start.Y, segment.End.X, segment.End.Y, highlighted ? new UiColor(255, 237, 150) : color, highlighted ? 4 : 2);
            if (segment.Tool == LightingTransformTool.Translate)
            {
                canvas.Fill(new(segment.End.X - 4, segment.End.Y - 4, 8, 8), color);
                Label(canvas, segment.Axis.ToString(), 16, new(segment.End.X + 6, segment.End.Y - 10, 24, 24), color);
            }
        }
    }

    private void DrawBeam(IUiCanvas canvas, FieldSpotLight light, UiColor color)
    {
        DrawWorldLine(canvas, light.Position, light.Position + light.Direction * light.Range, color, 1);
        foreach (float angle in new[] { light.InnerConeDegrees, light.OuterConeDegrees })
        {
            float radians = angle * MathF.PI / 180;
            Vector3 center = light.Position + light.Direction * light.Range * MathF.Cos(radians);
            float radius = light.Range * MathF.Sin(radians);
            int index = 0;
            foreach ((Vector3 start, Vector3 end) in LightingEditorGeometry.Ring(center, light.Direction, radius))
            {
                DrawWorldLine(canvas, start, end, color, 1);
                if (index++ % 12 == 0)
                {
                    DrawWorldLine(canvas, light.Position, start, color, 1);
                }
            }
        }
        if (project(light.Position + light.Direction * light.Range) is { } endPoint)
        {
            Label(canvas, "range " + light.Range.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture), 12, new(endPoint.X + 6, endPoint.Y, 140, 20), color);
        }
    }

    private void DrawWorldLine(IUiCanvas canvas, Vector3 start, Vector3 end, UiColor color, float width)
    {
        if (project(start) is { } first && project(end) is { } second)
        {
            canvas.Line(first.X, first.Y, second.X, second.Y, color, width);
        }
    }

    private void DrawShortcutDialog(IUiCanvas canvas, UiTheme theme)
    {
        canvas.Fill(new(0, 0, DesignWidth, DesignHeight), new UiColor(0, 0, 0, 165));
        Panel(canvas, new(350, 92, 572, 560));
        Label(canvas, "LIGHTING SHORTCUTS", 20, new(372, 108, 520, 28), theme.Foreground);
        Label(canvas, CapturingShortcut is { } capture ? "Press a chord for: " + LightingShortcutSettings.Label(capture) : "Click an action to rebind. Changes are saved in user preferences.", 13, new(372, 138, 520, 20), AccentColor);
        LightingEditorCommand[] commands = Enum.GetValues<LightingEditorCommand>();
        for (int index = 0; index < commands.Length; index++)
        {
            LightingEditorCommand command = commands[index];
            UiRect bounds = LightingEditorLayout.ShortcutRow(index);
            Button(canvas, theme, bounds, LightingShortcutSettings.Label(command), CapturingShortcut == command);
            Label(canvas, Shortcuts.Describe(command), 13, new(bounds.Right - 155, bounds.Y + 5, 150, 22), ErrorColor);
            if (command is not (LightingEditorCommand.OpenEditor or LightingEditorCommand.ToggleVisibility))
            {
                Button(canvas, theme, LightingEditorLayout.ShortcutClear(index), "Clear");
            }
        }
        Button(canvas, theme, LightingEditorLayout.Defaults, "Restore defaults");
        Button(canvas, theme, LightingEditorLayout.SettingsClose, CapturingShortcut is null ? "Done" : "Cancel capture");
    }

    private void DrawDiscardDialog(IUiCanvas canvas, UiTheme theme)
    {
        canvas.Fill(new(0, 0, DesignWidth, DesignHeight), new UiColor(0, 0, 0, 170));
        Panel(canvas, new(408, 270, 460, 172));
        canvas.Text("Discard unsaved lighting changes?", 21, new(432, 291, 412, 46), theme.Foreground);
        canvas.Text("Previous saves remain. Closing this scene editor keeps authoring enabled.", 14, new(432, 337, 412, 34), theme.Muted);
        Button(canvas, theme, LightingEditorLayout.Discard, "Discard changes");
        Button(canvas, theme, LightingEditorLayout.Continue, "Continue editing", true);
    }

    private void DrawColorPicker(IUiCanvas canvas, UiTheme theme)
    {
        Panel(canvas, new(600, 202, 330, 230));
        Label(canvas, "SPOTLIGHT COLOR", 19, new(618, 218, 296, 28), theme.Foreground);
        for (int index = 0; index < ColorPalette.Length; index++)
        {
            canvas.Fill(LightingEditorLayout.ColorChoice(index), ColorPalette[index]);
        }
        Label(canvas, "Use RGB sliders / values for any custom color.", 12, new(618, 362, 296, 20), theme.Muted);
        Button(canvas, theme, LightingEditorLayout.ColorClose, "Close picker");
    }

    private static UiColor LightColor(FieldSpotLight light)
    {
        return new((byte)Math.Clamp(light.Color.X * 255, 0, 255), (byte)Math.Clamp(light.Color.Y * 255, 0, 255), (byte)Math.Clamp(light.Color.Z * 255, 0, 255));
    }

    private static void Panel(IUiCanvas canvas, UiRect bounds)
    {
        canvas.Fill(bounds, PanelColor);
        canvas.Line(bounds.X, bounds.Y, bounds.Right, bounds.Y, new UiColor(67, 83, 102), 1);
    }

    private static void Button(IUiCanvas canvas, UiTheme theme, UiRect bounds, string label, bool selected = false, bool enabled = true)
    {
        canvas.Fill(bounds, selected ? new UiColor(35, 94, 120) : new UiColor(45, 54, 67));
        Label(canvas, label, 13, new(bounds.X + 8, bounds.Y + 6, bounds.Width - 16, bounds.Height - 6), enabled ? theme.Foreground : theme.Muted);
    }

    private static void Label(IUiCanvas canvas, string text, float size, UiRect bounds, UiColor color)
    {
        canvas.PushClip(bounds);
        try
        {
            canvas.Text(text, size, bounds, color);
        }
        finally
        {
            canvas.PopClip();
        }
    }
}
