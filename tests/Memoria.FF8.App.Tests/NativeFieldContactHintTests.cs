using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeFieldContactHintTests
{
    [Fact]
    public void CardButtonShowsAndHidesTheOriginalContactHintAcrossStandaloneRestore()
    {
        string directory = TestResources.GameDirectory;
        string source = TestResources.FixturePath("App.Tests.NativeFieldContactHintTests.1");
        GameSession session = new(PlayerStateLoader.Load(source, directory));
        using GameWorld world = new(session, directory);
        world.EnsureField();
        Assert.Equal(434, world.Events.FieldId);
        AssertHintHidden(world);

        ToggleHint(world, session);
        AssertHintShown(world);
        long frame = world.Events.Frame;
        string path = TestResources.ArtifactFile($"contact-hint-{Guid.NewGuid():N}.json");
        try
        {
            session.Save(path, compact: true);
            GameSession restoredSession = new(GameSession.Load(path));
            using GameWorld restored = new(restoredSession, directory);
            restored.EnsureField();
            Assert.Equal(frame, restored.Events.Frame);
            AssertHintShown(restored);
            Assert.Equal(System.Text.Json.JsonSerializer.Serialize(session.State.ExamTimer), System.Text.Json.JsonSerializer.Serialize(restoredSession.State.ExamTimer));

            ToggleHint(restored, restoredSession);
            AssertHintHidden(restored);
            Assert.Equal(434, restored.Events.FieldId);
            Assert.Null(restored.Events.LastFault);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static void ToggleHint(GameWorld world, GameSession session)
    {
        ushort raw = session.State.InputConfiguration!.RawForMappedPulse(0x80);
        Assert.True(world.Apply(new(PlayerActionKind.FieldButtons, raw)).Accepted);
        PlayerActionResult sampled = world.Apply(new(PlayerActionKind.Wait, Frames: 20, WaitWhilePlayable: true));
        Assert.True(sampled.NativeInputObserved);
        Assert.True(world.Apply(new(PlayerActionKind.FieldButtons, 0)).Accepted);
        world.Apply(new(PlayerActionKind.Wait, Frames: 16, WaitWhilePlayable: true));
        Assert.Null(world.Events.LastFault);
    }

    private static void AssertHintShown(GameWorld world)
    {
        Assert.Equal(0, world.Events.BackgroundStates[5]);
        Assert.Equal(-1, world.Events.BackgroundStates[6]);
        Assert.Equal(-1, world.Events.BackgroundStates[7]);
        DialogueState caption = Assert.Single(world.Events.Dialogues, dialogue => dialogue.Channel == 3);
        Assert.Equal(7, caption.MessageId);
        Assert.Equal(150, caption.X);
        Assert.Equal(15, caption.Y);
        Assert.False(caption.CanConfirm);
        Assert.Null(world.Events.LastFault);
    }

    private static void AssertHintHidden(GameWorld world)
    {
        Assert.Equal(-1, world.Events.BackgroundStates[5]);
        Assert.Equal(-1, world.Events.BackgroundStates[6]);
        Assert.Equal(-1, world.Events.BackgroundStates[7]);
        Assert.DoesNotContain(world.Events.Dialogues, dialogue => dialogue.Channel == 3);
    }
}
