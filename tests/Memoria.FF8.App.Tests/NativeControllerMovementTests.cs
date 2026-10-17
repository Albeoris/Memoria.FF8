using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

/// <summary>Isolated native world components; authentic checkpoint reconstruction is validated separately.</summary>
public sealed class NativeControllerMovementTests
{
    [Theory]
    [InlineData(1f, 0f, 31, 0)]
    [InlineData(-1f, 0f, -32, 0)]
    [InlineData(1f, -1f, 22, -22)]
    [InlineData(-1f, 1f, -23, 23)]
    [InlineData(0.75f, -0.25f, 23, -7)]
    [InlineData(-0.75f, 0.25f, -24, 8)]
    [InlineData(0.5f, 0f, 15, 0)]
    public void Foot_uses_original_signed_components_without_snapping_stick_heading(float horizontal, float vertical, int deltaX, int deltaZ)
    {
        GameSession session = FootSession();
        using WorldMapScene scene = new(session, GameDirectory());
        session.SetWorldState(scene.State with { CameraYaw = 0, BodyYaw = 1024 });
        GameState before = session.State;
        Assert.True(scene.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: horizontal, Vertical: vertical)).Accepted);
        Assert.Equal((24576 + deltaX, -29406 + deltaZ), (scene.State.X, scene.State.Z));
        GameSession independent = new(before);
        using WorldMapScene replay = new(independent, GameDirectory());
        Assert.True(replay.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: horizontal, Vertical: vertical)).Accepted);
        Assert.Equal(JsonSerializer.Serialize(session.State), JsonSerializer.Serialize(independent.State));
    }

    [Theory]
    [InlineData(1f, 8, 1)]
    [InlineData(-0.3f, -8, 4095)]
    public void Stationary_manual_camera_precedes_damping_and_round_trips_its_snapshot(float camera, int velocity, int yaw)
    {
        GameSession session = FootSession();
        using WorldMapScene scene = new(session, GameDirectory());
        session.SetWorldState(scene.State with { CameraYaw = 0, CameraVelocity = 0 });
        GameState before = session.State;
        PlayerAction input = new(PlayerActionKind.Wait, CameraHorizontal: camera);
        Assert.True(scene.Apply(input).Accepted);
        Assert.Equal((velocity, yaw), (scene.State.CameraVelocity, scene.State.CameraYaw));
        Assert.Equal((24576, -29406), (scene.State.X, scene.State.Z));
        GameSession independent = new(before);
        using WorldMapScene replay = new(independent, GameDirectory());
        Assert.Equal(scene.State.Frame - before.World!.Frame, replay.Apply(input).FramesAdvanced);
        Assert.Equal(JsonSerializer.Serialize(session.State), JsonSerializer.Serialize(independent.State));
        for (int tick = 0; tick < 18; tick++)
        {
            Assert.Equal(scene.Apply(input), replay.Apply(input));
        }
        Assert.Equal(Math.Sign(camera) * 128, scene.State.CameraVelocity);
        Assert.Equal(JsonSerializer.Serialize(session.State), JsonSerializer.Serialize(independent.State));
        Assert.Equal(scene.Apply(new PlayerAction(PlayerActionKind.Wait)), replay.Apply(new PlayerAction(PlayerActionKind.Wait)));
        Assert.Equal(Math.Sign(camera) > 0 ? 96 : -96, scene.State.CameraVelocity);
    }

    [Fact]
    public void Invalid_manual_camera_refuses_before_state_or_history_mutation()
    {
        GameSession session = FootSession();
        using WorldMapScene scene = new(session, GameDirectory());
        GameState before = session.State;
        foreach (float camera in new[] { float.NaN, float.PositiveInfinity, -1.01f, 1.01f })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => scene.Apply(new PlayerAction(PlayerActionKind.Wait, CameraHorizontal: camera)));
            Assert.Same(before, session.State);
        }
    }

    [Theory]
    [InlineData(1f, -1f, 31, 2)]
    [InlineData(0.5f, -0.5f, 15, 1)]
    [InlineData(-0.5f, 0.5f, -16, -1)]
    public void Car_keeps_fractional_native_steering_and_throttle_and_ignores_camera_in_variant_zero(float horizontal, float vertical, int yawDelta, int speed)
    {
        GameSession session = WorldEntrySession();
        // Declared native component bank.
        NativeWorldEntryMemory memory = session.State.WorldEntryMemory!.WriteBytes(256, 2, 40).WriteBytes(265, 1, 128);
        session.Restore(session.State with { Scene = SceneKind.WorldMap, FieldId = 262, WorldArrival = new WorldTransition(63, 33, -1), WorldEntryMemory = memory });
        using WorldMapScene scene = new(session, GameDirectory());
        GameSession independent = new(session.State);
        using WorldMapScene other = new(independent, GameDirectory());
        Assert.True(scene.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: horizontal, Vertical: vertical, CameraHorizontal: 1)).Accepted);
        Assert.True(other.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: horizontal, Vertical: vertical)).Accepted);
        Assert.Equal(2256 + yawDelta, scene.State.BodyYaw);
        Assert.Equal(speed, scene.State.NativeCarRuntime!.Speed);
        Assert.Equal(JsonSerializer.Serialize(scene.State), JsonSerializer.Serialize(other.State));
    }

    [Theory]
    [InlineData(0.5f, -0.5f, 15, 1)]
    [InlineData(-0.5f, 0.5f, -16, -1)]
    public void Secondary_stick_routes_fractional_vehicle_axes_to_the_existing_native_motor(float horizontal, float vertical, int yawDelta, int speed)
    {
        GameSession session = WorldEntrySession();
        NativeWorldEntryMemory memory = session.State.WorldEntryMemory!.WriteBytes(256, 2, 40).WriteBytes(265, 1, 128);
        session.Restore(session.State with { Scene = SceneKind.WorldMap, FieldId = 262, WorldArrival = new WorldTransition(63, 33, -1), WorldEntryMemory = memory });
        using WorldMapScene scene = new(session, GameDirectory());
        InputFrame input = new(-1, 1, false, false, false, false, MenuVertical: 1, MenuHorizontal: -1, CameraHorizontal: horizontal, SecondaryVertical: vertical, SecondaryHorizontal: horizontal);
        InputFrame routed = ControllerGameplayInput.Resolve(input, session.State);
        Assert.Equal((horizontal, vertical, 0f, 0f), (routed.Horizontal, routed.Vertical, routed.CameraHorizontal, routed.SecondaryVertical));
        Assert.Equal((1, -1), (routed.MenuVertical, routed.MenuHorizontal));
        Assert.True(scene.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: routed.Horizontal, Vertical: routed.Vertical, CameraHorizontal: routed.CameraHorizontal)).Accepted);
        Assert.Equal(2256 + yawDelta, scene.State.BodyYaw);
        Assert.Equal(speed, scene.State.NativeCarRuntime!.Speed);

        InputFrame keyboardCamera = new(0.25f, -0.75f, false, false, false, false, CameraHorizontal: 1);
        InputFrame retained = ControllerGameplayInput.Resolve(keyboardCamera, session.State);
        Assert.Equal((0.25f, -0.75f), (retained.Horizontal, retained.Vertical));
        Assert.Equal((0f, 0f), (retained.CameraHorizontal, retained.SecondaryVertical));
        InputFrame steeringOnly = ControllerGameplayInput.Resolve(input with { SecondaryVertical = 0 }, session.State);
        Assert.Equal((horizontal, 1f), (steeringOnly.Horizontal, steeringOnly.Vertical));
        InputFrame throttleOnly = ControllerGameplayInput.Resolve(input with { SecondaryHorizontal = 0 }, session.State);
        Assert.Equal((-1f, vertical), (throttleOnly.Horizontal, throttleOnly.Vertical));
    }

    [Theory]
    [InlineData(SceneKind.Field)]
    [InlineData(SceneKind.WorldMap)]
    public void Secondary_stick_preserves_field_and_foot_camera_input(SceneKind scene)
    {
        InputFrame input = new(0.75f, -0.25f, false, false, false, false, MenuVertical: 1, CameraHorizontal: -0.5f, SecondaryVertical: 0.25f, SecondaryHorizontal: -0.5f);
        Assert.Equal(input, ControllerGameplayInput.Resolve(input, new GameState { Scene = scene }));
    }

    [Fact]
    public void World_snapshot_restores_and_continues_fractional_movement_and_manual_camera()
    {
        GameSession fresh = FootSession();
        GameState source = fresh.State;
        GameState recreated = JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(source))!;
        GameSession session = new(recreated);
        using WorldMapScene world = new(session, GameDirectory());
        foreach (PlayerAction action in new PlayerAction[] { new(PlayerActionKind.Wait, CameraHorizontal: 0.5f), new(PlayerActionKind.Move, Horizontal: 0.75f, Vertical: -0.25f, CameraHorizontal: -0.4f) })
        {
            Assert.True(world.Apply(action).Accepted);
        }
        GameState final = session.State;
        GameState replayed = JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(final))!;
        Assert.Equal(JsonSerializer.Serialize(final), JsonSerializer.Serialize(replayed));
        GameSession independent = new(replayed);
        using WorldMapScene restored = new(independent, GameDirectory());
        Assert.Equal(world.Render(640, 448), restored.Render(640, 448));
        PlayerAction next = new(PlayerActionKind.Move, Horizontal: -0.25f, Vertical: 0.75f, CameraHorizontal: 1);
        Assert.Equal(world.Apply(next), restored.Apply(next));
        Assert.Equal(JsonSerializer.Serialize(session.State), JsonSerializer.Serialize(independent.State));
        Assert.Equal(world.Render(640, 448), restored.Render(640, 448));
    }

    private static GameSession FootSession()
    {
        GameSession session = WorldEntrySession();
        using WorldMapScene scene = new(session, GameDirectory());
        // Component input begins only after the original loader's ordinary zero-input callbacks.
        while (NativeWorldEntryControl.DelayAfterCallback(scene.State.Frame + 1, 0) > 0)
        {
            Assert.Equal(1, scene.Apply(new PlayerAction(PlayerActionKind.Wait)).FramesAdvanced);
        }
        Assert.Equal(23, scene.State.Frame);
        Assert.Equal((24576, -29406), (scene.State.X, scene.State.Z));
        Assert.Null(scene.CurrentDialogue);
        return session;
    }

    private static GameSession WorldEntrySession()
    {
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        using GameWorld initialization = new(session, GameDirectory());
        // Declared isolated world component entry; this does not reconstruct a field journey.
        session.Restore(session.State with { Scene = SceneKind.WorldMap, WorldArrival = new WorldTransition(0, 0, 0) });
        return session;
    }

    private static string GameDirectory()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed world corpus is required.");
        return directory;
    }
}
