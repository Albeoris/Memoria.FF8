using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlayPositionGateTests
{
    [Fact]
    public void OrdinaryWalkingSatisfiesAnOriginalDefaultScriptPositionGate()
    {
        string snapshot = TestResources.FixturePath("App.Tests.AutoPlayPositionGateTests.1");
        GameSession session = new(GameSession.Load(snapshot));
        using GameWorld world = new(session, TestResources.GameDirectory, validateWorldPresentation: false);
        world.EnsureField();
        Assert.Empty(world.DiscoverAutoPlayTargets());
        NativePlayerPositionGate gate = Assert.IsType<NativePlayerPositionGate>(world.Events.DiscoverAutoPlayPositionGate());
        Assert.Equal(1, gate.Register);
        Assert.Equal(1291, gate.ExclusiveUpperBound);
        Assert.True(world.Events.Scene.MapJumpDisabled);

        int moves = 0;
        world.Events.NativePlayerMovementProcessed += _ => moves++;
        Assert.True(world.WalkToAutoPlayPositionGate(gate, () => { }));

        Assert.True(moves > 0);
        Assert.True(world.Events.PlayerActor!.Y < 1291);
        Assert.False(world.Events.IsUserControlEnabled);
        Assert.Null(world.Events.LastFault);
    }

    [Fact]
    public void OrdinaryControllerWalkingSatisfiesCompoundFollowerPositionGates()
    {
        string snapshot = TestResources.FixturePath("App.Tests.AutoPlayPositionGateTests.2");
        string gameDirectory = TestResources.GameDirectory;
        GameSession session = new(GameSession.Load(snapshot));
        GameWorld? world = null;
        int movements = 0;
        int restores = 0;
        string capture = Path.Combine(Path.GetTempPath(), "autoplay-position-gate-" + Guid.NewGuid().ToString("N"));
        AutoPlayController? controller = null;
        try
        {
            GameWorld GetWorld()
            {
                if (world is null)
                {
                    world = new GameWorld(session, gameDirectory, validateWorldPresentation: false);
                    world.Events.NativePlayerMovementProcessed += _ => movements++;
                }
                return world;
            }
            void Restore(GameState state)
            {
                world?.Dispose();
                world = null;
                session.Restore(state);
                restores++;
            }
            GameWorld initialWorld = GetWorld();
            initialWorld.EnsureField();
            bool MovementIsPending()
            {
                return !initialWorld.Events.CanAcceptMovementInput || initialWorld.Events.PlayerActor is { NativeScriptMovement.Active: true } or { NativeJump.Active: true } or { NativeLadder.Active: true } or { NativeLadderMode3.Active: true };
            }
            for (int frame = 0; frame < 256 && MovementIsPending(); frame++)
            {
                initialWorld.Apply(new PlayerAction(PlayerActionKind.Wait, Frames: 1));
                Assert.Null(initialWorld.Events.LastFault);
            }
            Assert.False(MovementIsPending(), $"The saved native movement did not finish within the setup frame budget: frame={initialWorld.Events.Frame}; ladder={initialWorld.Events.PlayerActor?.NativeLadder}");
            NativePlayerPositionGate gate = Assert.IsType<NativePlayerPositionGate>(initialWorld.Events.DiscoverAutoPlayPositionGate());
            Assert.True(gate.Constraints.Count > 1);

            initialWorld.PublishEventProcess();
            controller = new(session, GetWorld, Restore, capture, deadlineTime: new RoutingClock());
            controller.StartFromCurrentState();
            for (int step = 0; step < 256 && session.State.Scene != SceneKind.Movie && session.State.Variables.GetValueOrDefault(256) < 390; step++)
            {
                controller.Advance(1);
                Assert.True(controller.Error is null, $"Controller failed at step {controller.State.Steps}: {controller.Error}");
                Assert.Null(world?.Events.LastFault);
            }

            Assert.True(session.State.Scene == SceneKind.Movie || session.State.Variables.GetValueOrDefault(256) >= 390, $"Follower position gates did not complete: field={session.State.FieldId}; story={session.State.Variables.GetValueOrDefault(256)}; steps={controller.State.Steps}; nativeMoves={movements}; position={world?.Events.Scene.NativePlayerPosition}; status={controller.Status}");
            Assert.True(movements > 0);
            Assert.Equal(0, restores);
        }
        finally
        {
            controller?.Stop();
            world?.Dispose();
            if (Directory.Exists(capture))
            {
                Directory.Delete(capture, recursive: true);
            }
        }
    }

    private sealed class RoutingClock : TimeProvider
    {
        public override long GetTimestamp() => 0;
    }
}
