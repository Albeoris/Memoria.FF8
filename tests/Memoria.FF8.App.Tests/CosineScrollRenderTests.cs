using System.Numerics;
using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class CosineScrollRenderTests
{
    [Fact]
    public void ReachedCosineCameraUsesCertifiedOffsetsAndRestoresItsEntryFrame()
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
        Assert.False(BackgroundScrollCertificate.CanSetCosinePosition(world.Field!, world.Events.Scene, 0, 0, 0, 39));
        Assert.False(BackgroundScrollCertificate.CanSetCosinePosition(world.Field!, world.Events.Scene, 0, 0, 225, 40));
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
        Assert.Null(world.Events.LastFault);
        Assert.Equal(9, world.Events.Frame);
        BackgroundScrollPosition entry = world.Events.Scene.BackgroundScrollPositions[0];
        Assert.Equal((byte)5, entry.Mode);
        Assert.Equal((short)40, entry.Duration);
        Assert.Equal((byte)0, entry.NativeActorId);
        Assert.Equal((short)224, entry.Y);
        Assert.Equal((short)1, entry.Counter);
        using var resumed = new GameWorld(new GameSession(session.State with { EventState = world.Events.Capture() }), directory);
        resumed.EnsureField();
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), resumed.Events.Capture()));
        Assert.Equal(world.Render(TimeSpan.Zero), resumed.Render(TimeSpan.Zero));
        // Renderer-only phase sweep; this does not advance the original VM or player route.
        for (short elapsed = 0; elapsed <= 40; elapsed++)
        {
            BackgroundScrollPosition position = entry with { Y = (short)NativeCosineInterpolation.Interpolate(224, 0, 40, elapsed), Counter = elapsed == 40 ? elapsed : (short)(elapsed + 1), Status = elapsed == 40 ? (byte)2 : (byte)1 };
            world.Events.Scene.BackgroundScrollPositions[0] = position;
            resumed.Events.Scene.BackgroundScrollPositions[0] = position;
            BackgroundScrollFrame scroll = BackgroundScrollCertificate.Prepare(world.Field!, world.Events.Scene);
            Assert.Equal(new Vector2(0, -position.Y), scroll.ActorOffset);
            Assert.Equal(Vector2.Zero, scroll.LayerOffsets.GetValueOrDefault(1));
            Assert.Equal(world.Render(TimeSpan.Zero), resumed.Render(TimeSpan.Zero));
        }
        Assert.Equal(9, world.Events.Frame);
        Assert.Empty(BackgroundScrollCertificate.Prepare(world.Field!, world.Events.Scene).LayerOffsets);
    }
}
