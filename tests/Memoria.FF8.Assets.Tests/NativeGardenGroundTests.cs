using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeGardenGroundTests
{
    [Fact]
    public void FootBoardingSuppressionKeepsGroundPointersAndPublishesFullFootprintContacts()
    {
        using WorldAssets assets = new(@"C:\Steam\steamapps\common\FINAL FANTASY VIII");
        IReadOnlyDictionary<int, int> replacements = WorldTerrainSelection.Replacements(285);
        WorldGroundSelection source = assets.LookupNativeGround(10845, -79939, replacements, [])!;
        NativeWorldGroundCacheState prior = NativeWorldGroundCacheState.CreateFresh(source.Identity) with { Alternatives = [new(0, [], source.Identity, source.Identity) { FailedSelections = 3 }] };
        List<int> calls = [];
        WorldNativeGroundResult result = assets.ResolveNativeGround(10845, -79939, 31, 0, 1162, 0, prior, replacements, 0, footprintContact: (probes, padding) =>
        {
            calls.Add(padding);
            Assert.Equal(5, probes.Count);
            Assert.All(probes, probe => Assert.Equal(probes[0].Y, probe.Y));
            Assert.Equal(32, probes[0].X - probes[1].X);
            return padding == 200 ? 2 : -1;
        }, commitSuppressed: true);
        Assert.Equal(new[] { 0, 200 }, calls);
        Assert.False(result.Blocked);
        Assert.False(result.PositionCommitted);
        Assert.False(result.GroundCommitted);
        Assert.Null(result.Center);
        Assert.Equal((10845, -79939, 0), (result.X, result.Z, result.GroundYaw));
        Assert.Equal(-1, result.PrimaryContact);
        Assert.Equal(2, result.BroadContact);
        NativeWorldGroundAlternative next = Assert.Single(result.Cache.Alternatives);
        Assert.NotEmpty(next.Entries);
        Assert.Equal(source.Identity, next.CurrentGround);
        Assert.Equal(source.Identity, next.PreviousGround);
        Assert.Equal(0, next.FailedSelections);
    }

    [Fact]
    public void ActualGardenSpawnPublishesFiveBodyVectorsAndBroadContactDoesNotBlockThePose()
    {
        using WorldAssets assets = new(@"C:\Steam\steamapps\common\FINAL FANTASY VIII");
        IReadOnlyDictionary<int, int> replacements = WorldTerrainSelection.Replacements(285);
        NativeWorldGroundCacheState prior = SpawnCache(assets, replacements);
        string before = JsonSerializer.Serialize(prior);
        List<(WorldNativeGardenProbe[] Probes, int Clearance)> calls = [];
        WorldNativeGardenGroundResult result = assets.ResolveNativeGardenGround(20480, -4600, 0, 0, 200, 0, prior, replacements, (probes, clearance) =>
        {
            calls.Add((probes.ToArray(), clearance));
            return clearance == 200 ? 7 : -1;
        });
        Assert.True(result.PositionCommitted);
        Assert.True(result.GroundCommitted);
        Assert.False(result.Blocked);
        Assert.Equal(-1, result.PrimaryContact);
        Assert.Equal(7, result.BroadContact);
        Assert.Equal(new[] { 0, 200, 0 }, calls.Select(call => call.Clearance));
        Assert.All(calls, call =>
        {
            Assert.Equal(5, call.Probes.Length);
            Assert.All(call.Probes, probe =>
            {
                Assert.Equal(call.Probes[0].Y, probe.Y);
                Assert.Equal(probe.Y, probe.LocalY);
                Assert.Equal(-probe.Z, probe.GraphicsZ);
                Assert.Equal(0, probe.Status);
                Assert.NotNull(probe.GroundIdentity);
            });
            Assert.Equal(new[] { (20480, -4600), (20416, -4600), (20480, -4664), (20544, -4600), (20480, -4536) }, call.Probes.Select(probe => (probe.X, probe.Z)));
        });
        Assert.Equal((short)200, result.BodyHeight);
        Assert.Equal((20480, -4600, 0), (result.X, result.Z, result.GroundYaw));
        Assert.Equal(before, JsonSerializer.Serialize(prior));
        Assert.All(result.Cache.Alternatives, alternative =>
        {
            Assert.Equal(0, alternative.FailedSelections);
            Assert.NotEmpty(alternative.Entries);
            Assert.Equal(result.Center!.Identity, alternative.CurrentGround);
        });
        NativeWorldGroundCacheState restored = JsonSerializer.Deserialize<NativeWorldGroundCacheState>(JsonSerializer.Serialize(result.Cache))!;
        assets.ValidateNativeGroundCache(restored, replacements);
        Assert.Equal(JsonSerializer.Serialize(result.Cache), JsonSerializer.Serialize(restored));
    }

    [Fact]
    public void FirstContactRejectionKeepsPoseAndPointersWithoutTryingAnotherCandidate()
    {
        using WorldAssets assets = new(@"C:\Steam\steamapps\common\FINAL FANTASY VIII");
        IReadOnlyDictionary<int, int> replacements = WorldTerrainSelection.Replacements(285);
        NativeWorldGroundCacheState prior = SpawnCache(assets, replacements);
        int calls = 0;
        WorldNativeGardenGroundResult result = assets.ResolveNativeGardenGround(20480, -4600, 1, 0, 200, 0, prior, replacements, (_, _) =>
        {
            calls++;
            return 3;
        });
        Assert.Equal(1, calls);
        Assert.True(result.Blocked);
        Assert.False(result.PositionCommitted);
        Assert.False(result.GroundCommitted);
        Assert.Null(result.Center);
        Assert.Equal((20480, -4600, (short)200, 3, 3), (result.X, result.Z, result.BodyHeight, result.PrimaryContact, result.BroadContact));
        NativeWorldGroundAlternative previous = Assert.Single(prior.Alternatives);
        NativeWorldGroundAlternative next = Assert.Single(result.Cache.Alternatives);
        Assert.Equal(previous.CurrentGround, next.CurrentGround);
        Assert.Equal(previous.PreviousGround, next.PreviousGround);
        Assert.Equal(0, next.FailedSelections);
        Assert.NotEmpty(next.Entries);
    }

    [Fact]
    public void RejectedFourteenUnitHeightRetryRestoresTheInitialHoverAndModeEightSkipsThatRetry()
    {
        using WorldAssets assets = new(@"C:\Steam\steamapps\common\FINAL FANTASY VIII");
        IReadOnlyDictionary<int, int> replacements = WorldTerrainSelection.Replacements(285);
        NativeWorldGroundCacheState prior = SpawnCache(assets, replacements);
        List<short> heights = [];
        WorldNativeGardenGroundResult result = assets.ResolveNativeGardenGround(20480, -4600, 0, 0, 400, 0, prior, replacements, (probes, _) =>
        {
            heights.Add(probes[0].Y);
            return heights.Count == 3 ? 9 : -1;
        });
        Assert.Equal(3, heights.Count);
        Assert.Equal((short)386, heights[2]);
        Assert.Equal(heights[0], result.BodyHeight);
        Assert.True(result.PositionCommitted);
        int modeEightCalls = 0;
        WorldNativeGardenGroundResult modeEight = assets.ResolveNativeGardenGround(20480, -4600, 0, 0, 200, 0, prior, replacements, (_, _) =>
        {
            modeEightCalls++;
            return -1;
        }, scriptMode: 8);
        Assert.Equal(2, modeEightCalls);
        Assert.False(modeEight.PositionCommitted);
        Assert.True(modeEight.GroundCommitted);
        Assert.Equal((short)200, modeEight.BodyHeight);
    }

    private static NativeWorldGroundCacheState SpawnCache(WorldAssets assets, IReadOnlyDictionary<int, int> replacements)
    {
        WorldGroundSelection source = assets.LookupNativeGround(20480, -4600, replacements, [])!;
        Assert.NotNull(source);
        return NativeWorldGroundCacheState.CreateFresh(source.Identity) with { Alternatives = [new(0, [], source.Identity, source.Identity) { FailedSelections = 3 }] };
    }
}
