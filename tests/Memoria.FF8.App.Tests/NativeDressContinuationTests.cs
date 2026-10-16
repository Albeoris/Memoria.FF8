using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeDressContinuationTests
{
    [Fact]
    public void Actual_uniform_choice_reloads_original_same_field_model_and_preserves_pending_save_replay_and_pixels()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original field corpus is required.");
        string source = TestResources.FixturePath("App.Tests.NativeDressContinuationTests.1");
        var session = new GameSession(GameSession.Load(source));
        using var world = new GameWorld(session, directory);
        world.EnsureField();
        Assert.Equal(240, world.Events.FieldId);
        Assert.Equal(PlayerTriggerKind.Dialogue, world.NextTrigger.Kind);
        ActorState normal = Assert.Single(world.Events.Actors, actor => actor.Active && actor.CharacterId == 0);
        Assert.Equal(0, normal.ModelId);
        Assert.StartsWith("d001", world.Field!.Models[normal.ModelId].Name);
        Assert.Equal(478, world.Field.Models[normal.ModelId].Vertices.Count);
        NativeCharacterAppearance original = session.CharacterAppearance!;
        Assert.Equal(0, original.Records[0].Dress);
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Choose, 2)).Accepted);
        for (int frame = 0; frame < 300 && world.NextTrigger.Kind != PlayerTriggerKind.FieldTransition; frame++)
        {
            world.Apply(new PlayerAction(PlayerActionKind.Wait));
            Assert.Null(world.Events.LastFault);
        }
        Assert.Equal(PlayerTriggerKind.FieldTransition, world.NextTrigger.Kind);
        Assert.Equal(240, session.State.FieldId);
        Assert.Equal(34, session.State.Variables[256]);
        Assert.Equal(1, session.State.Variables[720]);
        Assert.Equal(1, session.CharacterAppearance!.Records[0].Dress);
        Assert.Equal(original.Records.Skip(1), session.CharacterAppearance.Records.Skip(1));
        Assert.Equal(-882, session.State.X);
        Assert.Equal(206, session.State.Y);
        Assert.Equal(132, session.State.FieldArrival!.Triangle);
        Assert.Equal(120, session.State.FieldArrival.Direction);
        Assert.Null(session.State.EventState);
        Assert.True(session.State.DepartingFieldState!.Value.GetProperty("TransitionRequested").GetBoolean());
        AssertSameState(session.State, JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State))!);

        string path = Path.Combine(Path.GetTempPath(), "Memoria.FF8-dress-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            session.Save(path);
            var restoredSession = new GameSession(GameSession.Load(path));
            AssertSameState(session.State, restoredSession.State);
            JsonObject mismatched = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
            mismatched["CharacterAppearance"]!["Records"]![0]!["Dress"] = 2;
            File.WriteAllText(path, mismatched.ToJsonString());
            Assert.Throws<InvalidDataException>(() => GameSession.Load(path));
            mismatched["CharacterAppearance"]!["Records"]![0]!["Dress"] = 1;
            mismatched["DepartingFieldState"]!.AsObject().Remove("CharacterAppearance");
            File.WriteAllText(path, mismatched.ToJsonString());
            Assert.Throws<InvalidDataException>(() => GameSession.Load(path));

            using var restored = new GameWorld(restoredSession, directory);
            Assert.True(world.EnsureField());
            Assert.True(restored.EnsureField());
            Assert.Equal(0, world.Events.Frame);
            Assert.Equal(0, restored.Events.Frame);
            Assert.Null(session.State.DepartingFieldState);
            Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
            for (int frame = 0; frame < 30 && world.NextTrigger.Kind != PlayerTriggerKind.Playable; frame++)
            {
                var wait = new PlayerAction(PlayerActionKind.Wait);
                Assert.Equal(world.Apply(wait), restored.Apply(wait));
                Assert.Null(world.Events.LastFault);
            }
            Assert.Equal(PlayerTriggerKind.Playable, world.NextTrigger.Kind);
            ActorState uniform = Assert.Single(world.Events.Actors, actor => actor.Active && actor.CharacterId == 0);
            Assert.Equal(1, uniform.ObjectId);
            Assert.Equal(1, uniform.ModelId);
            Assert.StartsWith("d003", world.Field.Models[uniform.ModelId].Name);
            Assert.Equal(471, world.Field.Models[uniform.ModelId].Vertices.Count);
            world.SetRenderSize(320, 224);
            restored.SetRenderSize(320, 224);
            Assert.Equal(world.Render(TimeSpan.Zero), restored.Render(TimeSpan.Zero));
            session.SetEventState(world.Events.Capture());
            restoredSession.SetEventState(restored.Events.Capture());
            AssertSameState(session.State, restoredSession.State);
            GameState recreated = JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State))!;
            AssertSameState(session.State, recreated);
            using var replayed = new GameWorld(new GameSession(recreated), directory);
            replayed.EnsureField();
            replayed.SetRenderSize(320, 224);
            Assert.Equal(world.Render(TimeSpan.Zero), replayed.Render(TimeSpan.Zero));
            var next = new PlayerAction(PlayerActionKind.Wait);
            Assert.Equal(world.Apply(next), replayed.Apply(next));
            Assert.True(JsonElement.DeepEquals(world.Events.Capture(), replayed.Events.Capture()));
            Assert.Equal(world.Render(TimeSpan.Zero), replayed.Render(TimeSpan.Zero));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static void AssertSameState(GameState first, GameState second)
    {
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(first), JsonSerializer.SerializeToElement(second)));
    }
}
