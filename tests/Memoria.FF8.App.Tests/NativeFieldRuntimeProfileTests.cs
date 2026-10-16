using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeFieldRuntimeProfileTests
{
    [Fact]
    public void SharedAllocationRuleDoesNotDependOnFieldOrScriptIdentity()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed field corpus is required.");
        using var assets = new GameAssets(directory);
        Assert.Equal(NativeFieldRuntimeProfile.FF8En, assets.NativeRuntimeProfile);
        foreach (int fieldId in new[] { 997, 998 })
        {
            FieldAssets data = assets.LoadField(fieldId == 997 ? 130 : 135);
            var session = new GameSession(new GameState { Scene = SceneKind.Field, FieldId = fieldId });
            var engine = new EventEngine(session);
            engine.LoadField(fieldId, data.Jsm, data.Messages, nativeRuntimeProfile: assets.NativeRuntimeProfile);
            Assert.All(engine.Actors, actor => Assert.Equal(7929, actor.MovementSpeed));
            JsmProgram program = JsmProgram.Read(data.Jsm);
            Assert.All(engine.Actors.Where(actor => program.Objects[actor.ObjectId].IsDoor), actor => Assert.False(actor.LineEnabled));
            Assert.NotNull(engine.Scene.NativeFieldInitialization);
            Assert.Equal(NativeFieldInitializationStage.Allocated, engine.Scene.NativeFieldInitialization.Stage);
            Assert.True(engine.Scene.NativeFieldInitialization.IsFrom(assets.NativeRuntimeProfile, fieldId, Convert.ToHexString(SHA256.HashData(data.Jsm))));
            JsonElement before = engine.Capture();
            Assert.Throws<NotSupportedException>(() => engine.LoadField(fieldId, data.Jsm, data.Messages, nativeRuntimeProfile: new NativeFieldRuntimeProfile(2, NativeFieldRuntimeProfile.FF8EnExecutableHash, 20)));
            Assert.True(JsonElement.DeepEquals(before, engine.Capture()));
        }
    }

    [Fact]
    public void ActualPreinitSaveRetainsLoaderProvenanceAndLegacyAbsenceIsNotFilled()
    {
        (string directory, string source) = Corpus();
        using var world = new GameWorld(new GameSession(GameSession.Load(source)), directory);
        world.EnsureField();
        JsonElement allocated = world.Events.Capture();
        Assert.False(world.Events.IsUserControlEnabled);
        Assert.True(allocated.GetProperty("UserControlEnabled").GetBoolean());
        Assert.Equal(NativeFieldInitializationStage.Allocated, world.Events.Scene.NativeFieldInitialization!.Stage);
        using var resumed = new GameWorld(new GameSession(GameSession.Load(source)), directory);
        resumed.EnsureField();
        resumed.Events.Restore(allocated);
        Assert.False(resumed.Events.IsUserControlEnabled);
        Assert.True(JsonElement.DeepEquals(allocated, resumed.Events.Capture()));
        JsonNode malformed = JsonNode.Parse(allocated.GetRawText())!;
        malformed["Scene"]!["NativeFieldInitialization"]!["FieldClock"] = 19;
        using (JsonDocument saved = JsonDocument.Parse(malformed.ToJsonString()))
        {
            Assert.Throws<InvalidDataException>(() => resumed.Events.Restore(saved.RootElement));
        }
        Assert.True(JsonElement.DeepEquals(allocated, resumed.Events.Capture()));
        JsonNode legacy = JsonNode.Parse(allocated.GetRawText())!;
        legacy["Scene"]!.AsObject().Remove("NativeFieldInitialization");
        foreach (JsonNode? actor in legacy["Actors"]!.AsArray())
        {
            actor!.AsObject().Remove("NativeDisplayOffset");
        }
        using (JsonDocument saved = JsonDocument.Parse(legacy.ToJsonString()))
        {
            resumed.Events.Restore(saved.RootElement);
        }
        Assert.Null(resumed.Events.Scene.NativeFieldInitialization);
    }

    private static (string Directory, string Source) Corpus()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(System.IO.Directory.Exists(directory), "Installed field corpus is required.");
        DirectoryInfo? repository = new(TestResources.FixtureDirectory);
        Assert.NotNull(repository);
        return (directory, Path.Combine(repository.FullName, "../fixtures/external-fixture.json"));
    }
}
