using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class QuistisStationaryAnimationTests
{
    [Fact]
    public void CorridorFollowerIdlesWithQuistisMessageStillOpen()
    {
        string game = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(game), "Installed original field corpus is required.");
        var session = GameSession.CreateColdProcess();
        session.StartNewGame();
        using var world = new GameWorld(session, game);
        for (int input = 0; input < 6000; input++)
        {
            world.EnsureField();
            if (world.Events.FieldId == 229 && world.Events.NextTrigger.Kind == PlayerTriggerKind.Playable)
            {
                break;
            }
            DialogueState? dialogue = world.Events.CurrentDialogue;
            PlayerAction next = session.IsMoviePlaying ? new PlayerAction(PlayerActionKind.SkipMovie) : dialogue?.IsNamePrompt == true ? new PlayerAction(PlayerActionKind.EnterName, Text: "Squall") : dialogue?.FirstChoice is int choice ? new PlayerAction(PlayerActionKind.Choose, choice) : dialogue is { CanConfirm: true } ? new PlayerAction(PlayerActionKind.Confirm) : new PlayerAction(PlayerActionKind.Wait);
            Assert.True(world.Apply(next).Accepted);
        }
        Assert.Equal(229, world.Events.FieldId);
        // Keep the original corridor lane: lateral input reduces the peer distance and hides the reversed-radius defect.
        for (int input = 0; input < 800 && world.Events.PlayerActor!.X < -6200; input++)
        {
            Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Move, Vertical: 1)).Accepted);
        }
        ActorState squall = world.Events.PlayerActor!;
        ActorState quistis = world.Events.Actors[1];
        Assert.True(squall.X >= -6200);
        DialogueState message = Assert.Single(world.Events.Dialogues, dialogue => dialogue.MessageId == 1);
        Assert.Equal(1, message.MessageId);
        Assert.Equal(9, message.ObjectId);
        Assert.True(message.CanConfirm);
        Assert.True(world.Events.CanAcceptMovementInput);
        Assert.Equal(148, quistis.TalkRadius);
        Assert.Equal(48, squall.PushRadius);
        // Original object8/script44 closes message1 after WAIT100. Sample genuine callbacks inside that lifetime.
        for (int callback = 0; callback < 10; callback++)
        {
            long previousFrame = world.Events.Frame;
            world.Events.Step();
            Assert.Equal(previousFrame + 1, world.Events.Frame);
        }
        var stopped = (quistis.X, quistis.Y, quistis.Z);
        int walking = 0;
        for (int callback = 0; callback < 10; callback++)
        {
            long previousFrame = world.Events.Frame;
            world.Events.Step();
            Assert.Equal(previousFrame + 1, world.Events.Frame);
            Assert.Contains(message, world.Events.Dialogues);
            Assert.Equal(stopped, (quistis.X, quistis.Y, quistis.Z));
            Assert.Equal(quistis.BaseAnimationId, quistis.AnimationId);
            Assert.Null(world.Events.LastFault);
        }
        Console.WriteLine($"Open corridor message{message.MessageId}, player({squall.X},{squall.Y}), Quistis({quistis.X},{quistis.Y}), idle{quistis.AnimationId}; 10 stopped callbacks.");
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.FieldButtons)).Accepted);
        session.SetEventState(world.Events.Capture());
        string checkpoint = Path.Combine(AppContext.BaseDirectory, "quistis-open-message-checkpoint.json");
        session.Save(checkpoint);
        GameState recreated = GameSession.Load(checkpoint);
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(session.State), JsonSerializer.SerializeToElement(recreated)));
        using var rebuilt = new GameWorld(new GameSession(recreated), game);
        rebuilt.EnsureField();
        Assert.Equal(world.Render(TimeSpan.Zero), rebuilt.Render(TimeSpan.Zero));
        world.Events.Step();
        rebuilt.Events.Step();
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), rebuilt.Events.Capture()));
        for (int callback = 0; callback < 10; callback++)
        {
            Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Move, Vertical: 1)).Accepted);
            if (quistis.AnimationId == quistis.WalkAnimationId)
            {
                walking++;
            }
        }
        Assert.True(walking > 0);
        Assert.NotEqual(stopped, (quistis.X, quistis.Y, quistis.Z));
        for (int callback = 0; callback < 10; callback++)
        {
            long previousFrame = world.Events.Frame;
            world.Events.Step();
            Assert.Equal(previousFrame + 1, world.Events.Frame);
        }
        Assert.Equal(quistis.BaseAnimationId, quistis.AnimationId);
    }

    [Fact]
    public void FinalQuistisMessagePreservesScriptedStationaryAnimation()
    {
        string game = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(game), "Installed original field corpus is required.");
        var session = GameSession.CreateColdProcess();
        session.StartNewGame();
        using var world = new GameWorld(session, game);
        for (int input = 0; input < 7000; input++)
        {
            world.EnsureField();
            if (world.Events.FieldId == 229 && world.Events.CurrentDialogue?.MessageId == 10)
            {
                break;
            }
            DialogueState? dialogue = world.Events.CurrentDialogue;
            PlayerAction next = session.IsMoviePlaying ? new PlayerAction(PlayerActionKind.SkipMovie) : dialogue?.IsNamePrompt == true ? new PlayerAction(PlayerActionKind.EnterName, Text: "Squall") : dialogue?.FirstChoice is int choice ? new PlayerAction(PlayerActionKind.Choose, choice) : dialogue is { CanConfirm: true } ? new PlayerAction(PlayerActionKind.Confirm) : world.Events.FieldId == 229 && world.Events.CanAcceptMovementInput ? new PlayerAction(PlayerActionKind.Move, Vertical: 1) : new PlayerAction(PlayerActionKind.Wait);
            Assert.True(world.Apply(next).Accepted);
        }
        Assert.Equal(229, world.Events.FieldId);
        DialogueState message = Assert.IsType<DialogueState>(world.Events.CurrentDialogue);
        Assert.Equal(10, message.MessageId);
        Assert.Equal(1, message.ObjectId);
        Assert.False(world.Events.IsUserControlEnabled);
        ActorState quistis = world.Events.Actors[1];
        var position = (quistis.X, quistis.Y, quistis.Z);
        long start = world.Events.Frame;
        int animation = quistis.AnimationId;
        Assert.Equal(7, animation);
        for (int callback = 0; callback < 180; callback++)
        {
            world.Events.Step();
            Assert.Equal(start + callback + 1, world.Events.Frame);
            Assert.Equal(message, world.Events.CurrentDialogue);
            Assert.Equal(position, (quistis.X, quistis.Y, quistis.Z));
            Assert.Equal(animation, quistis.AnimationId);
            Assert.Null(world.Events.LastFault);
        }
        Console.WriteLine($"Final Quistis message10 stays open for 180 genuine callbacks at player({world.Events.PlayerActor!.X},{world.Events.PlayerActor.Y}); script-selected animation{animation} is preserved.");
    }
}
