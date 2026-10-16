using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class OpeningDialoguePagingTests
{
    [Fact]
    public void Opening_doctor_message_keeps_its_advanced_page_until_native_confirmation()
    {
        string game = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(game), "Installed original game corpus is required.");
        var session = GameSession.CreateColdProcess();
        session.StartNewGame();
        using var world = new GameWorld(session, game);
        for (int input = 0; input < 1000; input++)
        {
            world.EnsureField();
            DialogueState? dialogue = world.Events.CurrentDialogue;
            if (dialogue is { CanConfirm: true } && dialogue.Text.Contains("Looks like your eyes are focusing", StringComparison.Ordinal))
            {
                break;
            }
            PlayerAction action = session.IsMoviePlaying ? new(PlayerActionKind.SkipMovie) : dialogue?.FirstChoice is int choice ? new(PlayerActionKind.Choose, choice) : dialogue is { CanConfirm: true } ? new(PlayerActionKind.Confirm) : new(PlayerActionKind.Wait);
            Assert.True(world.Apply(action).Accepted);
            Assert.Null(world.Events.LastFault);
        }
        DialogueState opening = Assert.Single(world.Events.DialogueTemplates);
        Assert.Contains("Looks like your eyes are focusing", opening.Text);
        var cache = new DialoguePageCache();
        (IReadOnlyList<DialogueVisualPage> Pages, DialoguePager Pager) Present()
        {
            DialogueState dialogue = Assert.Single(world.Events.DialogueTemplates);
            DialogueWindow? window = world.Events.Scene.DialogueWindows.GetValueOrDefault(dialogue.Channel);
            string expanded = NativeMessageVariables.Expand(dialogue.Text, world.Events.MessageVariables);
            return cache.Get(dialogue, window, "Arial", 8, expanded, false, () => DialogueLayout.Create(DialogueTextFormatter.Parse(dialogue.Text, world.Events.Scene.Names, world.Events.MessageVariables), dialogue, window, 10, text => text.Length * 4));
        }
        var first = Present();
        Assert.Equal(2, first.Pages.Count);
        Assert.True(first.Pager.Advance(first.Pages.Count));
        for (int frame = 0; frame < 3; frame++)
        {
            var displayed = Present();
            Assert.Same(first.Pager, displayed.Pager);
            Assert.Equal(1, displayed.Pager.PageIndex);
            Assert.False(displayed.Pager.Advance(displayed.Pages.Count));
            Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
        }
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Confirm)).Accepted);
        cache.Complete(opening);
        Assert.Null(world.Events.LastFault);
        Assert.DoesNotContain(world.Events.DialogueTemplates, dialogue => ReferenceEquals(dialogue, opening));
    }
}
