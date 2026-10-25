using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8.App.Tests;

public sealed class InputTests
{
    [Fact]
    public void Native_defaults_match_actual_PC_consumer_masks_and_shortcuts()
    {
        KeyboardInput input = new();
        Runtime.InputFrame frame = input.Read(new KeyboardState(Keys.X, Keys.C, Keys.V, Keys.S, Keys.H, Keys.G, Keys.D, Keys.F, Keys.J, Keys.A), true);
        Assert.True(frame.Confirm);
        Assert.True(frame.Cancel);
        Assert.True(input.GameMenuRequested);
        Assert.True(input.PauseRequested);
        Assert.Equal((ushort)0x09FF, input.HeldFieldButtons);
        input.Read(new KeyboardState(), true);
        input.Read(new KeyboardState(Keys.F1, Keys.F2, Keys.F3, Keys.F4, Keys.F5, Keys.F6, Keys.F9, Keys.F10), true);
        Assert.True(input.SpeedToggleRequested);
        Assert.True(input.DebugRoomRequested);
        Assert.False(input.BattleInstantVictoryRequested);
        Assert.False(input.BattleInstantDefeatRequested);
        Assert.True(input.BattleDebugRequested);
        Assert.False(input.UiConfirmRequested);
        Assert.True(input.Read(new KeyboardState(Keys.Enter, Keys.Space, Keys.Escape), true).Confirm);
        Assert.True(input.UiCancelRequested);
    }

    [Theory]
    [InlineData(ControlAction.BattleInstantVictory)]
    [InlineData(ControlAction.BattleInstantDefeat)]
    public void Instant_outcomes_are_unbound_by_default_and_bindable_on_each_device(ControlAction action)
    {
        ControlsProfile defaults = ControlsProfile.CreateDefault();
        Assert.Empty(defaults.GetBindings(action, ControlDevice.Keyboard));
        Assert.Empty(defaults.GetBindings(action, ControlDevice.Controller));
        Assert.Contains(action, ControlsProfile.PlayerActions);
        KeyboardInput input = new() { Profile = defaults.WithBinding(action, ControlDevice.Keyboard, new InputChord([Keys.F3], [])) };
        input.Read(new KeyboardState(Keys.F3), true);
        Assert.Equal(action == ControlAction.BattleInstantVictory, input.BattleInstantVictoryRequested);
        Assert.Equal(action == ControlAction.BattleInstantDefeat, input.BattleInstantDefeatRequested);
        input.Read(new KeyboardState(Keys.F3), true);
        Assert.False(input.BattleInstantVictoryRequested);
        Assert.False(input.BattleInstantDefeatRequested);
        input.Profile = defaults.WithBinding(action, ControlDevice.Controller, new InputChord([], [Buttons.LeftStick]));
        input.Read(default, Pad(), true);
        input.Read(default, Pad(Buttons.LeftStick), true);
        Assert.Equal(action == ControlAction.BattleInstantVictory, input.BattleInstantVictoryRequested);
        Assert.Equal(action == ControlAction.BattleInstantDefeat, input.BattleInstantDefeatRequested);
    }

    [Fact]
    public void Native_held_buttons_and_edges_respect_focus_and_release()
    {
        KeyboardInput input = new();
        Assert.True(input.Read(new KeyboardState(Keys.Up, Keys.Right, Keys.X, Keys.C), true).Confirm);
        Assert.Equal((ushort)0x3050, input.HeldFieldButtons);
        Runtime.InputFrame held = input.Read(new KeyboardState(Keys.Up, Keys.Right, Keys.X, Keys.C), true);
        Assert.False(held.Confirm);
        Assert.False(held.Cancel);
        Assert.Equal(1, held.Horizontal);
        Assert.Equal(-1, held.Vertical);
        input.Read(new KeyboardState(Keys.X), false);
        Assert.Equal((ushort)0, input.HeldFieldButtons);
        Assert.False(input.Read(new KeyboardState(Keys.X), true).Confirm);
        Assert.Equal((ushort)0, input.HeldFieldButtons);
        input.Read(new KeyboardState(), true);
        Assert.True(input.Read(new KeyboardState(Keys.X), true).Confirm);
    }

    [Fact]
    public void Shortcut_rearms_after_release_and_never_repeats()
    {
        KeyboardInput input = new();
        input.Read(new KeyboardState(Keys.F1), true);
        Assert.True(input.SpeedToggleRequested);
        input.Read(new KeyboardState(Keys.F1), true);
        Assert.False(input.SpeedToggleRequested);
        input.Read(new KeyboardState(), true);
        input.Read(new KeyboardState(Keys.F1), false);
        input.Read(new KeyboardState(Keys.F1), true);
        Assert.False(input.SpeedToggleRequested);
        input.Read(new KeyboardState(), true);
        input.Read(new KeyboardState(Keys.F1), true);
        Assert.True(input.SpeedToggleRequested);
    }

    [Fact]
    public void Longer_chord_consumes_its_simple_binding_until_complete_release()
    {
        KeyboardInput input = new() { Profile = ControlsProfile.CreateDefault().WithBinding(ControlAction.SpeedToggle, ControlDevice.Keyboard, new InputChord([Keys.LeftControl, Keys.X], [])) };
        input.Read(new KeyboardState(Keys.LeftControl), true);
        Assert.False(input.Read(new KeyboardState(Keys.LeftControl, Keys.X), true).Confirm);
        Assert.True(input.SpeedToggleRequested);
        Assert.Equal((ushort)0, input.HeldFieldButtons);
        Assert.False(input.Read(new KeyboardState(Keys.X), true).Confirm);
        input.Read(new KeyboardState(), true);
        Assert.True(input.Read(new KeyboardState(Keys.X), true).Confirm);
    }

    [Fact]
    public void Controller_native_actions_and_chords_use_same_sampler_without_bypass()
    {
        KeyboardInput input = new() { Profile = ControlsProfile.CreateDefault().WithBinding(ControlAction.QuickSave, ControlDevice.Controller, new InputChord([], [Buttons.Back, Buttons.X])) };
        input.Read(default, Pad(), true);
        Runtime.InputFrame confirm = input.Read(default, Pad(Buttons.A), true);
        Assert.True(confirm.Confirm);
        Assert.Equal((ushort)0x40, input.HeldFieldButtons);
        Assert.False(input.Read(default, Pad(Buttons.A), true).Confirm);
        Assert.Equal((ushort)0x40, input.HeldFieldButtons);
        input.Read(default, Pad(), true);
        Runtime.InputFrame chord = input.Read(default, Pad(Buttons.Back, Buttons.X), true);
        Assert.True(chord.Save);
        Assert.False(input.UiDetailRequested);
        Assert.Equal((ushort)0, input.HeldFieldButtons);
        input.Read(default, Pad(Buttons.X), true);
        Assert.False(input.UiDetailRequested);
        input.Read(default, Pad(), true);
        input.Read(default, Pad(Buttons.B), true);
        Assert.Equal((ushort)0x10, input.HeldFieldButtons);
    }

    [Fact]
    public void Controller_connection_and_focus_changes_do_not_generate_action_edges()
    {
        KeyboardInput input = new();
        Assert.False(input.Read(default, Pad(Buttons.A), true).Confirm);
        input.Read(default, Pad(), true);
        Assert.True(input.Read(default, Pad(Buttons.A), true).Confirm);
        input.Read(default, Pad(Buttons.A), false);
        Assert.False(input.Read(default, Pad(Buttons.A), true).Confirm);
        input.Read(default, default, true);
        Assert.False(input.Read(default, Pad(Buttons.A), true).Confirm);
        Assert.Equal((ushort)0, input.HeldFieldButtons);
        Assert.Equal(0, input.Read(default, default, true).Horizontal);
    }

    [Fact]
    public void Radial_deadzone_preserves_non_octant_direction_and_digital_axes()
    {
        KeyboardInput input = new();
        GamePadState pad = new(new Vector2(0.8f, 0.3f), new Vector2(0.7f, 0), 0, 0, Buttons.None);
        Runtime.InputFrame frame = input.Read(default, pad, true);
        Assert.InRange(frame.Horizontal, 0.7f, 0.8f);
        Assert.InRange(frame.Vertical, -0.3f, -0.2f);
        Assert.Equal(0.8f / 0.3f, frame.Horizontal / -frame.Vertical, 4);
        Assert.True(frame.CameraHorizontal > 0);
        Assert.Equal(Vector2.Zero, KeyboardInput.ApplyRadialDeadZone(new Vector2(0.1f, 0.1f)));
        Assert.InRange(KeyboardInput.ApplyRadialDeadZone(new Vector2(1, 1)).Length(), 0.999f, 1.001f);
        Runtime.InputFrame digital = input.Read(new KeyboardState(Keys.Up, Keys.Right), pad, true);
        Assert.Equal(1, digital.Horizontal);
        Assert.Equal(-1, digital.Vertical);
    }

    [Fact]
    public void Immutable_profile_persists_conflicts_and_protects_navigation_and_sticks()
    {
        ControlsProfile initial = ControlsProfile.CreateDefault();
        InputChord f2 = new([Keys.F2], []);
        Assert.Equal(new[] { ControlAction.DebugRoom }, initial.FindConflicts(ControlAction.SpeedToggle, ControlDevice.Keyboard, f2));
        Assert.Throws<InvalidOperationException>(() => initial.WithBinding(ControlAction.SpeedToggle, ControlDevice.Keyboard, f2));
        ControlsProfile next = initial.WithBinding(ControlAction.SpeedToggle, ControlDevice.Keyboard, f2, true);
        Assert.Empty(next.GetBindings(ControlAction.DebugRoom, ControlDevice.Keyboard));
        Assert.NotEmpty(initial.GetBindings(ControlAction.DebugRoom, ControlDevice.Keyboard));
        Assert.Throws<InvalidOperationException>(() => next.WithoutBinding(ControlAction.Cancel, ControlDevice.Controller));
        Assert.Throws<ArgumentException>(() => next.WithBinding(ControlAction.SpeedToggle, ControlDevice.Controller, new InputChord([], [Buttons.LeftThumbstickLeft])));
        string path = Path.Combine(Path.GetTempPath(), "memoria-controls-" + Guid.NewGuid().ToString("N"), "controls.json");
        try
        {
            next.Save(path);
            ControlsProfile loaded = ControlsProfile.Load(path);
            Assert.Equal("F2", Assert.Single(loaded.GetBindings(ControlAction.SpeedToggle, ControlDevice.Keyboard)).DisplayName);
            Assert.Empty(loaded.GetBindings(ControlAction.DebugRoom, ControlDevice.Keyboard));
            Assert.Equal("F1", Assert.Single(loaded.Reset(ControlDevice.Keyboard).GetBindings(ControlAction.SpeedToggle, ControlDevice.Keyboard)).DisplayName);
            Assert.NotEmpty(loaded.GetBindings(ControlAction.Confirm, ControlDevice.Controller));
        }
        finally
        {
            File.Delete(path);
            Directory.Delete(Path.GetDirectoryName(path)!);
        }
    }

    private static GamePadState Pad(params Buttons[] buttons) => new(Vector2.Zero, Vector2.Zero, 0, 0, buttons);

    [Fact]
    public void Replacing_profile_disarms_held_sources_until_release()
    {
        KeyboardInput input = new() { Profile = ControlsProfile.CreateDefault().WithBinding(ControlAction.Confirm, ControlDevice.Keyboard, new InputChord([Keys.F1], []), true) };
        Assert.True(input.Read(new KeyboardState(Keys.F1), true).Confirm);
        input.Profile = ControlsProfile.CreateDefault();
        input.Read(new KeyboardState(Keys.F1), true);
        Assert.False(input.SpeedToggleRequested);
        input.Read(new KeyboardState(), true);
        input.Read(new KeyboardState(Keys.F1), true);
        Assert.True(input.SpeedToggleRequested);
    }

    [Fact]
    public void Recovery_navigation_keeps_focus_safety_after_remapping()
    {
        KeyboardInput input = new() { RecoveryNavigationEnabled = true };
        input.Read(default, false);
        Assert.False(input.Read(new KeyboardState(Keys.Enter), true).Confirm);
        input.Read(default, true);
        Assert.True(input.Read(new KeyboardState(Keys.Enter), true).Confirm);
    }
}
