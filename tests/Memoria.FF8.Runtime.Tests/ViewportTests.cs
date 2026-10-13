using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class ViewportTests
{
    [Fact]
    public void OriginalWorldAspectUsesTenToSevenAcrossViewportAndRenderLimits()
    {
        Assert.Equal(new PixelBounds(0, 0, 1029, 720), ViewportLayout.WorldRenderTarget(1280, 720, worldMap: true));
        Assert.Equal(new PixelBounds(0, 0, 960, 720), ViewportLayout.WorldRenderTarget(1280, 720));
        foreach ((int width, int height) in new[] { (640, 480), (1280, 720), (3840, 2160), (800, 1200) })
        {
            PixelBounds world = ViewportLayout.WorldRenderTarget(width, height, 1024, true);
            Assert.True(Math.Abs(world.Width - world.Height * 10d / 7) <= 1);
            Assert.InRange(Math.Max(world.Width, world.Height), 1, 1024);
        }
    }

    [Fact]
    public void Field_and_movie_fit_widescreen_portrait_small_and_resized_viewports_without_stretching()
    {
        foreach ((int width, int height) in new[] { (1920, 1080), (800, 1200), (320, 240), (1237, 753), (3840, 2160), (1, 1) })
        {
            foreach ((int sourceWidth, int sourceHeight) in new[] { (320, 240), (1280, 896) })
            {
                PixelBounds fit = ViewportLayout.Fit(sourceWidth, sourceHeight, width, height);
                Assert.InRange(fit.X, 0, width);
                Assert.InRange(fit.Y, 0, height);
                Assert.InRange(fit.X + fit.Width, 1, width);
                Assert.InRange(fit.Y + fit.Height, 1, height);
                Assert.True(Math.Abs(fit.Width - fit.Height * (double)sourceWidth / sourceHeight) <= 1);
                Assert.True(fit.Width == width || fit.Height == height);
            }
            Assert.True(ViewportLayout.UiScale(width, height) > 0);
        }
        LaunchOptions options = LaunchOptions.Parse(["--resolution", "1920x1080"]);
        Assert.Equal(1920, options.Width);
        Assert.Equal(1080, options.Height);
        Assert.Throws<ArgumentException>(() => LaunchOptions.Parse(["--resolution", "0x0"]));
    }
}
