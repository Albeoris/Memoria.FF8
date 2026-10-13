using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBlockedMovementTests
{
    [Fact]
    public void NaturalWorldReturnRecordsLoaderRetryClearAndAcceptsOriginalThirdProposal()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed world/field corpus is required.");
        DirectoryInfo? repository = new(AppContext.BaseDirectory);
        while (repository is not null && !File.Exists(Path.Combine(repository.FullName, "docs", "recreate-checkpoint.md")))
        {
            repository = repository.Parent;
        }
        string source = Path.Combine(repository!.FullName, "../fixtures/external-fixture.json");
        var session = new GameSession(GameSession.Load(source));
        using var world = new GameWorld(session, directory);
        world.EnsureField();
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: -1, Vertical: -1)).Accepted);
        Assert.Equal(136, session.State.FieldId);
        world.EnsureField();
        for (int tick = 0; tick < 61; tick++)
        {
            Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
        }
        Assert.Equal(61, world.Events.Frame);
        Assert.Equal((byte?)0, world.Events.Scene.NativePlayerEarlyRetryState);
        for (int tick = 0; tick < 13; tick++)
        {
            Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Move, Vertical: -1)).Accepted);
        }
        Assert.Equal(new FieldFixedPosition(716800, -7644592, 0), world.Events.Scene.NativePlayerPosition);
        Assert.Equal(74, world.Events.Frame);
        JsonElement beforeInvalid = world.Events.Capture();
        JsonNode invalid = JsonNode.Parse(beforeInvalid.GetRawText())!;
        invalid["Scene"]!["NativePlayerEarlyRetryState"] = 2;
        Assert.Throws<InvalidDataException>(() => world.Events.Restore(JsonSerializer.SerializeToElement(invalid)));
        Assert.True(JsonElement.DeepEquals(beforeInvalid, world.Events.Capture()));
        using var restored = new GameWorld(new GameSession(session.State with { EventState = world.Events.Capture() }), directory);
        restored.EnsureField();
        var originalUp = new PlayerAction(PlayerActionKind.Move, Vertical: -1);
        Assert.Equal(world.Apply(originalUp), restored.Apply(originalUp));
        Assert.Equal(new FieldFixedPosition(765334, -7527392, 0), world.Events.Scene.NativePlayerPosition);
        ActorState player = world.Events.Actors.Single(actor => actor.CharacterId == 0);
        Assert.Equal(112, player.Direction);
        Assert.Equal(9, player.WalkmeshTriangle);
        Assert.Equal((byte?)0, world.Events.Scene.NativePlayerEarlyRetryState);
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
        Assert.Equal(world.Render(TimeSpan.Zero), restored.Render(TimeSpan.Zero));
        for (int tick = 0; tick < 3; tick++)
        {
            var next = new PlayerAction(PlayerActionKind.Wait);
            Assert.Equal(world.Apply(next), restored.Apply(next));
            Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
            Assert.Equal(world.Render(TimeSpan.Zero), restored.Render(TimeSpan.Zero));
        }
        Console.WriteLine("Natural world1649 -> fresh136, native477C90 retryclear0; Wait61/MoveUp13 ->74; original14th Up accepts thirdcandidate/raw(765334,-7527392,0)/dir112/tri9, restored full VM/pixels match.");
    }

    [Fact]
    public void ActualFireCavernUnknownRetryStateRefusesAtomicallyAndOrdinarySidestepRestores()
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
        using var world = new GameWorld(session, directory);
        world.EnsureField();
        Assert.Equal(136, world.Events.FieldId);
        Assert.Equal(74, world.Events.Frame);
        Assert.Null(world.Events.Scene.NativePlayerEarlyRetryState);
        ActorState player = world.Events.Actors.Single(actor => actor.CharacterId == 0);
        FieldFixedPosition original = world.Events.Scene.NativePlayerPosition!;
        FieldNativeMovementRequest request = new(player.WalkmeshTriangle, original.X, original.Y, original.Z, 128, 7929, 48, true, false, 0, true);
        FieldNativeMovementResult first = FieldNativeMovementProbe.Resolve(world.Field!.Walkmesh, request, world.Events.Scene.LockedTriangles);
        FieldNativeMovementResult early = FieldNativeMovementProbe.Resolve(world.Field.Walkmesh, request with { EarlyRetryState = 1 }, world.Events.Scene.LockedTriangles);
        Console.WriteLine($"Actual13674 pure wall: normal{first.Status}/retry{first.RetryCount}/proposals{first.Proposals.Count}/dir{first.Direction}/triangle{first.Triangle}/XYZ({first.FixedX},{first.FixedY},{first.FixedZ}), early{early.Status}/retry{early.RetryCount}/proposals{early.Proposals.Count}/dir{early.Direction}/triangle{early.Triangle}/XYZ({early.FixedX},{early.FixedY},{early.FixedZ}); fans[{string.Join(',', first.Proposals[^1].FanProbes.Select(probe => probe.Steering))}].");
        Assert.Equal(FieldNativeMovementStatus.Accepted, first.Status);
        Assert.Equal(FieldNativeMovementStatus.Blocked, early.Status);
        Assert.Equal(3, first.Proposals.Count);
        Assert.Equal(2, early.Proposals.Count);
        Assert.Equal(first.Direction, early.Direction);
        Assert.Equal(first.Triangle, early.Triangle);
        JsonElement before = world.Events.Capture();
        Assert.Throws<NotSupportedException>(() => world.Apply(new PlayerAction(PlayerActionKind.Move, Vertical: -1)));
        Assert.True(JsonElement.DeepEquals(before, world.Events.Capture()));
        Assert.Equal(original, world.Events.Scene.NativePlayerPosition);
        Assert.Throws<NotSupportedException>(() => world.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: -1)));
        Assert.True(JsonElement.DeepEquals(before, world.Events.Capture()));
        Assert.Null(world.Events.Scene.NativeDoorDriverVersion);

        // The archived74 snapshot predates both native authorities. Establish
        // current movement and door state by executing the retained actual
        // fresh arrival's loader/init and the original ordinary61+13 actions.
        string freshSource = Path.Combine(repository.FullName, "../fixtures/external-fixture.json");
        var freshSession = new GameSession(GameSession.Load(freshSource));
        using var fresh = new GameWorld(freshSession, directory);
        fresh.EnsureField();
        for (int tick = 0; tick < 61; tick++)
        {
            Assert.True(fresh.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
        }
        for (int tick = 0; tick < 13; tick++)
        {
            Assert.True(fresh.Apply(new PlayerAction(PlayerActionKind.Move, Vertical: -1)).Accepted);
        }
        Assert.Equal(74, fresh.Events.Frame);
        Assert.Equal(original, fresh.Events.Scene.NativePlayerPosition);
        Assert.Equal((byte?)0, fresh.Events.Scene.NativePlayerEarlyRetryState);
        Assert.Equal(2, fresh.Events.Scene.NativeDoorDriverVersion);
        Assert.True(fresh.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: -1)).Accepted);
        Assert.Equal(new FieldFixedPosition(843664, -7644592, 0), fresh.Events.Scene.NativePlayerPosition);
        using var restored = new GameWorld(new GameSession(freshSession.State with { EventState = fresh.Events.Capture() }), directory);
        restored.EnsureField();
        for (int tick = 0; tick < 3; tick++)
        {
            var next = new PlayerAction(PlayerActionKind.Wait);
            Assert.Equal(fresh.Apply(next), restored.Apply(next));
            Assert.True(JsonElement.DeepEquals(fresh.Events.Capture(), restored.Events.Capture()));
            Assert.Equal(fresh.Render(TimeSpan.Zero), restored.Render(TimeSpan.Zero));
        }
    }
}
