using System.Globalization;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;
using Microsoft.Xna.Framework;

namespace Memoria.FF8.App.Tests;

public sealed class DialogueLayoutTests
{
    private static float Width(string value) => new StringInfo(value).LengthInTextElements * 10;

    private static IReadOnlyList<DialogueVisualPage> Layout(string text, DialogueState? dialogue = null, DialogueWindow? window = null, float lineHeight = 10)
    {
        return DialogueLayout.Create(DialogueTextFormatter.Parse(text), dialogue ?? new DialogueState(0, 1, text, 20, 20), window, lineHeight, Width);
    }

    [Fact]
    public void Explicit_pages_shrink_individually_without_combining_short_pages()
    {
        var pages = Layout("A long opening line{Line}second line{Page}Short{Next}End", window: new DialogueWindow(0, 10, 10, 280, 100));
        Assert.Equal(3, pages.Count);
        Assert.Equal(new float[] { 20, 10, 10 }, pages.Select(page => page.Height));
        Assert.True(pages[0].Width > pages[1].Width);
        Assert.True(pages[1].Width > pages[2].Width);
    }

    [Fact]
    public void Horizontal_growth_moves_only_when_reaching_screen_padding_and_tightens_height()
    {
        var dialogue = new DialogueState(0, 1, "", 200, 210);
        var bounds = DialoguePresentation.Bounds(dialogue, new DialogueWindow(0, 200, 210, 100, 150), new PixelBounds(0, 0, 320, 240), 180, 30);
        Assert.Equal(new PixelBounds(116, 186, 196, 46), bounds);
        var anchored = DialoguePresentation.Bounds(dialogue with { X = 12, Y = 16 }, null, new PixelBounds(0, 0, 320, 240), 100, 60);
        Assert.Equal(new PixelBounds(12, 16, 116, 76), anchored);
    }

    [Fact]
    public void Long_vertical_text_grows_to_eight_lines_then_keeps_explicit_boundary()
    {
        string source = string.Join("{Line}", Enumerable.Range(0, 19).Select(index => "Line " + index)) + "{Page}Final";
        var pages = Layout(source);
        Assert.Equal(new[] { 8, 8, 3, 1 }, pages.Select(page => page.Lines.Count));
    }

    [Fact]
    public void Script_request_above_eight_lines_is_honored_but_screen_height_limits_it()
    {
        string source = string.Join("{Line}", Enumerable.Range(0, 22));
        var pages = Layout(source, window: new DialogueWindow(0, 0, 0, 100, 136));
        Assert.Equal(new[] { 12, 10 }, pages.Select(page => page.Lines.Count));
        var largeFont = Layout(source, window: new DialogueWindow(0, 0, 0, 100, 500), lineHeight: 50);
        Assert.Equal(4, largeFont[0].Lines.Count);
        Assert.All(largeFont, page => Assert.True(page.Height + 16 <= 224));
    }

    [Fact]
    public void Speaker_repeats_with_separation_and_quote_hanging_indent_survives_wrapping()
    {
        var pages = Layout("Squall{Line}«" + new string('x', 300) + "{Line}  aligned»");
        Assert.True(pages.Count > 1);
        Assert.All(pages, page => Assert.Equal("Squall", page.Speaker));
        var lines = pages.SelectMany(page => page.Lines).ToArray();
        Assert.Equal(0, lines[0].Runs[0].X);
        Assert.All(lines.Skip(1), line => Assert.Equal(Width("«"), line.Runs[0].X));
        Assert.Equal("aligned»", string.Concat(lines[^1].Runs.Select(run => run.Run.Text)));
        Assert.Equal(10 + DialogueLayout.SpeakerGap + pages[0].Lines.Count * 10, pages[0].Height);
    }

    [Fact]
    public void Choices_keep_native_line_numbers_never_wrap_and_stay_together_even_offscreen()
    {
        string intro = string.Join("{Line}", Enumerable.Range(0, 9).Select(index => "Intro " + index));
        string option = new('q', 60);
        string source = intro + "{Line}" + string.Join("{Line}", Enumerable.Range(0, 24).Select(index => option + index));
        var dialogue = new DialogueState(0, 1, source, 30, 40, FirstChoice: 9, LastChoice: 32);
        var pages = Layout(source, dialogue);
        Assert.Equal(3, pages.Count);
        Assert.Equal(new[] { 8, 1, 24 }, pages.Select(page => page.Lines.Count));
        Assert.All(pages[^1].Lines, line => Assert.True(line.IsChoice));
        Assert.Equal(Enumerable.Range(9, 24), pages[^1].Lines.Select(line => line.SourceLineIndex));
        Assert.All(pages[^1].Lines, line => Assert.True(line.Width > 288));
        Assert.True(pages[^1].Height > 224);
    }

    [Fact]
    public void Unicode_graphemes_and_semantic_styles_survive_automatic_pagination()
    {
        const string cluster = "👩🏽‍🚀";
        string source = "{Color Red}" + string.Concat(Enumerable.Repeat(cluster, 250));
        var pages = Layout(source);
        Assert.True(pages.Count > 1);
        Assert.Equal(string.Concat(Enumerable.Repeat(cluster, 250)), string.Concat(pages.SelectMany(page => page.Lines).SelectMany(line => line.Runs).Select(run => run.Run.Text)));
        Assert.All(pages.SelectMany(page => page.Lines).SelectMany(line => line.Runs), run => Assert.NotEqual(Color.White, run.Run.Color));
    }

    [Fact]
    public void Choices_across_authored_boundaries_are_deferred_together_until_all_body_pages_are_read()
    {
        string source = "Prompt{Line}Yes{Page}Details{Line}No{Next}Explanation";
        var dialogue = new DialogueState(0, 1, source, 0, 0, FirstChoice: 1, LastChoice: 3);
        // Native choices are a contiguous range, including the intervening authored line.
        var pages = Layout(source, dialogue);
        Assert.Equal(3, pages.Count);
        Assert.Equal("Prompt", string.Concat(pages[0].Lines.SelectMany(line => line.Runs).Select(run => run.Run.Text)));
        Assert.False(pages[0].HasChoices);
        Assert.False(pages[1].HasChoices);
        Assert.True(pages[^1].HasChoices);
        Assert.Equal(new[] { 1, 2, 3 }, pages[^1].Lines.Where(line => line.IsChoice).Select(line => line.SourceLineIndex));
        Assert.Equal("Explanation", string.Concat(pages[^1].Lines[0].Runs.Select(run => run.Run.Text)));
    }

    [Fact]
    public void Large_speaker_heading_fits_screen_without_forcing_body_font_smaller()
    {
        string source = "VeryLongSpeakerName{Line}“Body.”";
        var pages = DialogueLayout.Create(DialogueTextFormatter.Parse(source), new DialogueState(0, 1, source, 0, 0), null, 55, value => Width(value) * 5);
        Assert.All(pages, page =>
        {
            Assert.InRange(page.Width, 0, 288);
            Assert.InRange(page.SpeakerScale, 0, 1);
            Assert.True(page.Height + 16 <= 224);
            Assert.Equal(page.Lines.Count * 55 + 55 * page.SpeakerScale + DialogueLayout.SpeakerGap, page.Height, 3);
        });
    }

    [Fact]
    public void Name_draft_is_literal_unicode_and_cannot_inject_message_tags()
    {
        var original = DialogueTextFormatter.Parse("Name:{Page}Enter name");
        var document = original.WithLiteralLine("{Color Red}👩🏽‍🚀");
        Assert.Equal(2, document.Pages.Count);
        var input = document.Pages[^1].Lines[^1];
        Assert.Equal("{Color Red}👩🏽‍🚀", input.Text);
        Assert.Equal(Color.White, Assert.Single(input.Runs).Color);
        Assert.Equal(2, input.SourceLineIndex);
        Assert.Single(original.Pages[^1].Lines);
    }

    [Fact]
    public void All_confirmable_channels_advance_before_native_confirmation_and_popups_do_not_consume_it()
    {
        var first = new DialoguePager();
        var second = new DialoguePager();
        var popup = new DialoguePager();
        var channels = new[]
        {
            (new DialogueState(0, 1, "", 0, 0), first, 2),
            (new DialogueState(1, 2, "", 0, 0), second, 3),
            (new DialogueState(2, 3, "", 0, 0, CanConfirm: false), popup, 5)
        };
        Assert.True(DialoguePager.AdvanceConfirmable(channels));
        Assert.Equal(1, first.PageIndex);
        Assert.Equal(1, second.PageIndex);
        Assert.Equal(0, popup.PageIndex);
        Assert.True(DialoguePager.AdvanceConfirmable(channels));
        Assert.Equal(1, first.PageIndex);
        Assert.Equal(2, second.PageIndex);
        Assert.False(DialoguePager.AdvanceConfirmable(channels));
    }

    [Fact]
    public void Popup_cycles_readable_pages_without_changing_native_ownership_and_replacement_resets()
    {
        var pages = Layout("First{Page}Second");
        var popup = new DialoguePager();
        popup.UpdatePopup(TimeSpan.FromSeconds(2.9), pages);
        Assert.Equal(0, popup.PageIndex);
        popup.UpdatePopup(TimeSpan.FromSeconds(0.2), pages);
        Assert.Equal(1, popup.PageIndex);
        popup.UpdatePopup(TimeSpan.FromSeconds(3), pages);
        Assert.Equal(0, popup.PageIndex);
        var replacement = new DialoguePager();
        Assert.Equal(0, replacement.PageIndex);
    }

    [Fact]
    public void Blink_is_periodic_visible_and_does_not_depend_on_simulation_frames()
    {
        Assert.Equal(1, DialogueBlink.Opacity(0), 5);
        Assert.Equal(0.55f, DialogueBlink.Opacity(0.5), 5);
        Assert.Equal(DialogueBlink.Opacity(0.125), DialogueBlink.Opacity(100.125), 5);
    }
}
