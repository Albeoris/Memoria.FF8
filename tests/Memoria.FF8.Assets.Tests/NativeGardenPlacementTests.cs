using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeGardenPlacementTests
{
    [Fact]
    public void ZeroDistanceSourceCheckUsesActualGroundAndCanonicalFivePointContactWithoutChangingCache()
    {
        using WorldAssets assets = new(TestResources.GameDirectory);
        IReadOnlyDictionary<int, int> replacements = WorldTerrainSelection.Replacements(750);
        WorldGroundSelection ground = assets.LookupNativeGround(20480, -4600, replacements, [])!;
        NativeWorldGroundCacheState cache = NativeWorldGroundCacheState.CreateFresh(ground.Identity);
        string before = JsonSerializer.Serialize(cache);
        int calls = 0;
        int height = -(int)ground.Ground.Height;
        WorldGardenPlacementResult result = assets.ResolveNativeGardenPlacement(new(20480, height, 4600), 0, 48, 48, 0, height, 0, cache, replacements, (probes, entity, padding, forceCircle) =>
        {
            calls++;
            Assert.Equal(5, probes.Count);
            Assert.Equal(48, entity);
            Assert.Equal(0, padding);
            Assert.True(forceCircle);
            Assert.Equal((20480, -4600), (probes[0].X, probes[0].Z));
            return -1;
        }, false);
        Assert.True(result.Accepted);
        Assert.Equal(cache.Alternatives.Length, calls);
        Assert.Equal(1, result.Proposals);
        Assert.Equal(new NativeWorldPathPosition(20480, height, 4600), result.Position);
        Assert.Equal(before, JsonSerializer.Serialize(cache));
        NativeWorldGroundCacheState restored = JsonSerializer.Deserialize<NativeWorldGroundCacheState>(before)!;
        WorldGardenPlacementResult resumed = assets.ResolveNativeGardenPlacement(new(20480, height, 4600), 0, 48, 48, 0, height, 0, restored, replacements, (_, _, _, _) => -1, false);
        Assert.Equal(result, resumed);
    }

    [Fact]
    public void EightRejectedProposalsRetainTheLastActualContact()
    {
        using WorldAssets assets = new(TestResources.GameDirectory);
        IReadOnlyDictionary<int, int> replacements = WorldTerrainSelection.Replacements(750);
        WorldGroundSelection ground = assets.LookupNativeGround(20480, -4600, replacements, [])!;
        NativeWorldGroundCacheState cache = NativeWorldGroundCacheState.CreateFresh(ground.Identity);
        int calls = 0;
        int height = -(int)ground.Ground.Height;
        WorldGardenPlacementResult result = assets.ResolveNativeGardenPlacement(new(20480, height, 4600), 0, 48, 48, 0, height, 0, cache, replacements, (_, _, _, _) => calls++ % 8, false);
        Assert.False(result.Accepted);
        Assert.Equal(8 * cache.Alternatives.Length, calls);
        Assert.Equal(8, result.Proposals);
        Assert.Equal(7, result.LastContactObjectIndex);
    }

    [Fact]
    public void FailedSourcePolygonPermissionDoesNotInventATargetOrContact()
    {
        using WorldAssets assets = new(TestResources.GameDirectory);
        IReadOnlyDictionary<int, int> replacements = WorldTerrainSelection.Replacements(750);
        WorldGroundSelection ground = assets.LookupNativeGround(20480, -4600, replacements, [])!;
        NativeWorldGroundCacheState cache = NativeWorldGroundCacheState.CreateFresh(ground.Identity);
        WorldGardenPlacementResult result = assets.ResolveNativeGardenPlacement(new(20480, 0, 4600), 0, 48, 0, 700, 0, 0, cache, replacements, (_, _, _, _) => throw new InvalidOperationException("Rejected source cannot execute object contact."));
        Assert.Equal(new WorldGardenPlacementResult(false, null, -1, 0, null), result);
    }
}
