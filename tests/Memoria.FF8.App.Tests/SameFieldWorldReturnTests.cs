using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class SameFieldWorldReturnTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { Converters = { new JsonStringEnumConverter() } };

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Cavern_exit_and_immediate_world_return_allocate_a_fresh_field_and_recreate_the_visit(bool afterIfrit)
    {
        string gameDirectory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(gameDirectory), "Installed original field and world corpus is required.");
        string repository = Repository();
        GameState source = GameSession.LoadReplaySource(Path.Combine(repository, "../fixtures/external-fixture.json"));
        if (!afterIfrit)
        {
            var root = new GameSession(new GameState());
            root.StartNewGame();
            ReplayJournal prefix = root.ReplayJournal!;
            foreach (ReplayJournalEntry entry in source.ReplayJournal!.Entries)
            {
                prefix = prefix.Append(entry);
                if (entry.ResultFieldId == 136 && entry.ResultTrigger == (int)PlayerTriggerKind.Playable)
                {
                    break;
                }
            }
            ReplayJournalEntry endpoint = prefix.Endpoint!;
            Assert.Equal(136, endpoint.ResultFieldId);
            source = new GameState { Scene = endpoint.ResultScene!.Value, FieldId = endpoint.ResultFieldId!.Value, ReplayJournal = prefix };
        }
        var session = new GameSession(GameWorld.RecreateSave(source, gameDirectory));
        using var world = new GameWorld(session, gameDirectory, persistReplayJournal: false);
        world.EnsureField();
        if (afterIfrit)
        {
            IfritCausalRecreationTests.WalkGateway(world, session, 136);
        }
        else
        {
            Vector2 inward = Vector2.Transform(Vector2.UnitY, Matrix3x2.CreateRotation(-world.Events.InputAxisDirection * MathF.Tau / 256));
            Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: inward.X, Vertical: inward.Y, Frames: 15)).Accepted);
        }
        IfritCausalRecreationTests.WalkGateway(world, session, 0, worldExit: true);
        Assert.Equal(SceneKind.WorldMap, session.State.Scene);
        Assert.Equal(136, world.Field!.Id);
        JsonElement frozenVm = world.Events.Capture();
        long departedFrame = world.Events.Frame;
        Assert.True(departedFrame > 0);

        for (int tick = 0; tick < 120 && session.State.Scene == SceneKind.WorldMap; tick++)
        {
            PlayerActionResult result = world.Apply(new PlayerAction(PlayerActionKind.Move, Vertical: -1));
            Assert.True(result.Accepted);
        }
        Assert.Equal(SceneKind.Field, session.State.Scene);
        Assert.Equal(136, session.State.FieldId);
        FieldTransition arrival = Assert.IsType<FieldTransition>(session.State.FieldArrival);
        Assert.True(world.EnsureField(), "Returning through the world loader must allocate a new VM even when the destination field ID equals the departing field ID.");
        Assert.Equal(0, world.Events.Frame);
        Assert.NotEqual(frozenVm.GetRawText(), world.Events.Capture().GetRawText());
        Assert.Null(session.State.DepartingFieldState);
        Assert.False(world.EnsureField());

        for (int tick = 0; tick < 120 && world.Events.NextTrigger.Kind != PlayerTriggerKind.Playable; tick++)
        {
            Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
        }
        Assert.Equal(PlayerTriggerKind.Playable, world.Events.NextTrigger.Kind);
        Assert.Null(world.Events.LastFault);
        ActorState player = world.Events.Actors.Single(actor => actor.Active && actor.CharacterId == 0);
        Assert.Equal((arrival.X, arrival.Y, arrival.Triangle), (player.X, player.Y, player.WalkmeshTriangle));
        Vector3 beforeMove = new(player.X, player.Y, player.Z);
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Move, Vertical: 1)).Accepted);
        Assert.NotEqual(beforeMove, new Vector3(player.X, player.Y, player.Z));

        session.SetEventState(world.Events.Capture());
        GameState reached = session.State;
        Assert.True(reached.ReplayJournal!.IsComplete, reached.ReplayJournal.UnavailableReason);
        GameState recreated = GameWorld.RecreateSave(reached, gameDirectory);
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(reached with { ReplayJournal = null }, JsonOptions), JsonSerializer.SerializeToElement(recreated with { ReplayJournal = null }, JsonOptions)));
        using var restored = new GameWorld(new GameSession(recreated), gameDirectory, persistReplayJournal: false);
        restored.EnsureField();
        world.SetRenderSize(320, 224);
        restored.SetRenderSize(320, 224);
        Assert.Equal(world.Render(TimeSpan.Zero), restored.Render(TimeSpan.Zero));
        PlayerAction next = new(PlayerActionKind.Wait);
        Assert.Equal(world.Apply(next), restored.Apply(next));
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
        Assert.Equal(world.Render(TimeSpan.Zero), restored.Render(TimeSpan.Zero));
        string output = Path.Combine(repository, "../fixtures");
        Directory.CreateDirectory(output);
        new GameSession(reached).Save(Path.Combine(output, afterIfrit ? "reentry-after-ifrit-checkpoint.json" : "reentry-before-ifrit-checkpoint.json"), registerJournalReference: false);
        Console.WriteLine($"Authentic cavern reentry field136: previous frame{departedFrame}, allocated frame0, arrival{arrival}; movement, portable reconstruction, VM and CPU pixels verified.");
    }

    private static string Repository()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "docs", "recreate-checkpoint.md")))
        {
            current = current.Parent;
        }
        return current?.FullName ?? throw new DirectoryNotFoundException("The original route evidence repository is required.");
    }
}
