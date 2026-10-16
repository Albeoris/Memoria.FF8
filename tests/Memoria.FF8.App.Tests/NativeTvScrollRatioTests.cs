using System.Collections.Immutable;
using System.Numerics;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeTvScrollRatioTests
{
    [Fact]
    public void SelectedCameraZeroWrapIgnoresUnselectedCameraRecordsButRejectsUnknownSelection()
    {
        using GameAssets assets = InstalledAssets();
        FieldAssets field = assets.LoadField(929);
        Assert.Equal(2, field.Cameras.Count);
        Assert.True(FieldNativeNormalBackgroundWrap.SupportsMetadata(field, 0));
        Assert.True(FieldNativeNormalBackgroundWrap.SupportsLayerCurrentMetadata(field, 0));
        Assert.False(FieldNativeNormalBackgroundWrap.SupportsMetadata(field, 1));
        Assert.False(FieldNativeNormalBackgroundWrap.SupportsMetadata(field, -1));
        Assert.False(FieldNativeNormalBackgroundWrap.SupportsMetadata(field, 2));
        Assert.False(FieldNativeNormalBackgroundWrap.SupportsMetadata(field with { Cameras = [] }, 0));
        Assert.False(FieldNativeNormalBackgroundWrap.SupportsMetadata(field with { Camera = field.Cameras[1] }, 0));
        ImmutableArray<FieldNativeBackgroundLayerCurrent> currents = ImmutableArray.Create(new FieldNativeBackgroundLayerCurrent(2, 0, 0, 0, 0, 0, 0));
        FieldNativeBackgroundWrapFrame original = FieldNativeNormalBackgroundWrap.Prepare(field, 0, new FieldNativeCameraCenter(0, 0), currents)!;
        Assert.NotNull(original);
        FieldAssets single = field with { Cameras = [field.Cameras[0]] };
        FieldAssets differentUnused = field with { Cameras = [field.Cameras[0], field.Cameras[1] with { Pan = new Vector2(short.MaxValue, short.MinValue) }] };
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(original), JsonSerializer.SerializeToElement(FieldNativeNormalBackgroundWrap.Prepare(single, 0, new FieldNativeCameraCenter(0, 0), currents))));
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(original), JsonSerializer.SerializeToElement(FieldNativeNormalBackgroundWrap.Prepare(differentUnused, 0, new FieldNativeCameraCenter(0, 0), currents))));
        Assert.Null(FieldNativeNormalBackgroundWrap.Prepare(field, 1, new FieldNativeCameraCenter(0, 0), currents));
    }

    [Fact]
    public void ReachedTvRatioProviderPreservesActualWordsAndRefusesUnknownMetadata()
    {
        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF8_TV_RATIO_SOURCE");
        Assert.SkipUnless(source is not null && File.Exists(source), "Select the preserved actual field929 SCROLLRATIO2 source with MEMORIA_FF8_TV_RATIO_SOURCE.");
        using GameAssets assets = InstalledAssets();
        FieldAssets field = assets.LoadField(929);
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(source!));
        JsonElement actual = document.RootElement.GetProperty("EventState");
        Assert.Equal(929, actual.GetProperty("FieldId").GetInt32());
        Assert.Equal(1218, actual.GetProperty("LastFault").GetProperty("Operation").GetInt32());
        JsonSerializerOptions options = new() { Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } };
        SceneEventState scene = actual.GetProperty("Scene").Deserialize<SceneEventState>(options)!;
        string before = JsonSerializer.Serialize(scene, options);
        BackgroundScrollSettings original = scene.BackgroundScrollLayers[2];
        Assert.Equal(new BackgroundScrollSettings(0, 0, 256, 256), original);
        Assert.True(BackgroundScrollCertificate.CanSetRatioForState(field, scene, [], 2, original with { ScaleX = 0, ScaleY = 0 }));
        Assert.False(BackgroundScrollCertificate.CanSetRatioForState(field with { Cameras = [] }, scene, [], 2, original with { ScaleX = 0, ScaleY = 0 }));
        Assert.False(BackgroundScrollCertificate.CanSetRatioForState(field with { NativeExecutableHash = new string('0', 64) }, scene, [], 2, original with { ScaleX = 0, ScaleY = 0 }));
        SceneEventState missing = actual.GetProperty("Scene").Deserialize<SceneEventState>(options)!;
        missing.BackgroundScrollLayers.Remove(2);
        Assert.False(BackgroundScrollCertificate.CanSetRatioForState(field, missing, [], 2, original with { ScaleX = 0, ScaleY = 0 }));
        Assert.Equal(before, JsonSerializer.Serialize(scene, options));
        JsmProgram program = JsmProgram.Read(field.Jsm);
        Assert.Equal(Albeoris.Games.FF8.Jsm.Jsm.Opcode.SCROLLRATIO2, program.Operations[1218].Opcode);
        Assert.Equal(new[] { 2, 0, 0 }, program.Operations.Skip(1215).Take(3).Select(operation => operation.Parameter));
    }

    private static GameAssets InstalledAssets()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original camera/background resources are required.");
        return new GameAssets(directory);
    }
}
