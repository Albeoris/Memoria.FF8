using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;

namespace Memoria.FF8.Assets.Tests;

public sealed class WorldSkyProjectionTests
{
    [Fact]
    public void FootHorizonUsesSeaLevelPointAndOriginalIntegerOffsetCorrection()
    {
        WorldSky sky = Colors();
        Vector3 player = new(24576, 658, -29406);
        WorldCamera camera = WorldCamera.Foot(0, 2048) with { TargetPosition = new Vector3(player.X, 914, player.Z) };
        WorldSkyGradient gradient = WorldSkyProjection.Foot(camera, player, sky);
        Assert.Equal(279, gradient.HorizonEighthY);
        Assert.Equal(-45.125f, gradient.CenterY);
        Assert.Equal(58.875f, gradient.BottomY);
        Assert.Equal(0, gradient.TopY);
        Assert.Equal(sky.Top, gradient.Top);
        Assert.Equal(sky.Center, gradient.Center);
        Assert.Equal(sky.Bottom, gradient.Bottom);
        Assert.Equal(712, WorldSkyProjection.ProjectHorizonEighthY(0, 20000));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(176)]
    [InlineData(1024)]
    [InlineData(2048)]
    [InlineData(3072)]
    [InlineData(4095)]
    public void FootHorizonIsYawIndependentWhenPlayerAndCameraTargetShareHorizontalPosition(int yaw)
    {
        Vector3 player = new(24576, 658, -29406);
        WorldCamera camera = WorldCamera.Foot(yaw, 2048) with { TargetPosition = new Vector3(player.X, 914, player.Z) };
        Assert.Equal(279, WorldSkyProjection.Foot(camera, player, Colors()).HorizonEighthY);
    }

    [Fact]
    public void ProjectionPreservesNativeNearDenominatorScreenBoundsAndHalfEvenRounding()
    {
        Assert.Equal(728, WorldSkyProjection.ProjectHorizonEighthY(1, 0));
        Assert.Equal(728, WorldSkyProjection.ProjectHorizonEighthY(1, -1));
        Assert.Equal(712, WorldSkyProjection.ProjectHorizonEighthY(0.5, 1024));
        Assert.Equal(728, WorldSkyProjection.ProjectHorizonEighthY(1.5, 1024));
        Assert.Equal(8000, WorldSkyProjection.ProjectHorizonEighthY(32767, 512));
        Assert.Equal(-8376, WorldSkyProjection.ProjectHorizonEighthY(-32768, 512));
        Assert.Throws<ArgumentOutOfRangeException>(() => WorldSkyProjection.ProjectHorizonEighthY(double.NaN, 1024));
        Assert.Throws<ArgumentException>(() => WorldSkyProjection.Foot(default, Vector3.Zero, Colors()));
    }

    [Fact]
    public void OriginalGradientBreaksInterpolateAllColorsAndSaturateOutsideVisibleBands()
    {
        var gradient = new WorldSkyGradient(960, new Rgba32(0, 255, 0, 4), new Rgba32(100, 55, 200, 5), new Rgba32(200, 255, 0, 6));
        Assert.Equal(40, gradient.CenterY);
        Assert.Equal(144, gradient.BottomY);
        Assert.Equal(new Rgba32(0, 255, 0, 255), gradient.Sample(-100));
        Assert.Equal(new Rgba32(50, 155, 100, 255), gradient.Sample(20));
        Assert.Equal(new Rgba32(100, 55, 200, 255), gradient.Sample(40));
        Assert.Equal(new Rgba32(150, 155, 100, 255), gradient.Sample(92));
        Assert.Equal(new Rgba32(200, 255, 0, 255), gradient.Sample(144));
        Assert.Equal(new Rgba32(200, 255, 0, 255), gradient.Sample(10000));

        WorldSkyGradient centerAboveScreen = gradient with { HorizonEighthY = 0 };
        Assert.Equal(-80, centerAboveScreen.CenterY);
        Assert.Equal(24, centerAboveScreen.BottomY);
        Assert.Equal(new Rgba32(177, 209, 46, 255), centerAboveScreen.Sample(0));
        WorldSkyGradient bottomAboveScreen = gradient with { HorizonEighthY = -1000 };
        Assert.Equal(new Rgba32(200, 255, 0, 255), bottomAboveScreen.Sample(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => gradient.Sample(double.PositiveInfinity));
    }

    private static WorldSky Colors()
    {
        return new WorldSky(Vector3.Zero, new Rgba32(10, 20, 30, 255), new Rgba32(40, 50, 60, 255), new Rgba32(70, 80, 90, 255));
    }
}
