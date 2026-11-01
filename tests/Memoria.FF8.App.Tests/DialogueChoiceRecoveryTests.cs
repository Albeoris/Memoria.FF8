using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class DialogueChoiceRecoveryTests
{
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
