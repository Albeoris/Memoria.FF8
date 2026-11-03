using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlayDeferredInputRequestTests
{
    [Fact]
    public void RepeatedInputPollingDoesNotCancelAnAcceptedDelayedSceneChange()
    {
        string snapshot = TestResources.FixturePath("App.Tests.AutoPlayDeferredInputRequestTests.1");
        byte[] original = File.ReadAllBytes(snapshot);
        GameSession session = new(GameSession.Load(snapshot));
        Assert.Equal(235, session.State.FieldId);
        string output = Path.Combine(Path.GetTempPath(), "ff8-deferred-input-" + Guid.NewGuid().ToString("N"));
        GameWorld? world = null;
        GameWorld GetWorld() => world ??= new(session, TestResources.GameDirectory, validateWorldPresentation: false);
        void Restore(GameState state)
        {
            world?.Dispose();
            world = null;
            session.Restore(state);
        }
        AutoPlayController controller = new(session, GetWorld, Restore, output);
        try
        {
            controller.StartFromCurrentState();
            for (int step = 0; step < 32 && session.State.FieldId == 235; step++)
            {
                controller.Advance(1);
                Assert.Null(controller.Error);
            }
            Assert.Equal(234, session.State.FieldId);
            Assert.NotNull(controller.State.InputSearch);
            Assert.Contains(controller.State.InputSearch.Frames, frame => frame.Trials.Any(trial => trial.Outcome == AutoPlayInputTrialOutcome.ReturnedToObservedContext));
            Assert.Equal(original, File.ReadAllBytes(snapshot));
        }
        finally
        {
            world?.Dispose();
        }
        if (Directory.Exists(output))
        {
            Directory.Delete(output, true);
        }
    }
}
