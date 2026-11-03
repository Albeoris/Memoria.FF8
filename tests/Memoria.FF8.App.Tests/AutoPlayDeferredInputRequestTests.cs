using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlayDeferredInputRequestTests
{
    [Fact]
    public void NeutralPollingRevealsTheBackQuerySkippedByNavigationInput()
    {
        VerifySceneChange("App.Tests.AutoPlayDeferredInputRequestTests.2", 236, 235, requireNeutral: true);
    }

    [Fact]
    public void RepeatedInputPollingDoesNotCancelAnAcceptedDelayedSceneChange()
    {
        VerifySceneChange("App.Tests.AutoPlayDeferredInputRequestTests.1", 235, 234, requireNeutral: false);
    }

    private static void VerifySceneChange(string fixture, int sourceField, int destinationField, bool requireNeutral)
    {
        string snapshot = TestResources.FixturePath(fixture);
        byte[] original = File.ReadAllBytes(snapshot);
        GameSession session = new(GameSession.Load(snapshot));
        Assert.Equal(sourceField, session.State.FieldId);
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
            for (int step = 0; step < 32 && session.State.FieldId == sourceField; step++)
            {
                controller.Advance(1);
                Assert.Null(controller.Error);
            }
            Assert.Equal(destinationField, session.State.FieldId);
            Assert.NotNull(controller.State.InputSearch);
            Assert.Contains(controller.State.InputSearch.Frames, frame => frame.Trials.Any(trial => trial.Outcome == AutoPlayInputTrialOutcome.ReturnedToObservedContext));
            if (requireNeutral)
            {
                Assert.Contains(controller.State.InputSearch.Frames, frame => frame.Trials.Any(trial => AutoPlayController.HasNeutralInputSearchReceipt(frame, trial)));
                Assert.Contains(controller.State.InputSearch.Frames, frame => frame.Trials.Any(trial => trial.SampledQuery is { Operation: 84, Mask: 16 }));
            }
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
