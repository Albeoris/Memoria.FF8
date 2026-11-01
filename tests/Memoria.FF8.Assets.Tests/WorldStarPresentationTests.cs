using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Rendering;

namespace Memoria.FF8.Assets.Tests;

public sealed class WorldStarPresentationTests
{
    [Fact]
    public void FixedRandomStreamRestartsForEveryStripAndUsesAlternatingHeightMasks()
    {
        byte[] random = Enumerable.Repeat((byte)255, 256).ToArray();
        random[1] = 11;
        random[2] = 100;
        random[3] = 23;
        random[4] = 100;
        WorldSkyGradient sky = new(1280, default, default, default);
        IReadOnlyList<WorldStarPoint> stars = WorldStarPresentation.Create(0, sky, 0, random, true);
        Assert.Equal(new Vector2(11, 100), stars[0].Position);
        Assert.Equal(new Vector2(23, 36), stars[1].Position);
        Assert.Contains(stars, star => star.Position == new Vector2(267, 100));
        Assert.Contains(stars, star => star.Position == new Vector2(279, 36));
        Assert.Equal((byte)11, random[1]);
    }

    [Fact]
    public void TwinkleUsesDiscreteCounterAndNativeByteColorWrites()
    {
        byte[] random = Enumerable.Repeat((byte)10, 256).ToArray();
        WorldSkyGradient sky = new(1280, default, default, default);
        IReadOnlyList<WorldStarPoint> first = WorldStarPresentation.Create(0, sky, 0, random, true);
        IReadOnlyList<WorldStarPoint> next = WorldStarPresentation.Create(0, sky, 2, random, true);
        Assert.Equal(new Rgba32(0, 120, 120, 255), first[0].Color);
        Assert.Equal(new Rgba32(123, 0, 123, 255), first[1].Color);
        Assert.Equal(new Rgba32(0, 108, 108, 255), next[0].Color);
        Assert.Equal(new Rgba32(144, 144, 144, 255), first[16].Color);
        Assert.Equal(first[16].Color, next[16].Color);
        Assert.Equal(new Rgba32(234, 234, 234, 255), first[31].Color);
    }

    [Fact]
    public void RegionalGateProcessModeAndClippingControlVisibility()
    {
        byte[] random = Enumerable.Repeat((byte)10, 256).ToArray();
        WorldSkyGradient sky = new(1280, default, default, default);
        Assert.Empty(WorldStarPresentation.Create(0, sky, 0, random, false));
        Assert.Empty(WorldStarPresentation.Create(0, sky, 0, random, true, 12));
        Assert.Empty(WorldStarPresentation.Create(0, sky with { HorizonEighthY = 0 }, 0, random, true));
        IReadOnlyList<WorldStarPoint> yaw = WorldStarPresentation.Create(64, sky, 0, random, true);
        Assert.All(yaw, star => Assert.Equal(202, star.Position.X));
        IReadOnlyList<WorldStarPoint> expanded = WorldStarPresentation.Create(64, sky, 0, random, true, left: -100, right: 420);
        Assert.Contains(expanded, star => star.Position.X == -54);
        Assert.All(yaw, star => Assert.Contains(star, expanded));
    }
}
