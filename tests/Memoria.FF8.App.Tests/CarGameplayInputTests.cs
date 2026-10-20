using Memoria.FF8.Events;
using Memoria.FF8.Runtime;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8.App.Tests;

public sealed class CarGameplayInputTests
{
    [Fact]
    public void Resuming_host_pause_does_not_submit_an_unsupported_native_menu_button()
    {
        KeyboardInput keyboard = new();
        InputFrame input = keyboard.Read(new KeyboardState(Keys.A, Keys.S), true);
        Assert.True(keyboard.PauseRequested);
        Assert.Equal(0x880, keyboard.HeldControlButtons);
        PlayerAction action = ControllerGameplayInput.CreateWorldAction(input, CarState(), keyboard.HeldControlButtons);
        Assert.Equal(0x80, action.Value);
        Assert.Equal((0f, -1f), ControllerGameplayInput.ReadVehicleAxes((uint)action.Value, action.Horizontal, action.Vertical));
    }

    [Theory]
    [InlineData(Keys.NumPad6, 0x80, -1f, "N6")]
    [InlineData(Keys.NumPad8, 0x10, 1f, "N8")]
    [InlineData(Keys.NumPad4, 0x40, 0f, "N4")]
    [InlineData(Keys.E, 0x02, 0f, "E")]
    public void Remapped_buttons_emit_the_native_car_packet_shown_by_the_binding_hint(Keys key, int buttons, float vertical, string hint)
    {
        KeyboardInput keyboard = new() { Profile = CarProfile() };
        InputFrame input = keyboard.Read(new KeyboardState(key), true);
        PlayerAction action = ControllerGameplayInput.CreateWorldAction(input, CarState(), keyboard.HeldControlButtons);
        (float horizontal, float throttle) = ControllerGameplayInput.ReadVehicleAxes((uint)action.Value, action.Horizontal, action.Vertical);

        Assert.Equal(PlayerActionKind.WorldButtons, action.Kind);
        Assert.Equal(buttons, action.Value);
        Assert.Equal((ushort)buttons, keyboard.HeldControlButtons);
        Assert.Equal(0, horizontal);
        Assert.Equal(vertical, throttle);
        Assert.Equal(0, action.CameraHorizontal);
        Assert.Equal(hint, keyboard.NativeButtonBindingHint((ushort)buttons));
    }

    [Fact]
    public void Car_tutorial_face_and_camera_hints_match_the_active_remapped_buttons()
    {
        KeyboardInput keyboard = new() { Profile = CarProfile() };

        Assert.Equal("N8 Back\nN6 Forward\nN4 Get on/off\nE POV", DialogueTextFormatter.Format("{Key Menu} Back{Line}{Key Cards} Forward{Line}{Key Confirm} Get on/off{Line}{Key EscapeKey2} POV", buttonBindingHint: keyboard.NativeButtonBindingHint));
    }

    [Theory]
    [InlineData(Keys.NumPad0, "N0")]
    [InlineData(Keys.NumPad1, "N1")]
    [InlineData(Keys.NumPad2, "N2")]
    [InlineData(Keys.NumPad3, "N3")]
    [InlineData(Keys.NumPad4, "N4")]
    [InlineData(Keys.NumPad5, "N5")]
    [InlineData(Keys.NumPad6, "N6")]
    [InlineData(Keys.NumPad7, "N7")]
    [InlineData(Keys.NumPad8, "N8")]
    [InlineData(Keys.NumPad9, "N9")]
    public void Numpad_binding_hints_use_compact_key_labels(Keys key, string expected)
    {
        InputChord chord = new([key], []);

        Assert.Equal(expected, chord.DisplayName);
    }

    [Theory]
    [InlineData(Keys.W, 0x1000)]
    [InlineData(Keys.S, 0x4000)]
    [InlineData(Keys.Up, 0x1000)]
    [InlineData(Keys.Down, 0x4000)]
    public void Vertical_direction_bindings_add_car_throttle_without_changing_the_sampled_buttons(Keys key, int buttons)
    {
        KeyboardInput keyboard = new() { Profile = CarProfile() };
        InputFrame input = keyboard.Read(new KeyboardState(key), true);
        PlayerAction action = ControllerGameplayInput.CreateWorldAction(input, CarState(), keyboard.HeldControlButtons);

        Assert.NotEqual(0, input.Vertical);
        Assert.Equal(PlayerActionKind.WorldButtons, action.Kind);
        int throttle = buttons == 0x1000 ? 0x80 : 0x10;
        Assert.Equal(buttons | throttle, action.Value);
        Assert.Equal((ushort)buttons, keyboard.HeldControlButtons);
        Assert.Equal((0f, buttons == 0x1000 ? -1f : 1f), ControllerGameplayInput.ReadVehicleAxes((uint)action.Value, action.Horizontal, action.Vertical));
    }

    [Theory]
    [InlineData(Keys.NumPad6, Keys.NumPad8, 0x90, 0f, 1f)]
    [InlineData(Keys.A, Keys.D, 0xA000, -1f, 0f)]
    public void Opposing_digital_buttons_retain_native_reverse_and_left_priorities(Keys first, Keys second, int buttons, float horizontal, float vertical)
    {
        KeyboardInput keyboard = new() { Profile = CarProfile() };
        InputFrame input = keyboard.Read(new KeyboardState(first, second), true);
        PlayerAction action = ControllerGameplayInput.CreateWorldAction(input, CarState(), keyboard.HeldControlButtons);

        Assert.Equal(buttons, action.Value);
        Assert.Equal((horizontal, vertical), ControllerGameplayInput.ReadVehicleAxes((uint)action.Value, action.Horizontal, action.Vertical));
    }

    [Theory]
    [InlineData(0.7f, 0.3f, -0.4f, 0.6f)]
    [InlineData(-0.6f, -0.4f, 0.5f, -0.7f)]
    [InlineData(0f, 0f, 0.8f, 0f)]
    public void Car_analog_input_uses_left_stick_steering_and_right_stick_vertical_throttle(float leftX, float leftY, float rightX, float rightY)
    {
        KeyboardInput keyboard = new() { Profile = CarProfile().WithControllerSettings(0, 0, 0.5f) };
        GamePadState pad = new(new Vector2(leftX, leftY), new Vector2(rightX, rightY), 0, 0, Buttons.None);
        InputFrame input = keyboard.Read(default, pad, true);
        PlayerAction action = ControllerGameplayInput.CreateWorldAction(input, CarState(), keyboard.HeldControlButtons);
        (float horizontal, float vertical) = ControllerGameplayInput.ReadVehicleAxes((uint)action.Value, action.Horizontal, action.Vertical);

        Assert.Equal(0, action.Value);
        Assert.Equal(leftX, horizontal, 5);
        Assert.Equal(-rightY, vertical, 5);
        Assert.Equal(0, action.CameraHorizontal);
    }

    [Theory]
    [InlineData(0x80u, 0.35f, -1f)]
    [InlineData(0x10u, 0.35f, 1f)]
    [InlineData(0x90u, 0.35f, 1f)]
    [InlineData(0x1000u, 0f, -0.6f)]
    [InlineData(0x4000u, 0f, -0.6f)]
    [InlineData(0x2000u, 1f, -0.6f)]
    [InlineData(0x8000u, -1f, -0.6f)]
    [InlineData(0xF000u, -1f, -0.6f)]
    public void Native_digital_axes_override_only_their_corresponding_analog_sources(uint buttons, float horizontal, float vertical)
    {
        Assert.Equal((horizontal, vertical), ControllerGameplayInput.ReadVehicleAxes(buttons, 0.35f, -0.6f));
    }

    [Fact]
    public void Slow_left_stick_field_walking_does_not_become_car_reverse()
    {
        KeyboardInput keyboard = new() { Profile = CarProfile().WithControllerSettings(0, 0, 0.5f) };
        GamePadState pad = new(new Vector2(0.3f, 0), Vector2.Zero, 0, 0, Buttons.None);
        InputFrame input = keyboard.Read(default, pad, true);
        PlayerAction action = ControllerGameplayInput.CreateWorldAction(input, CarState(), keyboard.HeldControlButtons);

        Assert.Equal((ushort)0x10, keyboard.HeldFieldButtons);
        Assert.Equal((ushort)0, keyboard.HeldControlButtons);
        Assert.Equal(0, action.Value);
        Assert.Equal((0.3f, 0f), ControllerGameplayInput.ReadVehicleAxes((uint)action.Value, action.Horizontal, action.Vertical));
    }

    [Theory]
    [InlineData(SceneKind.WorldMap, false, 0.75f, -0.25f, PlayerActionKind.Move)]
    [InlineData(SceneKind.WorldMap, false, 0f, 0f, PlayerActionKind.Wait)]
    [InlineData(SceneKind.Field, true, 0.75f, -0.25f, PlayerActionKind.Move)]
    public void Non_car_actions_preserve_movement_camera_and_existing_world_button_filter(SceneKind scene, bool car, float horizontal, float vertical, PlayerActionKind kind)
    {
        GameState state = car ? CarState() with { Scene = scene } : new GameState { Scene = scene };
        InputFrame input = new(horizontal, vertical, false, false, false, false, CameraHorizontal: -0.4f, SecondaryHorizontal: 0.6f, SecondaryVertical: 0.8f);
        PlayerAction action = ControllerGameplayInput.CreateWorldAction(input, state, 0xFFFF);

        Assert.Equal(kind, action.Kind);
        Assert.Equal(horizontal, action.Horizontal);
        Assert.Equal(vertical, action.Vertical);
        Assert.Equal(-0.4f, action.CameraHorizontal);
        Assert.Equal((ushort)0x02, action.WorldButtons);
        Assert.Equal(0, action.Value);
    }

    private static ControlsProfile CarProfile()
    {
        return ControlsProfile.CreateDefault().WithBinding(ControlAction.Card, ControlDevice.Keyboard, new([Keys.NumPad6], [])).WithBinding(ControlAction.Cancel, ControlDevice.Keyboard, new([Keys.NumPad8], [])).WithBinding(ControlAction.Confirm, ControlDevice.Keyboard, new([Keys.NumPad4], [])).WithBinding(ControlAction.CameraRight, ControlDevice.Keyboard, new([Keys.E], [])).WithBinding(ControlAction.MoveUp, ControlDevice.Keyboard, new([Keys.W], [])).WithBinding(ControlAction.MoveDown, ControlDevice.Keyboard, new([Keys.S], [])).WithBinding(ControlAction.MoveLeft, ControlDevice.Keyboard, new([Keys.A], []), true).WithBinding(ControlAction.MoveRight, ControlDevice.Keyboard, new([Keys.D], []), true);
    }

    private static GameState CarState()
    {
        return new GameState
        {
            Scene = SceneKind.WorldMap,
            World = new WorldState
            {
                NativeCarRuntime = new NativeCarWorldRuntimeState
                {
                    Version = 4,
                    Rule = NativeCarWorldRuntimeState.LoadRule,
                    EntityType = 32,
                    ShapeId = 74,
                    Speed = 0,
                    CameraFollowSpeed = 0,
                    TerrainTiltBlend = 0,
                    ModelTilt = 0,
                    MapDisplayMode = 0
                }
            }
        };
    }
}
