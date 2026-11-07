using System.Security.Cryptography;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlayWorldWalkingIntegrationTests(ITestOutputHelper output)
{
    [Fact]
    public void AuthenticWorldFootWithAuxiliaryObjectsWalksThroughItsNativeEntrance()
    {
        string source = TestResources.FixturePath("App.Tests.AutoPlayWorldWalkingIntegrationTests.1");
        byte[] original = Hash(source);
        GameSession session = new(GameSession.Load(source));
        using GameWorld world = new(session, TestResources.GameDirectory, validateWorldPresentation: false);
        world.EnsureField();
        GameState before = session.State;
        WorldMapScene scene = world.WorldScene!;
        Assert.Equal(SceneKind.WorldMap, before.Scene);
        Assert.Equal<ushort>(290, before.WorldEntryMemory!.StoryWord);
        Assert.NotNull(before.World!.NativeFootRuntime!.NormalObjects);
        try
        {
            IReadOnlyList<WorldAutoPlayTarget> targets = scene.DiscoverAutoPlayEntrances();
            WorldAutoPlayTarget entrance = Assert.Single(targets, target => target.DestinationField == 899);
            Assert.Same(before, session.State);
            Assert.NotEmpty(scene.GetAutoPlayEntrancePath(entrance));
            Assert.Same(before, session.State);

            int movement = 0;
            WorldAutoPlayTravelResult travel = world.ExecuteAutoPlayWorldTarget(entrance, () =>
            {
                Assert.NotNull(session.State.World!.NativeFootRuntime!.NormalObjects);
                movement++;
            });

            output.WriteLine($"World movement callbacks={travel.Action.FramesAdvanced}; result={travel.Action.Trigger}; progress={movement}; detail={travel.Action.Detail}");
            Assert.True(travel.MatchedTarget, travel.Action.Detail);
            Assert.Equal(PlayerTriggerKind.FieldTransition, travel.Action.Trigger.Kind);
            Assert.Equal(899, session.State.FieldId);
            Assert.True(movement > 0);
            Assert.InRange(travel.Action.FramesAdvanced, 1, 256);
            Assert.NotNull(scene.LastDeparture!.NativeFootRuntime!.NormalObjects);
            Assert.Equal<ushort>(290, session.State.WorldEntryMemory!.StoryWord);
        }
        finally
        {
            Assert.Equal(original, Hash(source));
        }
    }

    [Fact]
    public void ObstructedWorldCrossingReplansBeforeTryingAnotherEntrance()
    {
        string source = TestResources.FixturePath("App.Tests.AutoPlayWorldWalkingIntegrationTests.2");
        byte[] original = Hash(source);
        GameSession session = new(GameSession.Load(source));
        using GameWorld world = new(session, TestResources.GameDirectory, validateWorldPresentation: false);
        world.EnsureField();
        Assert.NotNull(session.State.World!.NativeFootRuntime!.NormalObjects);

        WorldAutoPlayTarget first = Assert.Single(world.DiscoverAutoPlayWorldTargets(), target => target.DestinationField == 792);
        WorldAutoPlayTravelResult travel = world.ExecuteAutoPlayWorldTarget(first);
        output.WriteLine($"First route callbacks={travel.Action.FramesAdvanced}; {travel.Action.Detail}");
        Assert.True(travel.MatchedTarget || travel.Action.FramesAdvanced > 213, travel.Action.Detail);
        if (!travel.MatchedTarget)
        {
            Assert.Equal(SceneKind.WorldMap, session.State.Scene);
            WorldAutoPlayTarget next = Assert.Single(world.DiscoverAutoPlayWorldTargets(), target => target.DestinationField == 899);
            travel = world.ExecuteAutoPlayWorldTarget(next);
            Assert.True(travel.MatchedTarget, travel.Action.Detail);
            Assert.Equal(899, session.State.FieldId);
        }
        Assert.Equal(PlayerTriggerKind.FieldTransition, travel.Action.Trigger.Kind);
        Assert.Equal<ushort>(324, session.State.WorldEntryMemory!.StoryWord);
        Assert.Equal(original, Hash(source));
    }

    [Fact]
    public void AuthenticTrainDismountDiscoversEntrancesWithActiveStationTrains()
    {
        string source = TestResources.FixturePath("App.Tests.AutoPlayWorldWalkingIntegrationTests.3");
        GameSession session = new(GameSession.Load(source));
        using GameWorld world = new(session, TestResources.GameDirectory, validateWorldPresentation: false);
        world.EnsureField();
        Assert.NotNull(session.State.World!.NativeTrainRuntime);
        Assert.True(world.Apply(new(PlayerActionKind.Choose, Value: 1)).Accepted);
        GameState dismounted = session.State;
        Assert.Null(dismounted.World!.NativeTrainRuntime);
        Assert.NotNull(dismounted.World.NativeFootRuntime!.StationTrains);
        WorldAutoPlayTarget entrance = world.WorldScene!.DiscoverAutoPlayEntrances()[0];
        Assert.Same(dismounted, session.State);
        WorldAutoPlayTravelResult travel = world.ExecuteAutoPlayWorldTarget(entrance);
        output.WriteLine($"Station route {entrance.Id} callbacks={travel.Action.FramesAdvanced}; {travel.Action.Detail}");
        Assert.True(travel.MatchedTarget, travel.Action.Detail);
        Assert.Equal(PlayerTriggerKind.FieldTransition, travel.Action.Trigger.Kind);
        Assert.NotNull(dismounted.World.NativeFootRuntime.StationTrainProcess);
    }

    [Fact]
    public void AuthenticTrainContinuingPastStationRestoresItsReversedRailPhase()
    {
        string source = TestResources.FixturePath("App.Tests.AutoPlayWorldWalkingIntegrationTests.3");
        GameSession session = new(GameSession.Load(source));
        using GameWorld world = new(session, TestResources.GameDirectory, validateWorldPresentation: false);
        world.EnsureField();
        Assert.True(world.Apply(new(PlayerActionKind.Choose, Value: 2)).Accepted);
        WorldMapScene scene = world.WorldScene!;
        NativeTrainWorldRuntimeState train = scene.State.NativeTrainRuntime!;
        long limit = train.Callback + 1200;
        while (train.Callback < limit && train.Primary.EndpointProjection is null && train.Alternate?.EndpointProjection is null)
        {
            PlayerActionResult step = scene.Apply(new(PlayerActionKind.Wait));
            Assert.True(step.Accepted, step.Detail);
            Assert.Equal(1, step.FramesAdvanced);
            train = scene.State.NativeTrainRuntime!;
        }
        Assert.NotNull(train.Primary.EndpointProjection ?? train.Alternate?.EndpointProjection);
        var restoredSession = new GameSession(session.State);
        using var restored = new WorldMapScene(restoredSession, TestResources.GameDirectory);
        PlayerAction wait = new(PlayerActionKind.Wait);
        Assert.Equal(scene.Apply(wait), restored.Apply(wait));
        Assert.Equal(scene.State.NativeTrainRuntime!.Primary.Direction, restored.State.NativeTrainRuntime!.Primary.Direction);
        Assert.Equal(scene.State.NativeTrainRuntime.Camera, restored.State.NativeTrainRuntime.Camera);
        Assert.True(scene.State.NativeTrainRuntime.CameraReceipt.Nodes.SequenceEqual(restored.State.NativeTrainRuntime.CameraReceipt.Nodes));
    }

    private static byte[] Hash(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return SHA256.HashData(stream);
    }
}
