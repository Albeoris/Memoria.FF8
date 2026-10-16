using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeTerrainCellOcclusionTests
{
    [Fact]
    public void ActualEstharFootCameraAdmitsTheFartherGeneratedCellThroughNativeBoundTriangles()
    {
        using WorldAssets assets = new(@"C:\Steam\steamapps\common\FINAL FANTASY VIII");
        NativeWorldFootCameraState camera = new(48534, -2022, -448, 48556, -2000, -706, 163, -112, -341, -7168);
        WorldNativeTerrainOcclusionResult result = assets.CertifyNativeTerrainCameraOcclusion(camera, -448, WorldTerrainSelection.Replacements(321), 0);
        Assert.True(result.Occluded == true, result.Reason);
    }
}
