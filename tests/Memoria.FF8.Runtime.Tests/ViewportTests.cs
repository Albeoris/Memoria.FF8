using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class ViewportTests
{
    [Theory]
    [InlineData(1280, 720, 2048, 1280, 720)]
    [InlineData(3840, 2160, 2048, 2048, 1152)]
    [InlineData(3840, 2160, 4096, 3840, 2160)]
    [InlineData(3440, 1440, 2048, 2048, 857)]
    [InlineData(720, 1280, 2048, 720, 504)]
    [InlineData(640, 480, 2048, 640, 448)]
    [InlineData(320, 224, 2048, 320, 224)]
    [InlineData(1237, 753, 2048, 1237, 753)]
    [InlineData(12000, 800, 64, 64, 4)]
    [InlineData(1, 1, 2048, 1, 1)]
    public void WorldTargetExpandsHorizontallyAndPreservesNativeVerticalScale(int width, int height, int limit, int expectedWidth, int expectedHeight)
    {
        PixelBounds target = ViewportLayout.WorldRenderTarget(width, height, limit, worldMap: true);
        Assert.Equal(new PixelBounds(0, 0, expectedWidth, expectedHeight), target);
        Assert.InRange(Math.Max(target.Width, target.Height), 1, limit);
    }

    [Fact]
    public void BattleTargetRetainsItsOriginalFourToThreeAspect()
    {
        Assert.Equal(new PixelBounds(0, 0, 960, 720), ViewportLayout.WorldRenderTarget(1280, 720));
        foreach ((int width, int height) in new[] { (640, 480), (1280, 720), (3840, 2160), (800, 1200) })
        {
            PixelBounds battle = ViewportLayout.WorldRenderTarget(width, height, 1024);
            Assert.True(Math.Abs(battle.Width - battle.Height * 4d / 3) <= 1);
            Assert.InRange(Math.Max(battle.Width, battle.Height), 1, 1024);
        }
    }

    [Theory]
    [InlineData(63)]
    [InlineData(4097)]
    public void WorldTargetRejectsUnsupportedResolutionLimits(int limit)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ViewportLayout.WorldRenderTarget(1280, 720, limit, worldMap: true));
    }

    [Theory]
    [InlineData(0, 720)]
    [InlineData(1280, 0)]
    [InlineData(-1, 720)]
    [InlineData(1280, -1)]
    public void WorldTargetRejectsNonpositiveViewportDimensions(int width, int height)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ViewportLayout.WorldRenderTarget(width, height, worldMap: true));
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
