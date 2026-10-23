using Memoria.FF8.GameUi;
using Memoria.FF8.Ui;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8.App.Tests;

public sealed class ControlsMenuTests
{
    [Theory]
    [InlineData("config-keyboard")]
    [InlineData("config-controller")]
    public void Bindings_use_full_width_rows_with_right_aligned_buttons_and_only_help_above(string screen)
    {
        ControlsProfile profile = ControlsProfile.CreateDefault().WithBinding(ControlAction.Card, ControlDevice.Keyboard, new([Keys.K], []));
        MenuController menus = Connected(() => profile, next => profile = next);
        menus.Open(screen);
        menus.SelectEntry(screen, nameof(ControlAction.Confirm));
        UiView view = menus.BuildView();
        view.Resize(960, 672);
        UiList commands = Assert.IsType<UiList>(view.FocusedElement);
        Assert.True(commands.Bounds.Width > 850);
        Assert.Equal(UiAlignment.End, commands.SecondaryAlignment);
        Assert.Equal(menus.Entries.Count, commands.SecondaryItems.Count);
        int confirm = menus.Entries.ToList().FindIndex(entry => entry.Id == nameof(ControlAction.Confirm));
        Assert.Contains(screen == "config-keyboard" ? "Enter" : "South button", commands.SecondaryItems[confirm]);
        Assert.Equal(screen == "config-keyboard" ? 2 : 1, ButtonPromptText.Runs(commands.SecondaryItems[confirm]).Count(run => run.IsButton));
        Assert.False(commands.AutoSecondaryWidth);
        Assert.Equal(0.3f, commands.SecondaryWidth);
        Assert.True(commands.AutoRowHeight);
        Assert.Equal(UiTextFlow.Wrap, commands.TextFlow);
        menus.SelectEntry(screen, nameof(ControlAction.QuickSave));
        menus.Navigate(MenuInput.Alternate);
        Assert.False(menus.IsCapturingControls);
        Assert.Contains(menus.Entries, entry => entry.Id == "capture");
        Assert.Contains(menus.Entries, entry => entry.Id == "clear");
        Assert.DoesNotContain(menus.Entries, entry => entry.Id == "escape");
        menus.Navigate(MenuInput.Cancel);
        Assert.Equal(screen, menus.ScreenId);
        menus.Navigate(MenuInput.Confirm);
        Assert.True(menus.IsCapturingControls);
    }

    [Fact]
    public void Clicking_an_action_starts_capture_directly()
    {
        ControlsProfile profile = ControlsProfile.CreateDefault();
        MenuController menus = Connected(() => profile, next => profile = next);
        menus.Open("config-keyboard");
        UiView view = menus.BuildView();
        UiList commands = Assert.IsType<UiList>(view.FocusedElement);
        view.Pointer(commands.Bounds.Right - 12, commands.Bounds.Y + commands.RowHeight * 2 + 12, true);
        Assert.True(menus.IsCapturingControls);
        Assert.Equal("config-keyboard-choice", menus.ScreenId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Simple_capture_returns_to_the_selected_action_and_drains_input(bool click)
    {
        ControlsProfile profile = ControlsProfile.CreateDefault();
        MenuController menus = Connected(() => profile, next => profile = next);
        menus.Open("config");
        menus.Push("config-keyboard");
        menus.SelectEntry("config-keyboard", nameof(ControlAction.MoveUp));
        if (click)
        {
            UiView view = menus.BuildView();
            UiList commands = Assert.IsType<UiList>(view.FocusedElement);
            view.Pointer(commands.Bounds.Right - 12, commands.Bounds.Y + commands.RowHeight * menus.SelectedIndex + 12, true);
        }
        else
        {
            menus.Navigate(MenuInput.Confirm);
        }
        Assert.True(menus.IsCapturingControls);
        menus.UpdateControlsCapture(default, default, true);
        menus.UpdateControlsCapture(new(Keys.K), default, true);
        menus.UpdateControlsCapture(default, default, true);
        Assert.Equal("config-keyboard", menus.ScreenId);
        Assert.Equal(nameof(ControlAction.MoveUp), menus.SelectedEntry?.Id);
        Assert.Equal("Binding saved.", menus.Message);
        Assert.Equal(Keys.K, profile.GetBindings(ControlAction.MoveUp, ControlDevice.Keyboard).Single().Keys.Single());
        Assert.True(menus.UpdateControlsCapture(new(Keys.K), default, true));
        Assert.True(menus.UpdateControlsCapture(default, default, true));
        Assert.False(menus.UpdateControlsCapture(default, default, true));
        menus.Navigate(MenuInput.Cancel);
        Assert.Equal("config", menus.ScreenId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Simple_capture_conflict_returns_to_list_after_keep_or_replace(bool replace)
    {
        ControlsProfile profile = ControlsProfile.CreateDefault();
        MenuController menus = Connected(() => profile, next => profile = next);
        menus.Open("config-keyboard");
        menus.SelectEntry("config-keyboard", nameof(ControlAction.QuickSave));
        menus.Navigate(MenuInput.Confirm);
        menus.UpdateControlsCapture(default, default, true);
        menus.UpdateControlsCapture(new(Keys.F9), default, true);
        menus.UpdateControlsCapture(default, default, true);
        Assert.Equal("config-controls-conflict", menus.ScreenId);
        if (replace)
        {
            menus.Navigate(MenuInput.Down);
        }
        menus.Navigate(MenuInput.Confirm);
        Assert.Equal("config-keyboard", menus.ScreenId);
        Assert.Equal(nameof(ControlAction.QuickSave), menus.SelectedEntry?.Id);
        Assert.Equal(replace ? Keys.F9 : Keys.F5, profile.GetBindings(ControlAction.QuickSave, ControlDevice.Keyboard).Single().Keys.Single());
    }

    [Fact]
    public void Simple_capture_cancel_returns_to_list_and_advanced_save_stays_in_advanced()
    {
        ControlsProfile profile = ControlsProfile.CreateDefault();
        MenuController menus = Connected(() => profile, next => profile = next);
        menus.Open("config-keyboard");
        menus.SelectEntry("config-keyboard", nameof(ControlAction.MoveUp));
        menus.Navigate(MenuInput.Confirm);
        menus.UpdateControlsCapture(default, default, true);
        menus.UpdateControlsCapture(new(Keys.Escape), default, true);
        Assert.Equal("config-keyboard", menus.ScreenId);
        menus.UpdateControlsCapture(default, default, true);
        menus.Navigate(MenuInput.Alternate);
        Capture(menus, new(Keys.K));
        Assert.Equal("config-keyboard-choice", menus.ScreenId);
        Assert.Equal("Binding saved.", menus.Message);
        menus.Navigate(MenuInput.Cancel);
        Assert.Equal("config-keyboard", menus.ScreenId);
    }

    [Fact]
    public void Controls_screens_render_using_the_live_canvas_without_a_graphics_device()
    {
        string gameDirectory = TestResources.GameDirectory;
        ControlsProfile profile = ControlsProfile.CreateDefault();
        MenuController menus = Connected(() => profile, next => profile = next);
        void Render(string name)
        {
            using System.Drawing.Bitmap bitmap = new(960, 672);
            using BattlePreviewCanvas canvas = new(bitmap, gameDirectory);
            UiView view = menus.BuildView();
            view.Resize(960, 672);
            view.Draw(canvas, new UiTheme());
            if (Environment.GetEnvironmentVariable("MEMORIA_CONTROLS_EVIDENCE") is string directory)
            {
                Directory.CreateDirectory(directory);
                bitmap.Save(Path.Combine(directory, name + ".png"), System.Drawing.Imaging.ImageFormat.Png);
            }
            Assert.True(Enumerable.Range(1, 9).Any(x => Enumerable.Range(1, 6).Any(y => bitmap.GetPixel(x * bitmap.Width / 10, y * bitmap.Height / 7).ToArgb() != System.Drawing.Color.Black.ToArgb())), "The anchored controls view must paint visible content.");
        }
        menus.Open("config-keyboard");
        Render("keyboard");
        menus.Open("config-controller");
        Render("controller");
        Select(menus, ControlAction.QuickSave);
        Render("change-binding");
        menus.Execute(new("controls-capture"));
        menus.UpdateControlsCapture(default, default, true);
        menus.UpdateControlsCapture(new(Keys.LeftControl, Keys.K), default, true);
        Render("capture");
        menus.Execute(new("controls-cancel"));
        Capture(menus, new(Keys.F9));
        Render("conflict");
        menus.Open("config-controls-reset");
        Render("reset");
        menus.ConnectPreferences(() => new UiPreferences { UiFontSize = 36 }, _ => { });
        profile = profile.WithBinding(ControlAction.RotateLeft, ControlDevice.Keyboard, new([Keys.LeftControl, Keys.LeftShift, Keys.LeftAlt, Keys.K], []));
        menus.Open("config-keyboard");
        menus.SelectEntry("config-keyboard", nameof(ControlAction.CameraRight));
        Render("keyboard-large-text");
    }

    [Fact]
    public void Player_and_developer_editors_partition_all_actions_on_both_devices()
    {
        MenuController menus = new();
        foreach (string screen in new[] { "config-keyboard", "config-controller" })
        {
            menus.Open(screen, true);
            HashSet<ControlAction> player = [];
            for (int group = 0; group < 4; group++)
            {
                foreach (MenuEntry entry in menus.Entries)
                {
                    if (Enum.TryParse(entry.Id, out ControlAction action))
                    {
                        player.Add(action);
                    }
                }
                menus.Execute(new("controls-group"));
            }
            Assert.Equal(ControlsProfile.PlayerActions.Count, player.Count);
            Assert.All(player, action => Assert.False(ControlsProfile.IsDeveloperAction(action)));
            menus.Open("debug-shortcuts", true);
            ControlAction[] developer = menus.Entries.Where(entry => Enum.TryParse<ControlAction>(entry.Id, out _)).Select(entry => Enum.Parse<ControlAction>(entry.Id)).ToArray();
            Assert.Equal(3, developer.Length);
            Assert.Empty(player.Intersect(developer));
            Assert.Equal(ControlsProfile.Definitions.Select(definition => definition.Action).Order(), player.Concat(developer).Order());
            Assert.NotNull(menus.BuildView());
        }
    }

    [Fact]
    public void Capture_requires_neutral_then_collects_simultaneous_keys_and_drains_release()
    {
        ControlsProfile profile = ControlsProfile.CreateDefault();
        int writes = 0;
        MenuController menus = Connected(() => profile, next =>
        {
            profile = next;
            writes++;
        });
        Select(menus, ControlAction.QuickSave);
        menus.Execute(new("controls-capture"));
        Assert.True(menus.UpdateControlsCapture(new(Keys.X), default, true));
        Assert.Equal(0, writes);
        menus.Navigate(MenuInput.Cancel);
        Assert.True(menus.IsOpen);
        Assert.True(menus.IsCapturingControls);
        Assert.True(menus.UpdateControlsCapture(default, default, true));
        menus.UpdateControlsCapture(new(Keys.LeftControl), default, true);
        menus.UpdateControlsCapture(new(Keys.LeftControl, Keys.K), default, true);
        Assert.Equal(0, writes);
        menus.UpdateControlsCapture(default, default, true);
        Assert.False(menus.IsCapturingControls);
        Assert.Equal(1, writes);
        Assert.Equal(new[] { Keys.K, Keys.LeftControl }.Order(), profile.GetBindings(ControlAction.QuickSave, ControlDevice.Keyboard).Single().Keys.Order());
        Assert.True(menus.UpdateControlsCapture(default, default, true));
        Assert.False(menus.UpdateControlsCapture(default, default, true));
    }

    [Fact]
    public void Escape_and_focus_loss_cancel_capture_without_writing()
    {
        ControlsProfile profile = ControlsProfile.CreateDefault();
        int writes = 0;
        MenuController menus = Connected(() => profile, _ => writes++);
        Select(menus, ControlAction.QuickSave);
        foreach (bool active in new[] { true, false })
        {
            menus.Execute(new("controls-capture"));
            menus.UpdateControlsCapture(default, default, true);
            menus.UpdateControlsCapture(new(Keys.Escape), default, active);
            Assert.False(menus.IsCapturingControls);
            Assert.True(menus.UpdateControlsCapture(new(Keys.Escape), default, true));
            Assert.True(menus.UpdateControlsCapture(default, default, true));
            Assert.False(menus.UpdateControlsCapture(default, default, true));
        }
        Assert.Equal(0, writes);
    }

    [Fact]
    public void Conflict_is_a_safe_confirmation_and_explicit_replacement_updates_owner()
    {
        ControlsProfile profile = ControlsProfile.CreateDefault();
        MenuController menus = Connected(() => profile, next => profile = next);
        Select(menus, ControlAction.QuickSave);
        Capture(menus, new(Keys.F9));
        Assert.Equal("config-controls-conflict", menus.ScreenId);
        Assert.Equal("keep", menus.SelectedEntry?.Id);
        Assert.Equal(Keys.F5, profile.GetBindings(ControlAction.QuickSave, ControlDevice.Keyboard).Single().Keys.Single());
        menus.Navigate(MenuInput.Confirm);
        Assert.Equal("config-keyboard-choice", menus.ScreenId);
        Assert.Equal(Keys.F9, profile.GetBindings(ControlAction.QuickLoad, ControlDevice.Keyboard).Single().Keys.Single());
        Capture(menus, new(Keys.F9));
        menus.Navigate(MenuInput.Down);
        menus.Navigate(MenuInput.Confirm);
        Assert.Equal("config-keyboard-choice", menus.ScreenId);
        Assert.Empty(profile.GetBindings(ControlAction.QuickLoad, ControlDevice.Keyboard));
        Assert.Equal(Keys.F9, profile.GetBindings(ControlAction.QuickSave, ControlDevice.Keyboard).Single().Keys.Single());
    }

    [Fact]
    public void Controller_capture_ignores_stick_directions_and_captures_button_combinations()
    {
        ControlsProfile profile = ControlsProfile.CreateDefault();
        MenuController menus = Connected(() => profile, next => profile = next);
        Select(menus, ControlAction.QuickSave, ControlDevice.Controller);
        menus.Execute(new("controls-capture"));
        menus.UpdateControlsCapture(default, default, true);
        GamePadState stick = new(new Microsoft.Xna.Framework.Vector2(1, 0), Microsoft.Xna.Framework.Vector2.Zero, 0, 0, []);
        menus.UpdateControlsCapture(default, stick, true);
        Assert.True(menus.IsCapturingControls);
        GamePadState chord = new(Microsoft.Xna.Framework.Vector2.Zero, Microsoft.Xna.Framework.Vector2.Zero, 0, 0, [Buttons.LeftShoulder, Buttons.Y]);
        menus.UpdateControlsCapture(default, chord, true);
        menus.UpdateControlsCapture(default, new GamePadState(Microsoft.Xna.Framework.Vector2.Zero, Microsoft.Xna.Framework.Vector2.Zero, 0, 0, []), true);
        Assert.Equal(new[] { Buttons.LeftShoulder, Buttons.Y }.Order(), profile.GetBindings(ControlAction.QuickSave, ControlDevice.Controller).Single().Buttons.Order());
    }

    [Fact]
    public void Persistence_failure_and_invalid_chords_leave_owner_unchanged_and_menu_usable()
    {
        ControlsProfile profile = ControlsProfile.CreateDefault();
        MenuController menus = Connected(() => profile, _ => throw new IOException("Cannot save controls."));
        Select(menus, ControlAction.QuickSave);
        Capture(menus, new(Keys.K));
        Assert.Equal("Cannot save controls.", menus.Message);
        Assert.Equal(Keys.F5, profile.GetBindings(ControlAction.QuickSave, ControlDevice.Keyboard).Single().Keys.Single());
        menus.UpdateControlsCapture(default, default, true);
        Capture(menus, new(Keys.K, Keys.L, Keys.M, Keys.N, Keys.O));
        Assert.False(menus.IsCapturingControls);
        Assert.NotEmpty(menus.Message);
        Assert.NotNull(menus.BuildView());
    }

    [Fact]
    public void Reset_requires_confirmation_and_demo_edits_do_not_write_live_profile()
    {
        ControlsProfile profile = ControlsProfile.CreateDefault().WithBinding(ControlAction.QuickSave, ControlDevice.Keyboard, new([Keys.K], []));
        int writes = 0;
        MenuController menus = Connected(() => profile, next =>
        {
            profile = next;
            writes++;
        });
        menus.Open("config-keyboard");
        menus.SelectEntry("config-keyboard", "reset");
        menus.Navigate(MenuInput.Confirm);
        Assert.Equal("config-controls-reset", menus.ScreenId);
        Assert.Equal("keep", menus.SelectedEntry?.Id);
        menus.Navigate(MenuInput.Down);
        menus.Navigate(MenuInput.Confirm);
        Assert.Equal(1, writes);
        Assert.Equal(Keys.F5, profile.GetBindings(ControlAction.QuickSave, ControlDevice.Keyboard).Single().Keys.Single());
        menus.Open("config-keyboard-choice", true);
        menus.Execute(new("controls-select", nameof(ControlAction.QuickSave), Value: nameof(ControlDevice.Keyboard)));
        Capture(menus, new(Keys.L));
        Assert.Equal(1, writes);
        Assert.Equal(Keys.F5, profile.GetBindings(ControlAction.QuickSave, ControlDevice.Keyboard).Single().Keys.Single());
    }

    [Fact]
    public void Device_switch_retains_group_and_action()
    {
        MenuController menus = Connected(ControlsProfile.CreateDefault, _ => { });
        menus.Open("config-keyboard");
        menus.SelectEntry("config-keyboard", nameof(ControlAction.QuickSave));
        menus.Execute(new("controls-switch-device"));
        Assert.Equal("config-controller", menus.ScreenId);
        Assert.Equal(nameof(ControlAction.QuickSave), menus.SelectedEntry?.Id);
        Assert.Contains(menus.Entries, entry => entry.Id == "group" && entry.Label.Contains("Shortcuts"));
    }

    [Fact]
    public void Player_reset_reviews_cross_owner_conflicts_before_any_write()
    {
        ControlsProfile profile = ControlsProfile.CreateDefault().WithBinding(ControlAction.DebugRoom, ControlDevice.Keyboard, new([Keys.F5], []), replaceConflicts: true);
        MenuController menus = Connected(() => profile, next => profile = next);
        menus.Open("config-keyboard");
        menus.SelectEntry("config-keyboard", "reset");
        menus.Navigate(MenuInput.Confirm);
        menus.SelectEntry("config-controls-reset", "reset");
        menus.Navigate(MenuInput.Confirm);
        Assert.Equal("config-controls-conflict", menus.ScreenId);
        Assert.Empty(profile.GetBindings(ControlAction.QuickSave, ControlDevice.Keyboard));
        Assert.Contains(menus.CurrentScreen.Details(menus), line => line.Contains("Debug Room"));
        Assert.NotNull(menus.BuildView());
        menus.SelectEntry("config-controls-conflict", "keep");
        menus.Navigate(MenuInput.Confirm);
        Assert.Single(profile.GetBindings(ControlAction.DebugRoom, ControlDevice.Keyboard));
        menus.SelectEntry("config-controls-reset", "reset");
        menus.Navigate(MenuInput.Confirm);
        menus.SelectEntry("config-controls-conflict", "replace");
        menus.Navigate(MenuInput.Confirm);
        Assert.Equal("config-keyboard", menus.ScreenId);
        Assert.Empty(profile.GetBindings(ControlAction.DebugRoom, ControlDevice.Keyboard));
        Assert.Equal(Keys.F5, Assert.Single(profile.GetBindings(ControlAction.QuickSave, ControlDevice.Keyboard)).Keys.Single());
    }

    [Fact]
    public void Controller_settings_adjust_in_both_directions_and_restore_only_preferences()
    {
        ControlsProfile profile = ControlsProfile.CreateDefault().WithControllerSettings(0.5f, 0.1f, 0.65f, ControllerPresentation.Xbox, false).WithBinding(ControlAction.QuickSave, ControlDevice.Controller, new([], [Buttons.LeftStick]));
        MenuController menus = Connected(() => profile, next => profile = next);
        menus.Open("config-controller-settings");
        menus.Navigate(MenuInput.Right);
        Assert.Equal(0.5f, profile.LeftStickDeadZone);
        menus.Navigate(MenuInput.Left);
        Assert.Equal(0.45f, profile.LeftStickDeadZone);
        menus.SelectEntry("config-controller-settings", "reset-settings");
        menus.Navigate(MenuInput.Confirm);
        Assert.Equal(0.45f, profile.LeftStickDeadZone);
        menus.SelectEntry("config-controller-settings-reset", "reset");
        menus.Navigate(MenuInput.Confirm);
        Assert.Equal(new ControllerSettings(), profile.Controller);
        Assert.Equal(Buttons.LeftStick, Assert.Single(profile.GetBindings(ControlAction.QuickSave, ControlDevice.Controller)).Buttons.Single());
    }

    private static MenuController Connected(Func<ControlsProfile> read, Action<ControlsProfile> write)
    {
        MenuController menus = new();
        menus.ConnectControls(read, write);
        return menus;
    }

    private static void Select(MenuController menus, ControlAction action, ControlDevice device = ControlDevice.Keyboard)
    {
        menus.Open(device == ControlDevice.Keyboard ? "config-keyboard-choice" : "config-controller-choice");
        menus.Execute(new("controls-select", action.ToString(), Value: device.ToString()));
    }

    private static void Capture(MenuController menus, KeyboardState keys)
    {
        menus.Execute(new("controls-capture"));
        menus.UpdateControlsCapture(default, default, true);
        menus.UpdateControlsCapture(keys, default, true);
        menus.UpdateControlsCapture(default, default, true);
    }
}
