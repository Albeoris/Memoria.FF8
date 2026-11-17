using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class CorridorCancellationTests
{
    [Theory]
    [InlineData(100, 1)]
    [InlineData(150, 6)]
    [InlineData(300, 6)]
    public void LeavingInfirmaryWhileCancellingContinuesThroughCorridor(int movementPercent, int cancelInterval)
    {
        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF8_CORRIDOR_SAVE");
        Assert.SkipUnless(File.Exists(source), "An external infirmary continuation is required.");
        string directory = TestResources.GameDirectory;
        GameState state = GameplaySaveSessionLoader.Load(source!, directory);
        GameSession session = new(state);
        using GameWorld world = new(session, directory);
        world.EnsureField();
        world.Events.PlayerMovementSpeedPercent = movementPercent;
        int? previousMessage = null;
        bool forcedWalkingObserved = false;
        for (int callback = 0; callback < 1800 && !session.IsMoviePlaying; callback++)
        {
            world.EnsureField();
            if (callback % cancelInterval == 0 && world.Events.CurrentDialogue is { CanConfirm: true })
            {
                world.Apply(new PlayerAction(PlayerActionKind.Cancel));
            }
            if (world.Events.CanAcceptMovementInput)
            {
                float? before = world.Events.PlayerActor?.X;
                world.Apply(new PlayerAction(PlayerActionKind.Move, Vertical: 1));
                if (!session.IsMoviePlaying && world.Events.IsUserControlEnabled && !world.Events.Scene.RunEnabled && world.Events.FieldId == 229 && world.Events.PlayerActor is { } player && player.X != before)
                {
                    Assert.Equal(2696, player.MovementSpeed);
                    forcedWalkingObserved = true;
                }
            }
            else
            {
                world.Events.Step();
            }
            if (previousMessage != world.Events.CurrentDialogue?.MessageId)
            {
                Console.WriteLine($"{movementPercent}% frame{world.Events.Frame} message{world.Events.CurrentDialogue?.MessageId} X{world.Events.PlayerActor?.X}.");
                previousMessage = world.Events.CurrentDialogue?.MessageId;
            }
        }
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_CORRIDOR_OUTPUT");
        if (output is not null)
        {
            Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output, $"after-{movementPercent}.json"), world.Events.Capture().GetRawText());
        }
        Assert.Null(world.Events.LastFault);
        Assert.True(session.IsMoviePlaying, $"Scene stalled at {world.Events.Frame}, {world.Events.NextTrigger}.");
        Assert.True(state.FieldId != 180 || forcedWalkingObserved);
        session.SetEventState(world.Events.Capture());
        GameSession restoredSession = new(session.PrepareSnapshot());
        using GameWorld restored = new(restoredSession, directory);
        restored.EnsureField();
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
        Assert.True(restored.Apply(new PlayerAction(PlayerActionKind.SkipMovie)).Accepted);
        for (int callback = 0; callback < 500 && restoredSession.State.FieldId == 229; callback++)
        {
            restored.Events.Step();
        }
        restored.EnsureField();
        Assert.Equal(232, restored.Events.FieldId);
        for (int callback = 0; callback < 500 && restored.Events.CurrentDialogue is null; callback++)
        {
            restored.Events.Step();
        }
        Assert.Equal(0, restored.Events.CurrentDialogue?.MessageId);
        Assert.Null(restored.Events.LastFault);
        string? checkpoint = Environment.GetEnvironmentVariable("MEMORIA_FF8_CORRIDOR_CHECKPOINT");
        if (checkpoint is not null && movementPercent == 150)
        {
            restored.WriteGameplaySave(checkpoint);
        }
    }

    [Fact]
    public void InterruptedSceneCanCompleteWithoutReplayingEarlierTravel()
    {
        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF8_CORRIDOR_MIGRATION_STAGE");
        string? destination = Environment.GetEnvironmentVariable("MEMORIA_FF8_CORRIDOR_MIGRATION_BOUNDARY");
        Assert.SkipUnless(File.Exists(source) && destination is not null, "External interrupted-scene migration paths are required.");
        string directory = TestResources.GameDirectory;
        GameSession session = new(GameplaySaveSessionLoader.Load(source!, directory));
        using GameWorld world = new(session, directory);
        world.EnsureField();
        Assert.False(world.Events.IsUserControlEnabled);
        for (int callback = 0; callback < 900 && !world.Events.IsUserControlEnabled; callback++)
        {
            if (world.Events.CurrentDialogue is { CanConfirm: true })
            {
                world.Apply(new PlayerAction(PlayerActionKind.Cancel));
            }
            world.Events.Step();
        }
        Assert.Null(world.Events.LastFault);
        Assert.True(world.Events.IsUserControlEnabled);
        Assert.DoesNotContain(world.Events.CaptureGameplaySaveScripts().Fibers, fiber => !fiber.Complete && fiber.ObjectId == 1 && fiber.ScriptId == 13);
        GameplaySaveComponentDocument document = world.CaptureGameplaySave();
        using FileStream target = new(destination!, FileMode.CreateNew);
        GameplaySaveCodec.Write(target, document);
    }

    [Fact]
    public void CompletedCorridorCheckpointPreservesTheNextNativeCallback()
    {
        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF8_CORRIDOR_RESUME");
        Assert.SkipUnless(File.Exists(source), "An external completed corridor checkpoint is required.");
        string directory = TestResources.GameDirectory;
        GameState state = GameplaySaveSessionLoader.Load(source!, directory);
        using GameWorld first = new(new GameSession(state), directory);
        using GameWorld second = new(new GameSession(state), directory);
        first.EnsureField();
        second.EnsureField();
        Assert.Equal(232, first.Events.FieldId);
        Assert.True(JsonElement.DeepEquals(first.Events.Capture(), second.Events.Capture()));
        long frame = first.Events.Frame;
        first.Events.Step();
        second.Events.Step();
        Assert.Equal(frame + 1, first.Events.Frame);
        Assert.Null(first.Events.LastFault);
        Assert.Null(second.Events.LastFault);
        Assert.True(JsonElement.DeepEquals(first.Events.Capture(), second.Events.Capture()));
    }
}
