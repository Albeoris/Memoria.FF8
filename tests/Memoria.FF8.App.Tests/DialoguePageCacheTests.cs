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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Native_confirmation_retains_the_last_page_and_revealed_text_until_the_window_closes(bool choice)
    {
        var cache = new DialoguePageCache();
        var window = new NativeDialogueWindowState(1, 4096, 0);
        var dialogue = new DialogueState(0, 1, "First{Page}Second", 8, 8, NativeWindow: window);
        if (choice)
        {
            dialogue = dialogue with { FirstChoice = 0, LastChoice = 1 };
        }
        IReadOnlyList<DialogueVisualPage> CreateTextPages() => DialogueLayout.Create(DialogueTextFormatter.Parse(dialogue.Text), dialogue, null, 10, value => value.Length * 5);
        var first = cache.Get(dialogue, null, "Arial", 8, dialogue.Text, false, CreateTextPages);
        Assert.True(first.Pager.Advance(first.Pages.Count));
        first.Pager.RevealCurrentPage(first.Pages[1]);
        int revealed = first.Pager.VisibleCharacters;
        Assert.True(revealed > 0);

        cache.Complete(dialogue);
        var closing = dialogue with { CanConfirm = false };
        window.Delta = -512;
        for (int progress = 3072; progress > 0; progress -= 1024)
        {
            window.Progress = progress;
            cache.RetainChannels([dialogue.Channel]);
            var retained = cache.Get(closing, null, "Arial", 8, dialogue.Text, false, CreateTextPages);
            Assert.Same(first.Pager, retained.Pager);
            Assert.Equal(1, retained.Pager.PageIndex);
            Assert.Equal(revealed, retained.Pager.VisibleCharacters);
        }

        cache.RetainChannels([]);
        Assert.Null(cache.ReadPager(dialogue.Channel));
    }

    [Fact]
    public void Native_message_sync_and_simultaneous_confirmation_preserve_each_physical_window()
    {
        var cache = new DialoguePageCache();
        var automatic = new DialogueState(0, 1, "First{Page}Second", 8, 8, NativeWindow: new NativeDialogueWindowState(1, 4096, 0));
        var popup = automatic with { Channel = 1, CanConfirm = false, NativeWindow = new NativeDialogueWindowState(1, 4096, 0) };
        var first = Get(cache, automatic);
        var second = Get(cache, popup);
        Assert.True(first.Pager.Advance(first.Pages.Count));
        Assert.True(second.Pager.Advance(second.Pages.Count));

        var synchronized = popup with { CanConfirm = true };
        Assert.Same(second.Pager, Get(cache, synchronized).Pager);
        cache.Complete(automatic);

        Assert.Same(first.Pager, Get(cache, automatic with { CanConfirm = false }).Pager);
        Assert.Same(second.Pager, Get(cache, synchronized with { CanConfirm = false }).Pager);
        Assert.Equal(1, first.Pager.PageIndex);
        Assert.Equal(1, second.Pager.PageIndex);
    }

    [Fact]
    public void Equal_native_window_reopened_on_the_same_channel_starts_a_new_presentation()
    {
        var cache = new DialoguePageCache();
        var dialogue = new DialogueState(0, 1, "First{Page}Second", 8, 8, NativeWindow: new NativeDialogueWindowState(1, 4096, 0));
        var first = Get(cache, dialogue);
        Assert.True(first.Pager.Advance(first.Pages.Count));
        cache.Complete(dialogue);
        var reopened = dialogue with { NativeWindow = dialogue.NativeWindow! with { } };
        Assert.Equal(dialogue, reopened);

        var replacement = Get(cache, reopened);

        Assert.NotSame(first.Pager, replacement.Pager);
        Assert.Equal(0, replacement.Pager.PageIndex);
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

    [Fact]
    public void Editing_a_name_draft_refreshes_text_without_jumping_back_to_the_first_page()
    {
        var cache = new DialoguePageCache();
        var dialogue = new DialogueState(0, 1, "Instruction{Page}Name", 8, 8, IsNamePrompt: true);
        var first = cache.Get(dialogue, null, "Arial", 8, "Leon", false, CreatePages, retainPosition: true);
        Assert.True(first.Pager.Advance(first.Pages.Count));
        var edited = cache.Get(dialogue, null, "Arial", 8, "Cecilia", false, CreatePages, retainPosition: true);
        Assert.NotSame(first.Pages, edited.Pages);
        Assert.Equal(1, edited.Pager.PageIndex);
        var reopened = cache.Get(dialogue with { }, null, "Arial", 8, "Cecilia", false, CreatePages, retainPosition: true);
        Assert.Equal(0, reopened.Pager.PageIndex);
    }
}
