using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeTrainStationChoiceTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public void InvalidVisibleChoicePreservesTheCompleteLoadedSession(int choice)
    {
        var session = new GameSession(LoadStationPrompt());
        using var world = new GameWorld(session, GameDirectory());
        Assert.True(world.EnsureField());
        AssertStationPrompt(world);
        JsonElement before = JsonSerializer.SerializeToElement(session.State);
        JsonElement dormantField = world.Events.Capture();

        Assert.Throws<ArgumentOutOfRangeException>(() => world.Apply(new PlayerAction(PlayerActionKind.Choose, Value: choice)));

        Assert.True(JsonElement.DeepEquals(before, JsonSerializer.SerializeToElement(session.State)));
        Assert.True(JsonElement.DeepEquals(dormantField, world.Events.Capture()));
        AssertStationPrompt(world);
    }

    [Fact]
    public void DismountUsesTheOriginalStationPoseAndRestoresTheNextFootCallbackAndPixels()
    {
        var session = new GameSession(LoadStationPrompt());
        using var world = new GameWorld(session, GameDirectory());
        world.EnsureField();
        AssertStationPrompt(world);
        WorldState before = session.State.World!;
        WorldTransition packet = session.State.WorldArrival!;

        PlayerActionResult dismount = world.Apply(new PlayerAction(PlayerActionKind.Choose, Value: 1));

        Assert.True(dismount.Accepted);
        Assert.Equal(1, dismount.FramesAdvanced);
        Assert.Null(world.CurrentDialogue);
        WorldState station = session.State.World!;
        Assert.Equal(before.Frame + 1, station.Frame);
        Assert.Equal((-28338, -23747, -794, 0), (station.X, station.Z, station.NativeHeight, station.BodyYaw));
        Assert.Equal(packet, session.State.WorldArrival);
        Assert.Null(station.NativeTrainRuntime);
        NativeFootWorldRuntimeState foot = Assert.IsType<NativeFootWorldRuntimeState>(station.NativeFootRuntime);
        Assert.Equal(NativeFootWorldRuntimeState.TrainDismountRule, foot.Rule);
        Assert.Equal((byte)0, foot.EntityType);
        Assert.Equal(station.Frame, foot.TrainDismount!.SourceCallback);
        Assert.Equal(0, foot.TrainDismount.Station.Index);
        Assert.Equal(station.Frame, foot.StationTrainProcess!.Callback);
        Assert.Equal(0, foot.StationTrainProcess.Primary.Pause);

        var restoredSession = new GameSession(Clone(session.State));
        using var restored = new GameWorld(restoredSession, GameDirectory());
        restored.EnsureField();
        world.SetRenderSize(320, 224);
        restored.SetRenderSize(320, 224);
        AssertSameState(session, restoredSession);
        Assert.Equal(world.Render(TimeSpan.Zero), restored.Render(TimeSpan.Zero));

        PlayerAction next = new(PlayerActionKind.Wait);
        Assert.Equal(world.Apply(next), restored.Apply(next));
        Assert.Equal(station.Frame + 1, session.State.World!.Frame);
        Assert.NotNull(session.State.World.NativeFootRuntime!.StationTrainProcess!.ProjectionCamera);
        Assert.Equal(session.State.World.Frame, session.State.World.NativeFootRuntime.StationTrainProcess.Callback);
        AssertSameState(session, restoredSession);
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
        Assert.Equal(world.Render(TimeSpan.Zero), restored.Render(TimeSpan.Zero));
    }

    [Fact]
    public void DecliningDismountReleasesTheSameTrainBankAndResumesItsNextCallback()
    {
        var session = new GameSession(LoadStationPrompt());
        using var world = new GameWorld(session, GameDirectory());
        world.EnsureField();
        AssertStationPrompt(world);
        long sourceFrame = session.State.World!.Frame;

        PlayerActionResult decline = world.Apply(new PlayerAction(PlayerActionKind.Choose, Value: 2));

        Assert.True(decline.Accepted);
        Assert.Equal(1, decline.FramesAdvanced);
        Assert.Null(world.CurrentDialogue);
        Assert.Null(session.State.World!.NativeFootRuntime);
        NativeTrainWorldRuntimeState train = Assert.IsType<NativeTrainWorldRuntimeState>(session.State.World.NativeTrainRuntime);
        Assert.Equal(sourceFrame + 1, train.Callback);
        Assert.Equal(0, train.Primary.Pause);
        Assert.Contains(train.Callback, train.StationDeclines);

        var restoredSession = new GameSession(Clone(session.State));
        using var restored = new GameWorld(restoredSession, GameDirectory());
        restored.EnsureField();
        PlayerAction next = new(PlayerActionKind.Wait);
        Assert.Equal(world.Apply(next), restored.Apply(next));
        Assert.Equal(train.Callback + 1, session.State.World.NativeTrainRuntime!.Callback);
        Assert.Null(world.CurrentDialogue);
        AssertSameState(session, restoredSession);
    }

    private static void AssertStationPrompt(GameWorld world)
    {
        Assert.NotNull(world.CurrentDialogue);
        Assert.Equal(4, world.CurrentDialogue.MessageId);
        Assert.Equal(0x5484B0, world.CurrentDialogue.Operation);
        Assert.Equal((1, 2), (world.CurrentDialogue.FirstChoice, world.CurrentDialogue.LastChoice));
        Assert.Equal(-1, world.WorldScene!.State.NativeTrainRuntime!.Primary.Pause);
    }

    private static void AssertSameState(GameSession expected, GameSession actual)
    {
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(expected.State), JsonSerializer.SerializeToElement(actual.State)));
    }

    private static GameState Clone(GameState state)
    {
        return JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(state))!;
    }

    private static GameState LoadStationPrompt()
    {
        DirectoryInfo? repository = new(TestResources.FixtureDirectory);
        Assert.NotNull(repository);
        GameState source = GameSession.Load(TestResources.FixturePath("App.Tests.NativeTrainStationChoiceTests.1"));
        return source;
    }

    private static string GameDirectory()
    {
        return TestResources.GameDirectory;
    }
}
