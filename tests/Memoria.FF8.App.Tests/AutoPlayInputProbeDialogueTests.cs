using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlayInputProbeDialogueTests
{
    [Fact]
    public void NativeWindowAnimationCannotChangeTheOwnerAcrossCheckpointReplay()
    {
        DialogueState live = Dialogue();
        DialogueState restored = JsonSerializer.Deserialize<DialogueState>(JsonSerializer.Serialize(live))!;
        live.NativeWindow!.Progress += 512;
        live.NativeWindow.Delta = -512;

        Assert.NotEqual(live, restored);
        Assert.True(AutoPlayController.HasSameProbeDialogueOwner(restored, live));
        Assert.True(AutoPlayController.HasSameProbeDialogueOwner(live, restored));
    }

    [Fact]
    public void EveryExistingDialogueIdentityAndInteractionFieldStillBoundsTheProbe()
    {
        DialogueState expected = Dialogue();
        DialogueState[] changed =
        [
            expected with { Channel = expected.Channel + 1 },
            expected with { MessageId = expected.MessageId + 1 },
            expected with { Text = "Different text" },
            expected with { X = expected.X + 1 },
            expected with { Y = expected.Y + 1 },
            expected with { FirstChoice = 0 },
            expected with { LastChoice = 1 },
            expected with { InitialChoice = 0 },
            expected with { CancelChoice = 1 },
            expected with { IsNamePrompt = true },
            expected with { ObjectId = expected.ObjectId + 1 },
            expected with { ScriptId = expected.ScriptId + 1 },
            expected with { Operation = expected.Operation + 1 },
            expected with { CanConfirm = true },
            expected with { NativeWindow = null },
            expected with { NativeWindow = expected.NativeWindow! with { Version = 2 } }
        ];
        foreach (DialogueState current in changed)
        {
            Assert.False(AutoPlayController.HasSameProbeDialogueOwner(current, expected));
        }
        Assert.False(AutoPlayController.HasSameProbeDialogueOwner(null, expected));
        Assert.False(AutoPlayController.HasSameProbeDialogueOwner(expected, null));
        Assert.True(AutoPlayController.HasSameProbeDialogueOwner(null, null));
    }

    [Fact]
    public void ConfiguredNativeInputProbeRetainsItsDialogueOwnerAfterNeutralReplay()
    {
        string manifest = TestResources.FixturePath("App.Tests.AutoPlayInputProbeDialogueTests.1");
        string gameDirectory = TestResources.GameDirectory;
        GameSession session = GameSession.CreateColdProcess();
        GameWorld? world = null;
        string output = Path.Combine(Path.GetTempPath(), "autoplay-probe-dialogue-" + Guid.NewGuid().ToString("N"));
        try
        {
            GameWorld GetWorld() => world ??= new(session, gameDirectory, validateWorldPresentation: false);
            void Restore(GameState state)
            {
                world?.Dispose();
                world = null;
                session.Restore(state);
            }
            AutoPlayController controller = new(session, GetWorld, Restore, output);
            controller.ResumeFrom(manifest);
            GetWorld().EnsureField();
            Assert.False(world!.Events.IsUserControlEnabled);
            Assert.NotNull(world.CurrentDialogue?.NativeWindow);
            Assert.False(world.CurrentDialogue!.CanConfirm);
            long frame = world.Events.Frame;

            controller.Advance(1);

            Assert.Null(controller.Error);
            Assert.True(controller.Running);
            Assert.True(GetWorld().Events.Frame > frame);
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

    private static DialogueState Dialogue() => new(0, 1, "A persistent native input display.", 20, 30, ObjectId: 2, ScriptId: 3, Operation: 4, CanConfirm: false, NativeWindow: new(1, 512, 512));
}
