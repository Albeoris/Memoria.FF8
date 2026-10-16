using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class Esthar547MovementReadinessTests
{
    [Fact]
    public void ActualReadyEastSourceFinishesPendingControllerBeforeOrdinaryMovement()
    {
        string root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "AGENTS.md")))
        {
            root = Directory.GetParent(root)!.FullName;
        }
        GameSession session = new(GameSession.Load(Path.Combine(root, "../fixtures/external-fixture.json")));
        using GameWorld world = new(session, @"C:\Steam\steamapps\common\FINAL FANTASY VIII");
        world.EnsureField();
        int callbacks = 0;
        while (session.FieldFade!.Physical.Mode == 4 && callbacks < 240)
        {
            world.Apply(new(PlayerActionKind.Wait));
            callbacks++;
        }
        Assert.True(session.FieldFade!.Physical.Mode != 4, $"Still black after {callbacks} callbacks at frame{world.Events.Frame}; pending={string.Join(';', world.Events.Capture().GetProperty("Fibers").EnumerateArray().Where(fiber => !fiber.GetProperty("Complete").GetBoolean()).Select(fiber => $"{fiber.GetProperty("ObjectId")}/{fiber.GetProperty("ScriptId")}/{fiber.GetProperty("Position")}/{fiber.GetProperty("WaitReason")}"))}");
        ActorState player = world.Events.PlayerActor!;
        float beforeX = player.X;
        float beforeY = player.Y;
        Assert.True(world.Apply(new(PlayerActionKind.Move, Horizontal: 1, Vertical: -1)).Accepted);
        Assert.True(player.X != beforeX || player.Y != beforeY, $"No displacement after {callbacks} callbacks, mode{session.FieldFade.Physical.Mode}, frame{world.Events.Frame}.");
        Console.WriteLine($"Actual547: {callbacks} ordinary callbacks from966; first movement at frame{world.Events.Frame}; fadeMode{session.FieldFade.Physical.Mode}; ({beforeX},{beforeY})->({player.X},{player.Y}).");
    }
}
