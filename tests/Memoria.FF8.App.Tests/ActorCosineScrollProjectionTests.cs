using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class ActorCosineScrollProjectionTests
{
    [Fact]
    public void AuthenticSquarePrefixUsesSelectedActorProjectionAndRestoresTheMovingCurve()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        DirectoryInfo? repository = new(AppContext.BaseDirectory);
        while (repository is not null && !File.Exists(Path.Combine(repository.FullName, "docs", "recreate-checkpoint.md")))
        {
            repository = repository.Parent;
        }
        string source = Path.Combine(repository!.FullName, "../fixtures/external-fixture.json");
        Assert.SkipUnless(Directory.Exists(directory) && File.Exists(source), "Authentic square journal and installed original resources are required.");
        GameState current = GameWorld.RecreateCommittedPrefix(GameSession.LoadReplaySource(source), directory);
        using GameWorld world = new(new GameSession(current), directory, persistReplayJournal: false);
        world.EnsureField();
        Assert.Equal(330, world.Field!.Id);
        NativeNormalBackgroundScrollCenter start = Assert.IsType<NativeNormalBackgroundScrollCenter>(world.Events.Scene.NormalBackgroundScrollCenter);
        Assert.Equal(((short)-80, (short)32), (start.X, start.Y));
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
        Assert.Null(world.Events.LastFault);
        NativeActorBackgroundScroll curve = Assert.IsType<NativeActorBackgroundScroll>(world.Events.Scene.ActorBackgroundScroll);
        Assert.Equal(3, curve.NativeObjectId);
        Assert.Equal(curve.ObjectId, world.Events.Scene.NativeActorObjectIds[curve.NativeActorId]);
        Assert.Equal((byte)2, curve.Mode);
        Assert.Equal((short)40, curve.Duration);
        Assert.Equal((short)1, curve.Counter);
        Assert.Equal((start.X, start.Y), (curve.PreviousX, curve.PreviousY));
        Assert.Equal((start.X, start.Y), (curve.X, curve.Y));
        Assert.Equal((curve.ProjectedTargetX!.Value, curve.ProjectedTargetY!.Value), ActorBackgroundScrollCertificate.Resolve(world.Field, world.Events.Scene, world.Events.Actors, curve.NativeActorId)!.Value);
        Assert.Equal(new Vector2(-start.X, -start.Y), ActorBackgroundScrollCertificate.Prepare(world.Field, world.Events.Scene)!.ActorOffset);
        JsonElement entry = world.Events.Capture();
        JsonNode badTarget = JsonNode.Parse(entry.GetRawText())!;
        badTarget["Scene"]!["ActorBackgroundScroll"]!["ProjectedTargetX"] = curve.ProjectedTargetX!.Value + 1;
        // At counter0 the target has zero interpolation weight. Arithmetic
        // alone cannot certify the saved callback's selected-body endpoint.
        Assert.Throws<InvalidDataException>(() => world.Events.Restore(JsonSerializer.SerializeToElement(badTarget)));
        Assert.True(JsonElement.DeepEquals(entry, world.Events.Capture()));
        using GameWorld restored = new(new GameSession(current with { EventState = world.Events.Capture() }), directory, persistReplayJournal: false);
        restored.EnsureField();
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
        Assert.Equal(world.Render(TimeSpan.Zero), restored.Render(TimeSpan.Zero));
        for (int callback = 0; callback < 3; callback++)
        {
            Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
            Assert.True(restored.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
            Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
            Assert.Equal(world.Render(TimeSpan.Zero), restored.Render(TimeSpan.Zero));
            NativeActorBackgroundScroll phase = world.Events.Scene.ActorBackgroundScroll!;
            Assert.True(NativeActorScrollPhase.IsValid(phase));
            Assert.Equal((phase.ProjectedTargetX!.Value, phase.ProjectedTargetY!.Value), ActorBackgroundScrollCertificate.Resolve(world.Field, world.Events.Scene, world.Events.Actors, phase.NativeActorId)!.Value);
        }
    }
}
