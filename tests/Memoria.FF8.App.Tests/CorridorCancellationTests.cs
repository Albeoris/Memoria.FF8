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
        for (int callback = 0; callback < 1800 && !session.IsMoviePlaying; callback++)
        {
            world.EnsureField();
            if (callback % cancelInterval == 0 && world.Events.CurrentDialogue is { CanConfirm: true })
            {
                world.Apply(new PlayerAction(PlayerActionKind.Cancel));
            }
            if (world.Events.CanAcceptMovementInput)
            {
                world.Apply(new PlayerAction(PlayerActionKind.Move, Vertical: 1));
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
        session.SetEventState(world.Events.Capture());
        using GameWorld restored = new(new GameSession(session.PrepareSnapshot()), directory);
        restored.EnsureField();
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
    }
}
