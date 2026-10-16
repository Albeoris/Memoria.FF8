using Memoria.FF8.Events;

namespace Memoria.FF8.App.Tests;

public sealed class DialogueRevealTests
{
    [Fact]
    public void Field_speed_changes_reveal_rate_and_confirm_reveals_before_advancing()
    {
        DialogueVisualPage page = Page(new string('a', 120));
        DialoguePager slow = new();
        DialoguePager fast = new();
        slow.UpdateReveal(TimeSpan.FromSeconds(1), page, 1);
        fast.UpdateReveal(TimeSpan.FromSeconds(1), page, 5);
        Assert.Equal(15, slow.VisibleCharacters);
        Assert.Equal(90, fast.VisibleCharacters);
        Assert.True(slow.RevealCurrentPage(page));
        Assert.Equal(120, slow.VisibleCharacters);
        Assert.Equal(0, slow.PageIndex);
        Assert.False(slow.RevealCurrentPage(page));
        Assert.True(slow.Advance(2));
        Assert.Equal(0, slow.VisibleCharacters);
        Assert.Equal(1, slow.PageIndex);
    }

    [Fact]
    public void Revealing_text_preserves_unicode_graphemes_and_a_shared_budget_across_runs()
    {
        int remaining = 2;
        Assert.Equal("A👩🏽‍🚀", DialoguePager.VisiblePrefix("A👩🏽‍🚀B", ref remaining));
        Assert.Equal(0, remaining);
        Assert.Equal("", DialoguePager.VisiblePrefix("Next styled run", ref remaining));
    }

    [Fact]
    public void Name_draft_can_be_shown_immediately_and_popup_page_resets_reveal()
    {
        DialogueVisualPage page = Page("Name entry");
        DialoguePager pager = new();
        pager.UpdateReveal(TimeSpan.Zero, page, 1, instant: true);
        Assert.False(pager.RevealCurrentPage(page));
        Assert.Equal(10, pager.VisibleCharacters);
        pager.UpdatePopup(TimeSpan.FromSeconds(4), [page, page]);
        Assert.Equal(1, pager.PageIndex);
        Assert.Equal(0, pager.VisibleCharacters);
    }

    private static DialogueVisualPage Page(string text)
    {
        return DialogueLayout.Create(DialogueTextFormatter.Parse(text), new DialogueState(0, 1, text, 0, 0), null, 10, value => value.Length)[0];
    }

    [Fact]
    public void Slow_popup_pages_remain_visible_until_their_text_can_finish_revealing()
    {
        DialogueVisualPage page = Page(new string('a', 120));
        DialoguePager pager = new();
        pager.UpdateReveal(TimeSpan.FromSeconds(7), page, 1);
        pager.UpdatePopup(TimeSpan.FromSeconds(7), [page, page], DialoguePager.CharactersPerSecond(1));
        Assert.Equal(0, pager.PageIndex);
        Assert.Equal(105, pager.VisibleCharacters);
        pager.UpdateReveal(TimeSpan.FromSeconds(1), page, 1);
        pager.UpdatePopup(TimeSpan.FromSeconds(1), [page, page], DialoguePager.CharactersPerSecond(1));
        Assert.Equal(120, pager.VisibleCharacters);
        Assert.Equal(0, pager.PageIndex);
        pager.UpdatePopup(TimeSpan.FromSeconds(1), [page, page], DialoguePager.CharactersPerSecond(1));
        Assert.Equal(1, pager.PageIndex);
    }
}
