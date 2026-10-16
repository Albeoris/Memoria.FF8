using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeDepartingModelPartsTests
{
    [Fact]
    public void AuthenticWorldEntryRetainsItsLastExecutedFieldSkinSampleAndRestoresTheNextWorldDraw()
    {
        string game = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(game), "Installed original world and field corpus is required.");
        DirectoryInfo? directory = new(TestResources.FixtureDirectory);
        string source = Path.Combine(directory!.FullName, "../fixtures/external-fixture.json");
        Assert.SkipUnless(File.Exists(source), "The preserved authentic World0 departure source is required; no snapshot padding or synthetic history is substituted.");
        byte[] original = File.ReadAllBytes(source);
        GameSession session = new(GameSession.Load(source));
        Assert.Equal(SceneKind.WorldMap, session.State.Scene);
        Assert.Equal(0, session.State.World!.Frame);
        using GameWorld world = new(session, game);
        world.EnsureField();
        JsonElement departure = world.Events.Capture();
        Assert.True(departure.GetProperty("TransitionRequested").GetBoolean());
        Assert.Equal(254, departure.GetProperty("Frame").GetInt64());
        Assert.Contains(world.Events.Scene.ModelParts!.Draws.Values, draw => draw.Frame == 253);
        Assert.All(world.Events.Scene.ModelParts.Draws.Values, draw => Assert.True(draw.Frame <= 253));
        Assert.Throws<NotSupportedException>(() => world.Events.GetNativeModelPartTranslations(world.Events.PlayerActor!.ObjectId));

        //Malformed negative component only: a frame254 live-field claim cannot consume frame253 samples.
        JsonObject invalid = JsonNode.Parse(departure.GetRawText())!.AsObject();
        invalid["TransitionRequested"] = false;
        JsonElement before = world.Events.Capture();
        Assert.Throws<InvalidDataException>(() => world.Events.Restore(JsonSerializer.SerializeToElement(invalid)));
        Assert.True(JsonElement.DeepEquals(before, world.Events.Capture()));

        GameSession guarded = new(session.State);
        GameState guardDeparture = guarded.State;
        PlayerAction wait = new(PlayerActionKind.Wait);
        Assert.True(world.Apply(wait).Accepted);
        NativeWorldSalaryState invalidSalary = NativeWorldSalaryState.Begin(guardDeparture).Advance(null, session.SalaryPresentation, session.GlobalSoundProcess, 1, 2);
        InvalidDataException refusal = Assert.Throws<InvalidDataException>(() => guarded.CommitWorldAction(session.State.World!, invalidSalary));
        Assert.Contains("World salary owners", refusal.Message);
        Assert.Same(guardDeparture, guarded.State);
        Assert.Equal(1, session.State.World!.Frame);
        byte[] pixels = world.Render(TimeSpan.Zero);
        Assert.NotEmpty(pixels);
        Assert.Equal(pixels, world.Render(TimeSpan.Zero));
        Assert.True(JsonElement.DeepEquals(departure.GetProperty("Scene").GetProperty("ModelParts"), world.Events.Capture().GetProperty("Scene").GetProperty("ModelParts")));
        string saved = Path.Combine(Path.GetTempPath(), "ff8-authentic-departing-models-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            session.Save(saved);
            using GameWorld restored = new(new GameSession(GameSession.Load(saved)), game);
            restored.EnsureField();
            Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
            Assert.Equal(pixels, restored.Render(TimeSpan.Zero));
            Assert.Equal(world.Apply(wait), restored.Apply(wait));
            Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
            Assert.Equal(world.Render(TimeSpan.Zero), restored.Render(TimeSpan.Zero));
        }
        finally
        {
            File.Delete(saved);
        }
        Assert.Equal(original, File.ReadAllBytes(source));
    }
}
