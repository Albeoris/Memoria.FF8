using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlayUnfocusedMessageTests
{
    [Fact]
    public void NativeAutomaticMessageClosesWithoutChangingFocusOrRewritingItsSourceCheckpoint()
    {
        string manifest = TestResources.FixturePath("App.Tests.AutoPlayUnfocusedMessageTests.1");
        string gameDirectory = TestResources.GameDirectory;
        byte[] originalManifest = Hash(manifest);
        AutoPlayRunState saved = JsonSerializer.Deserialize<AutoPlayRunState>(File.ReadAllText(manifest), AutoPlayController.JsonOptions)!;
        string snapshot = Path.GetFullPath(saved.Snapshot!, Path.GetDirectoryName(manifest)!);
        byte[] originalSnapshot = Hash(snapshot);
        GameSession session = GameSession.CreateColdProcess();
        GameWorld? world = null;
        string output = Path.Combine(Path.GetTempPath(), "autoplay-unfocused-message-" + Guid.NewGuid().ToString("N"));
        try
        {
            GameWorld GetWorld() => world ??= new(session, gameDirectory, validateWorldPresentation: false);
            void Restore(GameState state)
            {
                world?.Dispose();
                world = null;
                session.Restore(state);
            }
            AutoPlayController controller = new(session, GetWorld, Restore, output, deadlineTime: new InspectionClock());
            controller.ResumeFrom(manifest);
            GetWorld().EnsureField();
            Assert.Equal((byte)0, world!.Events.Scene.NativeDialogueFocus);
            Assert.Null(world.CurrentDialogue);
            var automatic = Assert.Single(world.Events.Dialogues);
            Assert.Equal(3, automatic.Channel);
            Assert.Equal(4, automatic.MessageId);
            Assert.True(automatic.CanConfirm);
            Assert.Null(automatic.FirstChoice);
            Assert.False(automatic.IsNamePrompt);
            Assert.True(world.Events.HasConfirmableAutomaticDialogue);
            long frame = world.Events.Frame;

            controller.Advance(1);

            Assert.Null(controller.Error);
            Assert.Equal((byte)0, GetWorld().Events.Scene.NativeDialogueFocus);
            Assert.Null(GetWorld().CurrentDialogue);
            var closing = Assert.Single(GetWorld().Events.Dialogues);
            Assert.Equal(automatic.Channel, closing.Channel);
            Assert.False(closing.CanConfirm);
            Assert.False(GetWorld().Events.HasConfirmableAutomaticDialogue);
            Assert.True(GetWorld().Events.Frame > frame);
            for (int step = 1; step < 30 && GetWorld().Events.Dialogues.Any(dialogue => dialogue.Channel == automatic.Channel && dialogue.MessageId == automatic.MessageId && dialogue.ObjectId == automatic.ObjectId && dialogue.ScriptId == automatic.ScriptId && dialogue.Operation == automatic.Operation) && controller.Error is null; step++)
            {
                controller.Advance(1);
            }

            Assert.Null(controller.Error);
            Assert.True(controller.Running);
            Assert.DoesNotContain(GetWorld().Events.Dialogues, dialogue => dialogue.Channel == automatic.Channel && dialogue.MessageId == automatic.MessageId && dialogue.ObjectId == automatic.ObjectId && dialogue.ScriptId == automatic.ScriptId && dialogue.Operation == automatic.Operation);
            Assert.Equal(originalManifest, Hash(manifest));
            Assert.Equal(originalSnapshot, Hash(snapshot));
        }
        finally
        {
            world?.Dispose();
            if (Directory.Exists(output))
            {
                Directory.Delete(output, recursive: true);
            }
        }
    }

    private static byte[] Hash(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return SHA256.HashData(stream);
    }

    private sealed class InspectionClock : TimeProvider
    {
        public override long GetTimestamp() => 0;
    }
}
