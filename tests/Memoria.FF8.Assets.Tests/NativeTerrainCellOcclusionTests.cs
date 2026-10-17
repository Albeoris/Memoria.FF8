using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeTerrainCellOcclusionTests
{
    [Fact]
    public void DeclaredFootDepartureExcludesCellsRejectedByBothNativeBoundTriangles()
    {
        using WorldAssets assets = new(TestResources.GameDirectory);
        NativeWorldFootCameraState camera = new(13249, -26779, -304, 13249, -26779, -560, 1536, -112, -341, -7168);
        WorldNativeTerrainOcclusionResult result = assets.CertifyNativeTerrainCameraOcclusion(camera, 304, WorldTerrainSelection.Replacements(285), 1);
        Assert.True(result.Occluded == false, result.Reason);
    }

    [Fact]
    public void ActualEstharRoadCameraExcludesTheOccluderCellOutsideNativeGeneration()
    {
        using WorldAssets assets = new(TestResources.GameDirectory);
        NativeWorldFootCameraState camera = new(56746, 13229, -400, 56749, 13197, -656, 2132, -124, -341, -7168);
        WorldNativeTerrainOcclusionResult result = assets.CertifyNativeTerrainCameraOcclusion(camera, -400, WorldTerrainSelection.Replacements(320), 0);
        Assert.True(result.Occluded == false, result.Reason);
    }

    [Fact]
    public void ActualEstharFootCameraAdmitsTheFartherGeneratedCellThroughNativeBoundTriangles()
    {
        using WorldAssets assets = new(TestResources.GameDirectory);
        NativeWorldFootCameraState camera = new(48534, -2022, -448, 48556, -2000, -706, 163, -112, -341, -7168);
        WorldNativeTerrainOcclusionResult result = assets.CertifyNativeTerrainCameraOcclusion(camera, -448, WorldTerrainSelection.Replacements(321), 0);
        Assert.True(result.Occluded == true, result.Reason);
    }
}
