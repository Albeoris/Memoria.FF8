using Memoria.FF8.Rendering;

namespace Memoria.FF8.App.Tests;

public sealed class NativeCarParticleTileTests
{
    [Theory]
    [InlineData(4096, 1, 0, 8)]
    [InlineData(8191, 1, 0, 8)]
    [InlineData(8192, 1, -1, 2)]
    [InlineData(4096, 255, -127, 254)]
    public void NativeScreenTileKeepsItsZeroSizeFallbackAndUnprojectedExtent(ushort scale, short halfSize, int offset, int size)
    {
        Assert.Equal((offset, size), NativeWorldParticleTile.Dimensions(scale, halfSize));
    }
}
