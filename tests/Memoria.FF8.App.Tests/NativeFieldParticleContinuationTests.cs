using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeFieldParticleContinuationTests
{
    [Fact]
    public void Original_launch_commands_draw_particles_and_restore_the_current_frame_and_next_callbacks()
    {
        string game = TestResources.GameDirectory;
        string source = TestResources.FixturePath("App.Tests.NativeFieldParticleContinuationTests.1");
        byte[] original = File.ReadAllBytes(source);
        GameSession session = new(GameSession.Load(source));
        using GameWorld world = new(session, game, validateWorldPresentation: false);
        world.EnsureField();
        world.SetRenderSize(320, 224);
        Assert.NotNull(world.Events.Scene.NativeFieldParticles);
        Assert.True(world.Apply(new(PlayerActionKind.Confirm)).Accepted);
        for (int tick = 0; tick < 500 && world.Events.FieldParticleSprites.Count < 20; tick++)
        {
            Assert.True(world.Apply(world.NextTrigger.Kind == PlayerTriggerKind.Dialogue ? new(PlayerActionKind.Confirm) : new(PlayerActionKind.Wait)).Accepted);
            Assert.Null(world.Events.LastFault);
        }
        Assert.True(world.Events.FieldParticleSprites.Count >= 20);
        Assert.All(Enumerable.Range(0, 6), slot => Assert.Equal(1, world.Events.Scene.NativeParticleSlots[slot]));
        JsonElement captured = world.Events.Capture();
        JsonNode malformed = JsonNode.Parse(captured.GetRawText())!;
        malformed["Scene"]!["NativeFieldParticleSprites"]![0]!["U"] = 255;
        Assert.Throws<InvalidDataException>(() => world.Events.Restore(JsonSerializer.SerializeToElement(malformed)));
        Assert.True(JsonElement.DeepEquals(captured, world.Events.Capture()));
        session.SetEventState(captured);
        string path = Path.Combine(TestResources.ArtifactDirectory, "particle-continuation.ff8");
        session.Save(path, compact: true);
        using GameWorld resumed = new(new GameSession(GameSession.Load(path)), game, validateWorldPresentation: false);
        resumed.EnsureField();
        resumed.SetRenderSize(320, 224);
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), resumed.Events.Capture()));
        Assert.Equal(world.Render(TimeSpan.Zero), resumed.Render(TimeSpan.Zero));
        for (int tick = 0; tick < 12; tick++)
        {
            Assert.Equal(world.Apply(new(PlayerActionKind.Wait)), resumed.Apply(new(PlayerActionKind.Wait)));
            Assert.True(JsonElement.DeepEquals(world.Events.Capture(), resumed.Events.Capture()));
            Assert.Equal(world.Render(TimeSpan.Zero), resumed.Render(TimeSpan.Zero));
        }
        Assert.Equal(original, File.ReadAllBytes(source));
    }

    [Fact]
    public void Historical_missing_particle_pool_requires_explicit_transient_restart_before_any_callback_mutation()
    {
        string game = TestResources.GameDirectory;
        string source = TestResources.FixturePath("App.Tests.NativeFieldParticleContinuationTests.2");
        using GameWorld world = new(new GameSession(GameSession.Load(source)), game, validateWorldPresentation: false);
        world.EnsureField();
        JsonElement before = world.Events.Capture();
        Assert.Throws<NotSupportedException>(() => world.Apply(new(PlayerActionKind.Wait)));
        Assert.True(JsonElement.DeepEquals(before, world.Events.Capture()));
        world.Events.RestartNativeFieldParticlesForMigration(0);
        Assert.True(world.Apply(new(PlayerActionKind.Wait)).Accepted);
        Assert.NotNull(world.Events.Scene.NativeFieldParticles);
    }
}
