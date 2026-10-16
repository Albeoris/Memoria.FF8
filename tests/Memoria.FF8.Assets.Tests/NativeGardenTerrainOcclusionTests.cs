using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeGardenTerrainOcclusionTests
{
    [Fact]
    public void ActualRetainedMissileBaseCarAndFreshGardenViewsExcludeEveryPotentialNativeOccluder()
    {
        using WorldAssets assets = new(@"C:\Steam\steamapps\common\FINAL FANTASY VIII");
        NativeWorldFootCameraState prior = new(-71662, -14849, -340, -71661, -14857, -599, 2284, -112, -341, -7168);
        WorldNativeTerrainOcclusionResult retained = assets.CertifyNativeTerrainCameraOcclusion(prior, -340, WorldTerrainSelection.Replacements(349), 32);
        Assert.True(retained.Occluded == false, retained.Reason);
        NativeWorldFootCameraState garden = new(20480, -4600, 200, 20480, -4600, -56, 0, -112, -341, -7168);
        WorldNativeTerrainOcclusionResult fresh = assets.CertifyNativeTerrainCameraOcclusion(garden, 0, WorldTerrainSelection.Replacements(285));
        Assert.True(fresh.Occluded == false, fresh.Reason);
        Assert.Equal(0, retained.Blocks);
        Assert.Equal(0, retained.Polygons);
    }
}
