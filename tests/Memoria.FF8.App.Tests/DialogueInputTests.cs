using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class DialogueInputTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Cancel_moves_to_the_authored_row_and_waits_for_confirm(int cancelChoice)
    {
        DialogueState dialogue = new(0, 10, "Choose", 0, 0, FirstChoice: 1, LastChoice: 3, InitialChoice: 1, CancelChoice: cancelChoice);
        int selected = 1;
        InputFrame cancel = new(0, 0, false, true, false, false);

        Assert.Null(DialoguePresentation.ResolveInput(dialogue, cancel, ref selected, ""));
        Assert.Equal(cancelChoice, selected);
        Assert.Null(DialoguePresentation.ResolveInput(dialogue, cancel, ref selected, ""));
        Assert.Equal(cancelChoice, selected);

        PlayerAction? action = DialoguePresentation.ResolveInput(dialogue, new InputFrame(0, 0, true, false, false, false), ref selected, "");
        Assert.Equal(new PlayerAction(PlayerActionKind.Choose, cancelChoice), action);
    }

    [Fact]
    public void Choice_without_cancel_row_ignores_cancel_and_still_allows_navigation()
    {
        DialogueState dialogue = new(0, 10, "Choose", 0, 0, FirstChoice: 3, LastChoice: 6);
        int selected = 4;
        Assert.Null(DialoguePresentation.ResolveInput(dialogue, new InputFrame(0, 0, false, true, false, false), ref selected, ""));
        Assert.Equal(4, selected);
        Assert.Null(DialoguePresentation.ResolveInput(dialogue, new InputFrame(0, 0, false, false, false, false, MenuVertical: 1), ref selected, ""));
        Assert.Equal(5, selected);
    }

    [Fact]
    public void Player_can_move_away_from_cancel_row_before_confirming()
    {
        DialogueState dialogue = new(0, 10, "Choose", 0, 0, FirstChoice: 1, LastChoice: 3, CancelChoice: 3);
        int selected = 1;
        Assert.Null(DialoguePresentation.ResolveInput(dialogue, new InputFrame(0, 0, false, true, false, false), ref selected, ""));
        Assert.Null(DialoguePresentation.ResolveInput(dialogue, new InputFrame(0, 0, false, false, false, false, MenuVertical: -1), ref selected, ""));
        Assert.Equal(new PlayerAction(PlayerActionKind.Choose, 2), DialoguePresentation.ResolveInput(dialogue, new InputFrame(0, 0, true, false, false, false), ref selected, ""));
    }

    [Fact]
    public void Confirm_takes_priority_over_simultaneous_cancel()
    {
        DialogueState dialogue = new(0, 10, "Choose", 0, 0, FirstChoice: 1, LastChoice: 3, CancelChoice: 3);
        int selected = 2;
        Assert.Equal(new PlayerAction(PlayerActionKind.Choose, 2), DialoguePresentation.ResolveInput(dialogue, new InputFrame(0, 0, true, true, false, false), ref selected, ""));
    }

    [Fact]
    public void Plain_messages_keep_cancel_and_name_prompts_require_confirm()
    {
        DialogueState dialogue = new(0, 10, "Message", 0, 0);
        int selected = 0;
        InputFrame cancel = new(0, 0, false, true, false, false);
        Assert.Equal(new PlayerAction(PlayerActionKind.Cancel), DialoguePresentation.ResolveInput(dialogue, cancel, ref selected, ""));
        Assert.Null(DialoguePresentation.ResolveInput(dialogue with { IsNamePrompt = true }, cancel, ref selected, "Squall"));
        Assert.Null(DialoguePresentation.ResolveInput(dialogue with { CanConfirm = false }, cancel, ref selected, ""));
    }
}