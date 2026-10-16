using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeTrainGroundCacheTests
{
    [Theory]
    [InlineData((byte)16)]
    [InlineData((byte)17)]
    [InlineData((byte)18)]
    [InlineData((byte)19)]
    [InlineData((byte)20)]
    [InlineData((byte)21)]
    [InlineData((byte)22)]
    public void NativeTrainUsesItsFiveProbeFootprintAndUnconditionalPermissionWithTheSharedCache(byte entity)
    {
        using WorldAssets assets = new(TestResources.GameDirectory);
        IReadOnlyDictionary<int, int> replacements = WorldTerrainSelection.Replacements(285);
        WorldGroundSelection source = assets.LookupNativeGround(10845, -79939, replacements, [])!;
        NativeWorldGroundCacheState initial = NativeWorldGroundCacheState.CreateFresh(source.Identity);
        Assert.Equal(32, NativeWorldTerrain.ProbeRadius(entity));
        Assert.Equal((byte)0, NativeWorldTerrain.PermissionMask(entity));
        Assert.True(NativeWorldTerrain.CanTraverse(0, entity));
        WorldNativeGroundResult train = assets.ResolveNativeGround(10845, -79939, 0, 0, 1162, null, initial, replacements, entity);
        WorldNativeGroundResult foot = assets.ResolveNativeGround(10845, -79939, 0, 0, 1162, null, initial, replacements, 0);
        Assert.False(train.Blocked);
        Assert.NotNull(train.Center);
        Assert.Equal((10845, -79939), (train.X, train.Z));
        Assert.Equal(JsonSerializer.Serialize(foot), JsonSerializer.Serialize(train));
        Assert.True(assets.ResolveNativeGround(10845, -79939, 0, 0, 1162, null, initial, replacements, 33).Blocked);
        NativeWorldGroundCacheState restored = JsonSerializer.Deserialize<NativeWorldGroundCacheState>(JsonSerializer.Serialize(train.Cache))!;
        WorldNativeGroundResult next = assets.ResolveNativeGround(10845, -79939, 0, 0, 1162, null, train.Cache, replacements, entity);
        WorldNativeGroundResult restoredNext = assets.ResolveNativeGround(10845, -79939, 0, 0, 1162, null, restored, replacements, entity);
        Assert.Equal(JsonSerializer.Serialize(next), JsonSerializer.Serialize(restoredNext));
        //53ECE0's train result is a probe-pointer low WORD; it cannot become a scalar terrain/body height.
        Assert.Throws<NotSupportedException>(() => NativeWorldTerrain.ResolveHeight(entity, -1162, 1, 0));
    }
}
