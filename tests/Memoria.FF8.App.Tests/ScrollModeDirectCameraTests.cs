using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class ScrollModeDirectCameraTests
{
    [Theory]
    [InlineData(false, 73, 11)]
    [InlineData(true, 112, 17)]
    public void SignedRatioSettingsUseTheRetainedDirectCameraBeforeAndAfterItsPostVmCommit(bool completed, short centerY, int expectedLayerY)
    {
        using GameAssets assets = OpenAssets();
        FieldAssets field = assets.LoadField(268);
        Assert.Equal(Vector2.Zero, field.Camera.Pan);
        Assert.Equal(0, field.Information!.BackgroundWrapMask);
        Assert.Contains(field.Background, tile => tile.Layer == 1);
        SceneEventState scene = Scene(field, completed);
        BackgroundScrollPosition camera = scene.BackgroundScrollPositions[0];
        BackgroundScrollSettings settings = new(0, 0, 0, -40);

        Assert.True(BackgroundScrollCertificate.CanSetForState(field, scene, [], 1, settings));
        Assert.Same(camera, scene.BackgroundScrollPositions[0]);
        Assert.Equal(new BackgroundScrollSettings(0, 0, 256, 256), scene.BackgroundScrollLayers[1]);

        scene.BackgroundScrollLayers[1] = settings;
        BackgroundScrollFrame frame = BackgroundScrollCertificate.Prepare(field, scene);
        Assert.Equal(new Vector2(0, -centerY), frame.ActorOffset);
        Assert.Equal(new Vector2(0, expectedLayerY), frame.LayerOffsets[1]);
        Assert.Equal(camera, scene.BackgroundScrollPositions[0]);
        Assert.Equal(settings, scene.BackgroundScrollLayers[1]);
    }

    [Fact]
    public void SettingsAdmissionAndRenderingSubtractTheExistingNonselectedLayerCurrent()
    {
        using GameAssets assets = OpenAssets();
        FieldAssets field = assets.LoadField(268);
        SceneEventState scene = Scene(field, completed: false);
        scene.BackgroundScrollPositions[1] = new BackgroundScrollPosition(0, 7, 0, 7, 0, 0, 0, 3, 2);
        BackgroundScrollSettings settings = new(0, 0, 0, -40);

        Assert.True(BackgroundScrollCertificate.CanSetForState(field, scene, [], 1, settings));
        scene.BackgroundScrollLayers[1] = settings;
        BackgroundScrollFrame frame = BackgroundScrollCertificate.Prepare(field, scene);
        Assert.Equal(new Vector2(0, 4), frame.LayerOffsets[1]);
    }

    [Fact]
    public void UnknownDirectCurrentAndOverflowingWordsRefuseWithoutChangingTheCameraOrSettings()
    {
        using GameAssets assets = OpenAssets();
        FieldAssets field = assets.LoadField(268);
        SceneEventState scene = Scene(field, completed: false);
        string before = JsonSerializer.Serialize(scene);

        Assert.False(BackgroundScrollCertificate.CanSetForState(field, scene, [], 1, new BackgroundScrollSettings(0, short.MaxValue, 0, -40)));
        Assert.False(BackgroundScrollCertificate.CanSetForState(field, scene, [], 8, new BackgroundScrollSettings(0, 0, 0, -40)));
        Assert.Equal(before, JsonSerializer.Serialize(scene));

        scene.BackgroundScrollPositions[0] = scene.BackgroundScrollPositions[0] with { X = null, Y = null };
        before = JsonSerializer.Serialize(scene);
        Assert.False(BackgroundScrollCertificate.CanSetForState(field, scene, [], 1, new BackgroundScrollSettings(0, 0, 0, -40)));
        Assert.Equal(before, JsonSerializer.Serialize(scene));
    }

    private static SceneEventState Scene(FieldAssets field, bool completed)
    {
        // Preserve every unrelated owner from the authentic reached Scene. Only
        // the explicit camera/scroll component inputs below vary; no game snapshot is published.
        DirectoryInfo? repository = new(TestResources.FixtureDirectory);
        Assert.NotNull(repository);
        using JsonDocument source = JsonDocument.Parse(File.ReadAllText(Path.Combine(repository.FullName, "../fixtures/external-fixture.json")));
        JsonNode scene = JsonNode.Parse(source.RootElement.GetProperty("EventState").GetProperty("Scene").GetRawText())!;
        Assert.Equal(field.Id, scene["NativeFieldInitialization"]!["FieldId"]!.GetValue<int>());
        Dictionary<int, BackgroundScrollSettings> settings = Enumerable.Range(0, 8).ToDictionary(layer => layer, _ => new BackgroundScrollSettings(0, 0, 256, 256));
        Dictionary<int, BackgroundScrollPosition> positions = new() { [0] = new BackgroundScrollPosition(0, completed ? (short)112 : (short)73, 0, 112, 0, 73, 0, 3, completed ? (byte)2 : (byte)0) };
        scene["BackgroundScrollLayers"] = JsonSerializer.SerializeToNode(settings);
        scene["BackgroundScrollPositions"] = JsonSerializer.SerializeToNode(positions);
        return scene.Deserialize<SceneEventState>(new JsonSerializerOptions { Converters = { new JsonStringEnumConverter() } })!;
    }

    private static GameAssets OpenAssets()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original field corpus is required.");
        return new GameAssets(directory);
    }
}
