using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlayRefreshSweepIntegrationTests
{
    [Fact]
    public void ActualGardenRefreshPersistsItsPendingGoalAndReachesNewProgressWithoutCompletionCreditForInspection()
    {
        string manifest = TestResources.FixturePath("App.Tests.AutoPlayRefreshSweepIntegrationTests.1");
        byte[] originalManifest = File.ReadAllBytes(manifest);
        AutoPlayRunState saved = JsonSerializer.Deserialize<AutoPlayRunState>(originalManifest, AutoPlayController.JsonOptions)!;
        string snapshot = Path.Combine(Path.GetDirectoryName(manifest)!, saved.Snapshot!);
        byte[] originalSnapshot = File.ReadAllBytes(snapshot);
        using InstalledRun run = new();
        run.Controller.ResumeFrom(manifest);
        run.World.EnsureField();
        Assert.Equal(145, run.Session.State.FieldId);
        Assert.Equal(6, run.World.Events.Frame);
        Assert.Empty(run.Controller.State.InspectedVisitsSinceProgress);
        int completed = CompletedCount(run);

        run.Controller.Advance(1);

        Assert.Null(run.Controller.Error);
        Assert.Equal(completed, CompletedCount(run));
        for (int tick = 0; tick < 128 && (run.Controller.State.InspectedVisitsSinceProgress.Count == 0 || run.Controller.State.RefreshTargetVisit is null) && run.Controller.Error is null; tick++)
        {
            run.Controller.Advance(1);
        }
        Assert.Null(run.Controller.Error);
        Assert.True(run.Controller.Running);
        Assert.NotEmpty(run.Controller.State.InspectedVisitsSinceProgress);
        string target = Assert.IsType<string>(run.Controller.State.RefreshTargetVisit);
        Assert.DoesNotContain(target, run.Controller.State.InspectedVisitsSinceProgress);
        string[] inspected = run.Controller.State.InspectedVisitsSinceProgress.Order(StringComparer.Ordinal).ToArray();
        run.Controller.SaveAndStop();
        using InstalledRun cold = new();
        cold.Controller.ResumeFrom(run.Controller.ReproductionPath!);
        cold.World.EnsureField();
        Assert.Equal(inspected, cold.Controller.State.InspectedVisitsSinceProgress.Order(StringComparer.Ordinal));
        Assert.Equal(target, cold.Controller.State.RefreshTargetVisit);
        Assert.True(JsonElement.DeepEquals(run.World.Events.Capture(), cold.World.Events.Capture()));
        int beforeScore = cold.Controller.State.LastProgressScore;
        long beforeStep = cold.Controller.State.Steps;

        for (int tick = 0; tick < 2048 && cold.Controller.State.LastProgressScore == beforeScore && cold.Controller.Error is null; tick++)
        {
            cold.Controller.Advance(1);
            if (cold.Controller.State.InspectedVisitsSinceProgress.Contains(target))
            {
                Assert.NotEqual(target, cold.Controller.State.RefreshTargetVisit);
            }
        }

        Assert.Null(cold.Controller.Error);
        Assert.True(cold.Controller.Running);
        Assert.True(cold.Controller.State.LastProgressScore > beforeScore);
        Assert.True(cold.Controller.State.LastProgressStep > beforeStep);
        Assert.True(cold.NativeCommands > 0);
        Assert.Null(cold.World.Events.LastFault);
        Assert.False(cold.Controller.State.ReachedEnding);
        Assert.Equal(originalManifest, File.ReadAllBytes(manifest));
        Assert.Equal(originalSnapshot, File.ReadAllBytes(snapshot));
    }

    private static int CompletedCount(InstalledRun run) => run.Controller.State.Coverage.Visits.Sum(visit => visit.Events.Count(item => item.Completed));

    private sealed class InstalledRun : IDisposable
    {
        private readonly string gameDirectory = TestResources.GameDirectory;
        private GameWorld? world;

        public InstalledRun()
        {
            Controller = new(Session, () => World, Restore, Output);
        }

        public GameSession Session { get; } = GameSession.CreateColdProcess();
        public string Output { get; } = Path.Combine(Path.GetTempPath(), "ff8-autoplay-refresh-sweep-" + Guid.NewGuid().ToString("N"));
        public AutoPlayController Controller { get; }
        public int NativeCommands { get; private set; }

        public GameWorld World
        {
            get
            {
                if (world is null)
                {
                    world = new(Session, gameDirectory, validateWorldPresentation: false);
                    world.Events.CommandExecuted += _ => NativeCommands++;
                }
                return world;
            }
        }

        private void Restore(GameState state)
        {
            world?.Dispose();
            world = null;
            Session.Restore(state);
        }

        public void Dispose()
        {
            world?.Dispose();
            if (Directory.Exists(Output))
            {
                Directory.Delete(Output, true);
            }
        }
    }
}
