using Memoria.FF8.Events;

namespace Memoria.FF8.App.Tests;

public sealed class DialoguePageCacheTests
{
    private static IReadOnlyList<DialogueVisualPage> CreatePages()
    {
        return [new DialogueVisualPage(null, [], 10, 10, false), new DialogueVisualPage(null, [], 10, 10, false)];
    }

    private static (IReadOnlyList<DialogueVisualPage> Pages, DialoguePager Pager) Get(DialoguePageCache cache, DialogueState dialogue, bool valueIdentity = false)
    {
        return cache.Get(dialogue, null, "Arial", 8, dialogue.Text, valueIdentity, CreatePages);
    }

    [Fact]
    public void Same_field_opening_retains_pages_and_pager_without_recreating_layout()
    {
        var cache = new DialoguePageCache();
        var dialogue = new DialogueState(0, 1, "First{Page}Second", 8, 8);
        var first = Get(cache, dialogue);
        Assert.True(first.Pager.Advance(first.Pages.Count));

        var repeated = cache.Get(dialogue, null, "Arial", 8, dialogue.Text, false, () => throw new InvalidOperationException("The same opening must retain its layout."));

        Assert.Same(first.Pages, repeated.Pages);
        Assert.Same(first.Pager, repeated.Pager);
        Assert.Equal(1, repeated.Pager.PageIndex);
    }

    [Fact]
    public void Equal_reopened_field_dialogue_starts_at_first_page()
    {
        var cache = new DialoguePageCache();
        var dialogue = new DialogueState(0, 1, "First{Page}Second", 8, 8);
        var first = Get(cache, dialogue);
        Assert.True(first.Pager.Advance(first.Pages.Count));
        DialogueState reopened = dialogue with { };
        Assert.Equal(dialogue, reopened);
        Assert.NotSame(dialogue, reopened);

        var replacement = Get(cache, reopened);

        Assert.NotSame(first.Pages, replacement.Pages);
        Assert.NotSame(first.Pager, replacement.Pager);
        Assert.Equal(0, replacement.Pager.PageIndex);
    }

    [Fact]
    public void Equal_reopened_popup_restarts_its_reading_timer()
    {
        var cache = new DialoguePageCache();
        var dialogue = new DialogueState(0, 1, "First{Page}Second", 8, 8, CanConfirm: false);
        var first = Get(cache, dialogue);
        first.Pager.UpdatePopup(TimeSpan.FromSeconds(2.9), first.Pages);

        var replacement = Get(cache, dialogue with { });
        replacement.Pager.UpdatePopup(TimeSpan.FromSeconds(0.2), replacement.Pages);

        Assert.NotSame(first.Pager, replacement.Pager);
        Assert.Equal(0, replacement.Pager.PageIndex);
        replacement.Pager.UpdatePopup(TimeSpan.FromSeconds(2.8), replacement.Pages);
        Assert.Equal(1, replacement.Pager.PageIndex);
    }

    [Fact]
    public void Computed_equal_world_dialogues_retain_their_current_page()
    {
        var cache = new DialoguePageCache();
        var dialogue = new DialogueState(0, 4, "Station{Page}Leave?", 8, 8, 1, 2, 1, 2);
        var first = Get(cache, dialogue, valueIdentity: true);
        Assert.True(first.Pager.Advance(first.Pages.Count));

        var computedAgain = Get(cache, dialogue with { }, valueIdentity: true);

        Assert.Same(first.Pager, computedAgain.Pager);
        Assert.Equal(1, computedAgain.Pager.PageIndex);
    }

    [Theory]
    [InlineData("variable")]
    [InlineData("name")]
    [InlineData("family")]
    [InlineData("size")]
    [InlineData("window")]
    [InlineData("identity")]
    public void Presentation_changes_recreate_layout_and_restart_paging(string change)
    {
        var cache = new DialoguePageCache();
        var dialogue = new DialogueState(0, 1, "First{Page}Second", 8, 8);
        var first = Get(cache, dialogue);
        Assert.True(first.Pager.Advance(first.Pages.Count));
        string expandedKey = change switch
        {
            "variable" => "First: 2{Page}Second",
            "name" => dialogue.Text + "\u0001Squall=Leon",
            _ => dialogue.Text
        };
        DialogueWindow? window = change == "window" ? new DialogueWindow(0, 8, 8, 200, 80) : null;

        var replacement = cache.Get(dialogue, window, change == "family" ? "Consolas" : "Arial", change == "size" ? 12 : 8, expandedKey, change == "identity", CreatePages);

        Assert.NotSame(first.Pager, replacement.Pager);
        Assert.Equal(0, replacement.Pager.PageIndex);
    }

    [Fact]
    public void Native_completion_invalidates_confirmable_channels_and_retains_persistent_popups()
    {
        var cache = new DialoguePageCache();
        var firstDialogue = new DialogueState(0, 1, "First", 8, 8);
        var secondDialogue = new DialogueState(1, 2, "Second", 8, 8);
        var popupDialogue = new DialogueState(2, 3, "Popup", 8, 8, CanConfirm: false);
        var choiceDialogue = new DialogueState(3, 4, "Choice", 8, 8, FirstChoice: 1, LastChoice: 2);
        var first = Get(cache, firstDialogue);
        var second = Get(cache, secondDialogue);
        var popup = Get(cache, popupDialogue);
        var choice = Get(cache, choiceDialogue);
        popup.Pager.UpdatePopup(TimeSpan.FromSeconds(2.9), popup.Pages);

        cache.Complete(firstDialogue);

        Assert.NotSame(first.Pager, Get(cache, firstDialogue).Pager);
        Assert.NotSame(second.Pager, Get(cache, secondDialogue).Pager);
        Assert.Same(choice.Pager, Get(cache, choiceDialogue).Pager);
        var retainedPopup = Get(cache, popupDialogue);
        Assert.Same(popup.Pager, retainedPopup.Pager);
        retainedPopup.Pager.UpdatePopup(TimeSpan.FromSeconds(0.2), retainedPopup.Pages);
        Assert.Equal(1, retainedPopup.Pager.PageIndex);
        var other = Get(cache, firstDialogue);
        cache.Complete(choiceDialogue);
        Assert.NotSame(choice.Pager, Get(cache, choiceDialogue).Pager);
        Assert.Same(other.Pager, Get(cache, firstDialogue).Pager);
    }

    [Fact]
    public void Retaining_visible_channels_drops_only_closed_channels()
    {
        var cache = new DialoguePageCache();
        var retainedDialogue = new DialogueState(0, 1, "Retain", 8, 8);
        var closedDialogue = new DialogueState(1, 2, "Close", 8, 8);
        var retained = Get(cache, retainedDialogue);
        var closed = Get(cache, closedDialogue);

        cache.RetainChannels([retainedDialogue.Channel]);

        Assert.Same(retained.Pager, Get(cache, retainedDialogue).Pager);
        Assert.NotSame(closed.Pager, Get(cache, closedDialogue).Pager);
    }

    [Fact]
    public void Clearing_cache_starts_a_fresh_presentation_for_the_same_opening()
    {
        var cache = new DialoguePageCache();
        var dialogue = new DialogueState(0, 1, "First", 8, 8);
        var first = Get(cache, dialogue);

        cache.Clear();

        Assert.NotSame(first.Pager, Get(cache, dialogue).Pager);
    }
}
