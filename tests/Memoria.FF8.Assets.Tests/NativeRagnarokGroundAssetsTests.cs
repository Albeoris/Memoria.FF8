using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeRagnarokGroundAssetsTests
{
    private const string GameDirectory = @"C:\Steam\steamapps\common\FINAL FANTASY VIII";

    [Theory]
    [InlineData((byte)5)]
    [InlineData((byte)6)]
    public void ScriptedTransferCommitsGroundButRetainsBodyAndFlightUsingProbePointerContactHeight(byte mode)
    {
        using WorldAssets assets = new(GameDirectory);
        IReadOnlyDictionary<int, int> replacements = WorldTerrainSelection.Replacements(321);
        WorldGroundSelection initial = assets.LookupNativeGround(10700, -25600, replacements, [])!;
        NativeWorldGroundCacheState cache = NativeWorldGroundCacheState.CreateFresh(initial.Identity);
        var heights = new List<int>();
        WorldNativeRagnarokGroundResult result = assets.ResolveNativeRagnarokGround(10700, -25600, 0, 0, -1000, -900, 0, cache, replacements, (probes, _) =>
        {
            heights.Add(probes[0].Y);
            return -1;
        }, scriptMode: mode);
        Assert.True(result.GroundCommitted);
        Assert.False(result.PositionCommitted);
        Assert.Equal((10700, -25600, (short)-1000, (short)-900), (result.X, result.Z, result.BodyHeight, result.FlightHeight));
        Assert.All(heights, height => Assert.Equal(unchecked((short)0x2035D08), height));
    }

    [Fact]
    public void InstalledGroundUsesFiveRadius256ProbesAndHeightAboveOriginalTerrain()
    {
        using WorldAssets assets = new(GameDirectory);
        IReadOnlyDictionary<int, int> replacements = WorldTerrainSelection.Replacements(321);
        const int x = 10700;
        const int z = -25600;
        WorldGroundSelection initial = assets.LookupNativeGround(x, z, replacements, [])!;
        NativeWorldGroundCacheState cache = NativeWorldGroundCacheState.CreateFresh(initial.Identity);
        var calls = new List<(int Padding, IReadOnlyList<WorldNativeGardenProbe> Probes)>();
        WorldNativeRagnarokGroundResult result = assets.ResolveNativeRagnarokGround(x, z, 0, 0, -1000, -1000, null, cache, replacements, (probes, padding) =>
        {
            calls.Add((padding, probes));
            return -1;
        });
        Assert.True(result.PositionCommitted);
        Assert.True(result.GroundCommitted);
        Assert.False(result.ResetSpeed);
        Assert.True(calls.Count >= 2);
        Assert.Equal(new[] { 0, 200 }, calls.Take(2).Select(call => call.Padding));
        Assert.Equal(5, calls[0].Probes.Count);
        Assert.Equal(new[] { (0, 0), (-256, 0), (0, 256), (256, 0), (0, -256) }, calls[0].Probes.Select(probe => (probe.X - x, probe.GraphicsZ + z)));
        Assert.All(calls[0].Probes, probe => Assert.Equal(result.BodyHeight, probe.Y));
        Assert.Equal(result.BodyHeight, result.FlightHeight);
        Assert.True(result.BodyHeight <= result.GroundHeight - 200);
    }

    [Fact]
    public void NativeContactRestoresPriorFlightHeightAndStopsWithoutGroundCommit()
    {
        using WorldAssets assets = new(GameDirectory);
        IReadOnlyDictionary<int, int> replacements = WorldTerrainSelection.Replacements(321);
        WorldGroundSelection initial = assets.LookupNativeGround(10700, -25600, replacements, [])!;
        NativeWorldGroundCacheState cache = NativeWorldGroundCacheState.CreateFresh(initial.Identity);
        WorldNativeRagnarokGroundResult result = assets.ResolveNativeRagnarokGround(10700, -25600, 12, -20, -1000, -900, null, cache, replacements, (_, _) => 3);
        Assert.True(result.Blocked);
        Assert.True(result.ResetSpeed);
        Assert.False(result.PositionCommitted);
        Assert.False(result.GroundCommitted);
        Assert.Equal((10700, -25600), (result.X, result.Z));
        Assert.Equal((short)-1000, result.BodyHeight);
        Assert.Equal((short)-1000, result.FlightHeight);
        Assert.Equal((3, 3), (result.PrimaryContact, result.BroadContact));
    }

    [Fact]
    public void OrdinaryHeightClampAndSuppressedPublicationKeepDistinctOwners()
    {
        using WorldAssets assets = new(GameDirectory);
        IReadOnlyDictionary<int, int> replacements = WorldTerrainSelection.Replacements(321);
        WorldGroundSelection initial = assets.LookupNativeGround(10700, -25600, replacements, [])!;
        NativeWorldGroundCacheState cache = NativeWorldGroundCacheState.CreateFresh(initial.Identity);
        WorldNativeRagnarokGroundResult result = assets.ResolveNativeRagnarokGround(10700, -25600, 0, 0, -1000, -4000, null, cache, replacements, (_, _) => -1, commitSuppressed: true);
        Assert.Equal((short)-3584, result.FlightHeight);
        Assert.Equal((short)-1000, result.BodyHeight);
        Assert.False(result.GroundCommitted);
        Assert.Throws<NotSupportedException>(() => assets.ResolveNativeRagnarokGround(10700, -25600, 0, 0, -1000, -1000, null, cache, replacements, (_, _) => -1, scriptMode: 7));
    }
}
