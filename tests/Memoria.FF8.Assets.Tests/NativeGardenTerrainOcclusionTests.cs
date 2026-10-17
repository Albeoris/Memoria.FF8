using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeGardenTerrainOcclusionTests
{
    [Fact]
    public void ForestFootCameraRejectsFastTerrainAtTheHalfThresholdBoundary()
    {
        using WorldAssets assets = new(TestResources.GameDirectory);
        NativeWorldFootCameraState camera = new(26919, -28895, -608, 26867, -28862, -835, 639, -238, -341, -7168);
        WorldNativeTerrainOcclusionResult result = assets.CertifyNativeTerrainCameraOcclusion(camera, 928, WorldTerrainSelection.Replacements(285), 0);
        Assert.True(result.Occluded == false, result.Reason);
    }

    [Fact]
    public void TerrainProjectionReusePreservesAlternatingCameraResultsWithinAllocationBudget()
    {
        using WorldAssets assets = new(TestResources.GameDirectory);
        NativeWorldFootCameraState forestCamera = new(26919, -28895, -608, 26867, -28862, -835, 639, -238, -341, -7168);
        NativeWorldFootCameraState positiveCamera = new(17850, -23780, 0, 17845, -23745, -259, 529, -112, -341, -7168);
        IReadOnlyDictionary<int, int> replacements = WorldTerrainSelection.Replacements(285);
        WorldNativeTerrainOcclusionResult expectedForest = new(false, 44, 2411, null);
        WorldNativeTerrainOcclusionResult expectedPositive = new(true, 34, 2081, null);
        Assert.Equal(expectedForest, assets.CertifyNativeTerrainCameraOcclusion(forestCamera, 928, replacements, 0));
        Assert.Equal(expectedPositive, assets.CertifyNativeTerrainCameraOcclusion(positiveCamera, 0, replacements, 0));
        for (int index = 0; index < 3; index++)
        {
            long started = GC.GetAllocatedBytesForCurrentThread();
            WorldNativeTerrainOcclusionResult forest = assets.CertifyNativeTerrainCameraOcclusion(forestCamera, 928, replacements, 0);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - started;
            Assert.Equal(expectedForest, forest);
            Assert.True(allocated <= 2_000_000, $"Warm terrain callback allocated {allocated} bytes.");
            Assert.Equal(expectedPositive, assets.CertifyNativeTerrainCameraOcclusion(positiveCamera, 0, replacements, 0));
        }
    }

    [Fact]
    public void ActualBalambFootCandidateUsesFloatStoresBeforeUnsignedFastClipping()
    {
        //Recorded b3ed5338 candidate: original4023D0 produces screenY
        //2033/1793/1988. All exceed the exclusive1792 admission limit.
        using WorldAssets assets = new(TestResources.GameDirectory);
        NativeWorldFootCameraState camera = new(17851, -23676, -8, 17847, -23646, -261, 532, -124, -341, -7168);
        WorldNativeTerrainOcclusionResult result = assets.CertifyNativeTerrainCameraOcclusion(camera, -8, WorldTerrainSelection.Replacements(285), 0);
        Assert.True(result.Occluded == false, result.Reason);
    }

    [Fact]
    public void OriginalBalambTerrainCanPublishPositiveOcclusionWithEveryAdmissionProven()
    {
        //A bounded native view of the reached map306/block0/polygon28.
        //This is a predicate fixture, not a claimed saved gameplay callback.
        using WorldAssets assets = new(TestResources.GameDirectory);
        NativeWorldFootCameraState camera = new(17850, -23780, 0, 17845, -23745, -259, 529, -112, -341, -7168);
        WorldNativeTerrainOcclusionResult result = assets.CertifyNativeTerrainCameraOcclusion(camera, 0, WorldTerrainSelection.Replacements(285), 0);
        Assert.True(result.Occluded == true, result.Reason);
    }

    [Fact]
    public void ActualFirstFootCallbackAfterBalambBeachDismountUsesNativeFastPolygonAdmission()
    {
        //Standalone1230: zero-input557A90 keeps yaw876/velocity0;552AD0
        //averages targets(16532,-24027,-439) toward body(16855,-23406,-37).
        using WorldAssets assets = new(TestResources.GameDirectory);
        NativeWorldFootCameraState camera = new(16855, -23406, -37, 16693, -23717, -403, 876, -112, -341, -7168);
        WorldNativeTerrainOcclusionResult result = assets.CertifyNativeTerrainCameraOcclusion(camera, -159, WorldTerrainSelection.Replacements(285), 0);
        Assert.True(result.Occluded == false, result.Reason);
    }

    [Fact]
    public void ActualRetainedMissileBaseCarAndFreshGardenViewsExcludeEveryPotentialNativeOccluder()
    {
        using WorldAssets assets = new(TestResources.GameDirectory);
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
