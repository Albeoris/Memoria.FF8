using Microsoft.Xna.Framework;

namespace Memoria.FF8.Tests;

public sealed class SpriteRegionDrawingTests
{
    [Fact]
    public void AtlasRegionUsesItsOwnOriginAndIndependentViewportScales()
    {
        Assert.True(SpriteRegionDrawing.TryClip(new(100, 200, 20, 10), new(10.5f, 20.5f), new(1.5f, 2), new(16, 23, 15, 12), out Rectangle source, out Vector2 position));
        Assert.Equal(new Rectangle(104, 202, 9, 5), source);
        Assert.Equal(new Vector2(16.5f, 24.5f), position);
    }

    [Fact]
    public void UniformWholeTextureRetainsTheExistingTextClipCoordinates()
    {
        Assert.True(SpriteRegionDrawing.TryClip(new(0, 0, 40, 20), new(10, 20), new(2), new(18, 26, 24, 18), out Rectangle source, out Vector2 position));
        Assert.Equal(new Rectangle(4, 3, 12, 9), source);
        Assert.Equal(new Vector2(18, 26), position);
    }

    [Fact]
    public void SubpixelWindowCannotExposeAPartiallyClippedTexel()
    {
        Assert.False(SpriteRegionDrawing.TryClip(new(10, 20, 1, 1), new(10, 10), new(2), new(11, 10, 1, 2), out _, out _));
    }
}
