using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class RemasteredOpeningTests
{
    [Fact]
    public void InstalledRemasteredColdProcessReachesClassroomAndRestoresStandaloneSave()
    {
        string? installation = Environment.GetEnvironmentVariable("MEMORIA_FF8_REMASTERED_DIR");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(installation) && Directory.Exists(installation), "Set MEMORIA_FF8_REMASTERED_DIR to an installed English Remastered game directory.");
        string directory = Path.GetFullPath(installation!);
        Assert.True(File.Exists(Path.Combine(directory, "main.zzz")), "The selected installation must be Remastered.");
        string? proofRoot = Environment.GetEnvironmentVariable("MEMORIA_FF8_REMASTERED_PROOF_DIR");
        bool retainProof = !string.IsNullOrWhiteSpace(proofRoot);
        string proof = Path.Combine(retainProof ? Path.GetFullPath(proofRoot!) : Path.GetTempPath(), "remastered-opening-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(proof);
        try
        {
            GameSession session = GameWorld.CreateColdProcess(directory);
            Assert.Equal(SceneKind.Title, session.State.Scene);
            session.StartNewGame();
            using GameWorld world = new(session, directory);
            List<object> boundaries = [];
            int previousField = -1;
            int centeringMoves = 0;
            int actions = 0;
            for (; actions < 6000 && session.State.FieldId != 232; actions++)
            {
                world.EnsureField();
                if (world.Events.FieldId != previousField)
                {
                    previousField = world.Events.FieldId;
                    boundaries.Add(new { Field = previousField, world.Events.Frame, Camera = world.Events.Scene.CameraIndex, Trigger = world.NextTrigger.Kind.ToString() });
                }
                DialogueState? dialogue = world.Events.CurrentDialogue;
                PlayerAction next;
                if (session.IsMoviePlaying)
                {
                    next = new(PlayerActionKind.SkipMovie);
                }
                else if (dialogue?.IsNamePrompt == true)
                {
                    next = new(PlayerActionKind.EnterName, Text: "Squall");
                }
                else if (dialogue?.FirstChoice is int choice)
                {
                    next = new(PlayerActionKind.Choose, choice);
                }
                else if (world.Events.CanConfirmDialogue)
                {
                    next = new(PlayerActionKind.Confirm);
                }
                else if (world.Events.FieldId == 229 && world.Events.CanAcceptMovementInput)
                {
                    next = centeringMoves++ < 2 ? new(PlayerActionKind.Move, Horizontal: -1) : new(PlayerActionKind.Move, Vertical: 1);
                }
                else
                {
                    next = new(PlayerActionKind.Wait);
                }
                Assert.True(world.Apply(next).Accepted);
                Assert.Null(world.Events.LastFault);
            }
            Assert.Equal(232, session.State.FieldId);
            world.EnsureField();
            bool frontSeen = false;
            bool returnSeen = false;
            int classroomActions = 0;
            for (; classroomActions < 2000; classroomActions++)
            {
                DialogueState? dialogue = world.Events.CurrentDialogue;
                if (returnSeen && dialogue is { IsNamePrompt: false, CanConfirm: true })
                {
                    break;
                }
                PlayerAction next;
                if (dialogue?.IsNamePrompt == true)
                {
                    next = new(PlayerActionKind.EnterName, Text: "Quistis");
                }
                else if (dialogue?.FirstChoice is int choice)
                {
                    next = new(PlayerActionKind.Choose, choice);
                }
                else
                {
                    next = new(world.Events.CanConfirmDialogue ? PlayerActionKind.Confirm : PlayerActionKind.Wait);
                }
                int previousCamera = world.Events.Scene.CameraIndex;
                Assert.True(world.Apply(next).Accepted);
                Assert.Null(world.Events.LastFault);
                Assert.Equal(232, session.State.FieldId);
                int camera = world.Events.Scene.CameraIndex;
                if (previousCamera == 1 && camera == 0)
                {
                    Assert.Equal(-1, world.Events.BackgroundStates[4]);
                    frontSeen = true;
                    boundaries.Add(new { Field = 232, world.Events.Frame, Camera = camera, Trigger = world.NextTrigger.Kind.ToString() });
                }
                if (frontSeen && previousCamera == 0 && camera == 1)
                {
                    Assert.Equal(-1, world.Events.BackgroundStates[5]);
                    Assert.Equal(0, world.Events.BackgroundStates[4]);
                    returnSeen = true;
                    boundaries.Add(new { Field = 232, world.Events.Frame, Camera = camera, Trigger = world.NextTrigger.Kind.ToString() });
                }
            }
            Assert.Equal(232, world.Field!.Id);
            Assert.True(frontSeen);
            Assert.True(returnSeen);
            DialogueState savedDialogue = Assert.IsType<DialogueState>(world.Events.CurrentDialogue);
            Assert.False(savedDialogue.IsNamePrompt);
            Assert.True(savedDialogue.CanConfirm);
            Assert.Equal(1, world.Events.Scene.CameraIndex);
            Assert.False(FieldPresentationReadiness.HasPendingInitialization(world.Events.Scene.NativeFieldInitialization));
            Assert.Null(world.Events.LastFault);
            session.SetEventState(world.Events.Capture());
            string checkpoint = Path.Combine(proof, "classroom.json");
            session.Save(checkpoint);
            byte[] pixels = world.Render(TimeSpan.Zero);
            WritePng(Path.Combine(proof, "classroom.png"), pixels, world.RenderWidth, world.RenderHeight);
            using GameWorld restored = new(new GameSession(GameSession.Load(checkpoint)), directory);
            restored.EnsureField();
            Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
            Assert.Equal(pixels, restored.Render(TimeSpan.Zero));
            long savedFrame = world.Events.Frame;
            int savedMessage = savedDialogue.MessageId;
            PlayerAction wait = new(PlayerActionKind.Wait);
            Assert.Equal(world.Apply(wait), restored.Apply(wait));
            Assert.Null(restored.Events.LastFault);
            Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
            Assert.Equal(world.Render(TimeSpan.Zero), restored.Render(TimeSpan.Zero));
            File.WriteAllText(Path.Combine(proof, "report.json"), JsonSerializer.Serialize(new
            {
                Edition = "Remastered",
                ColdProcess = true,
                OrdinaryOpeningActions = actions,
                OrdinaryClassroomActions = classroomActions,
                FrontCameraSeen = frontSeen,
                ReturnCameraSeen = returnSeen,
                Boundaries = boundaries,
                Saved = new { Field = 232, Frame = savedFrame, Camera = 1, Message = savedMessage, NamePrompt = false },
                Continuation = new { Field = restored.Events.FieldId, restored.Events.Frame, Camera = restored.Events.Scene.CameraIndex, Message = restored.Events.CurrentDialogue?.MessageId },
                SnapshotRoundTrip = true,
                NextWaitStateEqual = true,
                PixelsEqual = true,
                Limitation = "Engine opening-route and snapshot acceptance; original runtime parity and host audio playback are not established."
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
        finally
        {
            if (!retainProof)
            {
                Directory.Delete(proof, true);
            }
        }
    }

    private static void WritePng(string path, byte[] rgba, int width, int height)
    {
        using System.Drawing.Bitmap bitmap = new(width, height);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int offset = (y * width + x) * 4;
                bitmap.SetPixel(x, y, System.Drawing.Color.FromArgb(rgba[offset + 3], rgba[offset], rgba[offset + 1], rgba[offset + 2]));
            }
        }
        bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
    }
}
