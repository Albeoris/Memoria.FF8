using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlaySnapshotCaptureTests
{
    [Fact]
    public void AllocatedFieldCapturePreparesTheNativeCheckpointOnceAndPublishesTheSameState()
    {
        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF8_AUTOPLAY_CAPTURE_SNAPSHOT");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(source) && File.Exists(source), "Set MEMORIA_FF8_AUTOPLAY_CAPTURE_SNAPSHOT to an optional standalone allocated-field snapshot.");
        string directory = TestResources.GameDirectory;
        GameSession session = new(GameSession.Load(source!));
        using GameWorld world = new(session, directory, validateWorldPresentation: false);
        world.EnsureField();
        Assert.Equal(SceneKind.Field, session.State.Scene);
        Assert.True(world.HasCurrentFieldAllocation);
        Assert.False(session.HasActiveBattle);
        AutoPlayController controller = new(session, () => world, _ => throw new InvalidOperationException("Capturing must not restore gameplay."), Path.GetTempPath());
        int preparations = 0;
        void CountPreparation() => preparations++;
        world.Events.PreparingCheckpoint += CountPreparation;
        try
        {
            GameState expected = session.PrepareSnapshot();
            Assert.Equal(1, preparations);
            Assert.NotNull(expected.EventState);
            long frame = world.Events.Frame;
            preparations = 0;

            GameState actual = controller.CaptureState();

            Assert.Equal(1, preparations);
            Assert.Equal(frame, world.Events.Frame);
            Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(expected), JsonSerializer.SerializeToElement(actual)));
            Assert.NotNull(session.State.EventState);
            Assert.True(JsonElement.DeepEquals(expected.EventState.Value, session.State.EventState.Value));
            Assert.True(JsonElement.DeepEquals(actual.EventState!.Value, session.State.EventState.Value));
        }
        finally
        {
            world.Events.PreparingCheckpoint -= CountPreparation;
        }
    }
}
