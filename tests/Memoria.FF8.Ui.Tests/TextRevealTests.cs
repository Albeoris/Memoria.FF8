using Memoria.FF8.Ui;

namespace Memoria.FF8.Ui.Tests;

public sealed class TextRevealTests
{
    [Fact]
    public void Fractional_progress_accumulates_and_can_pause_finish_or_reset()
    {
        UiTextReveal reveal = new();
        reveal.Update(TimeSpan.FromMilliseconds(100), 10, 5);
        Assert.Equal(0, reveal.VisibleCharacters);
        reveal.Update(TimeSpan.FromMilliseconds(100), 10, 5);
        Assert.Equal(1, reveal.VisibleCharacters);
        reveal.Update(TimeSpan.FromSeconds(10), 10, 0);
        Assert.Equal(1, reveal.VisibleCharacters);
        Assert.True(reveal.RevealAll(10));
        Assert.Equal(10, reveal.VisibleCharacters);
        Assert.False(reveal.RevealAll(10));
        reveal.Reset();
        Assert.Equal(0, reveal.VisibleCharacters);
        reveal.Update(TimeSpan.Zero, 10, 5, instant: true);
        Assert.Equal(10, reveal.VisibleCharacters);
    }

    [Fact]
    public void Styled_runs_share_a_budget_without_splitting_combining_or_emoji_sequences()
    {
        const string text = "Ae\u0301👩🏽‍🚀B";
        Assert.Equal(4, UiTextReveal.CharacterCount(text));
        int remaining = 3;
        Assert.Equal("A", UiTextReveal.VisiblePrefix("A", ref remaining));
        Assert.Equal("e\u0301👩🏽‍🚀", UiTextReveal.VisiblePrefix(text[1..], ref remaining));
        Assert.Equal("", UiTextReveal.VisiblePrefix("Next run", ref remaining));
        Assert.Equal(0, remaining);
    }

    [Fact]
    public void Progress_clamps_to_content_and_negative_elapsed_time_does_not_rewind()
    {
        UiTextReveal reveal = new();
        reveal.Update(TimeSpan.FromSeconds(1), 10, 5);
        reveal.Update(TimeSpan.FromSeconds(-1), 10, 5);
        Assert.Equal(5, reveal.VisibleCharacters);
        reveal.Update(TimeSpan.Zero, 2, 5);
        Assert.Equal(2, reveal.VisibleCharacters);
        reveal.Update(TimeSpan.MaxValue, 2, double.MaxValue);
        Assert.Equal(2, reveal.VisibleCharacters);
    }

    [Fact]
    public void Invalid_counts_rates_and_budgets_are_rejected()
    {
        UiTextReveal reveal = new();
        Assert.Throws<ArgumentOutOfRangeException>(() => reveal.Update(TimeSpan.Zero, -1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => reveal.Update(TimeSpan.Zero, 1, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => reveal.Update(TimeSpan.Zero, 1, double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => reveal.RevealAll(-1));
        int remaining = -1;
        Assert.Throws<ArgumentOutOfRangeException>(() => UiTextReveal.VisiblePrefix("Text", ref remaining));
    }
}
