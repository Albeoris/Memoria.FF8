using System.Numerics;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class BackgroundScrollCertificateTests
{
    [Fact]
    public void OriginalFirstFloorZeroRecordHasForcedZeroCenterAndRejectsUnprovedCameraState()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed field corpus is required.");
        using var assets = new GameAssets(directory);
        FieldAssets field = assets.LoadField(172);
        var settings = new BackgroundScrollSettings(0, 0, 0, 0);
        Assert.Equal(new FieldBounds(-112, 112, 160, -160), field.Information!.CameraBounds[0]);
        Assert.Equal(new FieldBounds(0, 224, 320, 0), field.Information.ScreenBounds[0]);
        Assert.True(BackgroundScrollCertificate.CanSet(field, 1, settings, 0));
        Assert.False(BackgroundScrollCertificate.CanSet(field, 1, settings, 1));
        Assert.False(BackgroundScrollCertificate.CanSet(field, 0, settings, 0));
        Assert.False(BackgroundScrollCertificate.CanSet(field, 8, settings, 0));
        Assert.False(BackgroundScrollCertificate.CanSet(field, 1, settings with { OffsetX = 1 }, 0));
        Assert.False(BackgroundScrollCertificate.CanSet(field, 1, settings with { ScaleY = 256 }, 0));
        FieldBounds[] bounds = field.Information.CameraBounds.ToArray();
        bounds[0] = bounds[0] with { Right = 161 };
        FieldAssets unproved = field with { Information = field.Information with { CameraBounds = bounds } };
        Assert.False(BackgroundScrollCertificate.CanSet(unproved, 1, settings, 0));
        Assert.False(BackgroundScrollCertificate.CanSet(field with { Information = null }, 1, settings, 0));
        Assert.False(BackgroundScrollCertificate.CanSet(field with { Id = 165 }, 1, settings, 0));
        Assert.True(BackgroundScrollCertificate.CanSetPosition(field, 0, 0, 224, 0));
        Assert.True(BackgroundScrollCertificate.CanSetPosition(field, 0, 0, 0, 0));
        Assert.False(BackgroundScrollCertificate.CanSetPosition(field, 1, 0, 224, 0));
        Assert.False(BackgroundScrollCertificate.CanSetPosition(field, 0, 0, 223, 0));
        var scene = new SceneEventState();
        scene.BackgroundScrollLayers[1] = settings;
        scene.BackgroundScrollPositions[0] = new BackgroundScrollPosition(0, 224, 0, 224, 0, 0, 0, 3, 2);
        BackgroundScrollFrame frame = BackgroundScrollCertificate.Prepare(field, scene);
        Assert.Equal(new Vector2(0, -224), frame.ActorOffset);
        Assert.Equal(new Vector2(0, -224), frame.LayerOffsets[0]);
        Assert.Equal(Vector2.Zero, frame.LayerOffsets[1]);
        scene.BackgroundScrollPositions[0] = scene.BackgroundScrollPositions[0] with { Mode = 1 };
        Assert.False(BackgroundScrollCertificate.Supports(field, scene));
        Assert.Throws<NotSupportedException>(() => BackgroundScrollCertificate.Prepare(field, scene));
    }
}
