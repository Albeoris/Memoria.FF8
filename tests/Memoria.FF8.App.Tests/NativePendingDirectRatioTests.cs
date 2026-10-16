using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativePendingDirectRatioTests
{
    [Fact]
    public void ReachedRatioUsesPendingDirectNextDrawWithoutInventingCurrentWords()
    {
        string directory = TestResources.GameDirectory;
        string source = Environment.GetEnvironmentVariable("MEMORIA_FF8_PENDING_RATIO_SOURCE") ?? Path.Combine("../fixtures/external-fixture.json");
        Assert.SkipUnless(Directory.Exists(directory) && File.Exists(source), "The preserved actual248 ratio diagnostic and installed resources are required.");
        using GameAssets assets = new(directory);
        FieldAssets field = assets.LoadField(248);
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(source));
        JsonElement actual = document.RootElement.GetProperty("EventState");
        Assert.Equal(248, actual.GetProperty("FieldId").GetInt32());
        Assert.Equal(431, actual.GetProperty("LastFault").GetProperty("Operation").GetInt32());
        JsonSerializerOptions options = new() { Converters = { new JsonStringEnumConverter() } };
        SceneEventState scene = actual.GetProperty("Scene").Deserialize<SceneEventState>(options)!;
        Assert.Equal(0, field.Information!.BackgroundWrapMask);
        Assert.Equal(0, scene.CameraIndex);
        BackgroundScrollPosition pending = scene.BackgroundScrollPositions[0];
        Assert.Equal((3, 0, 0, 100), ((int)pending.Mode, (int)pending.Status, (int)pending.TargetX, (int)pending.TargetY));
        Assert.Null(pending.X);
        Assert.Null(pending.Y);
        BackgroundScrollSettings previous = scene.BackgroundScrollLayers[1];
        BackgroundScrollSettings ratio = previous with { ScaleX = 0, ScaleY = 0 };
        string before = JsonSerializer.Serialize(scene, options);
        Assert.Null(DirectBackgroundScrollCertificate.Prepare(field, scene));
        Assert.True(BackgroundScrollCertificate.CanSetRatioForState(field, scene, [], 1, ratio));
        BackgroundScrollFrame placement = Assert.IsType<BackgroundScrollFrame>(DirectBackgroundScrollCertificate.Prepare(field, scene.CreateBackgroundScrollProjection(1, ratio)));
        Assert.Equal(new Vector2(0, -100), placement.ActorOffset);
        Assert.Equal(before, JsonSerializer.Serialize(scene, options));

        //The declared callback result exercises the existing475DF0 target publication,
        //without editing/loading a replacement save or recovering unknown current words.
        SceneEventState after = actual.GetProperty("Scene").Deserialize<SceneEventState>(options)!;
        after.BackgroundScrollLayers[1] = ratio;
        after.BackgroundScrollPositions[0] = pending with { X = pending.TargetX, Y = pending.TargetY, Counter = 0, Status = 2 };
        BackgroundScrollFrame drawn = Assert.IsType<BackgroundScrollFrame>(DirectBackgroundScrollCertificate.Prepare(field, after));
        Assert.Equal(new Vector2(0, -100), drawn.ActorOffset);
        Assert.Equal(field.GetCamera(0).Pan, drawn.LayerOffsets.GetValueOrDefault(1));
        Assert.Equal(previous.OffsetX, after.BackgroundScrollLayers[1].OffsetX);
        Assert.Equal(previous.OffsetY, after.BackgroundScrollLayers[1].OffsetY);

        SceneEventState invalid = actual.GetProperty("Scene").Deserialize<SceneEventState>(options)!;
        invalid.BackgroundScrollPositions[0] = pending with { TargetY = short.MaxValue };
        Assert.False(BackgroundScrollCertificate.CanSetRatioForState(field, invalid, [], 1, ratio));
        Assert.Null(DirectBackgroundScrollCertificate.Prepare(field, invalid.CreateBackgroundScrollProjection(1, ratio)));
        Assert.Equal(before, JsonSerializer.Serialize(scene, options));
    }
}
