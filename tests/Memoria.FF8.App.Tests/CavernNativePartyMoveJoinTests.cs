using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class CavernNativePartyMoveJoinTests
{
    [Fact]
    public void ActualCompletedMoveThenBodyTurnRestoresRawCoordinatesAndIndependentContinuation()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed field corpus is required.");
        DirectoryInfo? repository = new(AppContext.BaseDirectory);
        while (repository is not null && !File.Exists(Path.Combine(repository.FullName, "docs", "recreate-checkpoint.md")))
        {
            repository = repository.Parent;
        }
        string source = Path.Combine(repository!.FullName, "../fixtures/external-fixture.json");
        byte[] original = File.ReadAllBytes(source);
        var session = new GameSession(GameSession.Load(source));
        using var world = new GameWorld(session, directory);
        using var replay = new GameWorld(new GameSession(GameSession.Load(source)), directory);
        world.EnsureField();
        replay.EnsureField();
        ActorState actor = world.Events.Actors.Single(actor => actor.ObjectId == 5);
        Assert.Equal(140, world.Events.Frame);
        Assert.Equal(128, actor.Direction);
        Assert.False(actor.NativeScriptMovement!.Active);
        Assert.Equal((byte)82, actor.NativeScriptMovement.FacingDirection);
        Assert.Equal(new NativeMotionAuthority(1, 82, 0), actor.NativeMotionAuthority);
        Assert.Equal((45f, 4006f, 33), (actor.X, actor.Y, actor.WalkmeshTriangle));
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), replay.Events.Capture()));
        world.SetRenderSize(320, 240);
        replay.SetRenderSize(320, 240);
        for (int tick = 0; tick < 5; tick++)
        {
            var next = new PlayerAction(PlayerActionKind.Wait);
            Assert.Equal(world.Apply(next), replay.Apply(next));
            Assert.True(JsonElement.DeepEquals(world.Events.Capture(), replay.Events.Capture()));
            Assert.Equal(world.Render(TimeSpan.Zero), replay.Render(TimeSpan.Zero));
        }
        Assert.Equal(original, File.ReadAllBytes(source));
    }

    [Fact]
    public void HistoricalWrongSpeedFractionalCompanionIsNotUpgradedOnRestore()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed field corpus is required.");
        DirectoryInfo? repository = new(AppContext.BaseDirectory);
        while (repository is not null && !File.Exists(Path.Combine(repository.FullName, "docs", "recreate-checkpoint.md")))
        {
            repository = repository.Parent;
        }
        string source = Path.Combine(repository!.FullName, "../fixtures/external-fixture.json");
        byte[] original = File.ReadAllBytes(source);
        var session = new GameSession(GameSession.Load(source));
        using var world = new GameWorld(session, directory);
        world.EnsureField();
        world.SetRenderSize(320, 240);
        ActorState quistis = world.Events.Actors.Single(actor => actor.ObjectId == 5);
        Assert.Equal(474, world.Events.Frame);
        Assert.Equal(1000, quistis.MovementSpeed);
        Assert.Null(quistis.NativeMotionAuthority);
        Assert.Null(quistis.NativeScriptMovement);
        Assert.Throws<EventScriptException>(() => world.Apply(new PlayerAction(PlayerActionKind.Wait)));
        Assert.Equal("JOIN", world.Events.LastFault!.Opcode);
        Assert.Equal(1000, quistis.MovementSpeed);
        Assert.Null(quistis.NativeMotionAuthority);
        Assert.Null(quistis.NativeScriptMovement);
        Assert.Equal(original, File.ReadAllBytes(source));
        string oldInitialization = Path.Combine(repository.FullName, "../fixtures/external-fixture.json");
        using var old = new GameWorld(new GameSession(GameSession.Load(oldInitialization)), directory);
        old.EnsureField();
        ActorState oldCompanion = old.Events.Actors.Single(actor => actor.ObjectId == 5);
        Assert.Equal(1000, oldCompanion.MovementSpeed);
        Assert.Null(oldCompanion.NativeMotionAuthority);
        Assert.False(old.Events.CanUseNativeScriptMove!(oldCompanion));
    }

    [Fact]
    public void FreshOriginalGuardActionsFinishNativeCompanionMoveBeforeNativeJoinAndRestoreIndependently()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed field corpus is required.");
        DirectoryInfo? repository = new(AppContext.BaseDirectory);
        while (repository is not null && !File.Exists(Path.Combine(repository.FullName, "docs", "recreate-checkpoint.md")))
        {
            repository = repository.Parent;
        }
        string evidence = Path.Combine(repository!.FullName, "../fixtures");
        var session = new GameSession(GameSession.Load(Path.Combine(evidence, "player-dollet-cavern-guards-arrival-checkpoint.json")));
        using var world = new GameWorld(session, directory);
        world.EnsureField();
        Assert.All(world.Events.Actors, actor => Assert.Equal(7929, actor.MovementSpeed));
        for (int tick = 0; tick < 60; tick++)
        {
            Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
        }
        using JsonDocument navigation = JsonDocument.Parse(File.ReadAllText(Path.Combine(evidence, "player-dollet-cavern-guard-contact-proof.json")));
        JsonSerializerOptions options = new() { Converters = { new JsonStringEnumConverter() } };
        foreach (JsonElement action in navigation.RootElement.GetProperty("ordinaryGuideMoves").EnumerateArray())
        {
            Assert.True(world.Apply(action.Deserialize<PlayerAction>(options)!).Accepted);
        }
        Assert.Equal(78, world.Events.Frame);
        Assert.Equal(7929, world.Events.Actors.Single(actor => actor.ObjectId == 5).MovementSpeed);
        Assert.Equal(new NativeMotionAuthority(1, 0, 0), world.Events.Actors.Single(actor => actor.ObjectId == 5).NativeMotionAuthority);
        using JsonDocument exam = JsonDocument.Parse(File.ReadAllText(Path.Combine(evidence, "player-dollet-cavern-exam-continuation-proof.json")));
        bool sawNativeMove = false;
        bool sawActorCollision = false;
        world.Events.ResolveNativeScriptMovement = Wrap(world.Events.ResolveNativeScriptMovement!);
        Func<NativeScriptMovementQuery, NativeScriptMovementResult?> Wrap(Func<NativeScriptMovementQuery, NativeScriptMovementResult?> resolver)
        {
            return query =>
            {
                NativeScriptMovementResult? result = resolver(query);
                sawActorCollision |= result?.Proposals.Any(proposal => proposal.FanProbes.Any(probe => probe.ActorCollision)) == true;
                return result;
            };
        }
        List<GameWorld> moveRestores = [];
        HashSet<int> restoredScripts = [];
        try
        {
            foreach (JsonElement action in exam.RootElement.GetProperty("ordinaryAcceptedActions").EnumerateArray())
            {
                PlayerAction next = action.Deserialize<PlayerAction>(options)!;
                PlayerActionResult actual = world.Apply(next);
                Assert.True(actual.Accepted, $"Historical exam stimulus {next} at field{world.Events.FieldId}/frame{world.Events.Frame}, trigger{world.Events.NextTrigger}: {actual.Detail}");
                foreach (GameWorld restored in moveRestores)
                {
                    Assert.Equal(actual, restored.Apply(next));
                    Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
                    Assert.Equal(world.Render(TimeSpan.Zero), restored.Render(TimeSpan.Zero));
                }
                ActorState companion = world.Events.Actors.Single(actor => actor.ObjectId == 5);
                sawNativeMove |= companion.NativeScriptMovement is { Active: true, ScriptId: 32 };
                if (companion.NativeScriptMovement is { Active: true } current && restoredScripts.Add(current.ScriptId))
                {
                    JsonElement snapshot = world.Events.Capture();
                    var restored = new GameWorld(new GameSession(session.State with { EventState = snapshot, Variables = new Dictionary<int, int>(session.State.Variables) }), directory);
                    restored.EnsureField();
                    restored.SetRenderSize(320, 240);
                    Assert.True(JsonElement.DeepEquals(snapshot, restored.Events.Capture()));
                    moveRestores.Add(restored);
                    JsonNode malformed = JsonNode.Parse(snapshot.GetRawText())!;
                    malformed["Actors"]![5]!["Direction"] = (current.FacingDirection + 1) & 255;
                    Assert.Throws<InvalidDataException>(() => world.Events.Restore(JsonSerializer.SerializeToElement(malformed)));
                    Assert.True(JsonElement.DeepEquals(snapshot, world.Events.Capture()));
                }
            }
        }
        finally
        {
            foreach (GameWorld restored in moveRestores)
            {
                restored.Dispose();
            }
        }
        Assert.Equal(new[] { 31, 32 }, restoredScripts.Order().ToArray());
        Assert.True(sawNativeMove);
        Assert.Equal(474, world.Events.Frame);
        Assert.All(world.Events.Actors.Where(actor => actor.ObjectId is 9 or 10), actor => Assert.Equal(2669, actor.MovementSpeed));
        ActorState quistis = world.Events.Actors.Single(actor => actor.ObjectId == 5);
        NativeScriptMovementState moving = Assert.IsType<NativeScriptMovementState>(quistis.NativeScriptMovement);
        Assert.False(moving.Active);
        Assert.Equal((32, 191, 7929, 0), (moving.ScriptId, moving.Operation, moving.Speed, moving.StopDistance));
        Assert.Equal((moving.FixedX / 4096f, moving.FixedY / 4096f), (quistis.X, quistis.Y));

        Assert.Equal((-182 * 4096, 5022 * 4096, 0), (moving.FixedX, moving.FixedY, moving.FixedZ));
        Assert.Equal((ushort)2, quistis.NativeMovementCompletion542);
        Console.WriteLine($"Fresh129 native MOVE complete before474: raw({moving.FixedX},{moving.FixedY},{moving.FixedZ}), tri{moving.Triangle}, dir{moving.FacingDirection}, contactsteering{sawActorCollision}.");
        var replaySession = new GameSession(session.State with { EventState = world.Events.Capture(), Variables = new Dictionary<int, int>(session.State.Variables) });
        using var replay = new GameWorld(replaySession, directory);
        replay.EnsureField();
        var wait = new PlayerAction(PlayerActionKind.Wait);
        Assert.Equal(world.Apply(wait), replay.Apply(wait));
        JsonElement joining = world.Events.Capture();
        JsonNode state = JsonNode.Parse(joining.GetRawText())!;
        Assert.NotNull(state["PartyJoin"]);
        Assert.Equal(2696, state["PartyJoin"]!["Targets"]![0]!["Speed"]!.GetValue<int>());
        Assert.Equal(475, world.Events.Frame);
        Assert.Equal((-182f, 5032.53125f), (quistis.X, quistis.Y));
        Assert.Null(quistis.NativeScriptMovement);
        using var joinedRestore = new GameWorld(new GameSession(session.State with { EventState = joining, Variables = new Dictionary<int, int>(session.State.Variables) }), directory);
        joinedRestore.EnsureField();
        bool completedJoin = false;
        for (int tick = 0; tick < 100; tick++)
        {
            Assert.Equal(world.Apply(wait), replay.Apply(wait));
            Assert.True(JsonElement.DeepEquals(world.Events.Capture(), replay.Events.Capture()));
            Assert.Equal(world.Render(TimeSpan.Zero), replay.Render(TimeSpan.Zero));
            Assert.Equal(world.Events.Frame, joinedRestore.Events.Frame + 1);
            Assert.True(joinedRestore.Apply(wait).Accepted);
            Assert.True(JsonElement.DeepEquals(world.Events.Capture(), joinedRestore.Events.Capture()));
            if (JsonNode.Parse(world.Events.Capture().GetRawText())!["PartyJoin"] is null)
            {
                completedJoin = true;
                break;
            }
        }
        Assert.True(completedJoin);
        Assert.Null(world.Events.LastFault);
        Assert.Equal(1, world.Events.Scene.NativePartyTraceVersion);
        Assert.True(world.Events.IsUserControlEnabled);
        Console.WriteLine($"Native129 JOIN completes at{world.Events.Frame}, Quistis({quistis.X},{quistis.Y})/tri{quistis.WalkmeshTriangle}, original UCON{world.Events.IsUserControlEnabled}.");
    }
}
