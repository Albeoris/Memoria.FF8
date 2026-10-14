using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class ClassroomCameraTransitionTests
{
    [Fact]
    public void NaturalClassroomCameraCutsHideThePreviousDoorBeforePresentingEitherView()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original field corpus is required.");
        GameSession session = new(new GameState());
        session.StartNewGame();
        using GameWorld world = new(session, directory, persistReplayJournal: false);
        int centeringMoves = 0;
        for (int action = 0; action < 6000 && session.State.FieldId != 232; action++)
        {
            world.EnsureField();
            DialogueState? dialogue = world.Events.CurrentDialogue;
            PlayerAction next;
            if (session.IsMoviePlaying)
            {
                next = new(PlayerActionKind.SkipMovie);
            }
            else if (dialogue?.IsNamePrompt == true)
            {
                next = new(PlayerActionKind.EnterName, Text: "Squall");
            }
            else if (dialogue?.FirstChoice is int choice)
            {
                next = new(PlayerActionKind.Choose, choice);
            }
            else if (world.Events.CanConfirmDialogue)
            {
                next = new(PlayerActionKind.Confirm);
            }
            else if (world.Events.FieldId == 229 && world.Events.IsUserControlEnabled)
            {
                next = centeringMoves++ < 2 ? new(PlayerActionKind.Move, Horizontal: -1) : new(PlayerActionKind.Move, Vertical: 1);
            }
            else
            {
                next = new(PlayerActionKind.Wait);
            }
            Assert.True(world.Apply(next).Accepted);
        }
        world.EnsureField();
        Assert.Equal(232, world.Field!.Id);
        JsonElement allocated = world.Events.Capture();
        Assert.True(FieldPresentationReadiness.HasPendingInitialization(world.Events.Scene.NativeFieldInitialization));
        Assert.True(JsonElement.DeepEquals(allocated, world.Events.Capture()));
        bool frontSeen = false;
        bool returnSeen = false;
        for (int callback = 0; callback < 1000 && !returnSeen; callback++)
        {
            int previousCamera = world.Events.Scene.CameraIndex;
            Assert.True(world.Apply(new PlayerAction(world.Events.CanConfirmDialogue ? PlayerActionKind.Confirm : PlayerActionKind.Wait)).Accepted);
            int camera = world.Events.Scene.CameraIndex;
            if (previousCamera == 1 && camera == 0)
            {
                Assert.Equal(-1, world.Events.BackgroundStates[4]);
                frontSeen = true;
            }
            if (frontSeen && previousCamera == 0 && camera == 1)
            {
                Assert.Equal(-1, world.Events.BackgroundStates[5]);
                Assert.Equal(0, world.Events.BackgroundStates[4]);
                returnSeen = true;
            }
            if (frontSeen)
            {
                Assert.False(FieldPresentationReadiness.HasPendingInitialization(world.Events.Scene.NativeFieldInitialization));
                JsonElement before = world.Events.Capture();
                byte[] pixels = world.Render(TimeSpan.Zero);
                using GameWorld restored = new(new GameSession(session.State with { Variables = session.State.Variables.ToDictionary(), EventState = before }), directory, persistReplayJournal: false);
                restored.EnsureField();
                Assert.Equal(pixels, restored.Render(TimeSpan.Zero));
                Assert.True(JsonElement.DeepEquals(before, world.Events.Capture()));
            }
        }
        Assert.True(frontSeen);
        Assert.True(returnSeen);
        Assert.Null(world.Events.LastFault);
        GameState source = session.State with { EventState = world.Events.Capture() };
        GameState recreated = GameWorld.RecreateSave(source, directory);
        using GameWorld replay = new(new GameSession(recreated), directory, persistReplayJournal: false);
        replay.EnsureField();
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), replay.Events.Capture()));
        Assert.Equal(world.Render(TimeSpan.Zero), replay.Render(TimeSpan.Zero));
        Assert.Equal(world.Apply(new PlayerAction(PlayerActionKind.Wait)), replay.Apply(new PlayerAction(PlayerActionKind.Wait)));
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), replay.Events.Capture()));
        Assert.Equal(world.Render(TimeSpan.Zero), replay.Render(TimeSpan.Zero));
    }
}
