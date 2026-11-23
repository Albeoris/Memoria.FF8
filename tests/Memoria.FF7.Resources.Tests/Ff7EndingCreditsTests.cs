namespace Memoria.FF7.Resources.Tests;

public sealed class Ff7EndingCreditsTests
{
    [Fact]
    public void InstalledEnglishCreditsRetainNativeGlyphLayoutAndScrollDuration()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(directory), "Select the original English FFVII installation.");

        Ff7EndingCredits credits = Ff7EndingCredits.Load(Ff7Installation.Discover(directory!));

        Assert.Equal(539, credits.Rows.Count);
        Assert.Equal(16045, credits.ScrollUpdateCount);
        Assert.Equal(new byte[] { 0x2F }, credits.Rows[0].Glyphs.ToArray());
        Assert.Equal(304, credits.Rows[0].CenteredX);
        Assert.Equal(80, credits.Rows[0].VerticalAdvance);
        Assert.Equal((byte)1, credits.Rows[6].Style);
        Assert.Equal(1, credits.Rows[6].DrawArgument);
        Assert.Equal(67, credits.Rows[6].CenteredX);
        Assert.Contains(credits.Rows, row => row.CenteredX == 0 && row.Glyphs.Span.Contains(Ff7EndingCredits.CenterCursor));
        Assert.Equal(30, credits.GlyphAdvance(0, 0));
        Assert.Equal(15, credits.GlyphAdvance(3, 0));
        Assert.NotSame(credits.Font(1), credits.Font(2));
        Assert.Same(credits.Font(3), credits.Font(5));
        Assert.True(credits.AfterCreditsTexture.Pixels.Length > 0);
    }
}
