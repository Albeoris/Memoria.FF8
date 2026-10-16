using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class WorldTrainRouteExclusionTests
{
    [Theory]
    [InlineData(0, 63, -28130, -17477, 6490, 43516)]
    [InlineData(1, 77, -61703, -21000, 5871, 32258)]
    public void RouteEnvelopeMatchesTheOriginalRailResource(int route, int count, int minimumX, int maximumX, int minimumZ, int maximumZ)
    {
        using WorldAssets assets = new(TestResources.GameDirectory);
        NativeWorldScriptedPathDefinition definition = assets.ReadScriptedPath(route);
        Assert.Equal(count, definition.Points.Length);
        Assert.Equal(minimumX, definition.Points.Min(point => point.X));
        Assert.Equal(maximumX, definition.Points.Max(point => point.X));
        Assert.Equal(minimumZ, definition.Points.Min(point => point.Z));
        Assert.Equal(maximumZ, definition.Points.Max(point => point.Z));
    }

    [Fact]
    public void CentraGardenDismountExcludesTrainsThroughNativeZEvenWhenXOverlaps()
    {
        NativeWorldFootCameraState camera = new(-24150, 66103, -452, -24150, 66103, -724, 69, -112, -341, -7168);
        Assert.False(WorldFootContactCertificate.ExcludesOriginalTrainRoutes(-24150));
        Assert.True(WorldFootContactCertificate.ExcludesOriginalTrainRoutes(NativeWorldPathProjectionCertificate.CreateFoot(camera)));
    }

    [Theory]
    [InlineData(-131072, 98303)]
    [InlineData(131071, -98304)]
    [InlineData(24576, -29406)]
    [InlineData(-24150, 66103)]
    public void SeparatedWrappedCameraRegionsRemainExcludedForAllCardinalYaws(int x, int rawZ)
    {
        foreach (int yaw in new[] { 0, 1024, 2048, 3072 })
        {
            Assert.True(WorldFootContactCertificate.ExcludesOriginalTrainRoutes(Projection(x, rawZ, yaw)));
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1024)]
    [InlineData(2048)]
    [InlineData(3072)]
    public void ACameraNearAnyOriginalRoutePointCannotInventTrainAbsence(int yaw)
    {
        using WorldAssets assets = new(TestResources.GameDirectory);
        foreach (int route in new[] { 0, 1 })
        {
            foreach (NativeWorldRailPoint point in assets.ReadScriptedPath(route).Points)
            {
                Assert.False(WorldFootContactCertificate.ExcludesOriginalTrainRoutes(Projection(point.X, -point.Z, yaw)));
            }
        }
    }

    private static NativeWorldPathProjectionCertificate Projection(int x, int rawZ, int yaw)
    {
        return NativeWorldPathProjectionCertificate.CreateFoot(new NativeWorldFootCameraState(x, rawZ, -452, x, rawZ, -724, yaw, -112, -341, -7168));
    }
}
