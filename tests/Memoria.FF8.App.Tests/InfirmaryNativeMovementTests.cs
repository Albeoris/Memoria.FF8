using Memoria.FF8.Events;
using Memoria.FF8.Runtime;
using System.Text.Json;

namespace Memoria.FF8.App.Tests;

public sealed class InfirmaryNativeMovementTests
{
    [Fact]
    public void FreshClassroomCancelsAsyncMovementWithoutInventingCompletionAndRestoresModeZero()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed field corpus is required.");
        var session = GameSession.CreateColdProcess();
        session.StartNewGame();
        using var world = new GameWorld(session, directory);
        int centering = 0;
        NativeScriptMovementState? beforeCancel = null;
        ushort? beforeCompletion = null;
        for (int input = 0; input < 8000; input++)
        {
            world.EnsureField();
            ActorState? quistis = world.Field!.Id == 232 ? world.Events.Actors.FirstOrDefault(actor => actor.ObjectId == 11) : null;
            if (quistis?.NativeScriptMovement is { Cancelled: true } cancelled)
            {
                Assert.NotNull(beforeCancel);
                Assert.False(cancelled.Active);
                Assert.Equal(beforeCompletion, quistis.NativeMovementCompletion542);
                Assert.Equal((beforeCancel.FixedX, beforeCancel.FixedY, beforeCancel.FixedZ, beforeCancel.Triangle), (cancelled.FixedX, cancelled.FixedY, cancelled.FixedZ, cancelled.Triangle));
                Assert.Equal(quistis.BaseAnimationId, quistis.AnimationId);
                Assert.Null(quistis.MovementTarget);
                JsonElement snapshot = world.Events.Capture();
                using var restored = new GameWorld(new GameSession(session.State with { EventState = snapshot, EventMemory = session.State.EventMemory}), directory);
                restored.EnsureField();
                Assert.True(JsonElement.DeepEquals(snapshot, restored.Events.Capture()));
                Assert.Equal(world.Render(TimeSpan.Zero), restored.Render(TimeSpan.Zero));
                for (int tick = 0; tick < 3; tick++)
                {
                    var wait = new PlayerAction(PlayerActionKind.Wait);
                    Assert.Equal(world.Apply(wait), restored.Apply(wait));
                    Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
                    Assert.Equal(world.Render(TimeSpan.Zero), restored.Render(TimeSpan.Zero));
                }
                return;
            }
            if (quistis?.NativeScriptMovement is { Active: true, Asynchronous: true } current)
            {
                beforeCancel = current;
                beforeCompletion = quistis.NativeMovementCompletion542;
            }
            DialogueState? dialogue = world.Events.CurrentDialogue;
            PlayerAction next;
            if (session.IsMoviePlaying)
            {
                next = new PlayerAction(PlayerActionKind.SkipMovie);
            }
            else if (dialogue?.IsNamePrompt == true)
            {
                next = new PlayerAction(PlayerActionKind.EnterName, Text: "Cecilia");
            }
            else if (dialogue?.FirstChoice is int choice)
            {
                next = new PlayerAction(PlayerActionKind.Choose, choice);
            }
            else if (dialogue?.CanConfirm == true)
            {
                next = new PlayerAction(PlayerActionKind.Confirm);
            }
            else if (world.Field.Id == 229 && world.Events.IsUserControlEnabled)
            {
                next = centering++ < 2 ? new PlayerAction(PlayerActionKind.Move, Horizontal: -1) : new PlayerAction(PlayerActionKind.Move, Vertical: 1);
            }
            else
            {
                next = new PlayerAction(PlayerActionKind.Wait);
            }
            PlayerActionResult result = world.Apply(next);
            Assert.True(result.Accepted, $"Original opening stimulus {next} at field{world.Events.FieldId}/frame{world.Events.Frame}, trigger{world.Events.NextTrigger}: {result.Detail}");
        }
        Assert.Fail("The authentic opening and classroom must reach original MOVECANCEL.");
    }

    [Fact]
    public void FreshOpeningRetainsOriginalMovementAndReachesCorridor()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed field corpus is required.");
        var session = GameSession.CreateColdProcess();
        session.StartNewGame();
        using var world = new GameWorld(session, directory);
        HashSet<(int Field, int Object, string Command)> observed = [];
        HashSet<(int Field, int Object)> verified = [];
        world.Events.CommandExecuted += command =>
        {
            if (command.Name is not ("MOVE" or "FMOVE" or "MOVEA" or "RMOVE" or "RFMOVE") || !observed.Add((command.FieldId, command.ObjectId, command.Name)))
            {
                return;
            }
            ActorState actor = world.Events.Actors[command.ObjectId];
            Console.WriteLine($"Opening {command.FieldId}/{command.ObjectId}/{command.ScriptId}/{command.Operation} {command.Name} [{string.Join(',', command.Arguments)}]: character{actor.CharacterId}, model{actor.ModelId}, radius{actor.PushRadius}, animation{actor.AnimationId}/walk{actor.WalkAnimationId}/run{actor.RunAnimationId}, direction{actor.Direction}, native{actor.NativeScriptMovement}, authority{actor.NativeMotionAuthority}, position({actor.X},{actor.Y},{actor.Z}), target{actor.MovementTarget}, actorTarget{actor.MovementActorId}, init{world.Events.Scene.NativeFieldInitialization?.Stage}, load{world.Events.Scene.NativeFieldLoadVersion}, layout[{string.Join(',', world.Events.Scene.NativeActorObjectIds)}].");
        };
        for (int input = 0; input < 6000; input++)
        {
            world.EnsureField();
            if (world.Field!.Id == 229 && world.Events.NextTrigger.Kind == PlayerTriggerKind.Playable)
            {
                break;
            }
            DialogueState? dialogue = world.Events.CurrentDialogue;
            PlayerAction next = session.IsMoviePlaying ? new PlayerAction(PlayerActionKind.SkipMovie) : dialogue?.IsNamePrompt == true ? new PlayerAction(PlayerActionKind.EnterName, Text: "Cecilia") : dialogue?.FirstChoice is int choice ? new PlayerAction(PlayerActionKind.Choose, choice) : dialogue is not null ? new PlayerAction(PlayerActionKind.Confirm) : new PlayerAction(PlayerActionKind.Wait);
            Assert.True(world.Apply(next).Accepted);
            foreach (ActorState moving in world.Events.Actors.Where(actor => actor.Visible && actor.NativeMovementCompletion542 == 1 && actor.NativeScriptMovement is { Active: true }))
            {
                NativeScriptMovementState motion = moving.NativeScriptMovement!;
                Assert.Equal(motion.MovementDirection, (byte)moving.Direction);
                if (motion.AutomaticAnimation)
                {
                    Assert.Equal(moving.MovementSpeed < 2696 ? moving.WalkAnimationId : moving.RunAnimationId, moving.AnimationId);
                    Assert.True(moving.AnimationPlaying);
                    Assert.True(moving.AnimationLoop);
                    Assert.Equal(world.Field!.GetAnimationFrameCount(moving.ModelId, moving.AnimationId) - 1, moving.AnimationLast);
                }
                if (!verified.Add((world.Events.FieldId, moving.ObjectId)))
                {
                    continue;
                }
                JsonElement snapshot = world.Events.Capture();
                using var restored = new GameWorld(new GameSession(session.State with { EventState = snapshot, EventMemory = session.State.EventMemory}), directory);
                restored.EnsureField();
                Assert.True(JsonElement.DeepEquals(snapshot, restored.Events.Capture()));
                Assert.Equal(world.Render(TimeSpan.Zero), restored.Render(TimeSpan.Zero));
            }
        }
        Assert.Equal(229, world.Field!.Id);
        Assert.Equal(PlayerTriggerKind.Playable, world.Events.NextTrigger.Kind);
        Assert.Null(world.Events.LastFault);
        Assert.Contains(observed, item => item.Field == 180);
        Assert.Contains(observed, item => item.Field == 181);
        Assert.Contains(verified, item => item.Field == 180);
        Assert.Contains(verified, item => item.Field == 181);
    }
}
