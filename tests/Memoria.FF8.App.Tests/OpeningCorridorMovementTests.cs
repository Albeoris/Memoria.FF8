using System.Numerics;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class OpeningCorridorMovementTests
{
    [Fact]
    public void NaturalOpeningCorridorUsesNativeWalkAnimationAndRetainsIndependentRestore()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed field corpus is required.");
        var session = GameSession.CreateColdProcess();
        session.StartNewGame();
        using var world = new GameWorld(session, directory);
        bool initialAnimationsVerified = false;
        for (int action = 0; action < 5000; action++)
        {
            world.EnsureField();
            if (world.Field!.Id == 229 && world.Events.NextTrigger.Kind == PlayerTriggerKind.Playable)
            {
                break;
            }
            DialogueState? dialogue = world.Events.CurrentDialogue;
            PlayerAction next = session.IsMoviePlaying ? new PlayerAction(PlayerActionKind.SkipMovie) : dialogue?.IsNamePrompt == true ? new PlayerAction(PlayerActionKind.EnterName, Text: "Cecilia") : dialogue is { CanConfirm: true, FirstChoice: int choice } ? new PlayerAction(PlayerActionKind.Choose, choice) : dialogue is { CanConfirm: true } ? new PlayerAction(PlayerActionKind.Confirm) : new PlayerAction(PlayerActionKind.Wait);
            try
            {
                PlayerActionResult result = world.Apply(next);
                Assert.True(result.Accepted);
                AdvanceClosingDialogue(world, next, result);
            }
            catch (EventScriptException)
            {
                Console.WriteLine($"Opening failure VM: {world.Events.Capture()}");
                throw;
            }
            if (world.Field.Id == 229 && world.Events.Scene.NativeFieldLoadVersion == 1 && !initialAnimationsVerified)
            {
                Assert.All(world.Events.Actors.Where(actor => world.Events.Scene.NativeActorObjectIds.Contains(actor.ObjectId) && actor.ModelId >= 0), actor =>
                {
                    Assert.True(actor.AnimationPlaying);
                    Assert.True(actor.AnimationLoop);
                    Assert.Equal(actor.BaseAnimationId, actor.AnimationId);
                    Assert.Equal(world.Field.GetAnimationFrameCount(actor.ModelId, actor.AnimationId) - 1, actor.AnimationLast);
                });
                initialAnimationsVerified = true;
            }
        }
        Assert.Equal(229, world.Field!.Id);
        Assert.Equal(PlayerTriggerKind.Playable, world.Events.NextTrigger.Kind);
        Assert.Equal(1, world.Events.Scene.NativeFieldLoadVersion);
        Assert.Equal(1, world.Events.Scene.NativePartyTraceVersion);
        ActorState player = Assert.Single(world.Events.Actors, actor => actor.Active && actor.CharacterId == 0);
        Assert.Equal(48, player.PushRadius);
        Assert.Equal(192, world.Events.InputAxisDirection);
        Assert.False(world.Events.Scene.RunEnabled);
        Assert.True(initialAnimationsVerified);
        // Move from the original edge into the corridor using ordinary keyboard
        // Right (the retained control-queue convention uses negative H).
        for (int tick = 0; tick < 2; tick++)
        {
            FieldNativeMovementResult centering = FieldNativeMovementProbe.Resolve(world.Field!.Walkmesh, new FieldNativeMovementRequest(player.WalkmeshTriangle, (int)(player.X * 4096), (int)(player.Y * 4096), (int)(player.Z * 4096), 128, 2696, 48, true, false, 0, true), world.Events.Scene.LockedTriangles);
            Assert.True(centering.Status == FieldNativeMovementStatus.Accepted, $"Centering{tick}: position({player.X},{player.Y},{player.Z})/{player.WalkmeshTriangle}, status{centering.Status}, retry{centering.RetryCount}, proposals{centering.Proposals.Count}, reason{centering.UnsupportedReason}");
            Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: -1)).Accepted);
        }
        JsonElement baseline = world.Events.Capture();
        GameState state = session.State with { EventState = baseline };
        FieldCamera camera = world.Field.GetCamera(world.Events.Scene.CameraIndex);
        ProjectedPoint origin = camera.Project(new Vector3(player.X, player.Y, player.Z));
        Console.WriteLine($"Original229 baseline ({player.X},{player.Y},{player.Z})/triangle{player.WalkmeshTriangle}; native{world.Events.Scene.NativePlayerPosition}; camera{camera}.");
        foreach ((float horizontal, float vertical, int direction) in new[] { (-1f, 0f, 128), (1f, 0f, 0), (0f, -1f, 192), (0f, 1f, 64), (-0.25f, -1f, 182), (0.25f, -1f, 202) })
        {
            using var probe = new GameWorld(new GameSession(state), directory);
            probe.EnsureField();
            ActorState moving = Assert.Single(probe.Events.Actors, actor => actor.Active && actor.CharacterId == 0);
            FieldNativeMovementResult ground = FieldNativeMovementProbe.Resolve(probe.Field!.Walkmesh, new FieldNativeMovementRequest(moving.WalkmeshTriangle, (int)(moving.X * 4096), (int)(moving.Y * 4096), (int)(moving.Z * 4096), (byte)direction, 2696, 48, true, false, 0, true), probe.Events.Scene.LockedTriangles);
            Assert.True(ground.Status == FieldNativeMovementStatus.Accepted, $"Original229 ({horizontal},{vertical}) from ({moving.X},{moving.Y},{moving.Z})/{moving.WalkmeshTriangle} direction{direction}: {ground.Status}, retry{ground.RetryCount}, reason{ground.UnsupportedReason}; camera{camera}.");
            Assert.True(probe.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: horizontal, Vertical: vertical)).Accepted);
            Assert.Equal(direction, moving.Direction);
            Assert.Equal(horizontal, probe.Events.ActionLog[^1].Action.Horizontal);
            Assert.Equal(vertical, probe.Events.ActionLog[^1].Action.Vertical);
            ProjectedPoint projected = camera.Project(new Vector3(moving.X, moving.Y, moving.Z));
            if (horizontal != 0 && vertical == 0)
            {
                Assert.Equal(-Math.Sign(horizontal), Math.Sign(projected.X - origin.X));
            }
            else if (horizontal == 0)
            {
                Assert.Equal(Math.Sign(vertical), Math.Sign(projected.Y - origin.Y));
            }
            Console.WriteLine($"Original229 input({horizontal},{vertical}) axis192: delta({moving.X - player.X},{moving.Y - player.Y},{moving.Z - player.Z}), projected({projected.X - origin.X},{projected.Y - origin.Y}), direction{moving.Direction}, animation{moving.AnimationId}/frame{moving.AnimationFrame}.");
            Assert.Equal(2696, moving.MovementSpeed);
            Assert.Equal(moving.WalkAnimationId, moving.AnimationId);
            Assert.True(moving.AnimationPlaying);
            Assert.True(moving.AnimationLoop);
            Assert.Equal(probe.Field!.GetAnimationFrameCount(moving.ModelId, moving.WalkAnimationId) - 1, moving.AnimationLast);
            Assert.NotEqual(0, moving.AnimationFrame);
            using var resumed = new GameWorld(new GameSession(state with { EventState = probe.Events.Capture() }), directory);
            resumed.EnsureField();
            Assert.True(JsonElement.DeepEquals(probe.Events.Capture(), resumed.Events.Capture()));
            Assert.Equal(probe.Render(TimeSpan.Zero), resumed.Render(TimeSpan.Zero));
            for (int tick = 0; tick < 3; tick++)
            {
                var next = new PlayerAction(PlayerActionKind.Wait);
                Assert.Equal(probe.Apply(next), resumed.Apply(next));
                Assert.True(JsonElement.DeepEquals(probe.Events.Capture(), resumed.Events.Capture()));
                Assert.Equal(probe.Render(TimeSpan.Zero), resumed.Render(TimeSpan.Zero));
            }
            Assert.Equal(probe.Apply(new PlayerAction(PlayerActionKind.Wait)), resumed.Apply(new PlayerAction(PlayerActionKind.Wait)));
            Assert.Equal(moving.BaseAnimationId, moving.AnimationId);
            Assert.True(JsonElement.DeepEquals(probe.Events.Capture(), resumed.Events.Capture()));
        }
        using (var wall = new GameWorld(new GameSession(state), directory))
        {
            wall.EnsureField();
            Assert.True(wall.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: -1)).Accepted);
            ActorState blocked = wall.Events.Actors[player.ObjectId];
            FieldFixedPosition beforeWall = wall.Events.Scene.NativePlayerPosition!;
            long frame = wall.Events.Frame;
            Assert.True(wall.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: -1)).Accepted);
            Assert.Equal(frame + 1, wall.Events.Frame);
            Assert.Equal(beforeWall, wall.Events.Scene.NativePlayerPosition);
            Assert.Equal(blocked.BaseAnimationId, blocked.AnimationId);
            using var restoredWall = new GameWorld(new GameSession(state with { EventState = wall.Events.Capture() }), directory);
            restoredWall.EnsureField();
            for (int tick = 0; tick < 3; tick++)
            {
                var next = new PlayerAction(PlayerActionKind.Move, Horizontal: -1);
                Assert.Equal(wall.Apply(next), restoredWall.Apply(next));
                Assert.True(JsonElement.DeepEquals(wall.Events.Capture(), restoredWall.Events.Capture()));
                Assert.Equal(wall.Render(TimeSpan.Zero), restoredWall.Render(TimeSpan.Zero));
                Assert.Equal(beforeWall, wall.Events.Scene.NativePlayerPosition);
            }
        }
        bool npcMovementSeen = false;
        float previousNpcX = world.Events.Actors[1].X;
        bool forcedNpcMovementSeen = false;
        for (int tick = 0; tick < 2000 && session.State.FieldId == 229 && !session.IsMoviePlaying; tick++)
        {
            PlayerAction next = world.Events.IsDialogueBlocking ? new PlayerAction(PlayerActionKind.Confirm) : world.Events.IsUserControlEnabled ? new PlayerAction(PlayerActionKind.Move, Vertical: 1) : new PlayerAction(PlayerActionKind.Wait);
            try
            {
                PlayerActionResult result = world.Apply(next);
                Assert.True(result.Accepted);
                AdvanceClosingDialogue(world, next, result);
            }
            catch (EventScriptException)
            {
                Console.WriteLine($"Opening failure VM: {world.Events.Capture()}");
                throw;
            }
            ActorState quistis = world.Events.Actors[1];
            if (!forcedNpcMovementSeen && quistis.X != previousNpcX)
            {
                Console.WriteLine($"Original229 Quistis movement: X{quistis.X}, target{quistis.MovementTarget}, ignoresmesh{quistis.MovementIgnoresWalkmesh}, animation{quistis.AnimationId}/frame{quistis.AnimationFrame}/last{quistis.AnimationLast}/playing{quistis.AnimationPlaying}.");
                forcedNpcMovementSeen = true;
                Assert.True(quistis.AnimationPlaying);
                // The native post-VM movement pass can move on the same update
                // that the original script selects animation phase zero.
                Assert.InRange(quistis.AnimationFrame, 0, quistis.AnimationLast);
                Assert.Equal(world.Field.GetAnimationFrameCount(quistis.ModelId, quistis.AnimationId) - 1, quistis.AnimationLast);
            }
            previousNpcX = quistis.X;
            // Async movement leaves its script free to select a different animation.
            foreach (ActorState npc in world.Events.Actors.Where(actor => actor.Visible && actor.CharacterId != 0 && actor.NativeMovementCompletion542 == 1 && actor.NativeScriptMovement is { Active: true, Asynchronous: false, AutomaticAnimation: true }))
            {
                if (!npcMovementSeen)
                {
                    Console.WriteLine($"Original229 moving NPC{npc.ObjectId}/character{npc.CharacterId}: {npc.NativeScriptMovement}, animation{npc.AnimationId}/frame{npc.AnimationFrame}/last{npc.AnimationLast}, playing{npc.AnimationPlaying}.");
                }
                npcMovementSeen = true;
                Assert.Equal(npc.MovementSpeed < 2696 ? npc.WalkAnimationId : npc.RunAnimationId, npc.AnimationId);
                Assert.True(npc.AnimationPlaying);
                Assert.Equal(world.Field!.GetAnimationFrameCount(npc.ModelId, npc.AnimationId) - 1, npc.AnimationLast);
            }
        }
        Assert.Null(world.Events.LastFault);
        if (!session.IsMoviePlaying)
        {
            JsonElement final = world.Events.Capture();
            Console.WriteLine($"Final dialogues{final.GetProperty("Dialogues")}, fibers{final.GetProperty("Fibers")}");
        }
        Assert.True(session.IsMoviePlaying, $"Natural corridor stopped at frame{world.Events.Frame}, trigger{world.Events.NextTrigger}, X{player.X}, Y{player.Y}.");
        Assert.True(forcedNpcMovementSeen, "Natural corridor must observe Quistis's original forced movement with its script-selected animation.");
        Console.WriteLine($"Natural229 movie{session.State.MovieIndex} at frame{world.Events.Frame}, player({player.X},{player.Y}); no fault.");
    }
    private static void AdvanceClosingDialogue(GameWorld world, PlayerAction action, PlayerActionResult result)
    {
        if (action.Kind == PlayerActionKind.Wait && result.FramesAdvanced == 0 && world.Events.CurrentDialogue is { CanConfirm: false, IsNamePrompt: false })
        {
            long frame = world.Events.Frame;
            world.AdvanceAutoPlayInputFrame();
            Assert.Equal(frame + 1, world.Events.Frame);
            Assert.Null(world.Events.LastFault);
        }
    }

}
