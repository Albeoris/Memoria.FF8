using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeWorldGroundContactTests
{
    [Fact]
    public void BlockingTheChosenTerrainCandidateKeepsTheBodyAndGroundPointersButCommitsTheLookupCache()
    {
        using WorldAssets assets = new(TestResources.GameDirectory);
        IReadOnlyDictionary<int, int> replacements = WorldTerrainSelection.Replacements(285);
        WorldGroundSelection source = assets.LookupNativeGround(10845, -79939, replacements, [])!;
        NativeWorldGroundCacheState prior = NativeWorldGroundCacheState.CreateFresh(source.Identity) with { Alternatives = [new(0, [], source.Identity, source.Identity) { FailedSelections = 3 }] };
        string before = JsonSerializer.Serialize(prior);
        WorldNativeGroundResult clear = assets.ResolveNativeGround(10845, -79939, 31, 0, 1162, null, prior, replacements, 0);
        List<(int X, int Z, int Height)> calls = [];
        WorldNativeGroundResult blocked = assets.ResolveNativeGround(10845, -79939, 31, 0, 1162, null, prior, replacements, 0, contact: (x, z, height) =>
        {
            calls.Add((x, z, height));
            return true;
        });
        Assert.Single(calls);
        Assert.Equal((clear.X, clear.Z, NativeWorldTerrain.ResolveHeight(0, -(int)clear.Center!.Ground.Height, clear.Center.Ground.Triangle.GroundType, -842).Height), calls[0]);
        Assert.True(blocked.Blocked);
        Assert.Null(blocked.Center);
        Assert.Equal((10845, -79939, 0), (blocked.X, blocked.Z, blocked.GroundYaw));
        NativeWorldGroundAlternative result = Assert.Single(blocked.Cache.Alternatives);
        Assert.Equal(source.Identity, result.CurrentGround);
        Assert.Equal(source.Identity, result.PreviousGround);
        Assert.Equal(0, result.FailedSelections);
        Assert.Equal(clear.Cache.Alternatives[0].Entries, result.Entries);
        Assert.NotEmpty(result.Entries);
        Assert.Equal(before, JsonSerializer.Serialize(prior));
    }

    [Fact]
    public void AContactFreeCallbackPreservesTheEstablishedGroundResultAndTerrainFailureDoesNotCallIt()
    {
        using WorldAssets assets = new(TestResources.GameDirectory);
        IReadOnlyDictionary<int, int> replacements = WorldTerrainSelection.Replacements(285);
        WorldGroundSelection source = assets.LookupNativeGround(10845, -79939, replacements, [])!;
        NativeWorldGroundCacheState prior = NativeWorldGroundCacheState.CreateFresh(source.Identity) with { Alternatives = [new(0, [], source.Identity, source.Identity) { FailedSelections = 0 }] };
        WorldNativeGroundResult baseline = assets.ResolveNativeGround(10845, -79939, 31, 0, 1162, null, prior, replacements, 0);
        WorldNativeGroundResult checkedResult = assets.ResolveNativeGround(10845, -79939, 31, 0, 1162, null, prior, replacements, 0, contact: (_, _, _) => false);
        Assert.Equal(JsonSerializer.Serialize(baseline), JsonSerializer.Serialize(checkedResult));
        int calls = 0;
        WorldNativeGroundResult blockedCar = assets.ResolveNativeGround(10845, -79939, 31, 0, 1162, null, prior, replacements, 33, contact: (_, _, _) =>
        {
            calls++;
            return true;
        });
        Assert.True(blockedCar.Blocked);
        Assert.Equal(0, calls);
    }
}
