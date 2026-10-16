using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeCardSaveCausalTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { Converters = { new JsonStringEnumConverter() } };

    [Theory]
    [InlineData(137)]
    [InlineData(165)]
    public void Ordinary_original_card_and_save_point_scripts_complete_and_continue_from_saved_snapshots(int fieldId)
    {
        string gameDirectory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(gameDirectory), "Installed original game corpus is required.");
        string repository = Repository();
        var session = new GameSession(SnapshotFixtures.Require($"MEMORIA_FF8_CARD_SAVE_{fieldId}_SNAPSHOT", fieldId));
        using var world = new GameWorld(session, gameDirectory);
        world.EnsureField();
        int executions = 0;
        List<int> grants = [];
        world.Events.CommandExecuted += command =>
        {
            if (fieldId == 137 && command.Name == "GETCARD" && command.ObjectId == 11 && command.ScriptId == 78 || fieldId == 165 && command.Name == "SAVEENABLE" && command.ObjectId == 33 && command.ScriptId == 187)
            {
                executions++;
                grants.Add(command.Arguments[0]);
                Console.WriteLine($"Ordinary {fieldId}/{command.ObjectId}/{command.ScriptId}/{command.Operation}: {command.Name}({command.Arguments[0]}) at frame{world.Events.Frame}.");
            }
        };
        string output = Path.Combine(repository, "../fixtures");
        Directory.CreateDirectory(output);
        try
        {
            if (fieldId == 137)
            {
                IfritCausalRecreationTests.WalkDirectToInteraction(world, session, 11);
                Apply(world, new PlayerAction(PlayerActionKind.Interact, 11));
            }
            else
            {
                ActorState point = world.Events.Actors[33];
                Vector2 target = point.LineStart is Vector3 start && point.LineEnd is Vector3 end ? new Vector2((start.X + end.X) / 2, (start.Y + end.Y) / 2) : new Vector2(point.X, point.Y);
                Console.WriteLine($"Native save point33: actor({point.X},{point.Y}), line{point.LineStart}..{point.LineEnd}, player{Player(world).X},{Player(world).Y}, target{target}.");
                for (int tick = 0; tick < 1500 && executions == 0; tick++)
                {
                    if (world.Events.IsDialogueBlocking || !world.Events.IsUserControlEnabled)
                    {
                        Apply(world, Automatic(world));
                        continue;
                    }
                    ActorState player = Player(world);
                    Vector2 delta = target - new Vector2(player.X, player.Y);
                    Vector2 axes = delta.LengthSquared() < 1 ? Vector2.UnitY : Vector2.Transform(Vector2.Normalize(delta), Matrix3x2.CreateRotation(-world.Events.InputAxisDirection * MathF.Tau / 256));
                    Apply(world, new PlayerAction(PlayerActionKind.Move, Horizontal: axes.X, Vertical: axes.Y));
                }
            }
            for (int tick = 0; tick < 1000 && (executions == 0 || world.Events.NextTrigger.Kind != PlayerTriggerKind.Playable); tick++)
            {
                Apply(world, Automatic(world));
            }
            Assert.True(executions > 0, $"The ordinary route did not reach the reported original command in field{fieldId}.");
            Assert.Null(world.Events.LastFault);
            Assert.Equal(PlayerTriggerKind.Playable, world.Events.NextTrigger.Kind);
            if (fieldId == 137)
            {
                Assert.Equal(7, grants.Count);
                Assert.All(grants, card => Assert.Equal(0x81, session.CardInventory!.Cards[card]));
            }
            else
            {
                Assert.Contains(1, grants);
                Assert.Equal(1, session.WorldControls!.Menu209 & 1);
            }
            session.SetEventState(world.Events.Capture());
            GameState completed = session.State;
            GameState recreated = JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(completed))!;
            Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(completed, JsonOptions), JsonSerializer.SerializeToElement(recreated, JsonOptions)));
            using var restored = new GameWorld(new GameSession(recreated), gameDirectory);
            restored.EnsureField();
            world.SetRenderSize(320, 224);
            restored.SetRenderSize(320, 224);
            Assert.Equal(world.Render(TimeSpan.Zero), restored.Render(TimeSpan.Zero));
            PlayerAction next = new(PlayerActionKind.Wait);
            Assert.Equal(world.Apply(next), restored.Apply(next));
            Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
            new GameSession(completed).Save(Path.Combine(output, $"field{fieldId}-complete.json"));
            File.WriteAllText(Path.Combine(output, $"field{fieldId}-proof.json"), JsonSerializer.Serialize(new { fieldId, Frame = completed.EventState!.Value.GetProperty("Frame").GetInt64(), executions, grants, FullGameStateEqual = true, CpuPixelsEqual = true, NextOrdinaryWaitEqual = true }, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            if (world.Events.FieldId == session.State.FieldId)
            {
                session.SetEventState(world.Events.Capture());
            }
            session.Save(Path.Combine(output, $"field{fieldId}-boundary.json"));
            Console.WriteLine($"Ordinary field{fieldId} boundary frame{world.Events.Frame}: {world.NextTrigger}, {world.Events.LastFault}; player{Player(world).X},{Player(world).Y}.");
            throw;
        }
    }

    private static ActorState Player(GameWorld world)
    {
        return world.Events.Actors.Single(actor => actor.Active && actor.CharacterId == 0);
    }

    private static PlayerAction Automatic(GameWorld world)
    {
        DialogueState? dialogue = world.Events.CurrentDialogue;
        return dialogue?.FirstChoice is int first ? new PlayerAction(PlayerActionKind.Choose, first) : dialogue is { CanConfirm: true } ? new PlayerAction(PlayerActionKind.Confirm) : new PlayerAction(PlayerActionKind.Wait);
    }

    private static void Apply(GameWorld world, PlayerAction action)
    {
        Assert.True(world.Apply(action).Accepted, $"Ordinary action {action} refused at {world.NextTrigger}.");
    }

    private static string Repository()
    {
        DirectoryInfo? current = new(TestResources.FixtureDirectory);
        return current?.FullName ?? throw new DirectoryNotFoundException("The authentic route evidence repository is required.");
    }
}
