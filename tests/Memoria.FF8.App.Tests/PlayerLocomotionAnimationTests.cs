using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class PlayerLocomotionAnimationTests
{
    [Fact]
    public void OrdinarySquallWalkAdvancesOncePerCallbackWrapsAndRecreatesFromNewGame()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed field corpus is required.");
        var session = GameSession.CreateColdProcess();
        session.StartNewGame();
        using var world = new GameWorld(session, directory);
        for (int action = 0; action < 5000; action++)
        {
            world.EnsureField();
            if (world.Field!.Id == 229 && world.Events.NextTrigger.Kind == PlayerTriggerKind.Playable)
            {
                break;
            }
            DialogueState? dialogue = world.Events.CurrentDialogue;
            PlayerAction next = session.IsMoviePlaying ? new PlayerAction(PlayerActionKind.SkipMovie) : dialogue?.IsNamePrompt == true ? new PlayerAction(PlayerActionKind.EnterName, Text: "Squall") : dialogue?.FirstChoice is int choice ? new PlayerAction(PlayerActionKind.Choose, choice) : dialogue is not null ? new PlayerAction(PlayerActionKind.Confirm) : new PlayerAction(PlayerActionKind.Wait);
            Assert.True(world.Apply(next).Accepted);
        }
        Assert.Equal(229, world.Field!.Id);
        Assert.Equal(PlayerTriggerKind.Playable, world.Events.NextTrigger.Kind);
        ActorState player = world.Events.PlayerActor!;
        Assert.Equal(0, player.CharacterId);
        Assert.True(player.NativePartyFlags!.AutoAnimation4);
        Assert.False(world.Events.Scene.RunEnabled);
        int frameCount = world.Field.GetAnimationFrameCount(player.ModelId, player.WalkAnimationId);
        Assert.True(frameCount > 2);
        Assert.True(player.AnimationSpeed > 0);
        var firstPose = world.Field.Models[player.ModelId].Pose(player.WalkAnimationId, 0);
        bool poseChanged = false;
        int callbacks = (frameCount * 16 + player.AnimationSpeed - 1) / player.AnimationSpeed + 4;
        int wraps = 0;
        for (int callback = 0; callback < callbacks; callback++)
        {
            float previousX = player.X;
            float previousY = player.Y;
            int previous = player.AnimationId == player.WalkAnimationId ? (int)(player.AnimationFrame * 16) : 0;
            int expected = unchecked((ushort)(previous + player.AnimationSpeed));
            if (expected >= frameCount * 16)
            {
                expected = 0;
                wraps++;
            }
            long before = world.Events.Frame;
            PlayerActionResult result = world.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: callback < 2 || callback % 2 == 0 ? -1 : 1));
            Assert.True(result.Accepted);
            Assert.Equal(1, result.FramesAdvanced);
            Assert.Equal(before + 1, world.Events.Frame);
            Assert.True(player.X != previousX || player.Y != previousY);
            Assert.Equal(player.WalkAnimationId, player.AnimationId);
            Assert.Equal(expected / 16f, player.AnimationFrame);
            poseChanged |= !firstPose.SequenceEqual(world.Field.Models[player.ModelId].Pose(player.AnimationId, player.AnimationFrame));
        }
        Assert.True(wraps > 0);
        Assert.True(poseChanged, "The walk must change the model pose as well as the animation counter.");
        Console.WriteLine($"Squall229: {callbacks} ordinary moving callbacks, {frameCount} walk frames, speed{player.AnimationSpeed}/16, {wraps} wraps; exact native478270 stepping, including the first callback.");

        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
        Assert.Equal(player.BaseAnimationId, player.AnimationId);
        int idleFrames = world.Field.GetAnimationFrameCount(player.ModelId, player.BaseAnimationId);
        Assert.Equal(player.AnimationSpeed >= idleFrames * 16 ? 0 : player.AnimationSpeed / 16f, player.AnimationFrame);
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: -1)).Accepted);
        Assert.Equal(player.WalkAnimationId, player.AnimationId);
        Assert.Equal(player.AnimationSpeed / 16f, player.AnimationFrame);

        GameState source = session.State with { EventState = world.Events.Capture() };
        DirectoryInfo? repository = new(AppContext.BaseDirectory);
        while (repository is not null && !File.Exists(Path.Combine(repository.FullName, "docs", "recreate-checkpoint.md")))
        {
            repository = repository.Parent;
        }
        string output = Path.Combine(repository!.FullName, "../fixtures");
        Directory.CreateDirectory(output);
        new GameSession(source).Save(Path.Combine(output, "squall-walk-source.json"), registerJournalReference: false);
        GameState recreated = GameWorld.RecreateSave(source, directory);
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(source with { ReplayJournal = null }), JsonSerializer.SerializeToElement(recreated with { ReplayJournal = null })));
        new GameSession(recreated).Save(Path.Combine(output, "squall-walk-recreated.json"), registerJournalReference: false);
        using var independent = new GameWorld(new GameSession(recreated), directory);
        independent.EnsureField();
        Assert.Equal(world.Render(TimeSpan.Zero), independent.Render(TimeSpan.Zero));
        PlayerAction continuation = new(PlayerActionKind.Move, Horizontal: 1);
        Assert.Equal(world.Apply(continuation), independent.Apply(continuation));
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), independent.Events.Capture()));
        Assert.Equal(world.Render(TimeSpan.Zero), independent.Render(TimeSpan.Zero));
        Console.WriteLine("Authentic New Game journal recreation matches complete GameState, CPU pixels and the next ordinary moving callback; both implementations are current Memoria.");
    }
}
