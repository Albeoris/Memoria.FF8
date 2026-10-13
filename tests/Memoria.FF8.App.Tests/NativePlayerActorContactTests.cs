using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativePlayerActorContactTests
{
    [Fact]
    public void ActualCavernGuardContactStopsNativeMovementAndPreservesIndependentContinuation()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed field corpus is required.");
        DirectoryInfo? repository = new(AppContext.BaseDirectory);
        while (repository is not null && !File.Exists(Path.Combine(repository.FullName, "docs", "recreate-checkpoint.md")))
        {
            repository = repository.Parent;
        }
        string source = Path.Combine(repository!.FullName, "../fixtures/external-fixture.json");
        var session = new GameSession(GameSession.Load(source));
        var replaySession = new GameSession(GameSession.Load(source));
        using var world = new GameWorld(session, directory);
        using var replay = new GameWorld(replaySession, directory);
        world.EnsureField();
        replay.EnsureField();
        Assert.Equal((129, 78L), (world.Events.FieldId, world.Events.Frame));
        FieldFixedPosition original = world.Events.Scene.NativePlayerPosition!;
        Assert.Equal(new FieldFixedPosition(-551110, 17929260, 0), original);
        ActorState player = world.Events.Actors.Single(actor => actor.CharacterId == 0);
        ActorState guard = world.Events.Actors.Single(actor => actor.ObjectId == 10);
        Assert.Equal((-157f, 4550f, 0f, 160), (guard.X, guard.Y, guard.Z, guard.PushRadius));
        Assert.True(guard.CollisionEnabled);
        Assert.True(guard.PushEnabled);
        Assert.False(guard.NativePushContactPending);
        NativePlayerMovementResult? certificate = null;
        Func<NativePlayerMovementQuery, NativePlayerMovementResult?> originalResolver = world.Events.ResolveNativePlayerMovement!;
        var move = new PlayerAction(PlayerActionKind.Move, Horizontal: 0.1643242f, Vertical: -0.9864063f);
        JsonElement before = world.Events.Capture();
        world.Events.ResolveNativePlayerMovement = query =>
        {
            NativePlayerMovementResult result = originalResolver(query)!;
            return result with { ProposalGroundProbes = result.ProposalGroundProbes!.Select(probes => (IReadOnlyList<NativePlayerGroundProbe>)probes.Select(probe => probe with { ActorCollision = false }).ToArray()).ToArray() };
        };
        Assert.Throws<NotSupportedException>(() => world.Apply(move));
        Assert.True(JsonElement.DeepEquals(before, world.Events.Capture()));
        world.Events.ResolveNativePlayerMovement = query => certificate = originalResolver(query);
        PlayerActionResult result = world.Apply(move);
        Assert.True(result.Accepted);
        Assert.Equal(1, result.FramesAdvanced);
        Assert.Equal(result, replay.Apply(move));
        Assert.NotNull(certificate);
        Assert.False(certificate.Accepted);
        Assert.Single(certificate.Proposals);
        Assert.Equal(original, new FieldFixedPosition(certificate.FixedX, certificate.FixedY, certificate.FixedZ));
        Assert.Equal(original, world.Events.Scene.NativePlayerPosition);
        Assert.Equal((byte?)0, world.Events.Scene.NativePlayerEarlyRetryState);
        Assert.True(guard.NativePushContactPending);
        Assert.Contains(certificate.ProposalGroundProbes![0], probe => probe.ActorCollision);
        Console.WriteLine($"Actual12978 guard contact: dir{certificate.Direction}, triangle{certificate.Triangle}, proposal{certificate.Proposals[0]}, collisions[{string.Join(',', certificate.ProposalGroundProbes[0].Select(probe => probe.ActorCollision))}], retained{original}.");
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), replay.Events.Capture()));
        Assert.Equal(world.Render(TimeSpan.Zero), replay.Render(TimeSpan.Zero));
        using var restored = new GameWorld(new GameSession(session.State with { EventState = world.Events.Capture() }), directory);
        restored.EnsureField();
        var wait = new PlayerAction(PlayerActionKind.Wait);
        Assert.Equal(world.Apply(wait), restored.Apply(wait));
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
        Assert.Equal(world.Render(TimeSpan.Zero), restored.Render(TimeSpan.Zero));
        Assert.False(world.Events.Actors.Single(actor => actor.ObjectId == 10).NativePushContactPending);
        Assert.Null(world.Events.LastFault);
    }
}
