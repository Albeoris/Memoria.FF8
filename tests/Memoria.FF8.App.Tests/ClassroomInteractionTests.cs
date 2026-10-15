using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class ClassroomInteractionTests
{
    [Fact]
    public void PersonalDeskUsesNativeRetainedLineActionAndNaturallyEntersTerminalField()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed field corpus is required.");
        GameState classroom = RecreateClassroom(directory);
        var session = new GameSession(classroom with { EventMemory = classroom.EventMemory});
        using var world = new GameWorld(session, directory, persistReplayJournal: false);
        world.EnsureField();
        Assert.Equal(1185, world.Events.Frame);
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: -1)).Accepted);
        ActorState desk = world.Events.Actors[23];
        Assert.True(desk.LineContact);
        Assert.True(desk.NativeLineFacingContact);
        Assert.Equal((byte?)252, desk.NativeLineFacingDirection);
        JsonElement beforeConfirm = world.Events.Capture();
        using var restored = new GameWorld(new GameSession(session.State with { EventMemory = session.State.EventMemory, EventState = beforeConfirm }), directory, persistReplayJournal: false);
        restored.EnsureField();
        var confirm = new PlayerAction(PlayerActionKind.Confirm);
        Assert.Equal(world.Apply(confirm), restored.Apply(confirm));
        Assert.Equal((byte)16, world.Events.Scene.NativeDoorPendingEvents![23]);
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
        for (int tick = 0; tick < 200 && session.State.FieldId == 232; tick++)
        {
            var wait = new PlayerAction(PlayerActionKind.Wait);
            Assert.Equal(world.Apply(wait), restored.Apply(wait));
            Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
        }
        Assert.Equal(234, session.State.FieldId);
        world.EnsureField();
        restored.EnsureField();
        for (int tick = 0; tick < 4; tick++)
        {
            Assert.Equal(world.Apply(new PlayerAction(PlayerActionKind.Wait)), restored.Apply(new PlayerAction(PlayerActionKind.Wait)));
            Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
        }
        Assert.Null(world.Events.LastFault);
        Assert.Equal(PlayerTriggerKind.Playable, world.Events.NextTrigger.Kind);
        for (int tick = 0; tick < 100 && world.Events.CurrentDialogue is null; tick++)
        {
            Assert.Equal(world.Apply(new PlayerAction(PlayerActionKind.Wait)), restored.Apply(new PlayerAction(PlayerActionKind.Wait)));
            Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
        }
        Assert.Equal(2, world.Events.CurrentDialogue?.MessageId);
        Assert.Equal(1, world.Events.CurrentDialogue?.FirstChoice);
        Assert.Equal(2, world.Events.CurrentDialogue?.LastChoice);
        Assert.Equal(world.Render(TimeSpan.Zero), restored.Render(TimeSpan.Zero));
    }

    [Fact]
    public void OrdinaryClassroomPushAndScriptControlHandoffPreserveNativeContinuation()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed field corpus is required.");
        GameState classroom = RecreateClassroom(directory);
        var session = new GameSession(classroom with { EventMemory = classroom.EventMemory});
        using (var world = new GameWorld(session, directory, persistReplayJournal: false))
        {
            world.EnsureField();
            int pushes = 0;
            world.Events.CommandExecuted += command =>
            {
                if (command.ObjectId == 9 && command.ScriptId == 79 && command.Name == "LBL")
                {
                    pushes++;
                }
            };
            Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Move, Vertical: 1, Frames: 5)).Accepted);
            Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: -1, Frames: 30)).Accepted);
            Assert.True(pushes > 0);
            Assert.Equal(1220, world.Events.Frame);
            Assert.Null(world.Events.LastFault);
            using var resumed = new GameWorld(new GameSession(session.State with { EventMemory = session.State.EventMemory, EventState = world.Events.Capture() }), directory, persistReplayJournal: false);
            resumed.EnsureField();
            Assert.Equal(world.Apply(new PlayerAction(PlayerActionKind.Wait)), resumed.Apply(new PlayerAction(PlayerActionKind.Wait)));
            Assert.True(JsonElement.DeepEquals(world.Events.Capture(), resumed.Events.Capture()));
        }
        using (var world = new GameWorld(new GameSession(classroom with { EventMemory = classroom.EventMemory}), directory, persistReplayJournal: false))
        {
            world.EnsureField();
            Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Move, Vertical: -1, Frames: 60)).Accepted);
            Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: -1, Frames: 30)).Accepted);
            Assert.False(world.Events.IsUserControlEnabled);
            ActorState player = Assert.Single(world.Events.Actors, actor => actor.CharacterId == 0 && actor.Active);
            Assert.Equal(player.BaseAnimationId, player.AnimationId);
            Assert.Equal(world.Field!.GetAnimationFrameCount(player.ModelId, player.BaseAnimationId) - 1, player.AnimationLast);
            Assert.Equal(0, player.AnimationFrame);
            Assert.True(player.AnimationLoop);
        }
    }

    private static GameState RecreateClassroom(string directory)
    {
        DirectoryInfo? repository = new(AppContext.BaseDirectory);
        while (repository is not null && !File.Exists(Path.Combine(repository.FullName, "docs", "recreate-checkpoint.md")))
        {
            repository = repository.Parent;
        }
        ReplayJournal source = GameSession.LoadReplaySource(Path.Combine(repository!.FullName, "../fixtures/external-fixture.json")).ReplayJournal!;
        var seed = GameSession.CreateColdProcess();
        seed.StartNewGame();
        ReplayJournal prefix = seed.State.ReplayJournal!;
        foreach (ReplayJournalEntry entry in source.Entries)
        {
            prefix = prefix.Append(entry);
            if (entry.ResultFieldId == 232 && entry.ResultTrigger == (int)PlayerTriggerKind.Playable)
            {
                break;
            }
        }
        ReplayJournalEntry endpoint = prefix.Endpoint!;
        return GameWorld.RecreateSave(new GameState { Scene = endpoint.ResultScene!.Value, FieldId = endpoint.ResultFieldId!.Value, ReplayJournal = prefix }, directory);
    }
}
