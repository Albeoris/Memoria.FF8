using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class QuistisStationaryAnimationTests
{
    [Fact]
    public void CorridorQuistisUsesLocomotionOnlyForCommittedDisplacement()
    {
        string game = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(game), "Installed original field corpus is required.");
        var session = GameSession.CreateColdProcess();
        session.StartNewGame();
        using var world = new GameWorld(session, game, persistReplayJournal: false);
        for (int input = 0; input < 6000; input++)
        {
            world.EnsureField();
            if (world.Field!.Id == 229 && world.Events.NextTrigger.Kind == PlayerTriggerKind.Playable)
            {
                break;
            }
            DialogueState? dialogue = world.Events.CurrentDialogue;
            PlayerAction next = session.IsMoviePlaying ? new PlayerAction(PlayerActionKind.SkipMovie) : dialogue?.IsNamePrompt == true ? new PlayerAction(PlayerActionKind.EnterName, Text: "Squall") : dialogue?.FirstChoice is int choice ? new PlayerAction(PlayerActionKind.Choose, choice) : dialogue is { CanConfirm: true } ? new PlayerAction(PlayerActionKind.Confirm) : new PlayerAction(PlayerActionKind.Wait);
            Assert.True(world.Apply(next).Accepted);
        }
        Assert.Equal(229, world.Events.FieldId);
        ActorState quistis = world.Events.Actors[1];
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: -1)).Accepted);
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: -1)).Accepted);
        for (int input = 0; input < 1000 && quistis.NativeScriptMovement is not { AutomaticAnimation: true }; input++)
        {
            DialogueState? dialogue = world.Events.CurrentDialogue;
            PlayerAction next = dialogue?.FirstChoice is int choice ? new PlayerAction(PlayerActionKind.Choose, choice) : dialogue is { CanConfirm: true } ? new PlayerAction(PlayerActionKind.Confirm) : world.Events.IsUserControlEnabled ? new PlayerAction(PlayerActionKind.Move, Vertical: 1) : new PlayerAction(PlayerActionKind.Wait);
            Assert.True(world.Apply(next).Accepted);
        }
        Assert.True(quistis.NativeScriptMovement is { AutomaticAnimation: true });
        List<object> samples = [];
        int stationaryLocomotion = 0;
        int movingIdle = 0;
        int movingCallbacks = 0;
        int stationaryCallbacks = 0;
        for (int callback = 0; callback < 900; callback++)
        {
            var before = (quistis.X, quistis.Y, quistis.Z);
            int previousAnimation = quistis.AnimationId;
            DialogueState? dialogue = world.Events.CurrentDialogue;
            PlayerAction action = dialogue?.FirstChoice is int choice ? new PlayerAction(PlayerActionKind.Choose, choice) : dialogue is { CanConfirm: true } ? new PlayerAction(PlayerActionKind.Confirm) : callback % 28 < 6 && world.Events.IsUserControlEnabled ? new PlayerAction(PlayerActionKind.Move, Vertical: 1) : new PlayerAction(PlayerActionKind.Wait);
            Assert.True(world.Apply(action).Accepted);
            Assert.Null(world.Events.LastFault);
            bool moved = before != (quistis.X, quistis.Y, quistis.Z);
            if (quistis.NativeScriptMovement is { AutomaticAnimation: true })
            {
                if (moved)
                {
                    movingCallbacks++;
                    if (quistis.AnimationId == quistis.BaseAnimationId)
                    {
                        movingIdle++;
                    }
                }
                else
                {
                    stationaryCallbacks++;
                    if (quistis.AnimationId != quistis.BaseAnimationId)
                    {
                        stationaryLocomotion++;
                    }
                }
            }
            if (previousAnimation != quistis.AnimationId || callback % 25 == 0)
            {
                samples.Add(new { world.Events.Frame, quistis.X, quistis.Y, moved, quistis.AnimationId, quistis.AnimationFrame, quistis.NativeMovementCompletion542, quistis.NativeScriptMovement });
            }
        }
        string output = Path.Combine(AppContext.BaseDirectory, "quistis-stationary-samples.json");
        File.WriteAllText(output, JsonSerializer.Serialize(samples));
        Console.WriteLine($"Quistis moving{movingCallbacks}/stationary{stationaryCallbacks}, stationary locomotion{stationaryLocomotion}/moving idle{movingIdle}; samples: {output}");
        Assert.True(movingCallbacks > 30);
        Assert.True(stationaryCallbacks > 100);
        Assert.Equal(0, stationaryLocomotion);
        Assert.Equal(0, movingIdle);

        for (int callback = 0; callback < 50; callback++)
        {
            Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
        }
        Assert.Equal(quistis.BaseAnimationId, quistis.AnimationId);
        session.SetEventState(world.Events.Capture());
        DirectoryInfo? repository = new(AppContext.BaseDirectory);
        while (repository is not null && !File.Exists(Path.Combine(repository.FullName, "docs", "recreate-checkpoint.md")))
        {
            repository = repository.Parent;
        }
        Assert.NotNull(repository);
        string checkpoint = Path.Combine(repository.FullName, "../fixtures/external-fixture.json");
        session.Save(checkpoint, registerJournalReference: false);
        GameState source = session.State;
        GameState recreated = GameWorld.RecreateSave(GameSession.LoadReplaySource(checkpoint), game);
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(source with { ReplayJournal = null }), JsonSerializer.SerializeToElement(recreated with { ReplayJournal = null })));
        using var rebuilt = new GameWorld(new GameSession(recreated), game, persistReplayJournal: false);
        rebuilt.EnsureField();
        Assert.Equal(world.Render(TimeSpan.Zero), rebuilt.Render(TimeSpan.Zero));
        foreach (PlayerAction action in new[] { new PlayerAction(PlayerActionKind.Wait), new PlayerAction(PlayerActionKind.Move, Vertical: 1) })
        {
            var before = (world.Events.PlayerActor!.X, world.Events.PlayerActor.Y);
            Assert.Equal(world.Apply(action), rebuilt.Apply(action));
            Assert.True(JsonElement.DeepEquals(world.Events.Capture(), rebuilt.Events.Capture()));
            Assert.Equal(world.Render(TimeSpan.Zero), rebuilt.Render(TimeSpan.Zero));
            if (action.Kind == PlayerActionKind.Move)
            {
                Assert.NotEqual(before, (world.Events.PlayerActor.X, world.Events.PlayerActor.Y));
            }
        }
    }
}
