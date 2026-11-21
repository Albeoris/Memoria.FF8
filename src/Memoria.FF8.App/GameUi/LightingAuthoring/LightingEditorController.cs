using System.Globalization;
using System.Numerics;
using Memoria.FF8.Mods;
using Memoria.FF8.Ui;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8.GameUi;

/// <summary>A private current-field draft. The host owns pause, scene identity, persistence and preview publication.</summary>
public sealed partial class LightingEditorController
{
    public const float DesignWidth = 1280;
    public const float DesignHeight = 720;
    private readonly Func<FieldLightingScene, string?> save;
    private readonly Action close;
    private readonly UiView view;
    private readonly LightingEditorKeyboard keyboard = new();
    private readonly float actorScale;
    private Func<Vector3, Vector2?> project = _ => null;
    private Func<Vector2, Vector3?> pickGround = _ => null;
    private Func<Vector2, LightingEditorRay?> screenRay = _ => null;
    private KeyboardState previousKeys;
    private MouseState previousMouse;
    private Vector2 pointer;
    private Vector2 hiddenPointer;
    private Vector2 focusPointer;
    private Vector2 interactionPointerOffset;
    private bool wasFocused = true;
    private Action? afterClose;
    private Vector3? pickedGround;
    private (string LightId, Vector2 Position)? markerPress;
    private LightingTransform? transform;
    private LightingTransformTool spotTool;
    private LightingPropertyEdit? propertyEdit;
    private LightingSliderDrag? slider;
    private bool selectAllText;
    private readonly Stack<string> textUndo = [];
    private readonly Stack<string> textRedo = [];
    private bool dirty;

    public LightingEditorController(FieldLightingScene scene, string modName, Vector3 pivot, float actorScale, Func<FieldLightingScene, string?> save, Action close, LightingShortcutSettings? shortcuts = null)
    {
        FieldLightingSceneIO.Validate(scene);
        Draft = FieldLightingSceneIO.Clone(scene);
        ModName = modName;
        Pivot = pivot;
        this.actorScale = float.IsFinite(actorScale) && actorScale > 0 ? actorScale : 100;
        this.save = save;
        this.close = close;
        Shortcuts = shortcuts ?? new LightingShortcutSettings();
        Shortcuts.Validate();
        History = new LightingEditorHistory(Draft);
        view = new UiView(new LightingEditorElement(this)) { DesignWidth = DesignWidth, DesignHeight = DesignHeight };
        view.Resize(DesignWidth, DesignHeight);
    }

    public event Action<FieldLightingScene>? DraftChanged;
    public event Action<LightingShortcutSettings>? SettingsChanged;
    public Func<LightingEditorCommand, AuthoringKeyChord, string?>? ShortcutConflict { get; set; }
    public FieldLightingScene Draft { get; private set; }
    public string ModName { get; }
    public LightingEditorHistory History { get; }
    public LightingShortcutSettings Shortcuts { get; private set; }
    public string? SelectedId { get; private set; }
    public FieldLight? Selected => Draft.Lights.FirstOrDefault(light => light.Id == SelectedId);
    public Vector3 Pivot { get; private set; }
    public bool OverlayVisible { get; private set; } = true;
    public bool IsDirty => dirty || propertyEdit is { } edit && edit.Text != edit.InitialText || transform is { Numeric.Length: > 0 };
    public bool IsClosed { get; private set; }
    public bool DiscardConfirmation { get; private set; }
    public bool ShortcutsVisible { get; private set; }
    public bool ColorPickerVisible { get; private set; }
    public LightingEditorCommand? CapturingShortcut { get; private set; }
    public LightingTransformTool ActiveTool => Selected is { IsSunlight: true } ? LightingTransformTool.Rotate : spotTool;
    public LightingAxis ActiveAxis => transform?.Axis ?? LightingAxis.None;
    public bool HasPendingTransform => transform is not null;
    internal bool IsDraggingLight => OverlayVisible && wasFocused && transform is { Dragging: true };
    public string NumericInput => transform?.Numeric ?? propertyEdit?.Text ?? "";
    public LightingProperty? EditingProperty => propertyEdit?.Property;
    public string Message { get; private set; } = "Insert adds a spotlight. No lamps means no model illumination.";
    public string RendererStatus { get; set; } = "";
    public string? SelectionLocationNotice
    {
        get
        {
            if (Selected is not { IsSunlight: false } light)
            {
                return null;
            }
            Vector2? location = project(light.Position);
            if (location is not { } point || point.X < 0 || point.X > DesignWidth || point.Y < 0 || point.Y > DesignHeight)
            {
                return "Selected light is outside this camera view.\nEdit Position, or use " + Shortcuts.Describe(LightingEditorCommand.Translate) + " with an axis and a signed distance.";
            }
            if (LightingEditorLayout.Sources.Contains(point.X, point.Y) || LightingEditorLayout.Properties.Contains(point.X, point.Y) || LightingEditorLayout.Header.Contains(point.X, point.Y) || LightingEditorLayout.Footer.Contains(point.X, point.Y))
            {
                return "Selected light is behind an editor panel.\nEdit Position, or use " + Shortcuts.Describe(LightingEditorCommand.Translate) + " with an axis and a signed distance.";
            }
            return null;
        }
    }
    public bool PickingPivot { get; private set; }
    internal int ListOffset { get; private set; }
    internal Vector2 PointerPosition => pointer;
    internal bool TextSelected => selectAllText;
    internal LightingGizmoSegment? HoveredGizmo => HitGizmo(pointer);

    public UiView BuildView()
    {
        return view;
    }

    /// <summary>All screen points use the same 1280x720 fitted design coordinates as BuildView.</summary>
    public void SetProjection(Func<Vector3, Vector2?> projector, Func<Vector2, Vector3?> groundPicker, Func<Vector2, LightingEditorRay?> ray)
    {
        project = projector;
        pickGround = groundPicker;
        screenRay = ray;
    }

    public void ConfigureShortcuts(LightingShortcutSettings settings)
    {
        settings.Validate();
        Shortcuts = settings;
        keyboard.Reset(previousKeys);
        SettingsChanged?.Invoke(settings);
    }

    public void Select(string? id)
    {
        if (!CommitPendingEdit())
        {
            return;
        }
        SelectedId = Draft.Lights.Any(light => light.Id == id) ? id : null;
        int index = Draft.Lights.ToList().FindIndex(light => light.Id == SelectedId);
        if (index >= 0)
        {
            ListOffset = Math.Clamp(index - 5, 0, Math.Max(0, Draft.Lights.Count - LightingEditorLayout.VisibleLights));
        }
    }

    public void SetPivot(Vector3 position)
    {
        if (float.IsFinite(position.X) && float.IsFinite(position.Y) && float.IsFinite(position.Z))
        {
            Pivot = position;
            pickedGround = position;
        }
    }

    public void AddLight()
    {
        AddLight(fill: false);
    }

    public void AddFillLight()
    {
        AddLight(fill: true);
    }

    public void AddSunlight()
    {
        AddLight(new FieldLight
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = "Sunlight",
            Type = FieldLight.SunlightType,
            Direction = Vector3.Normalize(new Vector3(0.45f, 0.3f, -1))
        }, primaryIfFirst: true);
    }

    private void AddLight(bool fill)
    {
        Vector3 ground = pickedGround ?? Pivot;
        Vector3 position = ground + (fill ? new Vector3(1, -1, 1) : Vector3.UnitZ * 1.5f) * actorScale;
        AddLight(new FieldLight
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = fill ? "Fill light" : "Spotlight",
            Position = position,
            Direction = fill ? Vector3.Normalize(ground + Vector3.UnitZ * actorScale * 0.6f - position) : -Vector3.UnitZ,
            Intensity = fill ? 0.35f : 1,
            Range = actorScale * 6,
            InnerConeDegrees = fill ? 55 : 35,
            OuterConeDegrees = fill ? 80 : 65
        }, primaryIfFirst: !fill);
    }

    private void AddLight(FieldLight light, bool primaryIfFirst)
    {
        if (!CommitPendingEdit())
        {
            return;
        }
        if (Draft.Lights.Count >= FieldLightingSceneIO.MaximumLights)
        {
            Message = "This field has reached the document's lamp-entry limit.";
            return;
        }
        bool enabled = Draft.Lights.Count(existing => existing.Enabled) < 8;
        string kind = light.Name;
        string name = kind + " " + (Draft.Lights.Count + 1).ToString(CultureInfo.InvariantCulture);
        light = light with { Name = name, Enabled = enabled };
        ApplyCommand(Draft with { Lights = [.. Draft.Lights, light], PrimaryLightId = primaryIfFirst && Draft.Lights.Count == 0 ? light.Id : Draft.PrimaryLightId });
        SelectedId = light.Id;
        ListOffset = Math.Max(0, Draft.Lights.Count - LightingEditorLayout.VisibleLights);
        Message = enabled ? kind + (light.IsSunlight ? " added. Use Aim to change its direction." : " added. Drag its marker to move it.") : "Eight enabled lamps is the limit. Disable another lamp to enable the new source.";
    }

    public void DeleteSelected()
    {
        if (!CommitPendingEdit() || Selected is not { } selected)
        {
            return;
        }
        ApplyCommand(Draft with { Lights = Draft.Lights.Where(light => light.Id != selected.Id).ToArray(), PrimaryLightId = Draft.PrimaryLightId == selected.Id ? null : Draft.PrimaryLightId });
        SelectedId = null;
        ListOffset = Math.Min(ListOffset, Math.Max(0, Draft.Lights.Count - LightingEditorLayout.VisibleLights));
    }

    public void ToggleEnabled()
    {
        if (!CommitPendingEdit() || Selected is not { } selected)
        {
            return;
        }
        if (!selected.Enabled && Draft.Lights.Count(light => light.Enabled) >= 8)
        {
            Message = "At most eight scene lamps can be enabled. Disable another lamp first.";
            return;
        }
        ApplyCommand(ReplaceLight(Draft, selected with { Enabled = !selected.Enabled }));
    }

    public void TogglePrimary()
    {
        if (CommitPendingEdit() && Selected is { } selected)
        {
            ApplyCommand(Draft with { PrimaryLightId = Draft.PrimaryLightId == selected.Id ? null : selected.Id });
        }
    }

    public void Undo()
    {
        if (transform is { Dragging: true })
        {
            CancelTransform();
            return;
        }
        if (propertyEdit is not null || transform is not null && (transform.Numeric.Length > 0 || textUndo.Count > 0 || textRedo.Count > 0))
        {
            UndoText(false);
            return;
        }
        CancelTransform();
        EndSlider(false);
        SetDraft(History.Undo());
    }

    public void Redo()
    {
        if (propertyEdit is not null || transform is not null && (transform.Numeric.Length > 0 || textUndo.Count > 0 || textRedo.Count > 0))
        {
            UndoText(true);
            return;
        }
        CancelTransform();
        EndSlider(false);
        SetDraft(History.Redo());
    }

    public bool Save()
    {
        if (!CommitPendingEdit())
        {
            return false;
        }
        try
        {
            FieldLightingSceneIO.Validate(Draft);
            string? error = save(FieldLightingSceneIO.Clone(Draft));
            if (error is not null)
            {
                Message = error;
                return false;
            }
            History.MarkSaved(Draft);
            dirty = false;
            Message = "Saved this field. The editor remains frozen; Cancel closes it. Authoring stays enabled.";
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or System.Text.Json.JsonException)
        {
            Message = "Save failed: " + exception.Message;
            return false;
        }
    }

    public void RequestClose(Action? afterClose = null)
    {
        this.afterClose = afterClose;
        if (IsDirty)
        {
            DiscardConfirmation = true;
        }
        else
        {
            CompleteClose();
        }
    }

    public void ContinueEditing()
    {
        DiscardConfirmation = false;
        afterClose = null;
    }

    public void DiscardAndClose()
    {
        if (DiscardConfirmation)
        {
            CompleteClose();
        }
    }

    public void ToggleVisibility()
    {
        EndViewportPan();
        orbitingInspection = false;
        markerPress = null;
        if (OverlayVisible)
        {
            hiddenPointer = pointer;
        }
        else
        {
            interactionPointerOffset += pointer - hiddenPointer;
        }
        OverlayVisible = !OverlayVisible;
    }

    private void CompleteClose()
    {
        IsClosed = true;
        DiscardConfirmation = false;
        close();
        afterClose?.Invoke();
        afterClose = null;
    }

    private void ApplyCommand(FieldLightingScene scene)
    {
        FieldLightingSceneIO.Validate(scene);
        SetDraft(scene);
        History.Commit(scene);
    }

    private void SetDraft(FieldLightingScene scene)
    {
        Draft = scene;
        dirty = History.IsDirty(scene);
        DraftChanged?.Invoke(scene);
    }

    private static FieldLightingScene ReplaceLight(FieldLightingScene scene, FieldLight light)
    {
        return scene with { Lights = scene.Lights.Select(existing => existing.Id == light.Id ? light : existing).ToArray() };
    }
}
