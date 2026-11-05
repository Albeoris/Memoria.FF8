using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlayInstalledWalkingTests(ITestOutputHelper output)
{
    [Fact]
    public void AuthenticStudioDoorRequiresAnActualNativeCrossingAfterTheApproach()
    {
        string path = TestResources.FixturePath("App.Tests.AutoPlayInstalledWalkingTests.1");
        byte[] original = Hash(path);
        GameSession session = new(GameSession.Load(path));
        using GameWorld world = new(session, TestResources.GameDirectory, validateWorldPresentation: false);
        world.EnsureField();
        Assert.Equal(927, world.Events.FieldId);
        Assert.Equal(6, world.Events.Frame);
        Assert.Equal(280, world.Events.Variables.GetValueOrDefault(256));
        Assert.False(world.Events.Actors[9].Active);
        Assert.Equal(PlayerTriggerKind.Playable, world.NextTrigger.Kind);
        FieldFixedPosition sourcePosition = Assert.IsType<FieldFixedPosition>(world.Events.Scene.NativePlayerPosition);
        AutoPlayTarget[] targets = GameWorld.DiscoverAutoPlayTargets(world.Field!, world.Events, world.Events.PlayerActor!).Where(target => target.Kind == AutoPlayTargetKind.Contact && target.ObjectId == 13).OrderBy(target => Vector2.DistanceSquared(new Vector2(sourcePosition.X / 4096f, sourcePosition.Y / 4096f), new Vector2(target.ApproachX ?? target.X, target.ApproachY ?? target.Y))).ToArray();
        Assert.NotEmpty(targets);
        JsonElement beforePlanning = world.Events.Capture();
        AutoPlayWalkingRoute? selectedRoute = null;
        AutoPlayTarget? selectedTarget = null;
        foreach (AutoPlayTarget target in targets)
        {
            if (world.TryFindAutoPlayWalkingRoute(target, out AutoPlayWalkingRoute route))
            {
                selectedRoute = route;
                selectedTarget = target;
                break;
            }
        }
        Assert.True(JsonElement.DeepEquals(beforePlanning, world.Events.Capture()));
        Assert.NotNull(selectedRoute);
        Assert.NotNull(selectedTarget);
        Assert.NotEmpty(selectedRoute.Waypoints);
        int progressCallbacks = 0;
        int nativeCallbacks = 0;
        Action<bool>? previous = world.Events.NativePlayerMovementProcessed;
        world.Events.NativePlayerMovementProcessed = accepted =>
        {
            nativeCallbacks++;
            previous?.Invoke(accepted);
        };
        try
        {
            AutoPlayWalkResult approach = world.WalkAutoPlayRoute(selectedRoute, () => progressCallbacks++, selectedTarget.ObjectId);
            FieldFixedPosition? approachPosition = world.Events.Scene.NativePlayerPosition;
            AutoPlayWalkResult? crossing = null;
            FieldFixedPosition? crossingPosition = null;
            Assert.True(approach.Arrived || approach.Interrupted, $"Target={selectedTarget.Id}; approach={approach}; nativeXYZ={approachPosition}; trigger={world.NextTrigger}");
            if (approach.Arrived)
            {
                Assert.True(GameWorld.AutoPlayWalkingWaypointReached(world.Field!.Walkmesh, world.Events.Scene.NativePlayerPosition!, world.Events.PlayerActor!.WalkmeshTriangle, selectedRoute.Waypoints[^1], Math.Max(0.125f, world.Events.AutoPlayMovementStepDistance * 0.75f)));
                int approachCallbacks = nativeCallbacks;
                crossing = world.WalkAutoPlayContact(selectedTarget, () => progressCallbacks++);
                crossingPosition = world.Events.Scene.NativePlayerPosition;
                Assert.True(nativeCallbacks > approachCallbacks);
            }
            for (int action = 0; action < 30 && session.State.Scene != SceneKind.Movie && session.State.FieldId != 913; action++)
            {
                world.Apply(new PlayerAction(PlayerActionKind.Wait, Frames: 300, WaitWhilePlayable: true));
                Assert.Null(world.Events.LastFault);
            }
            Assert.True(session.State.Scene == SceneKind.Movie || session.State.FieldId == 913, $"Target={selectedTarget.Id}; approach={approach}; approachXYZ={approachPosition}; crossing={crossing}; crossingXYZ={crossingPosition}; trigger={world.NextTrigger}; position={world.Events.Scene.NativePlayerPosition}; entry={world.Events.Actors[13].DoorLineEntryPending}; crossingPending={world.Events.Actors[13].NativeLineCrossingPending}; dispatchedCrossings={world.Events.LineCrossings.Count(item => item.ObjectId == 13)}");
            if (session.State.Scene == SceneKind.Movie)
            {
                Assert.Equal(12, session.State.MovieIndex);
            }
            Assert.Null(world.Events.LastFault);
            Assert.True(nativeCallbacks > 0);
            Assert.True(progressCallbacks > 0);
            Assert.True(world.Events.FieldId != 927 || world.Events.Frame > 6);
            Assert.NotEqual(sourcePosition, world.Events.Scene.NativePlayerPosition);
        }
        finally
        {
            world.Events.NativePlayerMovementProcessed = previous;
            Assert.Equal(original, Hash(path));
        }
    }

    [Fact]
    public void AuthenticPostMissionSoldierBlocksPlanningAndNaturalWalkingToTheStudioDoor()
    {
        string path = TestResources.FixturePath("App.Tests.AutoPlayInstalledWalkingTests.2");
        byte[] original = Hash(path);
        GameSession session = new(GameSession.Load(path));
        using GameWorld world = new(session, TestResources.GameDirectory, validateWorldPresentation: false);
        world.EnsureField();
        Assert.Equal(927, world.Events.FieldId);
        Assert.Equal(67, world.Events.Frame);
        Assert.Equal(285, world.Events.Variables.GetValueOrDefault(256));
        ActorState soldier = world.Events.Actors[9];
        Assert.True(soldier.Active);
        Assert.True(soldier.CollisionEnabled);
        Assert.Equal(600, soldier.PushRadius);
        AutoPlayTarget[] targets = GameWorld.DiscoverAutoPlayTargets(world.Field!, world.Events, world.Events.PlayerActor!).Where(target => target.Kind == AutoPlayTargetKind.Contact && target.ObjectId == 13).ToArray();
        Assert.NotEmpty(targets);
        JsonElement before = world.Events.Capture();
        foreach (AutoPlayTarget target in targets)
        {
            Assert.False(world.TryFindAutoPlayWalkingRoute(target, out _), $"Guarded studio approach remained reachable: {target.Id}");
        }
        Assert.True(JsonElement.DeepEquals(before, world.Events.Capture()));
        float minimumPlayerX = world.Events.Scene.NativePlayerPosition!.X / 4096f;
        Action<bool>? previous = world.Events.NativePlayerMovementProcessed;
        world.Events.NativePlayerMovementProcessed = accepted =>
        {
            minimumPlayerX = Math.Min(minimumPlayerX, world.Events.Scene.NativePlayerPosition!.X / 4096f);
            previous?.Invoke(accepted);
        };
        try
        {
            AutoPlayWalkResult result = world.WalkAutoPlayApproach(targets[0], IgnoreProgress);
            Assert.False(result.Arrived);
            Assert.Equal(927, session.State.FieldId);
            Assert.Equal(927, world.Events.FieldId);
            Assert.Equal(285, world.Events.Variables.GetValueOrDefault(256));
            Assert.True(minimumPlayerX > soldier.X, $"Native player crossed the blocking soldier: minimumX={minimumPlayerX}, soldierX={soldier.X}, result={result}");
            Assert.True(world.Events.Scene.NativePlayerPosition!.X / 4096f > soldier.X);
            Assert.Null(world.Events.LastFault);
        }
        finally
        {
            world.Events.NativePlayerMovementProcessed = previous;
            Assert.Equal(original, Hash(path));
        }
    }

    [Theory]
    [InlineData("App.Tests.AutoPlayInstalledWalkingTests.2", 927, 0, 926)]
    [InlineData("App.Tests.AutoPlayInstalledWalkingTests.3", 927, 0, 926)]
    [InlineData("App.Tests.AutoPlayInstalledWalkingTests.4", 897, 0, 898)]
    public void AuthenticGatewaysRemainReachableThroughOrdinaryNativeCallbacks(string resource, int sourceField, int gatewayIndex, int destinationField)
    {
        string path = TestResources.FixturePath(resource);
        byte[] original = Hash(path);
        string[] variants;
        using (GameWorld discovery = new(new GameSession(GameSession.Load(path)), TestResources.GameDirectory, validateWorldPresentation: false))
        {
            discovery.EnsureField();
            variants = GameWorld.DiscoverAutoPlayTargets(discovery.Field!, discovery.Events, discovery.Events.PlayerActor!).Where(target => target.Kind == AutoPlayTargetKind.Gateway && target.CoverageId == $"{sourceField}:gateway:{gatewayIndex}").Select(target => target.Id).ToArray();
        }
        Assert.NotEmpty(variants);
        List<string> diagnostics = [];
        bool exited = false;
        foreach (string id in variants)
        {
            GameSession session = new(GameSession.Load(path));
            using GameWorld world = new(session, TestResources.GameDirectory, validateWorldPresentation: false);
            world.EnsureField();
            AutoPlayTarget target = GameWorld.DiscoverAutoPlayTargets(world.Field!, world.Events, world.Events.PlayerActor!).Single(target => target.Id == id);
            bool found = world.TryFindAutoPlayWalkingRoute(target, out _);
            for (int attempt = 0; attempt < 30; attempt++)
            {
                AutoPlayWalkResult approach = world.WalkAutoPlayApproach(target, IgnoreProgress);
                AutoPlayWalkResult? crossing = null;
                if (approach.Arrived)
                {
                    crossing = world.WalkAutoPlayContact(target, IgnoreProgress);
                }
                string diagnostic = $"Target={id}; attempt={attempt}; initialRouteFound={found}; approach={approach}; crossing={crossing}; trigger={world.NextTrigger}; nativeXYZ={world.Events.Scene.NativePlayerPosition}; sessionField={session.State.FieldId}";
                diagnostics.Add(diagnostic);
                output.WriteLine(diagnostic);
                Assert.Null(world.Events.LastFault);
                int? gateway = crossing?.GatewayIndex ?? approach.GatewayIndex;
                if (gateway is not null)
                {
                    Assert.Equal(gatewayIndex, gateway.Value);
                    Assert.Equal(destinationField, session.State.FieldId);
                    exited = true;
                    break;
                }
                Assert.True(session.State.FieldId == sourceField && world.NextTrigger.Kind == PlayerTriggerKind.Playable, "Unexpected native boundary: " + diagnostic);
                if (!(crossing?.Interrupted ?? approach.Interrupted))
                {
                    break;
                }
                PlayerActionResult drain = world.Apply(new PlayerAction(PlayerActionKind.Wait, Frames: 1));
                Assert.True(drain.Accepted, "Native contact callback was not drained: " + diagnostic);
                Assert.Null(world.Events.LastFault);
                Assert.True(session.State.FieldId == sourceField && world.NextTrigger.Kind == PlayerTriggerKind.Playable, $"Unexpected boundary after native contact callback: {world.NextTrigger}; target={id}; attempt={attempt}");
            }
        }
        Assert.Equal(original, Hash(path));
        Assert.True(exited, string.Join(Environment.NewLine, diagnostics));
    }

    [Fact]
    public void AuthenticControllerLeavesTheSavePointInsteadOfRepeatingEmptyContactReceipts()
    {
        string manifest = TestResources.FixturePath("App.Tests.AutoPlayInstalledWalkingTests.5");
        string directory = TestResources.GameDirectory;
        byte[] originalManifest = Hash(manifest);
        AutoPlayRunState saved = JsonSerializer.Deserialize<AutoPlayRunState>(File.ReadAllText(manifest), AutoPlayController.JsonOptions)!;
        string snapshot = Path.GetFullPath(saved.Snapshot!, Path.GetDirectoryName(manifest)!);
        byte[] originalSnapshot = Hash(snapshot);
        GameSession session = GameSession.CreateColdProcess();
        GameWorld? world = null;
        string outputDirectory = Path.Combine(Path.GetTempPath(), "autoplay-empty-contact-" + Guid.NewGuid().ToString("N"));
        try
        {
            GameWorld GetWorld()
            {
                return world ??= new GameWorld(session, directory, validateWorldPresentation: false);
            }
            void Restore(GameState state)
            {
                world?.Dispose();
                world = null;
                session.Restore(state);
            }
            AutoPlayController controller = new(session, GetWorld, Restore, outputDirectory);
            controller.ResumeFrom(manifest);
            Assert.Equal(897, session.State.FieldId);
            for (int step = 0; step < 128 && session.State.FieldId == 897; step++)
            {
                controller.Advance(1);
                Assert.Null(controller.Error);
                Assert.True(controller.Running);
            }
            AutoPlayEventCoverage savePoint = controller.State.Coverage.Visits.Single(visit => visit.Visit.Key == "897:285").Events.Single(item => item.Event.Id == "897:line:19");
            output.WriteLine($"Field={session.State.FieldId}; steps={controller.State.Steps - saved.Steps}; savePointCompleted={savePoint.Completed}; savePointBlocked={savePoint.BlockedReason}; nativeXYZ={world?.Events.Scene.NativePlayerPosition}");
            Assert.True(savePoint.Completed || savePoint.BlockedReason is not null);
            Assert.Contains(session.State.FieldId, new[] { 898, 912 });
            Assert.Null(world?.Events.LastFault);
        }
        finally
        {
            world?.Dispose();
            Assert.Equal(originalManifest, Hash(manifest));
            Assert.Equal(originalSnapshot, Hash(snapshot));
            if (Directory.Exists(outputDirectory))
            {
                Directory.Delete(outputDirectory, recursive: true);
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WalkingPredictionUsesTheUpcomingMappedInputSample(bool physicalCancelHeld)
    {
        string path = TestResources.FixturePath("App.Tests.AutoPlayInstalledWalkingTests.4");
        byte[] original = Hash(path);
        GameSession session = new(GameSession.Load(path));
        using GameWorld world = new(session, TestResources.GameDirectory, validateWorldPresentation: false);
        world.EnsureField();
        Assert.True(world.Events.Scene.RunEnabled);
        Assert.Null(world.CurrentDialogue);
        try
        {
            if (physicalCancelHeld)
            {
                ushort raw = session.State.InputConfiguration!.RawForMappedPulse(0x10);
                Assert.True(world.Apply(new PlayerAction(PlayerActionKind.FieldButtons, raw)).Accepted);
                Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Wait, Frames: 1)).Accepted);
            }
            else
            {
                Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Cancel)).Accepted);
                Assert.Equal(0, world.Events.ButtonInput!.PhysicalHeld);
            }
            Assert.NotEqual(0, world.Events.ButtonInput!.Held!.Value & 0x10);
            FieldFixedPosition source = world.Events.Scene.NativePlayerPosition!;
            Vector2 delta = new(-1100, 450);
            Vector2 axes = world.Events.AutoPlayMovementAxes(delta);
            JsonElement before = world.Events.Capture();
            NativePlayerMovementResult predicted = world.Events.ProbeAutoPlayMovement(source, world.Events.PlayerActor!.WalkmeshTriangle, delta);
            Assert.True(predicted.Accepted);
            Assert.True(JsonElement.DeepEquals(before, world.Events.Capture()));
            PlayerActionResult actual = world.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: axes.X, Vertical: axes.Y));
            Assert.True(actual.Accepted);
            Assert.Equal(new FieldFixedPosition(predicted.FixedX, predicted.FixedY, predicted.FixedZ), world.Events.Scene.NativePlayerPosition);
            Assert.Equal(predicted.Triangle, world.Events.PlayerActor!.WalkmeshTriangle);
            Assert.Equal(physicalCancelHeld, (world.Events.ButtonInput!.Held!.Value & 0x10) != 0);
            Assert.Null(world.Events.LastFault);
        }
        finally
        {
            Assert.Equal(original, Hash(path));
        }
    }

    private static void IgnoreProgress()
    {
    }

    private static byte[] Hash(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return SHA256.HashData(stream);
    }
}
