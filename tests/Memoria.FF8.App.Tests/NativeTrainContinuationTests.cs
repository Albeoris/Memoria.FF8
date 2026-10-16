using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeTrainContinuationTests
{
    [Fact]
    public void Station_snapshot_boards_the_five_node_train_and_recreates_its_actual_field_endpoint()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original train and field corpus is required.");
        string source = Path.Combine(Repository(), "../fixtures/external-fixture.json");
        GameState state = GameSession.Load(source);
        Assert.Equal(104, state.FieldId);
        var session = new GameSession(state);
        using var world = new GameWorld(session, directory);
        world.EnsureField();
        world.Apply(new PlayerAction(PlayerActionKind.Wait));
        Assert.Equal(SceneKind.WorldMap, session.State.Scene);
        Assert.Equal(new WorldTransition(9, 22, 59), session.State.WorldArrival);
        NativeScriptedWorldRuntimeState allocation = session.State.World!.NativeScriptedRuntime!;
        Assert.Equal((byte)22, allocation.Path.EntityType);
        Assert.Equal(2, allocation.Path.Mode);
        Assert.Equal(5, allocation.Path.NodeCount);
        Assert.Equal(2, allocation.Path.CentralNode);
        Assert.Equal((byte)0, allocation.Load.FootRecord0.ShapeId);
        Assert.All(allocation.Path.Presentation, pose => Assert.Null(pose.Projection));
        Assert.Throws<NativeWorldPresentationDeferredException>(() => world.Render(TimeSpan.Zero));

        world.Apply(new PlayerAction(PlayerActionKind.Wait));
        NativeWorldScriptedPathState first = session.State.World!.NativeScriptedRuntime!.Path;
        Assert.Equal(1, first.Callback);
        Assert.Equal(new sbyte[] { 1, 1, 0, -1, -1 }, Enumerable.Range(0, 5).Select(first.NodeLink));
        foreach (int index in new[] { 0, 1, 3, 4 })
        {
            Assert.NotNull(first.Presentation[index].Projection);
            if (first.Presentation[index].Projection!.Visible)
            {
                Assert.NotNull(first.Presentation[index].Angles);
            }
        }
        long salaryCounter = session.SeeD!.SalaryMovementCounter;
        string temporary = Path.Combine(Path.GetTempPath(), "ff8-authentic-train-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            session.Save(temporary);
            var restoredSession = new GameSession(GameSession.Load(temporary));
            using var restored = new GameWorld(restoredSession, directory);
            restored.EnsureField();
            world.SetRenderSize(640, 448);
            restored.SetRenderSize(640, 448);
            Assert.Equal(world.Render(TimeSpan.Zero), restored.Render(TimeSpan.Zero));
            PlayerAction next = new(PlayerActionKind.Wait, Frames: 3);
            Assert.Equal(world.Apply(next), restored.Apply(next));
            EqualState(session.State, restoredSession.State);
            Assert.Equal(world.Render(TimeSpan.Zero), restored.Render(TimeSpan.Zero));

            PlayerAction finish = new(PlayerActionKind.Wait, Frames: 3000);
            Assert.Equal(world.Apply(finish), restored.Apply(finish));
            Assert.Equal(SceneKind.Field, session.State.Scene);
            Assert.Equal(265, session.State.FieldId);
            Assert.Equal(salaryCounter, session.SeeD!.SalaryMovementCounter);
            NativeSavedWorldScriptedReturn receipt = session.State.SavedWorld!.ScriptedReturn!;
            Assert.Equal(new WorldTransition(9, 22, 59), receipt.Packet);
            Assert.Equal((byte)22, receipt.EntityType105);
            Assert.Equal((ushort)205, receipt.StoryWord);
            Assert.Equal(265, receipt.FieldEntry.FieldId);
            Assert.Equal(receipt.SourceWorldFrame + 16, session.State.DepartingWorldState!.Frame);
            Assert.Equal(receipt.Load.FootRecord0.Pose, receipt.Pose0);
            Assert.NotEqual(receipt.Pose0.X, session.State.DepartingWorldState.X);
            EqualState(session.State, restoredSession.State);

            world.EnsureField();
            restored.EnsureField();
            session.SetEventState(world.Events.Capture());
            session.Save(temporary);
            var endpointSession = new GameSession(GameSession.Load(temporary));
            using var endpoint = new GameWorld(endpointSession, directory);
            endpoint.EnsureField();
            endpoint.SetRenderSize(640, 448);
            GameState independentlyRecreated = GameSession.Load(temporary);
            EqualState(session.State, independentlyRecreated);
            PlayerAction fieldNext = new(PlayerActionKind.Wait);
            Assert.Equal(world.Apply(fieldNext), endpoint.Apply(fieldNext));
            Assert.True(JsonElement.DeepEquals(world.Events.Capture(), endpoint.Events.Capture()));
            // The real field initializer establishes normal camera tracking on
            // its second callback; rendering before it is deliberately refused.
            Assert.Equal(world.Apply(fieldNext), endpoint.Apply(fieldNext));
            Assert.True(JsonElement.DeepEquals(world.Events.Capture(), endpoint.Events.Capture()));
            Assert.Equal(world.Render(TimeSpan.Zero), endpoint.Render(TimeSpan.Zero));
            Assert.Equal(world.Apply(fieldNext), endpoint.Apply(fieldNext));
            Assert.True(JsonElement.DeepEquals(world.Events.Capture(), endpoint.Events.Capture()));
            Assert.Equal(world.Render(TimeSpan.Zero), endpoint.Render(TimeSpan.Zero));
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    private static void EqualState(GameState first, GameState second)
    {
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(first), JsonSerializer.SerializeToElement(second)));
    }

    private static string Repository()
    {
        DirectoryInfo? directory = new(TestResources.FixtureDirectory);
        return directory!.FullName;
    }
}
