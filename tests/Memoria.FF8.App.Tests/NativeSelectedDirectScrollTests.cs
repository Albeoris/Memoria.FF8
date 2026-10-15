using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeSelectedDirectScrollTests
{
    [Fact]
    public void ActualPrisonSelectedCameraAdmitsDirectRecordWithoutInventingCurrentWords()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF8_SELECTED_DIRECT_SOURCE");
        Assert.SkipUnless(Directory.Exists(directory) && source is not null && File.Exists(source), "Select the genuine field809 DSCROLL2 failed diagnostic.");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(source!));
        JsonElement checkpoint = document.RootElement.GetProperty("EventState");
        Assert.Equal(809, checkpoint.GetProperty("FieldId").GetInt32());
        Assert.Equal("DSCROLL2", checkpoint.GetProperty("LastFault").GetProperty("Opcode").GetString());
        JsonSerializerOptions options = new() { Converters = { new JsonStringEnumConverter() } };
        SceneEventState scene = JsonSerializer.Deserialize<SceneEventState>(checkpoint.GetProperty("Scene"), options)!;
        using GameAssets assets = new(directory);
        FieldAssets field = assets.LoadField(809);
        Assert.Equal(1, scene.CameraIndex);
        Assert.Empty(scene.BackgroundScrollPositions);
        Assert.True(DirectBackgroundScrollCertificate.CanSet(field, scene, 1, 0, 0));
        Assert.False(DirectBackgroundScrollCertificate.CanSet(field, scene, 0, 0, 0));

        //Component phases use the existing520FA0/475DF0 row contract; this is
        //not a replacement research save or an inferred pre-event current.
        BackgroundScrollPosition pending = new(null, null, 0, 0, null, null, null, 3, 0);
        scene.BackgroundScrollPositions.Add(1, pending);
        Assert.True(DirectBackgroundScrollCertificate.CanSet(field, scene, 1, 0, 0));
        Assert.Null(DirectBackgroundScrollCertificate.Prepare(field, scene));
        scene.BackgroundScrollPositions[1] = pending with { X = 0, Y = 0, Counter = 0, Status = 2 };
        BackgroundScrollFrame zero = Assert.IsType<BackgroundScrollFrame>(DirectBackgroundScrollCertificate.Prepare(field, scene));
        Assert.Equal(Vector2.Zero, zero.ActorOffset);
        Assert.Equal(Vector2.Zero, zero.LayerOffsets.GetValueOrDefault(0));
        Assert.Equal(Vector2.Zero, zero.LayerOffsets.GetValueOrDefault(1));

        Assert.True(DirectBackgroundScrollCertificate.CanSet(field, scene, 1, 10, -20));
        scene.BackgroundScrollPositions[1] = new BackgroundScrollPosition(10, -20, 10, -20, 0, 0, 0, 3, 2);
        BackgroundScrollFrame moved = Assert.IsType<BackgroundScrollFrame>(DirectBackgroundScrollCertificate.Prepare(field, scene));
        Vector2 offset = new(-10, 20);
        Assert.Equal(offset, moved.ActorOffset);
        foreach (int layer in field.Background.Select(tile => tile.Layer).Distinct())
        {
            Vector2 expected = offset;
            if (layer > scene.CameraIndex)
            {
                Assert.True(BackgroundParallaxPlacement.TryOffset(10, -20, BackgroundScrollCertificate.ReadSettings(scene, layer), out expected));
                expected += field.GetCamera(1).Pan;
            }
            Assert.Equal(expected, moved.LayerOffsets.GetValueOrDefault(layer));
        }
        SceneEventState restored = JsonSerializer.Deserialize<SceneEventState>(JsonSerializer.Serialize(scene, options), options)!;
        BackgroundScrollFrame repeated = Assert.IsType<BackgroundScrollFrame>(DirectBackgroundScrollCertificate.Prepare(field, restored));
        Assert.Equal(moved.ActorOffset, repeated.ActorOffset);
        Assert.Equal(moved.LayerOffsets.OrderBy(pair => pair.Key), repeated.LayerOffsets.OrderBy(pair => pair.Key));
        restored.BackgroundScrollPositions[1] = restored.BackgroundScrollPositions[1] with { Status = 1 };
        Assert.False(DirectBackgroundScrollCertificate.CanSet(field, restored, 1, 0, 0));
        Assert.Null(DirectBackgroundScrollCertificate.Prepare(field, restored));
    }
}
