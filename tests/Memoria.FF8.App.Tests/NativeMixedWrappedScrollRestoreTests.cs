using System.Text.Json;
using System.Text.Json.Serialization;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeMixedWrappedScrollRestoreTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { Converters = { new JsonStringEnumConverter() } };

    [Fact]
    public void ActualSelectedActorCenterAndNonselectedLinearPhaseHaveTheSameIssueAndRestoreContract()
    {
        using GameAssets assets = new(GameDirectory());
        FieldAssets field = assets.LoadField(926);
        SceneEventState scene = ReadRecordedScene();
        Assert.NotEqual(0, field.Information!.BackgroundWrapMask);
        Assert.Equal((5, 2, (short?)-28, (short?)-15), (scene.BackgroundScrollPositions[0].Mode, scene.BackgroundScrollPositions[0].Status, scene.BackgroundScrollPositions[0].X, scene.BackgroundScrollPositions[0].Y));
        Assert.Equal((4, 1, (short?)44, (short?)2, (short?)7), (scene.BackgroundScrollPositions[3].Mode, scene.BackgroundScrollPositions[3].Status, scene.BackgroundScrollPositions[3].X, scene.BackgroundScrollPositions[3].Counter, scene.BackgroundScrollPositions[3].Duration));
        NativeLinearScrollQuery issue = new(3, 0, 0, 308, 0, 7);
        Assert.True(NativeWrappedBackgroundScrollCertificate.CanBegin(field, issue, scene));
        Assert.False(DirectBackgroundScrollCertificate.SupportsLinearPhase(field, scene));
        Assert.False(NativeNonselectedBackgroundScrollCertificate.SupportsLinearPhase(field, scene));
        // Normal-follow rendering cannot replace the retained selected actor center.
        Assert.False(NativeWrappedBackgroundScrollCertificate.SupportsPhase(field, scene));
        Assert.True(NativeWrappedBackgroundScrollCertificate.SupportsLinearPhase(field, scene));
        SceneEventState restored = JsonSerializer.Deserialize<SceneEventState>(JsonSerializer.Serialize(scene, JsonOptions), JsonOptions)!;
        Assert.True(NativeWrappedBackgroundScrollCertificate.SupportsLinearPhase(field, restored));
        Assert.Equal(scene.BackgroundScrollPositions, restored.BackgroundScrollPositions);
        Assert.Equal(scene.BackgroundScrollLayers, restored.BackgroundScrollLayers);
    }

    [Fact]
    public void MixedRestoreStillRefusesInvalidPhaseEndpointsAndMissingPhysicalSettings()
    {
        using GameAssets assets = new(GameDirectory());
        FieldAssets field = assets.LoadField(926);
        SceneEventState scene = ReadRecordedScene();
        BackgroundScrollPosition linear = scene.BackgroundScrollPositions[3];
        BackgroundScrollPosition selected = scene.BackgroundScrollPositions[0];
        foreach (BackgroundScrollPosition malformed in new[] { linear with { X = 45 }, linear with { Counter = 0 }, linear with { Duration = -1 }, linear with { PreviousX = null }, linear with { TargetX = 4097 }, linear with { NativeLinearVersion = 0 } })
        {
            scene.BackgroundScrollPositions[3] = malformed;
            Assert.False(NativeWrappedBackgroundScrollCertificate.SupportsLinearPhase(field, scene));
        }
        scene.BackgroundScrollPositions[3] = linear;
        scene.BackgroundScrollPositions[0] = selected with { PreviousY = 4097 };
        Assert.False(NativeWrappedBackgroundScrollCertificate.SupportsLinearPhase(field, scene));
        scene.BackgroundScrollPositions[0] = selected;
        scene.BackgroundScrollLayers.Remove(1);
        Assert.False(NativeWrappedBackgroundScrollCertificate.SupportsLinearPhase(field, scene));
    }

    private static SceneEventState ReadRecordedScene()
    {
        DirectoryInfo? repository = new(TestResources.FixtureDirectory);
        Assert.NotNull(repository);
        string source = Environment.GetEnvironmentVariable("MEMORIA_FF8_MIXED_SCROLL_SOURCE") ?? Path.Combine(repository.FullName, "../fixtures/external-fixture.json");
        Assert.SkipUnless(File.Exists(source), "The genuine same-epoch field926 mixed-scroll diagnostic source is required.");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(source));
        JsonElement events = document.RootElement.GetProperty("EventState");
        Assert.Equal(926, events.GetProperty("FieldId").GetInt32());
        Assert.Equal(1156, events.GetProperty("Frame").GetInt64());
        // Read the actual retained scene. No missing properties or phase words are supplied.
        return JsonSerializer.Deserialize<SceneEventState>(events.GetProperty("Scene"), JsonOptions)!;
    }

    private static string GameDirectory()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original field resources are required.");
        return directory;
    }
}
