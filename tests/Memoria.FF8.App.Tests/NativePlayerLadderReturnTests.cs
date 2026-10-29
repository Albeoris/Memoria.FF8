using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativePlayerLadderReturnTests
{
    [Fact]
    public void ReverseLadderExitPreservesFractionalHeightOnBlockedMovement()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed field corpus is required.");
        string source = TestResources.FixturePath("App.Tests.NativePlayerLadderReturnTests.1");
        var session = new GameSession(GameSession.Load(source));
        using var world = new GameWorld(session, directory);
        world.EnsureField();

        void Wait(int frames)
        {
            for (int tick = 0; tick < frames; tick++)
            {
                Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
            }
        }

        Assert.Equal((354, 1016L), (world.Events.FieldId, world.Events.Frame));
        for (int tick = 0; tick < 24; tick++)
        {
            Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: 1)).Accepted);
        }
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Confirm)).Accepted);
        Wait(90);
        ActorState player = world.Events.PlayerActor!;
        Assert.True(player.NativeLadder is { Active: true, Up: true });
        Assert.Equal((ushort?)1, player.NativeMovementCompletion542);
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.FieldButtons, 0x1000)).Accepted);
        Wait(60);
        Assert.Equal((short?)60, player.NativeTurnAccumulator474);
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.FieldButtons, 0x4000)).Accepted);
        Wait(125);
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.FieldButtons, 0)).Accepted);
        Assert.Null(player.NativeLadder);
        FieldFixedPosition original = world.Events.Scene.NativePlayerPosition!;
        Assert.Equal(new FieldFixedPosition(-646402, 10758294, 534213), original);
        Assert.NotEqual(0, original.Z & 4095);
        var blocked = new PlayerAction(PlayerActionKind.Move, Horizontal: 1);
        var clear = new PlayerAction(PlayerActionKind.Move, Vertical: -1);
        Func<NativePlayerMovementQuery, NativePlayerMovementResult?> resolve = world.Events.ResolveNativePlayerMovement!;
        JsonElement before = world.Events.Capture();
        foreach (PlayerAction action in new[] { blocked, clear })
        {
            using var invalid = new GameWorld(new GameSession(session.State with { EventState = before }), directory);
            invalid.EnsureField();
            Func<NativePlayerMovementQuery, NativePlayerMovementResult?> invalidResolve = invalid.Events.ResolveNativePlayerMovement!;
            invalid.Events.ResolveNativePlayerMovement = query =>
            {
                NativePlayerMovementResult result = invalidResolve(query)!;
                // Blocked steps must preserve XYZ exactly; accepted ground heights must be integral.
                return result with { FixedZ = result.FixedZ + 1 };
            };
            Assert.Throws<NotSupportedException>(() => invalid.Apply(action));
            Assert.Equal(original, invalid.Events.Scene.NativePlayerPosition);
        }
        NativePlayerMovementResult? certificate = null;
        world.Events.ResolveNativePlayerMovement = query => certificate = resolve(query);
        Assert.True(world.Apply(blocked).Accepted);
        Assert.NotNull(certificate);
        Assert.False(certificate.Accepted);
        Assert.Equal(original, world.Events.Scene.NativePlayerPosition);
        Assert.Equal(original, new FieldFixedPosition(certificate.FixedX, certificate.FixedY, certificate.FixedZ));
        using var restored = new GameWorld(new GameSession(session.State with { EventState = world.Events.Capture() }), directory);
        restored.EnsureField();
        Assert.Equal(world.Apply(blocked), restored.Apply(blocked));
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
        Assert.Equal(original, restored.Events.Scene.NativePlayerPosition);
        Assert.Equal(world.Apply(clear), restored.Apply(clear));
        Assert.True(certificate!.Accepted);
        Assert.Equal(0, world.Events.Scene.NativePlayerPosition!.Z & 4095);
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
        Assert.Null(world.Events.LastFault);
    }
}
