using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class FramebufferCaptureCentreTests
{
    [Theory]
    [InlineData(6, 4, 0, 0, 6, 4, 3, 2)]
    [InlineData(7, 5, 1, 1, 5, 3, 3, 2)]
    [InlineData(7, 5, 0, 0, 7, 5, 4, 3)]
    [InlineData(7, 5, 6, 4, 1, 1, 5, 4)]
    public void DefaultBackendUsesTheExactIntegerCentreReader(int width, int height, int x, int y, int cropWidth, int cropHeight, int destinationWidth, int destinationHeight)
    {
        uint[] source = Enumerable.Range(0, width * height).Select(index => (uint)index).ToArray();
        IFramebufferCaptureRenderer capture = new FullCapture(source);
        Assert.True(capture.TryCaptureArgb(width, height, new(x, y, cropWidth, cropHeight), destinationWidth, destinationHeight, out uint[] actual));
        Assert.NotSame(source, actual);
        for (int row = 0; row < destinationHeight; row++)
        {
            for (int column = 0; column < destinationWidth; column++)
            {
                int expectedX = x + (int)((2L * column + 1) * cropWidth / (2L * destinationWidth));
                int expectedY = y + (int)((2L * row + 1) * cropHeight / (2L * destinationHeight));
                Assert.Equal(source[expectedY * width + expectedX], actual[row * destinationWidth + column]);
            }
        }
    }

    [Fact]
    public void TwoToOneCentreTiesSelectTheExactIntegerSourcePixel()
    {
        IFramebufferCaptureRenderer capture = new FullCapture(Enumerable.Range(0, 24).Select(index => (uint)index).ToArray());
        Assert.True(capture.TryCaptureArgb(6, 4, new(0, 0, 6, 4), 3, 2, out uint[] actual));
        Assert.Equal(new uint[] { 7, 9, 11, 19, 21, 23 }, actual);
    }

    [Fact]
    public void DefaultBackendRejectsIncompleteRasterAndInvalidBoundsAndNeverReturnsFailedPixels()
    {
        IFramebufferCaptureRenderer incomplete = new FullCapture(new uint[7]);
        Assert.Throws<InvalidDataException>(() => incomplete.TryCaptureArgb(4, 2, new(0, 0, 4, 2), 2, 1, out _));
        FullCapture backend = new(new uint[8]);
        IFramebufferCaptureRenderer capture = backend;
        Assert.Throws<InvalidDataException>(() => capture.TryCaptureArgb(4, 2, new(3, 0, 2, 2), 2, 1, out _));
        Assert.Equal(0, backend.Calls);
        IFramebufferCaptureRenderer failed = new FullCapture(new uint[] { 99 }, false);
        Assert.False(failed.TryCaptureArgb(4, 2, new(0, 0, 4, 2), 2, 1, out uint[] pixels));
        Assert.Empty(pixels);
    }

    private sealed class FullCapture(uint[] source, bool succeeds = true) : IFramebufferCaptureRenderer
    {
        internal int Calls { get; private set; }

        public bool TryCaptureArgb(out uint[] pixels)
        {
            Calls++;
            pixels = source;
            return succeeds;
        }
    }
}
