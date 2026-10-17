using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class WorldCameraReturnTests
{
    [Fact]
    public void DormantWorldFieldStillRejectsChangedHistoricalLoaderControls()
    {
        GameState source = GameSession.Load(TestResources.FixturePath("App.Tests.WorldCameraReturnTests.1"));
        JsonNode field = JsonNode.Parse(source.EventState!.Value.GetRawText())!;
        field["WorldControls"]!["MusicKeep207"] = 1;
        var session = new GameSession(source with { EventState = JsonSerializer.SerializeToElement(field) });
        using var world = new GameWorld(session, TestResources.GameDirectory);
        InvalidDataException error = Assert.Throws<InvalidDataException>(() => world.EnsureField());
        Assert.Contains("canonical controls", error.Message);
    }

    [Fact]
    public void StationCameraReturnsAndBothModesSurviveColdSnapshotsAndHeldInput()
    {
        var session = new GameSession(GameSession.Load(TestResources.FixturePath("App.Tests.WorldCameraReturnTests.1")));
        using var world = new GameWorld(session, TestResources.GameDirectory);
        NativeWorldControlState? controlsBeforeRestore = session.State.WorldControls;
        world.EnsureField();
        Assert.Equal(controlsBeforeRestore, session.State.WorldControls);
        Assert.Equal(-3874, session.State.World!.NativeFootRuntime!.CameraOffsetZ);
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Wait, Frames: 3)).Accepted);
        Assert.Equal(-5410, session.State.World.NativeFootRuntime.CameraOffsetZ);
        world.EnsureField();
        var restoredSession = new GameSession(JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State))!);
        using var restored = new GameWorld(restoredSession, TestResources.GameDirectory);
        restored.EnsureField();
        PlayerAction settle = new(PlayerActionKind.Wait, Frames: 4);
        Assert.Equal(world.Apply(settle), restored.Apply(settle));
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(session.State), JsonSerializer.SerializeToElement(restoredSession.State)));
        NativeFootWorldRuntimeState foot = session.State.World.NativeFootRuntime;
        Assert.Equal((-341, -7168, -112), (foot.CameraOffsetY, foot.CameraOffsetZ, foot.Forest!.CameraPitch));
        Assert.Equal(0, foot.CameraTransition!.Flags & ~16);
        PlayerAction held = new(PlayerActionKind.Wait, WorldButtons: 2, Frames: 48);
        world.Apply(held);
        foot = session.State.World.NativeFootRuntime;
        Assert.Equal((1, -512, 640, 0), (foot.AnimationVariant, foot.Forest!.CameraPitch, foot.CameraTransition!.ProjectionDistance, foot.CameraTransition.Flags));
        Assert.Equal<byte?>(1, session.State.SavedWorld!.FootReturn!.AnimationVariant108);
        world.Apply(new PlayerAction(PlayerActionKind.Wait));
        world.Apply(held);
        foot = session.State.World.NativeFootRuntime;
        Assert.Equal((0, 1024), (foot.AnimationVariant, foot.CameraTransition!.ProjectionDistance));
        Assert.InRange(foot.Forest!.CameraPitch, (short)-512, (short)-112);
        Assert.True(foot.PriorTerrainOcclusion);
        Assert.Equal(60, foot.Forest.CameraCooldown);
        Assert.Equal(0, foot.CameraTransition.Flags & ~16);
        world.SetRenderSize(320, 224);
        Assert.NotEmpty(world.Render(TimeSpan.Zero));
    }
}
