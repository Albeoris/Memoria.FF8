using System.Numerics;
using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class OffsetParallaxTests
{
    [Theory]
    [InlineData(7, 9, 64, 8)]
    [InlineData(-7, 9, 64, 10)]
    [InlineData(440, 440, 0, 440)]
    [InlineData(-440, -440, 0, -440)]
    public void SharedOffsetFormulaKeepsSignedNativeDivisionAndAddsTheScriptOffset(int center, short offset, short scale, int expected)
    {
        Assert.True(BackgroundParallaxPlacement.TryOffset(center, 0, new BackgroundScrollSettings(offset, -3, scale, 0), out Vector2 result));
        Assert.Equal(new Vector2(expected, -3), result);
    }

    [Fact]
    public void NativeRawWordAndEighthPackingRefuseUnsupportedWrapInsteadOfRounding()
    {
        Assert.True(BackgroundParallaxPlacement.TryOffset(0, 0, new BackgroundScrollSettings(4095, -4096, 0, 0), out Vector2 offset));
        Assert.Equal(new Vector2(4095, -4096), offset);
        Assert.False(BackgroundParallaxPlacement.TryOffset(1, 0, new BackgroundScrollSettings(4095, 0, 0, 0), out _));
        Assert.False(BackgroundParallaxPlacement.TryOffset(0, -1, new BackgroundScrollSettings(0, -4096, 0, 0), out _));
        Assert.False(BackgroundParallaxPlacement.TryOffset(0, 0, new BackgroundScrollSettings(short.MaxValue, 0, 0, 0), out _));
    }

    [Fact]
    public void AuthenticMountainRoadPrefixPublishesOriginalOffsetAndRestoresItsPixelsAndCallbacks()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        DirectoryInfo? repository = new(AppContext.BaseDirectory);
        while (repository is not null && !File.Exists(Path.Combine(repository.FullName, "docs", "recreate-checkpoint.md")))
        {
            repository = repository.Parent;
        }
        string source = Path.Combine(repository!.FullName, "../fixtures/external-fixture.json");
        Assert.SkipUnless(Directory.Exists(directory) && File.Exists(source), "Authentic mountain-road journal and installed resources are required.");
        GameState current = GameWorld.RecreateCommittedPrefix(GameSession.Load(source), directory);
        using GameWorld world = new(new GameSession(current), directory, persistReplayJournal: false);
        world.EnsureField();
        Assert.Equal(321, world.Field!.Id);
        BackgroundScrollSettings settings = new(440, 0, 0, 0);
        Assert.True(BackgroundScrollCertificate.CanSetForState(world.Field, world.Events.Scene, world.Events.Actors, 1, settings));
        Assert.False(BackgroundScrollCertificate.CanSetForState(world.Field, world.Events.Scene, world.Events.Actors, 1, settings with { OffsetX = short.MaxValue }));
        BackgroundScrollFrame before = NormalCameraCertificate.Prepare(world.Field, world.Events.Scene, world.Events.Actors)!;
        Assert.Equal(new Vector2(-440, 0), before.ActorOffset);
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
        Assert.Null(world.Events.LastFault);
        Assert.Equal(settings, world.Events.Scene.BackgroundScrollLayers[1]);
        BackgroundScrollFrame after = NormalCameraCertificate.Prepare(world.Field, world.Events.Scene, world.Events.Actors)!;
        Assert.Equal(new Vector2(-440, 0), after.ActorOffset);
        Assert.Equal(new Vector2(440, 0), after.LayerOffsets[1]);
        Assert.Equal(new Vector2(440, 0), DirectBackgroundScrollCertificate.PrepareCenter(world.Field, world.Events.Scene, 440, 0).LayerOffsets[1]);
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
            Assert.Equal(new Vector2(440, 0), NormalCameraCertificate.Prepare(world.Field, world.Events.Scene, world.Events.Actors)!.LayerOffsets[1]);
        }
    }
}
