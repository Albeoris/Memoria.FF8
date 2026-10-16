using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleSummonSoundsTests
{
    [Fact]
    public void StreamPublicationRetainsItsOriginalPointerAndAddsTheNativeSoundNumberBase()
    {
        NativeBattleSummonStreamState original = NativeBattleSummonStreamState.Unknown();
        NativeBattleSummonStreamState published = original.Publish501860(0x20DFAB8 + 0x30000, 7);
        Assert.Equal(690007, published.Number);
        Assert.Equal(0x210FAB8u, published.SourceAddress);
        Assert.Null(original.Number);
        NativeBattleModuleSound receipt = published.Play5018C0(128, 63);
        Assert.Equal(690007u, receipt.Sound);
        Assert.Equal(0u, receipt.Flags);
        Assert.Null(receipt.Pan);
        Assert.Equal(128u, receipt.RawPan);
        Assert.Equal(63u, receipt.Volume);
        Assert.Null(receipt.Anchor);
        string json = JsonSerializer.Serialize(published);
        Assert.Equal(published, JsonSerializer.Deserialize<NativeBattleSummonStreamState>(json));
        Assert.Equal(published.Play5018C0(192, 127), JsonSerializer.Deserialize<NativeBattleSummonStreamState>(json)!.Play5018C0(192, 127));
    }

    [Fact]
    public void BattleSoundInitializationClearsOnlyTheNumberAndDoesNotReconstructTheSourcePointer()
    {
        NativeBattleSummonStreamState known = new(690010, 0x12345678);
        Assert.Equal(new(0, 0x12345678), known.Reset501160());
        Assert.Equal(new(0, null), NativeBattleSummonStreamState.Unknown().Reset501160());
        Assert.Throws<NotSupportedException>(() => NativeBattleSummonStreamState.Unknown().Play5018C0(128, 127));
    }

    [Fact]
    public void SpatialStreamPlaybackReadsThePublishedProjectionAndPreservesTheRequestedVolume()
    {
        NativeBattleSummonStreamState stream = new(690001, null);
        NativeBattleCameraProjection camera = new(new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096), 216, 160, 108);
        NativeBattleModuleAnchor anchor = new(0, 0, 1000, 0);
        NativeBattleModuleSound receipt = stream.Play501920(anchor, camera, 1);
        Assert.Equal(camera.ReadPan(new(0, 0, 1000)), receipt.Pan);
        Assert.Equal(anchor, receipt.Anchor);
        Assert.Equal(1u, receipt.Volume);
        Assert.Equal(690001u, receipt.Sound);
        Assert.Throws<NotSupportedException>(() => stream.Play501920(anchor, camera with { View = null }, 127));
    }
}
