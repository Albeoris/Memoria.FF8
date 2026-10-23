using Memoria.FF8.GameUi;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8.App.Tests;

public sealed class ControllerPreferencesTests
{
    [Theory]
    [InlineData(0f)]
    [InlineData(0.2f)]
    [InlineData(0.5f)]
    public void Full_circle_sweep_retains_720_distinct_vectors_while_menu_navigation_stays_discrete(float deadZone)
    {
        KeyboardInput input = new() { Profile = ControlsProfile.CreateDefault().WithControllerSettings(deadZone, deadZone, 0.5f) };
        HashSet<(float X, float Y)> directions = [];
        for (int step = 0; step < 720; step++)
        {
            float angle = step * MathF.Tau / 720;
            Vector2 direction = new(MathF.Cos(angle), MathF.Sin(angle));
            Runtime.InputFrame frame = input.Read(default, Pad(direction * 0.9f, direction * 0.8f), true, TimeSpan.FromMilliseconds(step * 16));
            Vector2 movement = new(frame.Horizontal, -frame.Vertical);
            Vector2 camera = new(frame.SecondaryHorizontal, -frame.SecondaryVertical);
            Assert.True(Vector2.Dot(direction, movement) > 0);
            Assert.True(Vector2.Dot(direction, camera) > 0);
            Assert.InRange(Math.Abs(direction.X * movement.Y - direction.Y * movement.X), 0, 0.00001f);
            Assert.InRange(Math.Abs(direction.X * camera.Y - direction.Y * camera.X), 0, 0.00001f);
            Assert.InRange(Math.Abs(frame.MenuHorizontal) + Math.Abs(frame.MenuVertical), 0, 1);
            directions.Add((MathF.Round(frame.Horizontal, 4), MathF.Round(frame.Vertical, 4)));
        }
        Assert.Equal(720, directions.Count);
    }

    [Fact]
    public void Controller_settings_survive_remapping_and_roundtrip_but_reset_with_controller()
    {
        ControlsProfile profile = ControlsProfile.CreateDefault().WithControllerSettings(0.35f, 0.1f, 0.65f, ControllerPresentation.PlayStation, false).WithBinding(ControlAction.QuickSave, ControlDevice.Controller, new([], [Buttons.LeftStick]));
        string directory = Path.Combine(Path.GetTempPath(), "memoria-pad-" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(directory, "controls.json");
        try
        {
            profile.Save(path);
            ControlsProfile loaded = ControlsProfile.Load(path);
            Assert.Equal(profile.Controller, loaded.Controller);
            Assert.Equal(Buttons.LeftStick, Assert.Single(loaded.GetBindings(ControlAction.QuickSave, ControlDevice.Controller)).Buttons.Single());
            Assert.Equal(profile.Controller, loaded.Reset(ControlDevice.Keyboard).Controller);
            Assert.Equal(new ControllerSettings(), loaded.Reset(ControlDevice.Controller).Controller);
            Assert.Throws<ArgumentException>(() => profile.WithControllerSettings(float.NaN, 0.2f, 0.5f));
            Assert.Throws<ArgumentException>(() => profile.WithControllerSettings(0.2f, 0.6f, 0.5f));
            Assert.Throws<ArgumentException>(() => profile.WithControllerSettings(0.2f, 0.2f, 0));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
    }

    [Theory]
    [InlineData(ControlDevice.Keyboard, false)]
    [InlineData(ControlDevice.Keyboard, true)]
    [InlineData(ControlDevice.Controller, false)]
    [InlineData(ControlDevice.Controller, true)]
    public void Scoped_reset_preserves_other_owner_device_and_controller_preferences(ControlDevice device, bool developer)
    {
        ControlsProfile profile = ControlsProfile.CreateDefault().WithControllerSettings(0.35f, 0.1f, 0.65f, ControllerPresentation.PlayStation, false)
            .WithBinding(ControlAction.QuickSave, ControlDevice.Keyboard, new([Keys.K], []))
            .WithBinding(ControlAction.DebugRoom, ControlDevice.Keyboard, new([Keys.L], []))
            .WithBinding(ControlAction.QuickSave, ControlDevice.Controller, new([], [Buttons.LeftStick]))
            .WithBinding(ControlAction.DebugRoom, ControlDevice.Controller, new([], [Buttons.RightStick]));
        ControlsProfile reset = profile.ResetBindings(device, developer);
        ControlsProfile defaults = ControlsProfile.CreateDefault();
        foreach (ControlDevice inspectedDevice in Enum.GetValues<ControlDevice>())
        {
            foreach (ControlActionDefinition definition in ControlsProfile.Definitions)
            {
                ControlsProfile expected = inspectedDevice == device && ControlsProfile.IsDeveloperAction(definition.Action) == developer ? defaults : profile;
                Assert.Equal(expected.GetBindings(definition.Action, inspectedDevice).Select(binding => binding.FormatDisplayName(ControllerPresentation.Generic)), reset.GetBindings(definition.Action, inspectedDevice).Select(binding => binding.FormatDisplayName(ControllerPresentation.Generic)));
            }
        }
        Assert.Equal(profile.Controller, reset.Controller);
    }

    [Fact]
    public void Reset_conflicts_require_explicit_review_and_replace_only_matching_chords()
    {
        ControlsProfile profile = ControlsProfile.CreateDefault().WithBinding(ControlAction.DebugRoom, ControlDevice.Keyboard, new([Keys.F5], []), replaceConflicts: true);
        Assert.Equal(new[] { ControlAction.DebugRoom }, profile.FindResetConflicts(ControlDevice.Keyboard, developer: false));
        Assert.Throws<InvalidOperationException>(() => profile.ResetBindings(ControlDevice.Keyboard, developer: false));
        ControlsProfile reset = profile.ResetBindings(ControlDevice.Keyboard, developer: false, replaceConflicts: true);
        Assert.Empty(reset.GetBindings(ControlAction.DebugRoom, ControlDevice.Keyboard));
        Assert.Equal(Keys.F4, Assert.Single(reset.GetBindings(ControlAction.BattleDebug, ControlDevice.Keyboard)).Keys.Single());
        Assert.Equal(Keys.F5, Assert.Single(reset.GetBindings(ControlAction.QuickSave, ControlDevice.Keyboard)).Keys.Single());
    }

    [Theory]
    [InlineData(ControllerPresentation.PlayStation, "\u00D7", "R3")]
    [InlineData(ControllerPresentation.Xbox, "[A]", "RS")]
    [InlineData(ControllerPresentation.Nintendo, "[B]", "R Stick")]
    [InlineData(ControllerPresentation.Generic, "South button", "RightStick")]
    public void Glyphs_follow_physical_button_identity_and_stick_clicks_can_bind(ControllerPresentation presentation, string face, string click)
    {
        Assert.Equal(face, new InputChord([], [Buttons.A]).FormatDisplayName(presentation));
        Assert.Equal(click, new InputChord([], [Buttons.RightStick]).FormatDisplayName(presentation));
        ControlsProfile profile = ControlsProfile.CreateDefault().WithBinding(ControlAction.Confirm, ControlDevice.Controller, new([], [Buttons.RightStick]));
        Assert.Throws<ArgumentException>(() => profile.WithBinding(ControlAction.Cancel, ControlDevice.Controller, new([], [Buttons.RightThumbstickUp])));
    }

    [Fact]
    public void Radial_preferences_preserve_heading_and_select_native_walk_below_adjusted_run_threshold()
    {
        KeyboardInput input = new() { Profile = ControlsProfile.CreateDefault().WithControllerSettings(0.3f, 0.1f, 0.5f) };
        Runtime.InputFrame walking = input.Read(default, Pad(new(0.5f, 0.2f), new(0.2f, -0.3f)), true);
        Assert.Equal(2.5f, walking.Horizontal / -walking.Vertical, 4);
        Assert.Equal(0x10, input.HeldFieldButtons & 0x10);
        Assert.True(walking.SecondaryVertical > 0);
        input.Read(default, Pad(new(0.9f, 0)), true);
        Assert.Equal(0, input.HeldFieldButtons & 0x10);
        Runtime.InputFrame drift = input.Read(default, Pad(new(0.2f, 0.1f), new(0.05f, 0.05f)), true);
        Assert.Equal(0, drift.Horizontal);
        Assert.Equal(0, drift.CameraHorizontal);
        Assert.Equal(0, drift.SecondaryVertical);
        input.Read(new(Keys.Right), Pad(new(0.4f, 0)), true);
        Assert.Equal(0, input.HeldFieldButtons & 0x10);
    }

    [Fact]
    public void Menu_stick_uses_one_dominant_axis_hysteresis_and_timed_repeat_without_losing_analog_movement()
    {
        KeyboardInput input = new();
        Runtime.InputFrame Read(float x, float y, int milliseconds) => input.Read(default, Pad(new(x, y)), true, TimeSpan.FromMilliseconds(milliseconds));
        Runtime.InputFrame initial = Read(0.9f, 0.7f, 0);
        Assert.Equal(1, initial.MenuHorizontal);
        Assert.Equal(0, initial.MenuVertical);
        Assert.True(initial.Vertical < 0);
        Assert.Equal(0, Read(0.8f, 0.81f, 100).MenuHorizontal);
        Assert.Equal(0, Read(0.8f, 0.81f, 100).MenuVertical);
        Assert.Equal(0, Read(0.9f, 0.7f, 349).MenuHorizontal);
        Assert.Equal(1, Read(0.9f, 0.7f, 350).MenuHorizontal);
        Assert.Equal(0, Read(0.9f, 0.7f, 449).MenuHorizontal);
        Assert.Equal(1, Read(0.9f, 0.7f, 450).MenuHorizontal);
        Assert.Equal(-1, Read(-0.9f, 0, 460).MenuHorizontal);
        Read(0, 0, 470);
        Assert.Equal(1, Read(0.9f, 0, 480).MenuHorizontal);
    }

    [Fact]
    public void Focus_reconnection_and_device_switch_require_held_sticks_to_return_to_neutral()
    {
        KeyboardInput input = new();
        GamePadState held = Pad(new(0.8f, 0), new(0.8f, 0.8f));
        input.Read(default, held, true);
        input.Read(default, held, false);
        Assert.Equal(0, input.Read(default, held, true).Horizontal);
        Assert.Equal(0, input.Read(default, held, true).SecondaryVertical);
        input.Read(default, Pad(Vector2.Zero), true);
        Assert.True(input.Read(default, held, true).Horizontal > 0);
        input.ControllerGeneration++;
        Assert.Equal(0, input.Read(default, held, true).Horizontal);
        input.Read(default, Pad(Vector2.Zero), true);
        Assert.True(input.Read(default, held, true).Horizontal > 0);
        input.Read(default, default, true);
        Assert.Equal(0, input.Read(default, held, true).Horizontal);
    }

    [Fact]
    public void Presentation_override_and_last_device_update_host_hints()
    {
        KeyboardInput input = new() { ControllerPresentation = ControllerPresentation.PlayStation };
        input.Read(default, Pad(Vector2.Zero), true);
        input.Read(default, Pad(Vector2.Zero, buttons: [Buttons.A]), true);
        Assert.Equal("\u00D7", input.BindingHint(ControlAction.Confirm));
        input.Profile = input.Profile.WithControllerSettings(0.2f, 0.2f, 0.5f, ControllerPresentation.Xbox);
        Assert.Equal("[A]", input.BindingHint(ControlAction.Confirm));
        input.Read(new(Keys.X), Pad(Vector2.Zero), true);
        input.Read(default, Pad(Vector2.Zero), true);
        input.Read(new(Keys.X), Pad(Vector2.Zero), true);
        Assert.Equal("X", input.BindingHint(ControlAction.Confirm));
        input.Read(default, Pad(Vector2.Zero), true);
        input.Read(default, Pad(Vector2.Zero, buttons: [Buttons.A]), true);
        input.Read(default, default, true);
        Assert.Equal("X", input.BindingHint(ControlAction.Confirm));
    }

    [Fact]
    public void Controller_settings_menu_writes_preferences_and_disconnect_cancels_partial_binding()
    {
        ControlsProfile profile = ControlsProfile.CreateDefault();
        MenuController menus = new();
        menus.ConnectControls(() => profile, value => profile = value);
        menus.Open("config-controller-settings");
        menus.Execute(new("controls-setting", "left-deadzone"));
        Assert.Equal(0.25f, profile.LeftStickDeadZone);
        menus.Execute(new("controls-setting", "vibration"));
        Assert.False(profile.VibrationEnabled);
        menus.Open("config-controller-choice");
        menus.Execute(new("controls-select", ControlAction.QuickSave.ToString(), Value: ControlDevice.Controller.ToString()));
        menus.Execute(new("controls-capture"));
        menus.UpdateControlsCapture(default, Pad(Vector2.Zero), true);
        menus.UpdateControlsCapture(default, Pad(Vector2.Zero, buttons: [Buttons.A]), true);
        menus.UpdateControlsCapture(default, default, true);
        Assert.False(menus.IsCapturingControls);
        Assert.Empty(profile.GetBindings(ControlAction.QuickSave, ControlDevice.Controller));
    }

    private static GamePadState Pad(Vector2 left, Vector2 right = default, params Buttons[] buttons) => new(left, right, 0, 0, buttons);
}
