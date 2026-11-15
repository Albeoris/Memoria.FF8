using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class GardenBoardingActivityTests
{
    [Fact]
    public void RefusedActionCanBeFollowedByApproachAndActualGardenBoarding()
    {
        string source = TestResources.FixturePath("App.Tests.GardenBoardingActivityTests.2");
        string directory = TestResources.GameDirectory;
        GameSession session = new(GameplaySaveSessionLoader.Load(source, directory));
        using GameWorld world = new(session, directory);
        Assert.True(world.Apply(new(PlayerActionKind.Wait, Frames: 24)).Accepted);
        Assert.True(world.Apply(new(PlayerActionKind.Confirm)).Accepted);
        for (int callback = 0; callback < 180; callback++)
        {
            WorldState state = session.State.World!;
            NativeGardenWorldRuntimeState garden = state.NativeGardenRuntime!;
            if (garden.BroadContact == garden.Allocation.Garden64Index || garden.BroadContact == garden.Allocation.Garden65Index)
            {
                break;
            }
            NativeWorldPathPosition target = garden.Objects[garden.Allocation.Garden64Index].Position;
            double dx = target.X - state.X;
            double dz = -target.Z - state.Z;
            double length = Math.Sqrt(dx * dx + dz * dz);
            double angle = state.CameraYaw * Math.PI / 2048;
            float horizontal = (float)((Math.Cos(angle) * dx + Math.Sin(angle) * dz) / length);
            float vertical = (float)((-Math.Sin(angle) * dx + Math.Cos(angle) * dz) / length);
            Assert.True(world.Apply(new(PlayerActionKind.Move, Horizontal: horizontal, Vertical: vertical)).Accepted);
        }
        Assert.True(world.Apply(new(PlayerActionKind.Wait)).Accepted);
        Assert.True(world.Apply(new(PlayerActionKind.Confirm)).Accepted);
        Assert.Null(session.State.World!.NativeFootRuntime);
        Assert.NotNull(session.State.World.NativeGardenRuntime);
        Assert.True(world.Apply(new(PlayerActionKind.Confirm)).Accepted);
        Assert.Null(session.State.World.NativeFootRuntime);
        Assert.True(world.Apply(new(PlayerActionKind.Wait, Frames: 48)).Accepted);
        Assert.Null(session.State.World.NativeFootRuntime);
        string snapshot = TestResources.ArtifactFile("garden-boarded.ff8save");
        session.Save(snapshot, compact: true);
        GameSession restoredSession = new(GameSession.Load(snapshot));
        using GameWorld restored = new(restoredSession, directory);
        Assert.Equal(world.Apply(new(PlayerActionKind.Wait)), restored.Apply(new(PlayerActionKind.Wait)));
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(session.PrepareSnapshot()), JsonSerializer.SerializeToElement(restoredSession.PrepareSnapshot())));
    }

    [Fact]
    public void LaterGameplaySaveContinuesWithoutMigration()
    {
        string source = TestResources.FixturePath("App.Tests.GardenBoardingActivityTests.3");
        string directory = TestResources.GameDirectory;
        GameSession session = new(GameplaySaveSessionLoader.Load(source, directory));
        using GameWorld world = new(session, directory);
        Assert.True(world.Apply(new(PlayerActionKind.Wait, Frames: 2)).Accepted);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RefusedBoardingBesideGardenCompletesWorldEventInput(bool gameplaySave)
    {
        string source = TestResources.FixturePath(gameplaySave ? "App.Tests.GardenBoardingActivityTests.2" : "App.Tests.GardenBoardingActivityTests.1");
        string directory = TestResources.GameDirectory;
        GameSession session = new(gameplaySave ? GameplaySaveSessionLoader.Load(source, directory) : GameSession.Load(source));
        using GameWorld world = new(session, directory);
        Assert.True(world.Apply(new(PlayerActionKind.Wait, Frames: 24)).Accepted);
        Assert.NotNull(session.State.World!.NativeFootRuntime);
        string snapshot = TestResources.ArtifactFile("garden-refused-boarding.ff8save");
        session.Save(snapshot, compact: true);
        GameSession restoredSession = new(GameSession.Load(snapshot));
        using GameWorld restored = new(restoredSession, directory);
        foreach (PlayerAction action in new PlayerAction[] { new(PlayerActionKind.Wait), new(PlayerActionKind.Confirm), new(PlayerActionKind.Wait, Frames: 3) })
        {
            Assert.Equal(world.Apply(action), restored.Apply(action));
        }
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(session.State), JsonSerializer.SerializeToElement(restoredSession.State)));
        Assert.True(world.Apply(new(PlayerActionKind.Confirm)).Accepted);
        Assert.Equal(SceneKind.WorldMap, session.State.Scene);
        Assert.NotNull(session.State.World!.NativeFootRuntime);
        Assert.Equal(-1, session.State.World.NativeGardenRuntime!.BroadContact);
        Assert.Equal(false, world.WorldScene!.CompleteNativeTransportActivity(session.State.World, NativeWorldSalaryState.Begin(session.State), true, false));
    }
}
