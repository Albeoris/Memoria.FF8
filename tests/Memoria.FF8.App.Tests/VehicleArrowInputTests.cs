using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class VehicleArrowInputTests
{
    [Theory]
    [InlineData(0x1000, 0x1080, -1f)]
    [InlineData(0x4000, 0x4010, 1f)]
    [InlineData(0x5000, 0x5000, 0f)]
    [InlineData(0x1010, 0x1010, 1f)]
    [InlineData(0x4080, 0x4080, -1f)]
    [InlineData(0x1090, 0x1090, 1f)]
    [InlineData(0x4090, 0x4090, 1f)]
    public void Garden_arrows_add_throttle_with_explicit_buttons_taking_priority(int buttons, int packet, float vertical)
    {
        InputFrame input = new(0, 0, false, false, false, false);
        PlayerAction action = ControllerGameplayInput.CreateWorldAction(input, GardenState(), (ushort)buttons);
        Assert.Equal(PlayerActionKind.WorldButtons, action.Kind);
        Assert.Equal(packet, action.Value);
        Assert.Equal((0f, vertical), ControllerGameplayInput.ReadVehicleAxes((uint)action.Value, action.Horizontal, action.Vertical));
    }

    [Theory]
    [InlineData(0x3080, 1f, -1f)]
    [InlineData(0xC010, -1f, 1f)]
    public void Garden_directional_throttle_keeps_diagonal_steering(int buttons, float horizontal, float vertical)
    {
        InputFrame input = new(0, 0, false, false, false, false);
        ushort directions = (ushort)(buttons & 0xF000);
        PlayerAction action = ControllerGameplayInput.CreateWorldAction(input, GardenState(), directions);
        Assert.Equal(buttons, action.Value);
        Assert.Equal((horizontal, vertical), ControllerGameplayInput.ReadVehicleAxes((uint)action.Value, action.Horizontal, action.Vertical));
    }

    [Theory]
    [InlineData(0.5f, 0f, 0.5f)]
    [InlineData(0.5f, -0.75f, -0.75f)]
    public void Garden_keeps_existing_primary_analog_throttle_and_accepts_the_right_stick_override(float primary, float secondary, float vertical)
    {
        InputFrame input = new(0.25f, primary, false, false, false, false, SecondaryVertical: secondary);
        PlayerAction action = ControllerGameplayInput.CreateWorldAction(input, GardenState(), 0);
        Assert.Equal((0.25f, vertical), ControllerGameplayInput.ReadVehicleAxes((uint)action.Value, action.Horizontal, action.Vertical));
    }

    [Theory]
    [InlineData(0x1000, 7)]
    [InlineData(0x4000, -7)]
    [InlineData(0x1010, -7)]
    [InlineData(0x4080, 7)]
    public void Garden_host_arrows_and_button_priority_reach_the_current_native_motor(int buttons, int speed)
    {
        InputFrame input = new(0, 0, false, false, false, false);
        PlayerAction action = ControllerGameplayInput.CreateWorldAction(input, GardenState(), (ushort)buttons);
        (float horizontal, float vertical) = ControllerGameplayInput.ReadVehicleAxes((uint)action.Value, action.Horizontal, action.Vertical);
        NativeGardenMotorResult result = NativeGardenMotor.Step(0, 0, 0, 0, 0, (sbyte)(-horizontal * 127), (sbyte)(-vertical * 127));
        Assert.Equal(speed, result.Speed);
    }

    [Fact]
    public void Walking_beside_Garden_keeps_foot_controls_and_does_not_add_throttle()
    {
        GameState garden = GardenState();
        NativeFootWorldRuntimeState foot = new() { ProofVersion = 1, Rule = "host-input-routing", EntityType = 0, AnimationVariant = 0, MapDisplayMode = 0, CameraOffsetY = 0, CameraOffsetZ = 0, ReloadSourceWorldFrame = null };
        GameState state = garden with { World = garden.World! with { NativeFootRuntime = foot } };
        InputFrame input = new(0.5f, -1, false, false, false, false, CameraHorizontal: 0.25f);
        PlayerAction action = ControllerGameplayInput.CreateWorldAction(input, state, 0x1000);
        Assert.Equal(new PlayerAction(PlayerActionKind.Move, Horizontal: 0.5f, Vertical: -1, CameraHorizontal: 0.25f), action);
    }

    [Theory]
    [InlineData(0x1000, 2)]
    [InlineData(0x4000, -2)]
    public void Standalone_car_host_arrows_reach_the_native_motor_and_restore(int buttons, int speed)
    {
        string fixture = TestResources.FixturePath("App.Tests.AutoPlayCarWorldTests.1");
        GameSession session = new(GameSession.Load(fixture));
        using GameWorld world = new(session, TestResources.GameDirectory, validateWorldPresentation: false);
        Assert.True(world.EnsureField());
        Assert.Equal(0, session.State.World!.NativeCarRuntime!.Speed);
        GameSession independent = new(JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State))!);
        using GameWorld restored = new(independent, TestResources.GameDirectory, validateWorldPresentation: false);
        Assert.True(restored.EnsureField());
        PlayerAction action = ControllerGameplayInput.CreateWorldAction(new(0, 0, false, false, false, false), session.State, (ushort)buttons);
        Assert.True(world.Apply(action).Accepted);
        Assert.True(restored.Apply(action).Accepted);
        Assert.Equal(speed, session.State.World!.NativeCarRuntime!.Speed);
        Assert.Equal(action.Value, session.State.WorldProcess!.InputHistory!.CurrentButtons);
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(session.State), JsonSerializer.SerializeToElement(independent.State)));
    }

    private static GameState GardenState()
    {
        // Host routing markers only; actual motor/restore checks use standalone fixtures above.
        NativeGardenWorldRuntimeState garden = new()
        {
            Version = 1,
            Rule = NativeGardenWorldRuntimeState.RuntimeRule,
            Speed = 0,
            GroundYaw = 0,
            CameraFollowSpeed = 0,
            CameraPitch = 0,
            AnimationVariant = 0,
            MapDisplayMode = 0,
            Allocation = null!,
            Objects = [],
            CameraDefaults = null!,
            GroundCache = null!,
            MovementMagnitude20409F4 = 0,
            PrimaryContact = -1,
            BroadContact = -1,
            Buttons = null!,
            TransferControls = null!,
            Forest = null!
        };
        return new GameState { Scene = SceneKind.WorldMap, World = new WorldState { NativeGardenRuntime = garden } };
    }
}
