using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlayTrainDirectEntryIntegrationTests
{
    [Fact]
    public void OriginalTrainCompletesThreeCodesAndRestoresTheSubmittedWrongAttempt()
    {
        string manifest = TestResources.FixturePath("App.Tests.AutoPlayTrainDirectEntryIntegrationTests.1");
        byte[] originalManifest = File.ReadAllBytes(manifest);
        AutoPlayRunState source = JsonSerializer.Deserialize<AutoPlayRunState>(originalManifest, AutoPlayController.JsonOptions)!;
        string snapshot = Path.GetFullPath(source.Snapshot!, Path.GetDirectoryName(manifest)!);
        byte[] originalSnapshot = File.ReadAllBytes(snapshot);
        Assert.False(source.DirectCodeEntry.TrainWrongCodeSubmitted);
        Assert.Null(source.InputSearch);
        using InstalledRun run = new();
        run.Controller.ResumeFrom(manifest);
        run.World.EnsureField();
        Assert.Equal(902, run.Session.State.FieldId);
        Assert.Equal(256, run.World.Events.Frame);

        for (int tick = 0; tick < 500 && run.Controller.State.DirectCodeEntry.TrainCodeSequencesCompleted == 0; tick++)
        {
            run.Controller.Advance(1);
            Assert.Null(run.Controller.Error);
        }

        Assert.Equal(1, run.Controller.State.DirectCodeEntry.TrainCodeSequencesCompleted);
        Assert.Equal(902, run.Session.State.FieldId);
        Assert.Equal(3, run.World.Events.EventMemory.Read(1038, 1).RequireExact());
        Assert.True(run.World.Events.IsUserControlEnabled);
        Assert.True(run.Controller.State.DirectCodeEntry.TrainWrongCodeSubmitted);
        Assert.True(run.Controller.State.DirectCodeEntry.TrainCorrectDigitsSubmitted >= 12);
        Assert.Null(run.Controller.State.InputSearch);
        Assert.False(run.Controller.State.ReachedEnding);
        int correctDigits = run.Controller.State.DirectCodeEntry.TrainCorrectDigitsSubmitted;
        run.Controller.SaveAndStop();
        string completedManifest = Assert.IsType<string>(run.Controller.ReproductionPath);

        using InstalledRun resumed = new();
        resumed.Controller.ResumeFrom(completedManifest);
        resumed.World.EnsureField();
        Assert.True(resumed.Controller.State.DirectCodeEntry.TrainWrongCodeSubmitted);
        Assert.Equal(correctDigits, resumed.Controller.State.DirectCodeEntry.TrainCorrectDigitsSubmitted);
        Assert.Equal(1, resumed.Controller.State.DirectCodeEntry.TrainCodeSequencesCompleted);
        Assert.Equal(3, resumed.World.Events.EventMemory.Read(1038, 1).RequireExact());
        resumed.Controller.Advance(1);
        Assert.Null(resumed.Controller.Error);
        Assert.True(resumed.Controller.State.DirectCodeEntry.TrainWrongCodeSubmitted);
        Assert.Null(resumed.Controller.State.InputSearch);
        Assert.False(resumed.Controller.State.ReachedEnding);
        Assert.Equal(originalManifest, File.ReadAllBytes(manifest));
        Assert.Equal(originalSnapshot, File.ReadAllBytes(snapshot));
    }

    private sealed class InstalledRun : IDisposable
    {
        private readonly string output = Path.Combine(Path.GetTempPath(), "ff8-autoplay-direct-train-" + Guid.NewGuid().ToString("N"));
        private GameWorld? world;

        public InstalledRun()
        {
            Controller = new(Session, () => World, Restore, output);
        }

        public GameSession Session { get; } = GameSession.CreateColdProcess();
        public AutoPlayController Controller { get; }
        public GameWorld World => world ??= new(Session, TestResources.GameDirectory, validateWorldPresentation: false);

        private void Restore(GameState state)
        {
            world?.Dispose();
            world = null;
            Session.Restore(state);
        }

        public void Dispose()
        {
            world?.Dispose();
            if (Directory.Exists(output))
            {
                Directory.Delete(output, true);
            }
        }
    }
}
