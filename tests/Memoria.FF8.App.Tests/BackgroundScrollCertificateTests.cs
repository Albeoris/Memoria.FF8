using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class BackgroundScrollCertificateTests
{
    [Fact]
    public void FirstFloorParallaxUsesSharedMetadataInsteadOfFieldIdentityOrOneSetting()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed field corpus is required.");
        using var assets = new GameAssets(directory);
        FieldAssets field = assets.LoadField(172);
        var settings = new BackgroundScrollSettings(0, 0, 0, 0);
        Assert.True(BackgroundScrollCertificate.CanSet(field, 1, settings, 0));
        Assert.True(BackgroundScrollCertificate.CanSet(field, 1, settings with { OffsetX = 1 }, 0));
        Assert.True(BackgroundScrollCertificate.CanSet(field, 1, settings with { ScaleY = 256 }, 0));
        Assert.True(BackgroundScrollCertificate.CanSet(field with { Id = 165 }, 1, settings, 0));
        Assert.False(BackgroundScrollCertificate.CanSet(field, 1, settings, 1));
        Assert.False(BackgroundScrollCertificate.CanSet(field, 0, settings, 0));
        Assert.False(BackgroundScrollCertificate.CanSet(field, 8, settings, 0));
        Assert.False(BackgroundScrollCertificate.CanSet(field with { Information = null }, 1, settings, 0));
        Assert.Throws<NotSupportedException>(() => BackgroundScrollCertificate.Prepare(field, new SceneEventState()));
    }
}
