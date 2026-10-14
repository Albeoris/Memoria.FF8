using System.Numerics;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class BackgroundScrollParallaxTests
{
    [Fact]
    public void OriginalLibraryBaselineAndThreeParallaxRecordsUseNativeClampedCenter()
    {
        using var assets = OpenAssets();
        FieldAssets field = assets.LoadField(230);
        SceneEventState scene = Scene();
        ActorState[] actors = [Actor(-1351, 0, -248)];
        Assert.Equal(0, field.Information!.BackgroundWrapMask);
        Assert.Equal(new FieldBounds(-112, 112, 320, -320), field.Information.CameraBounds[0]);
        BackgroundScrollFrame baseline = NormalCameraCertificate.Prepare(field, scene, actors)!;
        Assert.Equal(new Vector2(160, 0), baseline.ActorOffset);
        foreach (int layer in new[] { 0, 1, 2, 3 })
        {
            Assert.Equal(baseline.ActorOffset, baseline.LayerOffsets[layer]);
        }
        foreach ((int layer, short scale) in new[] { (1, (short)64), (2, (short)48), (3, (short)32) })
        {
            var settings = new BackgroundScrollSettings(0, 0, scale, 0);
            Assert.True(BackgroundScrollCertificate.CanSet(field, layer, settings, 0));
            Assert.True(BackgroundScrollCertificate.Supports(field, scene, actors));
            scene.BackgroundScrollLayers[layer] = settings;
        }
        BackgroundScrollFrame parallax = NormalCameraCertificate.Prepare(field, scene, actors)!;
        Assert.Equal(new Vector2(160, 0), parallax.ActorOffset);
        Assert.Equal(new Vector2(160, 0), parallax.LayerOffsets[0]);
        Assert.Equal(new Vector2(40, 0), parallax.LayerOffsets[1]);
        Assert.Equal(new Vector2(30, 0), parallax.LayerOffsets[2]);
        Assert.Equal(new Vector2(20, 0), parallax.LayerOffsets[3]);
        Assert.Throws<NotSupportedException>(() => BackgroundScrollCertificate.Prepare(field, scene));
    }

    [Theory]
    [InlineData(-7, 1)]
    [InlineData(7, -1)]
    public void ParallaxUsesSignedTruncationInsteadOfFloorOrFloatingDivision(int x, int expected)
    {
        using var assets = OpenAssets();
        FieldAssets field = assets.LoadField(230);
        var camera = new FieldCamera(Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, new Vector3(0, 0, 1000), Vector2.Zero, 1000);
        field = field with { Camera = camera, Cameras = [camera], Information = field.Information! with { CameraHeight = 0 } };
        SceneEventState scene = Scene();
        scene.BackgroundScrollLayers[1] = new BackgroundScrollSettings(0, 0, 64, 0);
        BackgroundScrollFrame frame = NormalCameraCertificate.Prepare(field, scene, [Actor(x, 0, 0)])!;
        Assert.Equal(new Vector2(-x, 0), frame.ActorOffset);
        Assert.Equal(new Vector2(expected, 0), frame.LayerOffsets[1]);
    }

    [Fact]
    public void UnknownWrapOffsetsScalesLayersBindingsAndDirectPhasesRemainRefused()
    {
        using var assets = OpenAssets();
        FieldAssets field = assets.LoadField(230);
        SceneEventState scene = Scene();
        ActorState[] actors = [Actor(-1351, 0, -248)];
        var settings = new BackgroundScrollSettings(0, 0, 64, 0);
        Assert.False(BackgroundScrollCertificate.CanSet(field, 0, settings, 0));
        Assert.False(BackgroundScrollCertificate.CanSet(field, 4, settings, 0));
        Assert.False(BackgroundScrollCertificate.CanSet(field, 1, settings, 1));
        Assert.False(BackgroundScrollCertificate.CanSet(field, 1, settings with { OffsetX = short.MaxValue }, 0));
        Assert.False(BackgroundScrollCertificate.CanSet(field, 1, settings with { ScaleX = -1 }, 0));
        Assert.False(BackgroundScrollCertificate.CanSet(field, 1, settings with { ScaleX = 257 }, 0));
        FieldAssets wrapping = field with { Information = field.Information! with { BackgroundWrapMask = 2 } };
        Assert.False(BackgroundScrollCertificate.CanSet(wrapping, 1, settings, 0));
        Assert.False(BackgroundScrollCertificate.Supports(wrapping, scene, actors));
        scene.BackgroundScrollLayers[1] = settings with { OffsetX = short.MaxValue };
        Assert.False(BackgroundScrollCertificate.Supports(field, scene, actors));
        scene.BackgroundScrollLayers.Clear();
        scene.FieldPartyNativeActorIds[0] = 1;
        Assert.False(BackgroundScrollCertificate.Supports(field, scene, actors));
        scene.FieldPartyNativeActorIds[0] = 0;
        scene.BackgroundScrollPositions[0] = new BackgroundScrollPosition(0, 0, 0, 0, 0, 0, 0, 3, 2);
        Assert.False(BackgroundScrollCertificate.Supports(field, scene, actors));
    }

    [Fact]
    public void SparseRuntimeFollowerSlotsKeepTheSameNativeLeaderCamera()
    {
        using var assets = OpenAssets();
        FieldAssets field = assets.LoadField(230);
        SceneEventState scene = Scene();
        ActorState[] actors = [Actor(-1351, 0, -248)];
        BackgroundScrollFrame before = NormalCameraCertificate.Prepare(field, scene, actors)!;
        scene.NativeActorObjectIds.AddRange([1, 2]);
        scene.Party[2] = 5;
        scene.FieldPartyNativeActorIds[2] = 2;
        BackgroundScrollFrame sparse = NormalCameraCertificate.Prepare(field, scene, actors)!;
        Assert.Equal(before.ActorOffset, sparse.ActorOffset);
        Assert.Equal(before.LayerOffsets, sparse.LayerOffsets);
        scene.FieldPartyNativeActorIds[1] = 2;
        Assert.Throws<NotSupportedException>(() => NormalCameraCertificate.Prepare(field, scene, actors));
    }

    private static SceneEventState Scene()
    {
        // Isolated projection fixture; it is neither restored by an engine nor published as a causal save.
        SceneEventState scene = JsonSerializer.Deserialize<SceneEventState>("{\"AudioLoadEpoch\":0,\"Party\":[0,255,255],\"NativeFieldLoadVersion\":1,\"NativeActorLayoutVersion\":1}")!;
        scene.NativeActorObjectIds.Add(0);
        scene.FieldPartyNativeActorIds.AddRange([0, 255, 255]);
        return scene;
    }

    private static ActorState Actor(int x, int y, int z)
    {
        return JsonSerializer.Deserialize<ActorState>($"{{\"ObjectId\":0,\"Label\":0,\"CharacterId\":0,\"ModelId\":0,\"X\":{x},\"Y\":{y},\"Z\":{z}}}")!;
    }

    private static GameAssets OpenAssets()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed field corpus is required.");
        return new GameAssets(directory);
    }
}
