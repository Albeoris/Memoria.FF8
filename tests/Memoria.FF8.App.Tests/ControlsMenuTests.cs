using Memoria.FF8.GameUi;
using Memoria.FF8.Ui;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8.App.Tests;

public sealed class ControlsMenuTests
{
    [Fact]
    public void Controls_screens_render_using_the_live_canvas_without_a_graphics_device()
    {
        ControlsProfile profile = ControlsProfile.CreateDefault();
        MenuController menus = Connected(() => profile, next => profile = next);
        void Render(string name)
        {
            using System.Drawing.Bitmap bitmap = new(960, 672);
            using BattlePreviewCanvas canvas = new(bitmap);
            UiView view = menus.BuildView();
            view.Resize(960, 672);
            view.Draw(canvas, new UiTheme());
            if (Environment.GetEnvironmentVariable("MEMORIA_CONTROLS_EVIDENCE") is string directory)
            {
                Directory.CreateDirectory(directory);
                bitmap.Save(Path.Combine(directory, name + ".png"), System.Drawing.Imaging.ImageFormat.Png);
            }
            Assert.NotEqual(System.Drawing.Color.Black.ToArgb(), bitmap.GetPixel(22, 22).ToArgb());
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
    }

    [Fact]
    public void Native_and_extended_actions_are_available_on_both_devices()
    {
        MenuController menus = new();
        foreach (string screen in new[] { "config-keyboard", "config-controller" })
        {
            menus.Open(screen, true);
            Assert.Contains(menus.Entries, entry => entry.Id == nameof(ControlAction.MoveUp));
            Assert.Contains(menus.Entries, entry => entry.Id == nameof(ControlAction.Confirm));
            Assert.Contains(menus.Entries, entry => entry.Id == nameof(ControlAction.QuickSave));
            Assert.Contains(menus.Entries, entry => entry.Id == nameof(ControlAction.BattleDebug));
            Assert.Contains(menus.Entries, entry => entry.Id == "reset");
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
        menus.Navigate(MenuInput.Up);
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
