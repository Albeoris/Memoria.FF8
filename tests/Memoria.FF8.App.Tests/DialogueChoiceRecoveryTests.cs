using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class DialogueChoiceRecoveryTests
{
    [Fact]
    public void Saved_second_choice_retains_its_cursor_during_every_native_closing_callback()
    {
        string source = TestResources.FixturePath("App.Tests.DialogueChoiceRecoveryTests.2");
        var session = new GameSession(PlayerStateLoader.Load(source, TestResources.GameDirectory));
        using var world = new GameWorld(session, TestResources.GameDirectory);
        world.EnsureField();
        for (int tick = 0; tick < 60 && world.CurrentDialogue is not { CanConfirm: true, FirstChoice: not null }; tick++)
        {
            Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
        }
        DialogueState dialogue = Assert.IsType<DialogueState>(world.CurrentDialogue);
        Assert.True(dialogue.CanConfirm);
        int second = dialogue.FirstChoice!.Value + 1;
        Assert.True(second <= dialogue.LastChoice);
        DialogueState? presented = null;
        int selected = 0;
        string name = "";
        DialoguePresentation.UpdateDraft(dialogue, ref presented, session.State.DialogueDraft, ref selected, ref name, "Squall");
        selected = second;
        PlayerAction action = Assert.IsType<PlayerAction>(DialoguePresentation.ResolveInput(dialogue, new InputFrame(0, 0, true, false, false, false), ref selected, name));
        Assert.True(world.Apply(action).Accepted);
        session.SetDialogueDraft(null);
        int closingCallbacks = 0;
        for (int tick = 0; tick < 16; tick++)
        {
            DialogueState? current = world.CurrentDialogue;
            DialoguePresentation.UpdateDraft(current, ref presented, session.State.DialogueDraft, ref selected, ref name, "Squall");
            if (current is null || current.NativeWindow != dialogue.NativeWindow)
            {
                break;
            }
            Assert.Equal(second, selected);
            Assert.False(current.CanConfirm);
            closingCallbacks++;
            Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
        }
        Assert.True(closingCallbacks > 0);
        Assert.Equal(0, dialogue.NativeWindow!.Progress);
        Assert.Null(world.Events.LastFault);
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_CHOICE_CURSOR_OUTPUT");
        if (!string.IsNullOrWhiteSpace(output))
        {
            world.CommitFieldPresentation();
            session.SetEventState(world.Events.Capture());
            session.Save(output, compact: true);
            var restoredSession = new GameSession(GameSession.Load(output));
            using var restored = new GameWorld(restoredSession, TestResources.GameDirectory);
            restored.EnsureField();
            Assert.Equal(world.Events.Frame, restored.Events.Frame);
            Assert.Equal(world.CurrentDialogue, restored.CurrentDialogue);
        }
    }

    [Fact]
    public void Saved_draw_selection_recovers_focus_on_its_next_callback_and_accepts_a_choice()
    {
        string source = TestResources.FixturePath("App.Tests.DialogueChoiceRecoveryTests.1");
        var session = new GameSession(GameSession.Load(source));
        using var world = new GameWorld(session, TestResources.GameDirectory);
        Assert.True(world.EnsureField());
        Assert.Null(world.CurrentDialogue);
        DialogueState pending = Assert.Single(world.Events.Dialogues);
        Assert.Equal(6, pending.Channel);
        Assert.True(pending.CanConfirm);
        Assert.NotNull(pending.FirstChoice);
        long frame = world.Events.Frame;
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
        Assert.Equal(frame + 1, world.Events.Frame);
        DialogueState recovered = Assert.IsType<DialogueState>(world.CurrentDialogue);
        Assert.Equal(6, recovered.Channel);
        Assert.Equal((byte)6, world.Events.Scene.NativeDialogueFocus);
        int selected = DialoguePresentation.ResolveDraft(recovered, session.State.DialogueDraft, "").Selection;
        DialogueDocument document = DialogueTextFormatter.Parse(recovered.Text, world.Events.Scene.Names);
        DialogueVisualPage page = Assert.Single(DialogueLayout.Create(document, recovered, null, 10, text => text.Length));
        Assert.Contains(page.Lines, line => line.IsChoice && line.SourceLineIndex == selected);
        Save("recovered.json");
        for (int callback = 0; callback < 8; callback++)
        {
            Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
            Assert.Equal(6, world.CurrentDialogue?.Channel);
        }
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Choose, recovered.CancelChoice!.Value)).Accepted);
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Wait, Frames: 8)).Accepted);
        Assert.Null(world.Events.Scene.DrawPointInteraction);
        Assert.Null(world.Events.LastFault);
        Assert.DoesNotContain(world.Events.Dialogues, dialogue => dialogue.FirstChoice is not null);
        Save("completed.json");

        void Save(string name)
        {
            string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_DIALOGUE_RECOVERY_OUTPUT");
            if (!string.IsNullOrWhiteSpace(output))
            {
                Directory.CreateDirectory(output);
                world.CommitFieldPresentation();
                session.SetEventState(world.Events.Capture());
                string path = Path.Combine(output, name);
                session.Save(path, compact: true);
                var restoredSession = new GameSession(GameSession.Load(path));
                using var restored = new GameWorld(restoredSession, TestResources.GameDirectory);
                Assert.True(restored.EnsureField());
                Assert.Equal(world.Events.Frame, restored.Events.Frame);
                Assert.Equal(world.CurrentDialogue, restored.CurrentDialogue);
                Assert.Equal(world.Events.Scene.DrawPointInteraction, restored.Events.Scene.DrawPointInteraction);
            }
        }
    }
}
