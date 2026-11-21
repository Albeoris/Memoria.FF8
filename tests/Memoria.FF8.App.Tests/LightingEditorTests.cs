using System.Numerics;
using Memoria.FF8.GameUi;
using Memoria.FF8.Mods;
using Memoria.FF8.Ui;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8.App.Tests;

public sealed class LightingEditorTests
{
    [Fact]
    public void Opening_selection_visibility_and_editor_pivot_do_not_create_sources_or_dirty_document()
    {
        LightingEditorController editor = Create();
        Assert.Empty(editor.Draft.Lights);
        editor.SetPivot(new(17, 23, -40));
        editor.Select(null);
        editor.ToggleVisibility();
        Assert.False(editor.OverlayVisible);
        Assert.False(editor.IsDirty);
        Assert.Equal(0, editor.History.CommandCount);
    }

    [Fact]
    public void Save_keeps_history_and_undo_can_cross_its_checkpoint_without_rewriting_saved_scene()
    {
        FieldLightingScene? saved = null;
        int saves = 0;
        LightingEditorController editor = Create(scene =>
        {
            saved = scene;
            saves++;
            return null;
        });
        editor.AddLight();
        string firstId = editor.SelectedId!;
        Assert.Equal(firstId, editor.Draft.PrimaryLightId);
        Assert.True(editor.Save());
        Assert.False(editor.IsDirty);
        editor.AddLight();
        editor.TogglePrimary();
        Assert.NotEqual(firstId, editor.Draft.PrimaryLightId);
        editor.Undo();
        Assert.Equal(firstId, editor.Draft.PrimaryLightId);
        editor.Undo();
        Assert.False(editor.IsDirty);
        editor.Undo();
        Assert.True(editor.IsDirty);
        Assert.Empty(editor.Draft.Lights);
        Assert.Single(saved!.Lights);
        Assert.Equal(1, saves);
        editor.Redo();
        Assert.False(editor.IsDirty);
    }

    [Fact]
    public void Signed_axis_transforms_change_real_position_and_aim_and_orbit_rotates_both()
    {
        LightingEditorController editor = Create();
        editor.AddLight();
        FieldLight original = editor.Selected!;
        editor.BeginTransform(LightingTransformTool.Translate, LightingAxis.Z);
        editor.SetTransformNumeric("-500");
        Assert.True(editor.ConfirmTransform());
        Assert.Equal(-350, editor.Selected!.Position.Z);
        Assert.Equal(original.Direction, editor.Selected.Direction);
        editor.BeginTransform(LightingTransformTool.Rotate, LightingAxis.Y);
        editor.SetTransformNumeric("90");
        editor.ConfirmTransform();
        AssertVector(new(-1, 0, 0), editor.Selected.Direction);
        Assert.Equal(-350, editor.Selected.Position.Z);
        editor.SetPivot(new(10, 0, 0));
        editor.BeginTransform(LightingTransformTool.Orbit, LightingAxis.Y);
        editor.SetTransformNumeric("90");
        editor.ConfirmTransform();
        AssertVector(new(-340, 0, 10), editor.Selected.Position);
        AssertVector(Vector3.UnitZ, editor.Selected.Direction);
        editor.Undo();
        AssertVector(new(0, 0, -350), editor.Selected.Position);
        AssertVector(new(-1, 0, 0), editor.Selected.Direction);
    }

    [Fact]
    public void Invalid_numeric_edit_blocks_save_retains_input_and_does_not_change_document()
    {
        int saves = 0;
        LightingEditorController editor = Create(_ =>
        {
            saves++;
            return null;
        });
        editor.AddLight();
        float range = editor.Selected!.Range;
        editor.BeginPropertyEdit(LightingProperty.Range);
        editor.SetPropertyText("-1");
        Assert.False(editor.Save());
        Assert.Equal("-1", editor.NumericInput);
        Assert.Equal(range, editor.Selected.Range);
        Assert.Equal(0, saves);
        editor.SetPropertyText("2400");
        Assert.True(editor.Save());
        Assert.Equal(2400, editor.Selected.Range);
        Assert.False(editor.IsDirty);
    }

    [Fact]
    public void Save_conflict_preserves_edit_history_and_draft_then_cancel_discards_only_after_confirmation()
    {
        bool closed = false;
        bool continued = false;
        LightingEditorController editor = Create(_ => "The lighting file changed externally.", () => closed = true);
        editor.AddLight();
        Assert.False(editor.Save());
        Assert.True(editor.IsDirty);
        Assert.Contains("externally", editor.Message);
        editor.RequestClose(() => continued = true);
        Assert.True(editor.DiscardConfirmation);
        Assert.False(closed);
        editor.ToggleVisibility();
        editor.Execute(LightingEditorCommand.Confirm);
        Assert.False(closed);
        editor.ToggleVisibility();
        editor.ContinueEditing();
        Assert.False(editor.DiscardConfirmation);
        editor.RequestClose(() => continued = true);
        editor.DiscardAndClose();
        Assert.True(closed);
        Assert.True(continued);
    }

    [Fact]
    public void Primary_deletion_clears_role_and_undo_restores_stable_identity_and_role()
    {
        LightingEditorController editor = Create();
        editor.AddLight();
        string primary = editor.SelectedId!;
        editor.AddLight();
        editor.Select(primary);
        editor.ToggleEnabled();
        Assert.Equal(primary, editor.Draft.PrimaryLightId);
        Assert.False(editor.Selected!.Enabled);
        editor.DeleteSelected();
        Assert.Null(editor.Draft.PrimaryLightId);
        editor.Undo();
        Assert.Equal(primary, editor.Draft.PrimaryLightId);
        Assert.Contains(editor.Draft.Lights, light => light.Id == primary && !light.Enabled);
    }

    [Fact]
    public void Eight_enabled_lamp_budget_keeps_additional_entries_disabled_and_reports_limit()
    {
        LightingEditorController editor = Create();
        for (int index = 0; index < 9; index++)
        {
            editor.AddLight();
        }
        Assert.Equal(9, editor.Draft.Lights.Count);
        Assert.Equal(8, editor.Draft.Lights.Count(light => light.Enabled));
        Assert.False(editor.Selected!.Enabled);
        editor.ToggleEnabled();
        Assert.False(editor.Selected.Enabled);
        Assert.Contains("eight", editor.Message);
    }

    [Fact]
    public void Held_insert_and_delete_are_press_triggered_and_text_delete_never_deletes_a_lamp()
    {
        LightingEditorController editor = Create();
        Poll(editor, new(Keys.Insert));
        Poll(editor, new(Keys.Insert));
        Assert.Single(editor.Draft.Lights);
        Poll(editor, new());
        editor.BeginPropertyEdit(LightingProperty.Name);
        Poll(editor, new(Keys.Delete));
        Assert.Single(editor.Draft.Lights);
        Assert.Equal("", editor.NumericInput);
        editor.OnTextInput('A');
        editor.OnTextInput('B');
        Poll(editor, new());
        Poll(editor, new(Keys.LeftControl, Keys.Z));
        Assert.Equal("A", editor.NumericInput);
        Assert.Single(editor.Draft.Lights);
        editor.CancelProperty();
        Poll(editor, new());
        Poll(editor, new(Keys.Delete));
        Poll(editor, new(Keys.Delete));
        Assert.Empty(editor.Draft.Lights);
        Assert.Equal(2, editor.History.CommandCount);
    }

    [Fact]
    public void One_gizmo_drag_is_one_command_and_gizmos_win_over_ground_picking()
    {
        LightingEditorController editor = Create();
        int groundPicks = 0;
        editor.SetProjection(Project, _ =>
        {
            groundPicks++;
            return Vector3.Zero;
        }, Ray);
        editor.AddLight();
        Poll(editor, new(), 675, 360, true);
        Assert.True(editor.HasPendingTransform);
        Assert.Equal(LightingAxis.X, editor.ActiveAxis);
        Poll(editor, new(), 695, 360, true);
        Poll(editor, new(), 715, 360, true);
        Assert.Equal(40, editor.Selected!.Position.X);
        Poll(editor, new(), 715, 360);
        Assert.False(editor.HasPendingTransform);
        Assert.Equal(2, editor.History.CommandCount);
        Assert.Equal(0, groundPicks);
        editor.Undo();
        Assert.Equal(0, editor.Selected.Position.X);
    }

    [Fact]
    public void Source_marker_click_selects_and_drag_hides_panels_until_one_world_space_edit_finishes()
    {
        LightingEditorController editor = Create();
        editor.AddLight();
        string source = editor.SelectedId!;
        editor.Select(null);
        Poll(editor, new(), 640, 360, true);
        Poll(editor, new(), 642, 361, true);
        Assert.Equal(source, editor.SelectedId);
        Assert.False(editor.IsDraggingLight);
        Poll(editor, new(), 642, 361);
        Assert.Equal(1, editor.History.CommandCount);

        Poll(editor, new(), 640, 360, true);
        Poll(editor, new(), 665, 345, true);
        Assert.True(editor.IsDraggingLight);
        Assert.True(editor.OverlayVisible);
        Assert.Equal(LightingAxis.None, editor.ActiveAxis);
        AssertVector(new(25, 15, 150), editor.Selected!.Position);
        RecordingCanvas canvas = new();
        editor.BuildView().Draw(canvas, new UiTheme());
        Assert.DoesNotContain(canvas.Texts, text => text.StartsWith("FIELD LIGHTS", StringComparison.Ordinal));
        Assert.Contains(editor.Selected.Name, canvas.Texts);
        Assert.True(canvas.LineCount > 100);

        Poll(editor, new(), 1120, 300);
        Assert.False(editor.HasPendingTransform);
        Assert.False(editor.IsDraggingLight);
        Assert.Null(editor.EditingProperty);
        AssertVector(new(480, 60, 150), editor.Selected.Position);
        Assert.Equal(2, editor.History.CommandCount);
        canvas.Texts.Clear();
        editor.BuildView().Draw(canvas, new UiTheme());
        Assert.Contains(canvas.Texts, text => text.StartsWith("FIELD LIGHTS", StringComparison.Ordinal));
        editor.Undo();
        AssertVector(new(0, 0, 150), editor.Selected.Position);
    }

    [Fact]
    public void Source_marker_drag_can_be_cancelled_and_focus_loss_preserves_last_preview()
    {
        LightingEditorController editor = Create();
        editor.AddLight();
        Poll(editor, new(), 640, 360, true);
        Poll(editor, new(), 665, 345, true);
        MouseState cancel = new(670, 345, 0, ButtonState.Pressed, ButtonState.Released, ButtonState.Pressed, ButtonState.Released, ButtonState.Released);
        editor.Poll(new(), cancel, true, 1280, 720);
        Assert.False(editor.IsDraggingLight);
        AssertVector(new(0, 0, 150), editor.Selected!.Position);
        Assert.Equal(1, editor.History.CommandCount);

        Poll(editor, new(), 640, 360);
        Poll(editor, new(), 640, 360, true);
        Poll(editor, new(), 665, 345, true);
        editor.Poll(new(), Mouse(0, 0, false), false, 1280, 720);
        Assert.False(editor.IsDraggingLight);
        Poll(editor, new(), 1120, 300);
        Assert.False(editor.HasPendingTransform);
        AssertVector(new(25, 15, 150), editor.Selected.Position);
        Assert.Equal(2, editor.History.CommandCount);
    }

    [Fact]
    public void Add_fill_uses_a_broad_dim_spotlight_without_taking_the_primary_role()
    {
        LightingEditorController editor = Create();
        Poll(editor, new(), 140, 136, true);
        Poll(editor, new(), 140, 136);
        FieldLight fill = Assert.Single(editor.Draft.Lights);
        Assert.Null(editor.Draft.PrimaryLightId);
        Assert.StartsWith("Fill light", fill.Name);
        Assert.InRange(fill.Intensity, 0.01f, 0.99f);
        Assert.True(fill.OuterConeDegrees > 65);
        editor.Undo();
        editor.AddLight();
        string primary = editor.Draft.PrimaryLightId!;
        editor.AddFillLight();
        Assert.Equal(primary, editor.Draft.PrimaryLightId);
        Assert.Equal(2, editor.History.CommandCount);
    }

    [Fact]
    public void Add_sunlight_selects_a_directional_source_and_preserves_an_existing_primary_source()
    {
        LightingEditorController editor = Create();
        editor.AddLight();
        string primary = editor.SelectedId!;
        editor.PointerPress(new(LightingEditorLayout.AddSunlight.X + 5, LightingEditorLayout.AddSunlight.Y + 5));
        FieldLight sunlight = Assert.IsType<FieldLight>(editor.Selected);
        Assert.Equal(FieldLight.SunlightType, sunlight.Type);
        Assert.True(sunlight.Enabled);
        Assert.InRange(sunlight.Direction.Length(), 0.999f, 1.001f);
        Assert.True(sunlight.Direction.Z < 0);
        Assert.Equal(primary, editor.Draft.PrimaryLightId);

        editor.Select(null);
        UiRect sunlightRow = LightingEditorLayout.LightRow(1);
        editor.PointerPress(new(sunlightRow.X + 5, sunlightRow.Y + 5));
        Assert.Equal(sunlight.Id, editor.SelectedId);
        editor.TogglePrimary();
        Assert.Equal(sunlight.Id, editor.Draft.PrimaryLightId);
        editor.Undo();
        Assert.Equal(primary, editor.Draft.PrimaryLightId);
    }

    [Fact]
    public void Sunlight_exposes_aim_and_strength_without_positional_or_cone_controls()
    {
        LightingEditorController editor = Create();
        editor.AddSunlight();
        FieldLight sunlight = Assert.IsType<FieldLight>(editor.Selected);
        Assert.Equal(sunlight.Id, editor.Draft.PrimaryLightId);
        Assert.Equal(LightingTransformTool.Rotate, editor.ActiveTool);
        IReadOnlyList<LightingGizmoSegment> gizmo = editor.GizmoSegments();
        Assert.NotEmpty(gizmo);
        Assert.All(gizmo, segment => Assert.Equal(LightingTransformTool.Rotate, segment.Tool));

        RecordingCanvas canvas = new();
        editor.BuildView().Draw(canvas, new UiTheme());
        Assert.Contains("SUNLIGHT PROPERTIES", canvas.Texts);
        Assert.Contains("Intensity", canvas.Texts);
        Assert.DoesNotContain("Range", canvas.Texts);
        Assert.DoesNotContain("Outer °", canvas.Texts);
        Assert.DoesNotContain(canvas.Texts, text => text.StartsWith("Position", StringComparison.Ordinal));
        editor.BeginPropertyEdit(LightingProperty.PositionX);
        Assert.Null(editor.EditingProperty);
        editor.BeginPropertyEdit(LightingProperty.Range);
        Assert.Null(editor.EditingProperty);
        editor.BeginTransform(LightingTransformTool.Translate, LightingAxis.X);
        Assert.False(editor.HasPendingTransform);
        editor.BeginTransform(LightingTransformTool.Orbit, LightingAxis.Z);
        Assert.False(editor.HasPendingTransform);

        editor.BeginTransform(LightingTransformTool.Rotate, LightingAxis.Z);
        editor.SetTransformNumeric("90");
        Assert.True(editor.ConfirmTransform());
        AssertVector(new(-sunlight.Direction.Y, sunlight.Direction.X, sunlight.Direction.Z), editor.Selected!.Direction);
        editor.Undo();
        AssertVector(sunlight.Direction, editor.Selected!.Direction);
    }

    [Fact]
    public void Hidden_transform_is_suspended_and_reappears_without_mouse_jump_or_keyboard_mutation()
    {
        LightingEditorController editor = Create();
        editor.AddLight();
        Poll(editor, new(), 675, 360, true);
        Poll(editor, new(), 695, 360, true);
        Assert.Equal(20, editor.Selected!.Position.X);
        Poll(editor, new(Keys.Escape), 695, 360, true);
        Poll(editor, new(Keys.Insert), 900, 500, true);
        Assert.Single(editor.Draft.Lights);
        Assert.Equal(20, editor.Selected.Position.X);
        Poll(editor, new(), 900, 500, true);
        Poll(editor, new(Keys.Escape), 900, 500, true);
        Poll(editor, new(), 900, 500, true);
        Assert.True(editor.OverlayVisible);
        Assert.Equal(20, editor.Selected.Position.X);
        Poll(editor, new(), 910, 500, true);
        Assert.Equal(30, editor.Selected.Position.X);
        Poll(editor, new(), 910, 500);
        Assert.Equal(2, editor.History.CommandCount);
    }

    [Fact]
    public void Slider_preview_frames_form_one_history_entry_and_numeric_rgb_uses_familiar_channels()
    {
        LightingEditorController editor = Create();
        editor.AddLight();
        Poll(editor, new(), 1120, 312, true);
        Poll(editor, new(), 1090, 312, true);
        Poll(editor, new(), 1060, 312, true);
        Poll(editor, new(), 1060, 312);
        Assert.Equal(0.5f, editor.Selected!.Color.X, 3);
        Assert.Equal(2, editor.History.CommandCount);
        editor.BeginPropertyEdit(LightingProperty.Green);
        editor.SetPropertyText("64");
        Assert.True(editor.CommitProperty());
        Assert.Equal(64 / 255f, editor.Selected.Color.Y, 5);
        editor.Undo();
        Assert.Equal(1, editor.Selected.Color.Y);
        editor.Undo();
        Assert.Equal(Vector3.One, editor.Selected.Color);
    }

    [Fact]
    public void Rebinding_visibility_removes_escape_behavior_and_capture_reports_conflicts_without_executing_actions()
    {
        LightingEditorController editor = Create();
        Poll(editor, new(), 940, 40, true);
        Poll(editor, new(), 940, 40);
        Poll(editor, new(), 500, 195, true);
        Assert.Equal(LightingEditorCommand.ToggleVisibility, editor.CapturingShortcut);
        Poll(editor, new(Keys.G), 500, 195);
        Assert.NotNull(editor.CapturingShortcut);
        Assert.False(editor.HasPendingTransform);
        Assert.Contains("overlapping", editor.Message);
        Poll(editor, new());
        Poll(editor, new(Keys.F8));
        Assert.Null(editor.CapturingShortcut);
        Assert.Equal(Keys.F8, editor.Shortcuts.Bindings[LightingEditorCommand.ToggleVisibility].Key);
        Poll(editor, new());
        Poll(editor, new(Keys.Escape));
        Assert.True(editor.OverlayVisible);
        Poll(editor, new());
        Poll(editor, new(Keys.F8));
        Assert.False(editor.OverlayVisible);
        Assert.Throws<InvalidDataException>(() => editor.Shortcuts.Rebind(LightingEditorCommand.OpenEditor, null));
    }

    [Fact]
    public void Focus_loss_during_drag_preserves_values_and_reanchors_on_return()
    {
        LightingEditorController editor = Create();
        editor.AddLight();
        Poll(editor, new(), 675, 360, true);
        Poll(editor, new(), 695, 360, true);
        editor.Poll(new(), Mouse(0, 0, false), false, 1280, 720);
        editor.Poll(new(), Mouse(1100, 600, true), true, 1280, 720);
        Poll(editor, new(), 1100, 600, true);
        Assert.Equal(20, editor.Selected!.Position.X);
        Poll(editor, new(), 1110, 600, true);
        Assert.Equal(30, editor.Selected.Position.X);
        editor.CancelTransform();
        Poll(editor, new(), 1120, 312);
        Poll(editor, new(), 1120, 312, true);
        Poll(editor, new(), 1090, 312, true);
        float red = editor.Selected.Color.X;
        editor.Poll(new(), Mouse(0, 0, false), false, 1280, 720);
        editor.Poll(new(), Mouse(900, 600, true), true, 1280, 720);
        Poll(editor, new(), 900, 600, true);
        Assert.Equal(red, editor.Selected.Color.X);
        Poll(editor, new(), 900, 600);
        Assert.Equal(2, editor.History.CommandCount);
    }

    [Fact]
    public void Numeric_transform_undo_is_local_and_cancelling_adds_no_scene_command()
    {
        LightingEditorController editor = Create();
        editor.AddLight();
        editor.BeginTransform(LightingTransformTool.Translate, LightingAxis.X);
        editor.OnTextInput('2');
        editor.OnTextInput('0');
        Assert.Equal(20, editor.Selected!.Position.X);
        editor.Undo();
        Assert.Equal("2", editor.NumericInput);
        Assert.Equal(2, editor.Selected.Position.X);
        editor.Undo();
        Assert.Equal("", editor.NumericInput);
        editor.Redo();
        Assert.Equal("2", editor.NumericInput);
        Assert.Single(editor.Draft.Lights);
        editor.CancelTransform();
        Assert.Equal(0, editor.Selected.Position.X);
        Assert.Equal(1, editor.History.CommandCount);
    }

    [Fact]
    public void Save_button_commits_pending_transform_and_writes_in_the_same_click()
    {
        int saves = 0;
        LightingEditorController editor = Create(_ =>
        {
            saves++;
            return null;
        });
        editor.AddLight();
        editor.BeginTransform(LightingTransformTool.Translate, LightingAxis.X);
        editor.SetTransformNumeric("-50");
        Poll(editor, new(), 1060, 40, true);
        Assert.Equal(1, saves);
        Assert.Equal(-50, editor.Selected!.Position.X);
        Assert.False(editor.HasPendingTransform);
        Assert.False(editor.IsDirty);
        Poll(editor, new(), 900, 623);
        editor.BeginTransform(LightingTransformTool.Translate, LightingAxis.X);
        editor.SetTransformNumeric("100");
        Poll(editor, new(), 900, 623, true);
        Assert.Equal(-50, editor.Selected.Position.X);
        Assert.False(editor.HasPendingTransform);
        Assert.False(editor.IsDirty);
    }

    [Fact]
    public void Unconstrained_aim_uses_the_frozen_camera_axis_instead_of_spinning_a_downward_beam_around_world_height()
    {
        LightingEditorController editor = Create();
        editor.SetProjection(Project, _ => Vector3.Zero, _ => new LightingEditorRay(new(0, 1000, 1000), Vector3.Normalize(new(0, -1, -1))));
        editor.AddLight();
        Vector3 originalPosition = editor.Selected!.Position;
        editor.BeginTransform(LightingTransformTool.Rotate);
        editor.SetTransformNumeric("90");
        editor.ConfirmTransform();
        Assert.Equal(originalPosition, editor.Selected.Position);
        Assert.True(Vector3.Distance(-Vector3.UnitZ, editor.Selected.Direction) > 0.5f);
        Assert.Equal(1, editor.Selected.Direction.Length(), 4);
    }

    [Fact]
    public void Explicit_add_uses_actor_scale_and_offscreen_lamps_remain_at_their_real_position_with_recovery_help()
    {
        LightingEditorController editor = Create();
        editor.AddLight();
        Assert.Equal(150, editor.Selected!.Position.Z);
        Assert.Equal(600, editor.Selected.Range);
        editor.BeginTransform(LightingTransformTool.Translate, LightingAxis.X);
        editor.SetTransformNumeric("5000");
        editor.ConfirmTransform();
        Assert.Equal(5000, editor.Selected.Position.X);
        Assert.Contains("outside this camera view", editor.SelectionLocationNotice!);
        editor.Undo();
        Assert.Null(editor.SelectionLocationNotice);
    }

    [Theory]
    [InlineData(1280, 720)]
    [InlineData(800, 600)]
    public void Authored_editor_fixture_renders_properties_gizmos_and_color_picker(int width, int height)
    {
        LightingEditorController editor = Create();
        editor.SetProjection(value => new Vector2(630 + value.X * 0.45f + value.Y * 0.25f, 480 + value.Y * 0.15f - value.Z * 0.55f), _ => Vector3.Zero, Ray);
        editor.AddLight();
        editor.BeginPropertyEdit(LightingProperty.Name);
        editor.SetPropertyText("Ceiling · warm key");
        editor.CommitProperty();
        editor.BeginPropertyEdit(LightingProperty.Range);
        editor.SetPropertyText("550");
        editor.CommitProperty();
        editor.BeginPropertyEdit(LightingProperty.Blue);
        editor.SetPropertyText("170");
        editor.CommitProperty();
        editor.BuildView().Resize(width, height);
        using SkiaSharp.SKBitmap bitmap = new(width, height);
        using BattlePreviewCanvas canvas = new(bitmap, "");
        void Render(string suffix)
        {
            canvas.BeginFrame(TimeSpan.Zero);
            canvas.Fill(new(0, 0, width, height), new(36, 43, 52));
            editor.BuildView().Draw(canvas, new UiTheme());
            Assert.Contains(bitmap.Pixels, pixel => pixel.Red > 180 && pixel.Green > 180 && pixel.Blue > 180);
            if (Environment.GetEnvironmentVariable("MEMORIA_FF8_LIGHTING_UI_CAPTURE") is { Length: > 0 } output)
            {
                Directory.CreateDirectory(output);
                bitmap.Save(Path.Combine(output, $"lighting-editor-{width}x{height}-{suffix}.png"));
            }
        }
        Render("move");
        editor.SetTool(LightingTransformTool.Rotate);
        Render("aim");
        editor.SetTool(LightingTransformTool.Orbit);
        Render("orbit");
        float scale = Math.Min(width / 1280f, height / 720f);
        int x = (int)(1230 * scale + (width - 1280 * scale) / 2);
        int y = (int)(135 * scale + (height - 720 * scale) / 2);
        editor.Poll(new(), Mouse(x, y, true), true, width, height);
        Assert.True(editor.ColorPickerVisible);
        Render("color");
    }

    [Theory]
    [InlineData(1280, 720)]
    [InlineData(1920, 1080)]
    [InlineData(900, 1200)]
    public void Pointer_fit_scaling_hits_same_toolbar_and_hidden_view_draws_no_panels_or_guides(int width, int height)
    {
        LightingEditorController editor = Create();
        float scale = Math.Min(width / 1280f, height / 720f);
        int x = (int)(70 * scale + (width - 1280 * scale) / 2);
        int y = (int)(136 * scale + (height - 720 * scale) / 2);
        editor.Poll(new(), Mouse(x, y, true), true, width, height);
        Assert.Single(editor.Draft.Lights);
        RecordingCanvas canvas = new();
        editor.BuildView().Draw(canvas, new UiTheme());
        Assert.Contains(canvas.Texts, text => text.Contains("FIELD LIGHTS", StringComparison.Ordinal));
        Assert.True(canvas.LineCount > 100);
        editor.ToggleVisibility();
        canvas.Texts.Clear();
        canvas.LineCount = 0;
        editor.BuildView().Draw(canvas, new UiTheme());
        Assert.Empty(canvas.Texts);
        Assert.Equal(0, canvas.LineCount);
    }

    private static LightingEditorController Create(Func<FieldLightingScene, string?>? save = null, Action? close = null)
    {
        LightingEditorController editor = new(FieldLightingScene.Empty("light_fixture_a"), "Authored lights", Vector3.Zero, 100, save ?? (_ => null), close ?? (() => { }));
        editor.SetProjection(Project, _ => Vector3.Zero, Ray);
        return editor;
    }

    private static Vector2? Project(Vector3 value)
    {
        return new Vector2(640 + value.X, 360 - value.Y);
    }

    private static LightingEditorRay? Ray(Vector2 value)
    {
        return new(new(value.X - 640, 360 - value.Y, 10000), -Vector3.UnitZ);
    }

    private static void Poll(LightingEditorController editor, KeyboardState keys, int x = 600, int y = 400, bool down = false)
    {
        editor.Poll(keys, Mouse(x, y, down), true, 1280, 720);
    }

    private static MouseState Mouse(int x, int y, bool down)
    {
        return new(x, y, 0, down ? ButtonState.Pressed : ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
    }

    private static void AssertVector(Vector3 expected, Vector3 actual)
    {
        Assert.InRange(Vector3.Distance(expected, actual), 0, 0.001f);
    }

    private sealed class RecordingCanvas : IUiCanvas
    {
        public List<string> Texts { get; } = [];
        public int LineCount { get; set; }

        public void Fill(UiRect bounds, UiColor color)
        {
        }

        public void Text(string text, float fontSize, UiRect bounds, UiColor color)
        {
            Texts.Add(text);
        }

        public void Text(string text, float fontSize, UiRect bounds, UiColor color, UiAlignment verticalAlignment)
        {
            Texts.Add(text);
        }

        public void MenuPointer(UiRect bounds)
        {
        }

        public void Image(UiImageSource source, UiRect bounds)
        {
        }

        public void Line(float x1, float y1, float x2, float y2, UiColor color, float thickness)
        {
            LineCount++;
        }

        public void PushClip(UiRect bounds)
        {
        }

        public void PopClip()
        {
        }
    }
}
