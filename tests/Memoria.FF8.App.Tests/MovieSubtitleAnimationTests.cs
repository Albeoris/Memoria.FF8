using Memoria.FF8.Media;

namespace Memoria.FF8.App.Tests;

public sealed class MovieSubtitleAnimationTests
{
    [Fact]
    public void Sparse_channels_interpolate_through_independent_keyframes()
    {
        MovieSubtitleKeyframe[] keyframes =
        [
            new() { Seconds = 0, Opacity = 0, Reveal = 0 },
            new() { Seconds = 1, Reveal = 1, Blur = 0.5f },
            new() { Seconds = 2, Opacity = 0.8f, ScaleY = 0, SplitX = 0.5f },
            new() { Seconds = 3, Blur = 0 }
        ];

        MovieSubtitleAnimation halfway = MovieSubtitleAnimation.At(keyframes, 0.5);
        Assert.Equal(0.2f, halfway.Opacity);
        Assert.Equal(0.5f, halfway.Reveal);
        Assert.Equal(0.25f, halfway.Blur);
        Assert.Equal(0.75f, halfway.ScaleY);
        Assert.Equal(0.125f, halfway.SplitX);
        Assert.Equal(1, halfway.ScaleX);
        Assert.Equal(0, halfway.OffsetY);

        MovieSubtitleAnimation later = MovieSubtitleAnimation.At(keyframes, 2.5);
        Assert.Equal(0.8f, later.Opacity);
        Assert.Equal(1, later.Reveal);
        Assert.Equal(0.125f, later.Blur);
        Assert.Equal(0, later.ScaleY);
        Assert.Equal(0.5f, later.SplitX);
    }

    [Fact]
    public void A_cue_without_keyframes_starts_as_plain_visible_text()
    {
        MovieSubtitleAnimation animation = MovieSubtitleAnimation.At([], 0);

        Assert.Equal(new(1, 1, 0, 0, 1, 1, 0, 0, 0, 1, 0, 0, 0), animation);
    }
}
