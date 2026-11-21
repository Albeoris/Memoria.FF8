using System.Globalization;
using System.Numerics;
using Memoria.FF8.Mods;

namespace Memoria.FF8.GameUi;

public enum LightingProperty
{
    Name,
    PositionX,
    PositionY,
    PositionZ,
    DirectionX,
    DirectionY,
    DirectionZ,
    Red,
    Green,
    Blue,
    Intensity,
    Range,
    InnerCone,
    OuterCone,
    ShadowOpacity,
    ShadowSoftness,
    ReceiverX,
    ReceiverY,
    ReceiverZ,
    ReceiverWidth,
    ReceiverExtent,
    ReceiverHeading
}

public sealed partial class LightingEditorController
{
    private sealed class LightingPropertyEdit(LightingProperty property, string text)
    {
        public LightingProperty Property { get; } = property;
        public string InitialText { get; } = text;
        public string Text { get; set; } = text;
    }

    private sealed record LightingSliderDrag(FieldLightingScene Before, LightingProperty Property, float Value, float StartX, float Span);

    public Func<string>? ReadClipboard { get; set; }
    public Action<string>? WriteClipboard { get; set; }

    public void BeginPropertyEdit(LightingProperty property)
    {
        if (!CommitPendingEdit() || !CanEditProperty(property))
        {
            return;
        }
        propertyEdit = new LightingPropertyEdit(property, PropertyText(property));
        selectAllText = true;
        textUndo.Clear();
        textRedo.Clear();
    }

    public void SetPropertyText(string value)
    {
        if (propertyEdit is { } edit)
        {
            textUndo.Push(edit.Text);
            textRedo.Clear();
            edit.Text = value;
            selectAllText = false;
        }
    }

    public void OnTextInput(char character)
    {
        if (!OverlayVisible || !wasFocused || IsClosed || DiscardConfirmation || ShortcutsVisible || ColorPickerVisible || CapturingShortcut is not null || char.IsControl(character))
        {
            return;
        }
        if (propertyEdit is { } edit)
        {
            if (edit.Property == LightingProperty.Name || IsNumericCharacter(character))
            {
                SetPropertyText((selectAllText ? "" : edit.Text) + character);
            }
        }
        else if (transform is { } active && IsNumericCharacter(character))
        {
            SetTransformNumeric(active.Numeric + character);
        }
    }

    public bool CommitProperty()
    {
        if (propertyEdit is not { } edit)
        {
            return true;
        }
        try
        {
            FieldLightingScene candidate;
            if (edit.Property == LightingProperty.Name)
            {
                if (Selected is not { } selected)
                {
                    return false;
                }
                candidate = ReplaceLight(Draft, selected with { Name = edit.Text.Trim() });
            }
            else
            {
                if (!float.TryParse(edit.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out float value) || !float.IsFinite(value))
                {
                    Message = "Enter a finite number. The pending value is retained for correction.";
                    return false;
                }
                candidate = WithProperty(Draft, edit.Property, value);
            }
            FieldLightingSceneIO.Validate(candidate);
            propertyEdit = null;
            textUndo.Clear();
            textRedo.Clear();
            selectAllText = false;
            ApplyCommand(candidate);
            return true;
        }
        catch (InvalidDataException exception)
        {
            Message = exception.Message;
            return false;
        }
    }

    public void CancelProperty()
    {
        propertyEdit = null;
        selectAllText = false;
        textUndo.Clear();
        textRedo.Clear();
    }

    public string PropertyText(LightingProperty property)
    {
        if (propertyEdit?.Property == property)
        {
            return propertyEdit.Text;
        }
        if (property == LightingProperty.Name)
        {
            return Selected?.Name ?? "";
        }
        return PropertyValue(property).ToString("0.###", CultureInfo.InvariantCulture);
    }

    internal float PropertyValue(LightingProperty property)
    {
        FieldLight? light = Selected;
        return property switch
        {
            LightingProperty.PositionX => light?.Position.X ?? 0,
            LightingProperty.PositionY => light?.Position.Y ?? 0,
            LightingProperty.PositionZ => light?.Position.Z ?? 0,
            LightingProperty.DirectionX => light?.Direction.X ?? 0,
            LightingProperty.DirectionY => light?.Direction.Y ?? 0,
            LightingProperty.DirectionZ => light?.Direction.Z ?? -1,
            LightingProperty.Red => (light?.Color.X ?? 1) * 255,
            LightingProperty.Green => (light?.Color.Y ?? 1) * 255,
            LightingProperty.Blue => (light?.Color.Z ?? 1) * 255,
            LightingProperty.Intensity => light?.Intensity ?? 1,
            LightingProperty.Range => light?.Range ?? actorScale * 12,
            LightingProperty.InnerCone => light?.InnerConeDegrees ?? 35,
            LightingProperty.OuterCone => light?.OuterConeDegrees ?? 65,
            LightingProperty.ShadowOpacity => Draft.GroundShadow.Opacity,
            LightingProperty.ShadowSoftness => Draft.GroundShadow.Softness,
            LightingProperty.ReceiverX => SelectedReceiver?.Center.X ?? 0,
            LightingProperty.ReceiverY => SelectedReceiver?.Center.Y ?? 0,
            LightingProperty.ReceiverZ => SelectedReceiver?.Center.Z ?? 0,
            LightingProperty.ReceiverWidth => SelectedReceiver?.Width ?? 0,
            LightingProperty.ReceiverExtent => SelectedReceiver?.Extent ?? 0,
            LightingProperty.ReceiverHeading => SelectedReceiver?.HeadingDegrees ?? 0,
            _ => 0
        };
    }

    internal (float Min, float Max) PropertyRange(LightingProperty property)
    {
        return property switch
        {
            LightingProperty.Red or LightingProperty.Green or LightingProperty.Blue => (0, 255),
            LightingProperty.Intensity => (0, Math.Max(8, Selected?.Intensity ?? 1)),
            LightingProperty.Range => (1, Math.Max(actorScale * 30, Selected?.Range ?? 1)),
            LightingProperty.InnerCone => (0, Math.Max(0, (Selected?.OuterConeDegrees ?? 85) - 0.1f)),
            LightingProperty.OuterCone => (Math.Min(85, (Selected?.InnerConeDegrees ?? 0) + 0.1f), 85),
            LightingProperty.ShadowOpacity => (0, 1),
            LightingProperty.ShadowSoftness => (0, 3),
            _ => (-actorScale * 20, actorScale * 20)
        };
    }

    private FieldLightingScene WithProperty(FieldLightingScene scene, LightingProperty property, float value)
    {
        if (IsReceiverProperty(property) && SelectedReceiver is { } receiver)
        {
            FieldShadowReceiver editedReceiver = property switch
            {
                LightingProperty.ReceiverX => receiver with { Center = receiver.Center with { X = value } },
                LightingProperty.ReceiverY => receiver with { Center = receiver.Center with { Y = value } },
                LightingProperty.ReceiverZ => receiver with { Center = receiver.Center with { Z = value } },
                LightingProperty.ReceiverWidth => receiver with { Width = value },
                LightingProperty.ReceiverExtent => receiver with { Extent = value, Center = receiver.Kind == "wall" ? receiver.Center + Vector3.UnitZ * ((value - receiver.Extent) * 0.5f) : receiver.Center },
                LightingProperty.ReceiverHeading => receiver with { HeadingDegrees = value },
                _ => throw new ArgumentOutOfRangeException(nameof(property))
            };
            return ReplaceReceiver(scene, editedReceiver);
        }
        if (property == LightingProperty.ShadowOpacity)
        {
            return scene with { GroundShadow = scene.GroundShadow with { Opacity = value } };
        }
        if (property == LightingProperty.ShadowSoftness)
        {
            return scene with { GroundShadow = scene.GroundShadow with { Softness = value } };
        }
        FieldLight light = scene.Lights.First(item => item.Id == SelectedId);
        FieldLight edited = property switch
        {
            LightingProperty.PositionX => light with { Position = light.Position with { X = value } },
            LightingProperty.PositionY => light with { Position = light.Position with { Y = value } },
            LightingProperty.PositionZ => light with { Position = light.Position with { Z = value } },
            LightingProperty.DirectionX => light with { Direction = NormalizeDirection(light.Direction with { X = value }) },
            LightingProperty.DirectionY => light with { Direction = NormalizeDirection(light.Direction with { Y = value }) },
            LightingProperty.DirectionZ => light with { Direction = NormalizeDirection(light.Direction with { Z = value }) },
            LightingProperty.Red => light with { Color = light.Color with { X = value / 255 } },
            LightingProperty.Green => light with { Color = light.Color with { Y = value / 255 } },
            LightingProperty.Blue => light with { Color = light.Color with { Z = value / 255 } },
            LightingProperty.Intensity => light with { Intensity = value },
            LightingProperty.Range => light with { Range = value },
            LightingProperty.InnerCone => light with { InnerConeDegrees = value },
            LightingProperty.OuterCone => light with { OuterConeDegrees = value },
            _ => light
        };
        return ReplaceLight(scene, edited);
    }

    private static bool IsReceiverProperty(LightingProperty property)
    {
        return property is >= LightingProperty.ReceiverX and <= LightingProperty.ReceiverHeading;
    }

    private bool CanEditProperty(LightingProperty property)
    {
        return property switch
        {
            >= LightingProperty.ReceiverX and <= LightingProperty.ReceiverHeading => SelectedReceiver is not null,
            LightingProperty.ShadowOpacity or LightingProperty.ShadowSoftness => true,
            >= LightingProperty.PositionX and <= LightingProperty.PositionZ or >= LightingProperty.Range and <= LightingProperty.OuterCone => Selected is { IsSunlight: false },
            _ => Selected is not null
        };
    }

    private static Vector3 NormalizeDirection(Vector3 direction)
    {
        if (direction.LengthSquared() < 0.000001f)
        {
            throw new InvalidDataException("Aim direction cannot be zero.");
        }
        return Vector3.Normalize(direction);
    }

    private bool CommitPendingEdit()
    {
        if (!CommitProperty() || !ConfirmTransform())
        {
            return false;
        }
        EndSlider(true);
        return true;
    }

    private void BeginSlider(LightingProperty property, float x, float span)
    {
        if (!CommitPendingEdit() || !CanEditProperty(property))
        {
            return;
        }
        interactionPointerOffset = Vector2.Zero;
        slider = new LightingSliderDrag(Draft, property, PropertyValue(property), x, span);
    }

    private void UpdateSlider()
    {
        if (slider is not { } active)
        {
            return;
        }
        (float min, float max) = PropertyRange(active.Property);
        float value = Math.Clamp(active.Value + (pointer.X - interactionPointerOffset.X - active.StartX) / active.Span * (max - min), min, max);
        FieldLightingScene candidate = WithProperty(active.Before, active.Property, value);
        FieldLightingSceneIO.Validate(candidate);
        SetDraft(candidate);
    }

    private void EndSlider(bool commit)
    {
        if (slider is not { } active)
        {
            return;
        }
        slider = null;
        interactionPointerOffset = Vector2.Zero;
        if (commit)
        {
            History.Commit(Draft);
        }
        else
        {
            SetDraft(active.Before);
        }
    }

    private void UndoText(bool redo)
    {
        Stack<string> source = redo ? textRedo : textUndo;
        Stack<string> target = redo ? textUndo : textRedo;
        if (propertyEdit is { } edit && source.TryPop(out string? value))
        {
            target.Push(edit.Text);
            edit.Text = value;
            selectAllText = false;
        }
        else if (transform is { } active && source.TryPop(out string? numeric))
        {
            target.Push(active.Numeric);
            active.Numeric = numeric;
            UpdateTransform();
        }
    }

    private static bool IsNumericCharacter(char value)
    {
        return char.IsAsciiDigit(value) || value is '.' or '-' or '+' or 'e' or 'E';
    }
}
