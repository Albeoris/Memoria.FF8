using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlayNavigationContinuationTests
{
    [Fact]
    public void AnUnscopedHistoricalGraphLearnsItsFirstActualNavigationEdgeAndSurvivesColdResume()
    {
        string manifest = TestResources.FixturePath("App.Tests.AutoPlayNavigationContinuationTests.1");
        byte[] originalManifest = File.ReadAllBytes(manifest);
        AutoPlayRunState saved = JsonSerializer.Deserialize<AutoPlayRunState>(originalManifest, AutoPlayController.JsonOptions)!;
        string snapshot = Path.Combine(Path.GetDirectoryName(manifest)!, saved.Snapshot!);
        byte[] originalSnapshot = File.ReadAllBytes(snapshot);
        Assert.Empty(saved.Coverage.NavigationVisits);
        Assert.Empty(saved.Coverage.NavigationRoutes);
        Assert.All(saved.Coverage.Choices, choice => Assert.True(choice.Finished));
        using InstalledRun run = new();
        run.Controller.ResumeFrom(manifest);
        for (int tick = 0; tick < 64 && run.Controller.State.Coverage.NavigationRoutes.Count == 0 && run.Controller.Error is null; tick++)
        {
            run.Controller.Advance(1);
        }

        Assert.Null(run.Controller.Error);
        AutoPlayNavigationRoute route = Assert.Single(run.Controller.State.Coverage.NavigationRoutes);
        Assert.Equal("240:gateway:0", route.EventId);
        Assert.Equal("237:142", route.Route.DestinationVisitKey);
        Assert.Equal(237, run.Controller.State.ActiveField);
        Assert.Equal(route.DestinationKey, run.Controller.State.ActiveNavigationKey);
        Assert.True(run.World.Events.IsUserControlEnabled);
        Assert.Equal(2, run.Controller.State.Coverage.NavigationVisits.Count);
        Assert.Contains(run.Controller.State.Coverage.NavigationVisits, vertex => vertex.Key == route.SourceKey && vertex.VisitKey == "240:142");
        Assert.Contains(run.Controller.State.Coverage.NavigationVisits, vertex => vertex.Key == route.DestinationKey && vertex.VisitKey == "237:142");
        Assert.Equal(saved.Coverage.Choices.Count, run.Controller.State.Coverage.Choices.Count);
        Assert.Equal(saved.Coverage.Choices.Sum(choice => choice.CompletedOptions.Count), run.Controller.State.Coverage.Choices.Sum(choice => choice.CompletedOptions.Count));
        string navigation = JsonSerializer.Serialize(run.Controller.State.Coverage.NavigationRoutes, AutoPlayController.JsonOptions);
        run.Controller.SaveAndStop();
        using InstalledRun cold = new();
        cold.Controller.ResumeFrom(run.Controller.ReproductionPath!);
        cold.Controller.Advance(1);
        Assert.Null(cold.Controller.Error);
        Assert.Equal(navigation, JsonSerializer.Serialize(cold.Controller.State.Coverage.NavigationRoutes, AutoPlayController.JsonOptions));
        Assert.Equal(originalManifest, File.ReadAllBytes(manifest));
        Assert.Equal(originalSnapshot, File.ReadAllBytes(snapshot));
    }

    private sealed class InstalledRun : IDisposable
    {
        private readonly string output = Path.Combine(TestResources.ArtifactDirectory, "navigation-continuation-" + Guid.NewGuid().ToString("N"));
        private GameWorld? world;

        internal InstalledRun()
        {
            Controller = new(Session, () => World, state =>
            {
                world?.Dispose();
                world = null;
                Session.Restore(state);
            }, output);
        }

        internal GameSession Session { get; } = GameSession.CreateColdProcess();
        internal GameWorld World => world ??= new(Session, TestResources.GameDirectory, validateWorldPresentation: false);
        internal AutoPlayController Controller { get; }

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
