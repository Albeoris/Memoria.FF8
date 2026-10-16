using System.Numerics;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class TowerParallaxContinuationTests
{
    [Fact]
    public void OriginalTowerCameraMetadataUsesItsActualSharedPanAndSelectedNormalCenter()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed tower resources are required.");
        using GameAssets assets = new(directory);
        FieldAssets field = assets.LoadField(325);
        Assert.True(NormalCameraCertificate.HasBaseline(field, 0), $"Tower metadata: cameras={field.Cameras.Count}, pan={field.Camera.Pan}, layers={string.Join(',', field.Background.Select(tile => tile.Layer).Distinct())}, wrap={field.Information!.BackgroundWrapMask}");
        Assert.Equal(new Vector2(0, 64), field.Camera.Pan);
        SceneEventState scene = JsonSerializer.Deserialize<SceneEventState>("{\"NativeFieldLoadVersion\":1,\"CameraIndex\":0}")!;
        scene.BackgroundScrollLayers[1] = new BackgroundScrollSettings(32, -48, 128, 128);
        BackgroundScrollFrame positive = DirectBackgroundScrollCertificate.PrepareCenter(field, scene, 7, -7);
        Assert.Equal(new Vector2(-7, 7), positive.ActorOffset);
        Assert.Equal(new Vector2(-7, 7), positive.LayerOffsets[0]);
        Assert.Equal(new Vector2(29, 19), positive.LayerOffsets[1]);
        Assert.False(NormalCameraCertificate.HasBaseline(field with { Camera = field.Camera with { Pan = new Vector2(0, 0.5f) } }, 0));
        Assert.False(NormalCameraCertificate.HasBaseline(field with { Camera = field.Camera with { Pan = new Vector2(0, 64) }, Information = field.Information! with { BackgroundWrapMask = 2 } }, 0));
        Assert.False(BackgroundParallaxPlacement.SupportsDrawingOrigin(0, -848, new Vector2(0, 64)));
        Assert.True(BackgroundParallaxPlacement.SupportsDrawingOrigin(0, -847, new Vector2(0, 64)));
    }

    [Fact]
    public void AuthenticTowerPrefixUsesPanSplitAndRestoresTheReachedParallaxPixelsAndCallbacks()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        DirectoryInfo? repository = new(TestResources.FixtureDirectory);
        string source = Path.Combine(repository!.FullName, "../fixtures/external-fixture.json");
        Assert.SkipUnless(Directory.Exists(directory) && File.Exists(source), "Authentic tower snapshot and installed resources are required.");
        GameState current = GameSession.Load(source);
        using GameWorld world = new(new GameSession(current), directory);
        world.EnsureField();
        Assert.Equal(325, world.Field!.Id);
        Assert.Equal(new Vector2(0, 64), world.Field.Camera.Pan);
        BackgroundScrollSettings settings = new(32, -48, 128, 128);
        Assert.True(BackgroundScrollCertificate.CanSetForState(world.Field, world.Events.Scene, world.Events.Actors, 1, settings));
        BackgroundScrollFrame before = NormalCameraCertificate.Prepare(world.Field, world.Events.Scene, world.Events.Actors)!;
        Assert.Equal(before.ActorOffset, before.LayerOffsets[0]);
        Assert.Equal(before.ActorOffset + world.Field.Camera.Pan, before.LayerOffsets[1]);
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
        Assert.Null(world.Events.LastFault);
        Assert.Equal(settings, world.Events.Scene.BackgroundScrollLayers[1]);
        BackgroundScrollFrame after = NormalCameraCertificate.Prepare(world.Field, world.Events.Scene, world.Events.Actors)!;
        int x = (int)-after.ActorOffset.X;
        int y = (int)-after.ActorOffset.Y;
        Assert.Equal(after.ActorOffset, after.LayerOffsets[0]);
        Assert.Equal(new Vector2(32 - x * 128 / 256, 64 - 48 - y * 128 / 256), after.LayerOffsets[1]);
        Assert.Equal(after.LayerOffsets[1], DirectBackgroundScrollCertificate.PrepareCenter(world.Field, world.Events.Scene, (short)x, (short)y).LayerOffsets[1]);
        using GameWorld restored = new(new GameSession(current with { EventState = world.Events.Capture() }), directory);
        restored.EnsureField();
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
        Assert.Equal(world.Render(TimeSpan.Zero), restored.Render(TimeSpan.Zero));
        for (int callback = 0; callback < 3; callback++)
        {
            Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
            Assert.True(restored.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
            Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
            Assert.Equal(world.Render(TimeSpan.Zero), restored.Render(TimeSpan.Zero));
        }
    }
}
