using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class SceneInspectorTests
{
    [Fact]
    public void Debug_room_inspector_tracks_the_loaded_scene_and_rejects_unavailable_commands()
    {
        MenuController menu = new();
        menu.Open("debug-room", true);
        Assert.False(menu.Entries.Single(entry => entry.Id == "scene-inspector").Enabled);
        bool available = false;
        int opened = 0;
        menu.ConnectSceneInspector(() => available, () => opened++);
        menu.Execute(new MenuAction("open-scene-inspector"));
        Assert.Equal(0, opened);
        Assert.Contains("loaded field", menu.Message);
        available = true;
        Assert.True(menu.Entries.Single(entry => entry.Id == "scene-inspector").Enabled);
        menu.SelectEntry("debug-room", "scene-inspector");
        menu.Navigate(MenuInput.Confirm);
        Assert.Equal(1, opened);
    }

    [Fact]
    public void Inspector_pages_filters_and_preserves_selection_by_identity_while_refreshing()
    {
        SceneInspectorSnapshot snapshot = Snapshot(Enumerable.Range(0, 22).Select(index => Entry("layer-" + index, enabled: index % 2 == 0)).ToArray());
        SceneInspectorController inspector = new(() => snapshot, _ => { }, () => { });
        inspector.Navigate(MenuInput.PageDown);
        Assert.Equal("layer-8", inspector.Selected!.Id);
        Assert.Equal(1, inspector.Page);
        Assert.Equal(3, inspector.PageCount);
        snapshot = snapshot with { Frame = 99, Entries = snapshot.Entries.Reverse().ToArray() };
        inspector.Refresh();
        Assert.Equal("layer-8", inspector.Selected!.Id);
        Assert.Equal(99, inspector.Snapshot!.Frame);
        inspector.Navigate(MenuInput.Alternate);
        Assert.Equal(SceneInspectorFilter.Enabled, inspector.Filter);
        Assert.All(inspector.Entries, entry => Assert.True(entry.Enabled));
        inspector.Navigate(MenuInput.Alternate);
        Assert.All(inspector.Entries, entry => Assert.False(entry.Enabled));
        inspector.Navigate(MenuInput.Right);
        Assert.Equal(SceneInspectorKind.Object, inspector.Kind);
        Assert.Empty(inspector.Entries);
        inspector.Navigate(MenuInput.Confirm);
    }

    [Fact]
    public void Toggle_refreshes_effective_state_and_reset_is_reachable_by_keyboard()
    {
        SceneInspectorSnapshot snapshot = Snapshot([Entry("layer-0")]);
        List<SceneInspectorCommand> commands = [];
        SceneInspectorController inspector = new(() => snapshot, command =>
        {
            commands.Add(command);
            snapshot = snapshot with { Entries = snapshot.Entries.Select(entry => entry with { Enabled = command.Reset || command.Enabled!.Value }).ToArray() };
        }, () => { });
        inspector.Navigate(MenuInput.Confirm);
        Assert.Equal(new SceneInspectorCommand("layer-0", false), Assert.Single(commands));
        Assert.False(inspector.Selected!.Enabled);
        inspector.Navigate(MenuInput.Tab);
        inspector.Navigate(MenuInput.Tab);
        Assert.Equal(SceneInspectorFocus.Reset, inspector.Focus);
        inspector.Navigate(MenuInput.Confirm);
        Assert.Equal(new SceneInspectorCommand(Reset: true), commands[1]);
        Assert.True(inspector.Selected!.Enabled);
        Assert.Contains("configured mods", inspector.Message);
    }

    [Fact]
    public void Pointer_selects_rows_and_scaled_toggle_buttons_activate_once_per_press()
    {
        SceneInspectorSnapshot snapshot = Snapshot([Entry("first"), Entry("second")]);
        List<SceneInspectorCommand> commands = [];
        SceneInspectorController inspector = new(() => snapshot, commands.Add, () => { });
        UiView view = inspector.BuildView();
        view.Resize(1920, 1344);
        view.Pointer(1000, 544, true);
        Assert.Equal("second", inspector.Selected!.Id);
        Assert.Empty(commands);
        view.Pointer(1000, 544, false);
        view.Pointer(1760, 544, true);
        view.Pointer(1760, 544, true);
        Assert.Equal(new SceneInspectorCommand("second", false), Assert.Single(commands));
    }

    [Fact]
    public void Details_identify_mods_and_native_state_and_leave_the_main_preview_visible()
    {
        SceneInspectorSnapshot snapshot = Snapshot([Entry("layer-0") with { Details = ["Native visible: On", "Mod effect: package example", "Inspection override: none", "Layer: foreground", "Tiles: 42"] }]);
        SceneInspectorController inspector = new(() => snapshot, _ => { }, () => { });
        RecordingCanvas canvas = new();
        inspector.BuildView().Draw(canvas, new UiTheme());
        Assert.Contains(canvas.Texts, text => text.Contains("field 72 · frame 16", StringComparison.Ordinal));
        Assert.Contains("Native visible: On", canvas.Texts);
        Assert.Contains("Mod effect: package example", canvas.Texts);
        Assert.DoesNotContain(canvas.Fills, bounds => bounds.X < 408 && bounds.Y < 550);
        inspector.Navigate(MenuInput.PageRight);
        canvas.Texts.Clear();
        inspector.BuildView().Draw(canvas, new UiTheme());
        Assert.Contains("Tiles: 42", canvas.Texts);
    }

    [Fact]
    public void Unavailable_scenes_and_rejected_overrides_never_mutate_cached_entries()
    {
        SceneInspectorSnapshot? snapshot = Snapshot([Entry("layer-0")]);
        int requests = 0;
        int closed = 0;
        SceneInspectorController inspector = new(() => snapshot, _ =>
        {
            requests++;
            throw new InvalidOperationException("Scene changed.");
        }, () => closed++);
        inspector.Navigate(MenuInput.Confirm);
        Assert.True(inspector.Selected!.Enabled);
        Assert.Contains("Scene changed", inspector.Message);
        snapshot = null;
        inspector.Refresh();
        inspector.ToggleSelected();
        inspector.ResetAll();
        Assert.Equal(1, requests);
        inspector.Navigate(MenuInput.Cancel);
        Assert.Equal(1, closed);
    }

    [Fact]
    public void Events_explain_script_suspension_and_can_be_toggled_independently_of_objects()
    {
        SceneInspectorSnapshot snapshot = Snapshot([Entry("object-0") with { Kind = SceneInspectorKind.Object }, Entry("event-0-1") with { Kind = SceneInspectorKind.Event }]);
        SceneInspectorCommand? applied = null;
        SceneInspectorController inspector = new(() => snapshot, command => applied = command, () => { });
        inspector.Navigate(MenuInput.Left);
        Assert.Equal(SceneInspectorKind.Event, inspector.Kind);
        inspector.Navigate(MenuInput.Confirm);
        Assert.Equal(new SceneInspectorCommand("event-0-1", false), applied);
        RecordingCanvas canvas = new();
        inspector.BuildView().Draw(canvas, new UiTheme());
        Assert.Contains(canvas.Texts, text => text.Contains("waiting for it", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(960, 672)]
    [InlineData(1920, 1080)]
    [InlineData(800, 1200)]
    public void Complete_field_preview_follows_the_inspector_letterbox_without_overlapping_the_sidebar(int width, int height)
    {
        Microsoft.Xna.Framework.Rectangle bounds = RecreateGame.SceneInspectorPreviewBounds(new Microsoft.Xna.Framework.Rectangle(10, 20, width, height));
        float scale = Math.Min(width / 960f, height / 672f);
        float sidebarX = 10 + (width - 960 * scale) / 2 + 408 * scale;
        Assert.True(bounds.Right < sidebarX);
        Assert.Equal(bounds.Width, bounds.Height);
        Assert.InRange(bounds.Left, 10, width + 10);
        Assert.InRange(bounds.Bottom, 20, height + 20);
        PixelBounds fit = ViewportLayout.Fit(320, 240, bounds.Width, bounds.Height);
        Assert.InRange(fit.Width, 1, bounds.Width);
        Assert.InRange(fit.Height, 1, bounds.Height);
    }

    private static SceneInspectorEntry Entry(string id, bool enabled = true)
    {
        return new SceneInspectorEntry(id, SceneInspectorKind.Layer, id, enabled, []);
    }

    private static SceneInspectorSnapshot Snapshot(SceneInspectorEntry[] entries)
    {
        return new SceneInspectorSnapshot(72, "Example field", 16, entries);
    }

    private sealed class RecordingCanvas : IUiCanvas
    {
        public List<string> Texts { get; } = [];
        public List<UiRect> Fills { get; } = [];

        public void Fill(UiRect bounds, UiColor color)
        {
            Fills.Add(bounds);
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
        }

        public void PushClip(UiRect bounds)
        {
        }

        public void PopClip()
        {
        }
    }
}
