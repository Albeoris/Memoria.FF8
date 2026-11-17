using System.Numerics;
using Memoria.FF8.Mods;
using Memoria.FF8.Ui;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8.GameUi;

public sealed partial class LightingEditorController
{
    private const float MarkerDragThreshold = 4;

    public void ResetInput(KeyboardState keys, MouseState mouse)
    {
        keyboard.Reset(keys);
        previousKeys = keys;
        previousMouse = mouse;
    }

    public void Poll(KeyboardState keys, MouseState mouse, bool focused, int width, int height)
    {
        if (IsClosed)
        {
            return;
        }
        view.Resize(Math.Max(1, width), Math.Max(1, height));
        Vector2 previousPointer = pointer;
        pointer = LightingEditorViewport.WindowToDesign(new Vector2(mouse.X, mouse.Y), Math.Max(1, width), Math.Max(1, height));
        if (!focused || !wasFocused)
        {
            if (wasFocused)
            {
                focusPointer = previousPointer;
                markerPress = null;
                EndViewportPan();
                orbitingInspection = false;
            }
            else if (focused && OverlayVisible)
            {
                interactionPointerOffset += pointer - focusPointer;
                if (transform is { Dragging: true } && mouse.LeftButton == ButtonState.Released)
                {
                    FinishPointerTransform();
                }
            }
            wasFocused = focused;
            ResetInput(keys, mouse);
            return;
        }
        IReadOnlyList<LightingEditorCommand> commands = keyboard.Poll(keys, Shortcuts);
        bool leftPressed = mouse.LeftButton == ButtonState.Pressed && previousMouse.LeftButton == ButtonState.Released;
        bool leftReleased = mouse.LeftButton == ButtonState.Released && previousMouse.LeftButton == ButtonState.Pressed;
        bool rightPressed = mouse.RightButton == ButtonState.Pressed && previousMouse.RightButton == ButtonState.Released;
        KeyboardState oldKeys = previousKeys;
        MouseState oldMouse = previousMouse;
        previousKeys = keys;
        previousMouse = mouse;
        if (CapturingShortcut is not null && OverlayVisible)
        {
            CaptureKey(keys, oldKeys);
            if (leftPressed && LightingEditorLayout.SettingsClose.Contains(pointer.X, pointer.Y))
            {
                CapturingShortcut = null;
            }
            return;
        }
        if (commands.Contains(LightingEditorCommand.ToggleVisibility))
        {
            ToggleVisibility();
            if (OverlayVisible && transform is { Dragging: true } active && mouse.LeftButton == ButtonState.Released)
            {
                active.Dragging = false;
            }
            return;
        }
        if (!OverlayVisible)
        {
            return;
        }
        if (DiscardConfirmation || ShortcutsVisible || ColorPickerVisible)
        {
            if (leftPressed)
            {
                PointerPress(pointer);
            }
            return;
        }
        if (rightPressed)
        {
            markerPress = null;
            EndViewportPan();
            orbitingInspection = false;
            CancelTransform();
            CancelProperty();
            EndSlider(false);
            CancelReceiverPlacement();
            PickingPivot = false;
            return;
        }
        if (previousPanPointer is not null && HandleViewportInput(keys, mouse, oldMouse))
        {
            return;
        }
        if (orbitingInspection && HandleInspectionNavigation(keys, mouse, oldMouse, pointer, previousPointer))
        {
            return;
        }
        if (propertyEdit is not null)
        {
            ProcessTextKeys(keys, oldKeys, commands);
            if (leftPressed)
            {
                PointerPress(pointer);
            }
            return;
        }
        if (markerPress is { } press)
        {
            if (mouse.LeftButton == ButtonState.Released || SelectedId != press.LightId)
            {
                markerPress = null;
                return;
            }
            if (Vector2.DistanceSquared(pointer, press.Position) < MarkerDragThreshold * MarkerDragThreshold)
            {
                return;
            }
            markerPress = null;
            BeginTransform(LightingTransformTool.Translate, LightingAxis.None, true, press.Position);
        }
        foreach (LightingEditorCommand command in commands)
        {
            if (transform is not { Dragging: true } || command is LightingEditorCommand.AxisX or LightingEditorCommand.AxisY or LightingEditorCommand.AxisZ or LightingEditorCommand.Confirm or LightingEditorCommand.Undo)
            {
                Execute(command);
            }
        }
        if (transform is not null)
        {
            if (leftPressed && !transform.Dragging && (LightingEditorLayout.Header.Contains(pointer.X, pointer.Y) || LightingEditorLayout.CancelEdit.Contains(pointer.X, pointer.Y)))
            {
                PointerPress(pointer);
                return;
            }
            if (keys.IsKeyDown(Keys.Back) && oldKeys.IsKeyUp(Keys.Back) && transform.Numeric.Length > 0)
            {
                SetTransformNumeric(transform.Numeric[..^1]);
            }
            if (leftPressed && !transform.Dragging || leftReleased && transform.Dragging)
            {
                FinishPointerTransform();
            }
            else
            {
                UpdateTransform();
            }
            return;
        }
        if (slider is not null)
        {
            if (mouse.LeftButton == ButtonState.Pressed)
            {
                if (leftPressed)
                {
                    interactionPointerOffset += pointer - previousPointer;
                }
                UpdateSlider();
            }
            if (leftReleased)
            {
                EndSlider(true);
            }
            return;
        }
        if (HandleInspectionNavigation(keys, mouse, oldMouse, pointer, previousPointer) || HandleViewportInput(keys, mouse, oldMouse))
        {
            return;
        }
        if (mouse.ScrollWheelValue != oldMouse.ScrollWheelValue && LightingEditorLayout.Sources.Contains(pointer.X, pointer.Y))
        {
            ListOffset = Math.Clamp(ListOffset - Math.Sign(mouse.ScrollWheelValue - oldMouse.ScrollWheelValue), 0, Math.Max(0, Draft.Lights.Count - LightingEditorLayout.VisibleLights));
        }
        if (leftPressed)
        {
            PointerPress(pointer);
        }
    }

    public void Execute(LightingEditorCommand command)
    {
        if (command == LightingEditorCommand.ToggleVisibility)
        {
            ToggleVisibility();
            return;
        }
        if (!OverlayVisible || DiscardConfirmation || ShortcutsVisible || ColorPickerVisible || CapturingShortcut is not null)
        {
            return;
        }
        switch (command)
        {
            case LightingEditorCommand.AddLight:
                AddLight();
                break;
            case LightingEditorCommand.DeleteLight:
                DeleteSelected();
                break;
            case LightingEditorCommand.Translate:
                BeginTransform(LightingTransformTool.Translate);
                break;
            case LightingEditorCommand.Rotate:
                BeginTransform(LightingTransformTool.Rotate);
                break;
            case LightingEditorCommand.Orbit:
                BeginTransform(LightingTransformTool.Orbit);
                break;
            case LightingEditorCommand.AxisX:
                Constrain(LightingAxis.X);
                break;
            case LightingEditorCommand.AxisY:
                Constrain(LightingAxis.Y);
                break;
            case LightingEditorCommand.AxisZ:
                Constrain(LightingAxis.Z);
                break;
            case LightingEditorCommand.Confirm:
                CommitPendingEdit();
                break;
            case LightingEditorCommand.Undo:
                Undo();
                break;
            case LightingEditorCommand.Redo:
                Redo();
                break;
            case LightingEditorCommand.Save:
                Save();
                break;
        }
    }

    private void ProcessTextKeys(KeyboardState keys, KeyboardState oldKeys, IReadOnlyList<LightingEditorCommand> commands)
    {
        bool control = keys.IsKeyDown(Keys.LeftControl) || keys.IsKeyDown(Keys.RightControl);
        bool Pressed(Keys key)
        {
            return keys.IsKeyDown(key) && oldKeys.IsKeyUp(key);
        }
        if (commands.Contains(LightingEditorCommand.Save))
        {
            Save();
        }
        else if (commands.Contains(LightingEditorCommand.Confirm))
        {
            CommitProperty();
        }
        else if (commands.Contains(LightingEditorCommand.Undo))
        {
            UndoText(false);
        }
        else if (commands.Contains(LightingEditorCommand.Redo))
        {
            UndoText(true);
        }
        else if (control && Pressed(Keys.A))
        {
            selectAllText = true;
        }
        else if (control && (Pressed(Keys.C) || Pressed(Keys.X)) && propertyEdit is { } copy && selectAllText)
        {
            WriteClipboard?.Invoke(copy.Text);
            if (Pressed(Keys.X))
            {
                SetPropertyText("");
            }
        }
        else if (control && Pressed(Keys.V) && propertyEdit is { } paste && ReadClipboard is not null)
        {
            string text = ReadClipboard();
            if (paste.Property == LightingProperty.Name || text.All(IsNumericCharacter))
            {
                SetPropertyText((selectAllText ? "" : paste.Text) + text);
            }
        }
        else if ((Pressed(Keys.Back) || Pressed(Keys.Delete)) && propertyEdit is { } edit)
        {
            SetPropertyText(selectAllText ? "" : edit.Text.Length > 0 ? edit.Text[..^1] : "");
        }
    }

    internal void PointerPress(Vector2 position)
    {
        if (!OverlayVisible)
        {
            return;
        }
        bool Hit(UiRect rect)
        {
            return rect.Contains(position.X, position.Y);
        }
        if (DiscardConfirmation)
        {
            if (Hit(LightingEditorLayout.Discard))
            {
                DiscardAndClose();
            }
            else if (Hit(LightingEditorLayout.Continue))
            {
                ContinueEditing();
            }
            return;
        }
        if (ShortcutsVisible)
        {
            HandleShortcutPointer(position);
            return;
        }
        if (ColorPickerVisible)
        {
            for (int index = 0; index < ColorPalette.Length; index++)
            {
                if (Hit(LightingEditorLayout.ColorChoice(index)) && Selected is { } selected)
                {
                    UiColor color = ColorPalette[index];
                    ApplyCommand(ReplaceLight(Draft, selected with { Color = new Vector3(color.R, color.G, color.B) / 255 }));
                    ColorPickerVisible = false;
                    return;
                }
            }
            if (Hit(LightingEditorLayout.ColorClose))
            {
                ColorPickerVisible = false;
            }
            return;
        }
        if (Hit(LightingEditorLayout.Cancel))
        {
            RequestClose();
            return;
        }
        if (Hit(LightingEditorLayout.CancelEdit))
        {
            CancelProperty();
            CancelTransform();
            return;
        }
        if (!CommitPendingEdit())
        {
            return;
        }
        if (HandleReceiverInput(position, true) || TryPressInspectionTool(position))
        {
            return;
        }
        if (Hit(LightingEditorLayout.Save))
        {
            Save();
        }
        else if (Hit(LightingEditorLayout.Undo))
        {
            Undo();
        }
        else if (Hit(LightingEditorLayout.Redo))
        {
            Redo();
        }
        else if (Hit(LightingEditorLayout.Settings))
        {
            ShortcutsVisible = true;
        }
        else if (Hit(LightingEditorLayout.Add))
        {
            AddLight();
        }
        else if (Hit(LightingEditorLayout.AddFill))
        {
            AddFillLight();
        }
        else if (Hit(LightingEditorLayout.Delete))
        {
            DeleteSelected();
        }
        else if (Hit(LightingEditorLayout.Move))
        {
            SetTool(LightingTransformTool.Translate);
        }
        else if (Hit(LightingEditorLayout.Rotate))
        {
            SetTool(LightingTransformTool.Rotate);
        }
        else if (Hit(LightingEditorLayout.Orbit))
        {
            SetTool(LightingTransformTool.Orbit);
        }
        else if (Hit(LightingEditorLayout.Pivot))
        {
            PickingPivot = !PickingPivot;
            Message = "Pick a valid walkmesh ground point to reposition the scene pivot. Lamp data stays unchanged.";
        }
        else if (Hit(LightingEditorLayout.ResetView))
        {
            Viewport.Reset();
        }
        else if (Hit(LightingEditorLayout.Enabled))
        {
            ToggleEnabled();
        }
        else if (Hit(LightingEditorLayout.Primary))
        {
            TogglePrimary();
        }
        else if (Hit(LightingEditorLayout.ColorSwatch) && Selected is not null)
        {
            ColorPickerVisible = true;
        }
        else if (Hit(LightingEditorLayout.Sources))
        {
            for (int index = 0; index < LightingEditorLayout.VisibleLights; index++)
            {
                if (Hit(LightingEditorLayout.LightRow(index)) && ListOffset + index < Draft.Lights.Count)
                {
                    Select(Draft.Lights[ListOffset + index].Id);
                    break;
                }
            }
        }
        else if (Hit(LightingEditorLayout.Properties))
        {
            foreach (LightingProperty property in Enum.GetValues<LightingProperty>())
            {
                if (IsReceiverProperty(property) || Selected is null && property is not (LightingProperty.ShadowOpacity or LightingProperty.ShadowSoftness))
                {
                    continue;
                }
                if (Hit(LightingEditorLayout.Numeric(property)))
                {
                    BeginPropertyEdit(property);
                    break;
                }
                if (property >= LightingProperty.Red && Hit(LightingEditorLayout.Slider(property)))
                {
                    BeginSlider(property, position.X, LightingEditorLayout.Slider(property).Width);
                    break;
                }
            }
        }
        else if (!IsViewportPointer(position))
        {
            return;
        }
        else
        {
            if (!PickingPivot)
            {
                FieldSpotLight? nearest = null;
                float distanceSquared = 12 * 12;
                foreach (FieldSpotLight light in Draft.Lights.Reverse())
                {
                    if (project(light.Position) is not { } marker)
                    {
                        continue;
                    }
                    float distance = Vector2.DistanceSquared(marker, position);
                    if (distance < distanceSquared)
                    {
                        nearest = light;
                        distanceSquared = distance;
                    }
                }
                if (nearest is not null)
                {
                    Select(nearest.Id);
                    markerPress = (nearest.Id, position);
                    return;
                }
                if (HitGizmo(position) is { } gizmo)
                {
                    BeginTransform(gizmo.Tool, gizmo.Axis, true);
                    return;
                }
            }
            if (pickGround(position) is { } ground)
            {
                pickedGround = ground;
                if (PickingPivot)
                {
                    SetPivot(ground);
                    PickingPivot = false;
                }
            }
        }
    }

    private void HandleShortcutPointer(Vector2 position)
    {
        if (LightingEditorLayout.SettingsClose.Contains(position.X, position.Y))
        {
            ShortcutsVisible = false;
            CapturingShortcut = null;
            return;
        }
        if (LightingEditorLayout.Defaults.Contains(position.X, position.Y))
        {
            TryConfigureShortcuts(new LightingShortcutSettings());
            return;
        }
        LightingEditorCommand[] commands = Enum.GetValues<LightingEditorCommand>();
        for (int index = 0; index < commands.Length; index++)
        {
            if (LightingEditorLayout.ShortcutRow(index).Contains(position.X, position.Y))
            {
                CapturingShortcut = commands[index];
                Message = "Press the new shortcut. The captured chord will not execute an action.";
                return;
            }
            if (LightingEditorLayout.ShortcutClear(index).Contains(position.X, position.Y))
            {
                try
                {
                    TryConfigureShortcuts(Shortcuts.Rebind(commands[index], null));
                }
                catch (InvalidDataException exception)
                {
                    Message = exception.Message;
                }
                return;
            }
        }
    }

    private void CaptureKey(KeyboardState keys, KeyboardState previous)
    {
        foreach (Keys key in keys.GetPressedKeys())
        {
            if (previous.IsKeyDown(key) || key is Keys.LeftControl or Keys.RightControl or Keys.LeftShift or Keys.RightShift or Keys.LeftAlt or Keys.RightAlt)
            {
                continue;
            }
            AuthoringKeyChord chord = new(key, keys.IsKeyDown(Keys.LeftControl) || keys.IsKeyDown(Keys.RightControl), keys.IsKeyDown(Keys.LeftShift) || keys.IsKeyDown(Keys.RightShift), keys.IsKeyDown(Keys.LeftAlt) || keys.IsKeyDown(Keys.RightAlt));
            try
            {
                if (TryConfigureShortcuts(Shortcuts.Rebind(CapturingShortcut!.Value, chord)))
                {
                    CapturingShortcut = null;
                }
            }
            catch (InvalidDataException exception)
            {
                Message = exception.Message;
            }
            return;
        }
    }

    private bool TryConfigureShortcuts(LightingShortcutSettings settings)
    {
        foreach ((LightingEditorCommand command, AuthoringKeyChord chord) in settings.Bindings)
        {
            if (ShortcutConflict?.Invoke(command, chord) is { } conflict)
            {
                Message = conflict;
                return false;
            }
        }
        ConfigureShortcuts(settings);
        Message = "Lighting shortcuts saved to user preferences.";
        return true;
    }
}
