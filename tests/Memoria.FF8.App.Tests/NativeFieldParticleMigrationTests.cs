using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeFieldParticleMigrationTests
{
    [Fact]
    public void PendingHistoricalRestartMatchesExplicitResourceBackedRestart()
    {
        GameState original = GameSession.Load(TestResources.FixturePath("App.Tests.NativeFieldParticleContinuationTests.1"));
        JsonNode checkpoint = JsonNode.Parse(original.EventState!.Value.GetRawText())!;
        JsonObject scene = checkpoint["Scene"]!.AsObject();
        scene.Remove("NativeFieldParticles");
        scene.Remove("NativeFieldParticleRandomPhase");
        scene.Remove("NativeFieldParticleSprites");
        GameState historical = original with { EventState = JsonSerializer.SerializeToElement(checkpoint) };
        using GameWorld explicitRestart = new(new GameSession(historical), TestResources.GameDirectory, validateWorldPresentation: false);
        explicitRestart.EnsureField();
        explicitRestart.Events.RestartNativeFieldParticlesForMigration(0);

        scene["PendingNativeFieldParticleMigrationVersion"] = 1;
        using GameWorld migrated = new(new GameSession(original with { EventState = JsonSerializer.SerializeToElement(checkpoint) }), TestResources.GameDirectory, validateWorldPresentation: false);
        migrated.EnsureField();
        Assert.Null(migrated.Events.Scene.PendingNativeFieldParticleMigrationVersion);
        Assert.True(JsonElement.DeepEquals(explicitRestart.Events.Capture(), migrated.Events.Capture()));
        JsonElement completed = migrated.Events.Capture();
        migrated.Events.CompleteNativeFieldParticleMigration();
        Assert.True(JsonElement.DeepEquals(completed, migrated.Events.Capture()));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void PendingRestartDoesNotOverwriteExistingPresentationOrUnknownVersions(int version)
    {
        using GameWorld world = new(new GameSession(GameSession.Load(TestResources.FixturePath("App.Tests.NativeFieldParticleContinuationTests.1"))), TestResources.GameDirectory, validateWorldPresentation: false);
        world.EnsureField();
        JsonNode checkpoint = JsonNode.Parse(world.Events.Capture().GetRawText())!;
        checkpoint["Scene"]!["PendingNativeFieldParticleMigrationVersion"] = version;
        world.Events.Restore(JsonSerializer.SerializeToElement(checkpoint));
        JsonElement before = world.Events.Capture();
        Assert.Throws<InvalidDataException>(() => world.Events.CompleteNativeFieldParticleMigration());
        Assert.True(JsonElement.DeepEquals(before, world.Events.Capture()));
    }
}
