using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class RagnarokOrdinaryExitTests
{
    [Fact]
    public void SavedFieldExitKeepsItsOriginalPacketAndContinuesAfterColdRestore()
    {
        string directory = TestResources.GameDirectory;
        using GameAssets assets = new(directory);
        GameSession session = new(PlayerStateLoader.Load(TestResources.FixturePath("ragnarok-ordinary-field-exit"), directory, sharedAssets: assets));
        using GameWorld world = new(session, directory, validateWorldPresentation: false);
        world.EnsureField();
        PlayerAction[] actions =
        [
            new(PlayerActionKind.Move, Horizontal: 1, Frames: 1),
            new(PlayerActionKind.Move, Horizontal: 1, Vertical: 1, Frames: 13),
            new(PlayerActionKind.Move, Horizontal: 1, Frames: 15),
            new(PlayerActionKind.Move, Horizontal: 1, Vertical: 1, Frames: 13),
            new(PlayerActionKind.Move, Horizontal: 1, Frames: 17),
            new(PlayerActionKind.Wait)
        ];
        foreach (PlayerAction action in actions)
        {
            if (session.State.Scene == SceneKind.WorldMap)
            {
                break;
            }
            Assert.True(world.Apply(action).Accepted);
        }
        Assert.Equal(848, session.State.FieldId);
        for (int callback = 0; callback < 30; callback++)
        {
            world.Apply(new(PlayerActionKind.Wait));
        }
        AutoPlayTarget[] targets = GameWorld.DiscoverAutoPlayTargets(world.Field!, world.Events, world.Events.PlayerActor!).Where(target => target.Kind == AutoPlayTargetKind.Contact && target.ObjectId == 9).ToArray();
        AutoPlayTarget? selected = null;
        AutoPlayWalkingRoute? route = null;
        foreach (AutoPlayTarget target in targets)
        {
            if (world.TryFindAutoPlayWalkingRoute(target, out AutoPlayWalkingRoute candidate))
            {
                selected = target;
                route = candidate;
                break;
            }
        }
        Assert.NotNull(selected);
        Assert.NotNull(route);
        AutoPlayWalkResult? approach = null;
        AutoPlayWalkResult? crossing = null;
        // Ordinary incidental contacts can interrupt the approach before the exit line.
        for (int attempt = 0; attempt < 8 && session.State.Scene == SceneKind.Field; attempt++)
        {
            approach = world.WalkAutoPlayApproach(selected, () => { });
            if (approach.Arrived)
            {
                crossing = world.WalkAutoPlayContact(selected, () => { });
                break;
            }
            world.Apply(new(PlayerActionKind.Wait));
        }
        for (int callback = 0; callback < 180 && session.State.Scene == SceneKind.Field; callback++)
        {
            world.Apply(new(PlayerActionKind.Wait));
        }
        Assert.True(session.State.Scene == SceneKind.WorldMap, $"Target={selected}; approach={approach}; crossing={crossing}; position={world.Events.Scene.NativePlayerPosition}; trigger={world.NextTrigger}");
        Assert.Null(world.Events.LastFault);
        Assert.Equal(new WorldTransition(21, 50, 21), session.State.WorldArrival);
        NativeRagnarokWorldRuntimeState runtime = Assert.IsType<NativeRagnarokWorldRuntimeState>(session.State.World!.NativeRagnarokRuntime);
        Assert.Equal(session.State.WorldArrival, runtime.Allocation.Packet);
        Assert.Equal((byte)50, runtime.Allocation.ControlledEntityType);
        Assert.Null(runtime.Allocation.ParkedRagnarokPose2);
        session.SetDepartingFieldState(world.Events.Capture());
        string output = Environment.GetEnvironmentVariable("MEMORIA_FF8_TEST_OUTPUT_DIRECTORY") ?? TestResources.ArtifactDirectory;
        Directory.CreateDirectory(output);
        string path = Path.Combine(output, "ragnarok-world-exit.json");
        session.Save(path);
        GameSession restoredSession = new(GameSession.Load(path));
        using GameWorld restored = new(restoredSession, directory, validateWorldPresentation: false);
        restored.EnsureField();
        Assert.True(world.Apply(new(PlayerActionKind.Wait)).Accepted);
        Assert.True(restored.Apply(new(PlayerActionKind.Wait)).Accepted);
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(session.State.World), JsonSerializer.SerializeToElement(restoredSession.State.World)));
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
    }
}
