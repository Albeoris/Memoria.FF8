using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeSelectedCameraWrappedScrollTests
{
    [Fact]
    public void ActualTvCameraOneUsesItsOwnMatrixClampAndNonselectedLayerTwoPhase()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original field assets are required.");
        DirectoryInfo? repository = new(TestResources.FixtureDirectory);
        Assert.NotNull(repository);
        string source = Environment.GetEnvironmentVariable("MEMORIA_FF8_SELECTED_CAMERA_SCROLL_SOURCE") ?? Path.Combine(repository.FullName, "../fixtures/external-fixture.json");
        Assert.SkipUnless(File.Exists(source), "The genuine field929 post-movie diagnostic is required.");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(source));
        JsonElement events = document.RootElement.GetProperty("EventState");
        Assert.Equal(929, events.GetProperty("FieldId").GetInt32());
        Assert.Equal(1027, events.GetProperty("Frame").GetInt64());
        JsonSerializerOptions options = new() { Converters = { new JsonStringEnumConverter() } };
        SceneEventState scene = JsonSerializer.Deserialize<SceneEventState>(events.GetProperty("Scene"), options)!;
        ActorState[] actors = JsonSerializer.Deserialize<ActorState[]>(events.GetProperty("Actors"), options)!;
        using GameAssets assets = new(directory);
        FieldAssets field = assets.LoadField(929);
        Assert.Equal(1, scene.CameraIndex);
        Assert.Equal(2, field.Cameras.Count);
        Assert.NotEqual(field.GetCamera(0), field.GetCamera(1));
        Assert.Equal(Vector2.Zero, field.GetCamera(1).Pan);
        Assert.Equal(new FieldBounds(-112, 112, 160, -160), field.Information!.CameraBounds[1]);
        NativeLinearScrollQuery query = new(2, 0, 0, 308, 0, 7);
        Assert.True(NativeWrappedBackgroundScrollCertificate.CanSet(field, query, 1));
        Assert.True(NativeWrappedBackgroundScrollCertificate.CanBegin(field, query, scene));
        Assert.True(NativeWrappedBackgroundScrollCertificate.SupportsLinearPhase(field, scene));
        Assert.False(NativeWrappedBackgroundScrollCertificate.CanSet(field, query with { Layer = 1 }, 1));
        Assert.False(FieldNativeNormalBackgroundWrap.SupportsMetadata(field, 2));
        BackgroundScrollFrame frame = Assert.IsType<BackgroundScrollFrame>(NormalCameraCertificate.Prepare(field, scene, actors));
        Assert.Equal(Vector2.Zero, frame.ActorOffset);
        FieldNativeBackgroundWrapLayer moved = frame.NativeWrap!.Layers.Single(layer => layer.Layer == 2);
        Assert.Equal(((short)-308, (short)0, (short)352, (short)224), (moved.OffsetX, moved.OffsetY, moved.PeriodX, moved.PeriodY));
        Assert.True(FieldNativeNormalBackgroundWrap.Validate(field, 1, frame.ActorOffset, frame.LayerOffsets, frame.NativeWrap));
        Assert.Equal(((short)0, (short)0), DirectBackgroundScrollCertificate.ResolveNormalCenter(field, scene, actors));
        // Different clamp bounds prove slot1 does not silently reuse slot0.
        FieldInformation changed = field.Information with { CameraBounds = field.Information.CameraBounds.Select((bounds, index) => index == 0 ? new FieldBounds(-112, 112, 260, -60) : bounds).ToArray() };
        FieldNativeCameraResult slotOne = FieldNativeCameraProbe.Resolve(field.GetCamera(1), changed, 0, 0, 0, 1);
        FieldNativeCameraResult slotZero = FieldNativeCameraProbe.Resolve(field.GetCamera(0), changed, 0, 0, 0, 0);
        Assert.Equal(new FieldNativeCameraCenter(0, 0), slotOne.Center);
        Assert.Equal(new FieldNativeCameraCenter(100, 0), slotZero.Center);
        scene.BackgroundScrollPositions[1] = new BackgroundScrollPosition(0, 0, 0, 0, null, null, 0, 3, 2);
        Assert.False(NativeWrappedBackgroundScrollCertificate.CanBegin(field, query, scene));
        Assert.False(NativeWrappedBackgroundScrollCertificate.SupportsLinearPhase(field, scene));
    }
}
