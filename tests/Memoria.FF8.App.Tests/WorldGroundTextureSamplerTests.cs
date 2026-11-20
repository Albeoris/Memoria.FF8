using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Rendering;

namespace Memoria.FF8.App.Tests;

public sealed class WorldGroundTextureSamplerTests
{
    [Fact]
    public void MagnificationInterpolatesBetweenTexelCenters()
    {
        Rgba32 black = new(0, 0, 0);
        Rgba32 white = new(255, 255, 255);
        PixelImage image = new(2, 1, [black, white]);
        WorldGroundTextureSampler sampler = WorldGroundTextureSampler.For(image);

        Assert.Equal(black, sampler.Sample(new(0.25f, 0.5f), 1, default, default, black));
        Assert.Equal(new Rgba32(128, 128, 128), sampler.Sample(new(0.5f, 0.5f), 1, default, default, white));
        Assert.Equal(white, sampler.Sample(new(0.75f, 0.5f), 1, default, default, white));
    }

    [Fact]
    public void MinificationIncludesTheOddEdgeAndKeepsTransparentColorOut()
    {
        Rgba32 red = new(255, 0, 0, 128);
        Rgba32 transparentGreen = new(0, 255, 0, 0);
        Rgba32 blue = new(0, 0, 255, 128);
        PixelImage image = new(3, 1, [red, transparentGreen, blue]);
        WorldGroundTextureSampler sampler = WorldGroundTextureSampler.For(image);
        Vector3 textureStepX = new(1, 0, 0);

        Assert.Equal(new Rgba32(128, 0, 128, 128), sampler.Sample(new(1 / 6f, 0.5f), 1, textureStepX, default, red));
        Assert.Equal(new Rgba32(128, 0, 128, 128), sampler.Sample(new(5 / 6f, 0.5f), 1, textureStepX, default, blue));
    }
}
