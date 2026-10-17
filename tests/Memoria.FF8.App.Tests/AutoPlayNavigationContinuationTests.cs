using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlayNavigationContinuationTests
{
    [Fact]
    public void AnActualNativeLockUnderThePlayerAllowsExitWithoutUnlockingOrLandingOnLockedTriangles()
    {
        string manifest = TestResources.FixturePath("App.Tests.AutoPlayNavigationContinuationTests.4");
        byte[] originalManifest = File.ReadAllBytes(manifest);
        AutoPlayRunState saved = JsonSerializer.Deserialize<AutoPlayRunState>(originalManifest, AutoPlayController.JsonOptions)!;
        string snapshot = Path.Combine(Path.GetDirectoryName(manifest)!, saved.Snapshot!);
        byte[] originalSnapshot = File.ReadAllBytes(snapshot);
        using InstalledRun run = new();
        run.Controller.ResumeFrom(manifest);
        run.World.EnsureField();
        Assert.Equal(741, run.World.Field!.Id);
        Assert.Equal(71, run.World.Events.PlayerActor!.WalkmeshTriangle);
        Assert.Contains(71, run.World.Events.Scene.LockedTriangles);
        string nativeBefore = run.World.Events.Capture().GetRawText();
        int[] locks = run.World.Events.Scene.LockedTriangles.Order().ToArray();
        AutoPlayNavigationReceipt receipt = run.World.CaptureAutoPlayNavigationReceipt();
        Assert.Contains(71, receipt.ReachableTriangles);
        Assert.Contains(59, receipt.ReachableTriangles);
        Assert.Contains(72, receipt.ReachableTriangles);
        AutoPlayTarget[] targets = run.World.DiscoverAutoPlayTargets().ToArray();
        Assert.NotEmpty(targets);
        Assert.All(targets, target =>
        {
            Assert.Contains(target.Triangle, receipt.ReachableTriangles);
            Assert.DoesNotContain(target.Triangle, locks);
            if (target.ApproachTriangle is int approach)
            {
                Assert.Contains(approach, receipt.ReachableTriangles);
                Assert.DoesNotContain(approach, locks);
            }
        });
        Assert.Equal(nativeBefore, run.World.Events.Capture().GetRawText());
        Assert.Equal(locks, run.World.Events.Scene.LockedTriangles.Order());
        run.Controller.Advance(1);
        Assert.Null(run.Controller.Error);
        Assert.Equal(saved.Steps + 1, run.Controller.State.Steps);
        run.Controller.SaveAndStop();
        using InstalledRun cold = new();
        cold.Controller.ResumeFrom(run.Controller.ReproductionPath!);
        cold.Controller.Advance(1);
        Assert.Null(cold.Controller.Error);
        Assert.Equal(originalManifest, File.ReadAllBytes(manifest));
        Assert.Equal(originalSnapshot, File.ReadAllBytes(snapshot));
    }

    [Fact]
    public void AnActualCanonicalChoiceReturnCanRepeatItsAuthenticatedEdgeAfterSourceObservationChanges()
    {
        string manifest = TestResources.FixturePath("App.Tests.AutoPlayNavigationContinuationTests.3");
        byte[] originalManifest = File.ReadAllBytes(manifest);
        AutoPlayRunState saved = JsonSerializer.Deserialize<AutoPlayRunState>(originalManifest, AutoPlayController.JsonOptions)!;
        string snapshot = Path.Combine(Path.GetDirectoryName(manifest)!, saved.Snapshot!);
        byte[] originalSnapshot = File.ReadAllBytes(snapshot);
        string source = Assert.IsType<string>(saved.ActiveNavigationKey);
        Assert.Equal("127:line:16", saved.ActiveEvent);
        AutoPlayEvent observed = saved.Coverage.NavigationVisits.Single(visit => visit.Key == source).Events.Single(item => item.Id == saved.ActiveEvent);
        Assert.NotEqual(saved.ActiveConditionSignature, observed.ConditionSignature);
        AutoPlayNavigationRoute route = Assert.Single(saved.Coverage.NavigationRoutes, edge => edge.SourceKey == source && edge.EventId == saved.ActiveEvent && edge.Route.ConditionSignature == saved.ActiveConditionSignature && edge.Route.TargetId == saved.ActiveTargetId && edge.Route.Selections!.SequenceEqual(saved.ActiveSelections!));
        string history = JsonSerializer.Serialize(saved.Coverage.NavigationRetirements, AutoPlayController.JsonOptions);
        using InstalledRun run = new();
        run.Controller.ResumeFrom(manifest);
        run.Controller.Advance(1);

        Assert.Null(run.Controller.Error);
        Assert.Equal(saved.Steps + 1, run.Controller.State.Steps);
        Assert.Equal(saved.Coverage.NavigationRoutes.Count, run.Controller.State.Coverage.NavigationRoutes.Count);
        Assert.Equal(JsonSerializer.Serialize(route, AutoPlayController.JsonOptions), JsonSerializer.Serialize(run.Controller.State.Coverage.NavigationRoutes.Single(edge => edge.SourceKey == route.SourceKey && edge.DestinationKey == route.DestinationKey && edge.EventId == route.EventId && edge.Route.ConditionSignature == route.Route.ConditionSignature && edge.Route.TargetId == route.Route.TargetId && edge.Route.Selections!.SequenceEqual(route.Route.Selections!)), AutoPlayController.JsonOptions));
        Assert.Equal(JsonSerializer.Serialize(observed, AutoPlayController.JsonOptions), JsonSerializer.Serialize(run.Controller.State.Coverage.NavigationVisits.Single(visit => visit.Key == source).Events.Single(item => item.Id == saved.ActiveEvent), AutoPlayController.JsonOptions));
        Assert.Equal(history, JsonSerializer.Serialize(run.Controller.State.Coverage.NavigationRetirements, AutoPlayController.JsonOptions));
        run.Controller.SaveAndStop();
        using InstalledRun cold = new();
        cold.Controller.ResumeFrom(run.Controller.ReproductionPath!);
        cold.Controller.Advance(1);
        Assert.Null(cold.Controller.Error);
        Assert.Equal(history, JsonSerializer.Serialize(cold.Controller.State.Coverage.NavigationRetirements, AutoPlayController.JsonOptions));
        Assert.Equal(originalManifest, File.ReadAllBytes(manifest));
        Assert.Equal(originalSnapshot, File.ReadAllBytes(snapshot));
    }

    [Fact]
    public void AnActualChangedArrivalRetiresItsObsoleteNavigationEndpointAndColdResumes()
    {
        string manifest = TestResources.FixturePath("App.Tests.AutoPlayNavigationContinuationTests.2");
        byte[] originalManifest = File.ReadAllBytes(manifest);
        AutoPlayRunState saved = JsonSerializer.Deserialize<AutoPlayRunState>(originalManifest, AutoPlayController.JsonOptions)!;
        string snapshot = Path.Combine(Path.GetDirectoryName(manifest)!, saved.Snapshot!);
        byte[] originalSnapshot = File.ReadAllBytes(snapshot);
        string source = Assert.IsType<string>(saved.ActiveNavigationKey);
        string oldGoal = Assert.IsType<string>(saved.RefreshTargetVisit);
        Assert.Equal("102:gateway:3", saved.ActiveEvent);
        Assert.Empty(saved.Coverage.NavigationRetirements);
        Assert.Contains(saved.Coverage.NavigationRoutes, route => route.SourceKey == source && route.DestinationKey == oldGoal && route.EventId == saved.ActiveEvent);
        using InstalledRun run = new();
        run.Controller.ResumeFrom(manifest);
        for (int tick = 0; tick < 64 && run.Controller.State.Coverage.NavigationRetirements.Count == 0 && run.Controller.Error is null; tick++)
        {
            run.Controller.Advance(1);
        }

        Assert.Null(run.Controller.Error);
        AutoPlayRetiredNavigationRoute retired = Assert.Single(run.Controller.State.Coverage.NavigationRetirements);
        Assert.Equal(source, retired.Route.SourceKey);
        Assert.Equal(oldGoal, retired.Route.DestinationKey);
        Assert.Equal(saved.ActiveEvent, retired.Route.EventId);
        Assert.NotEqual(oldGoal, retired.ActualDestinationKey);
        Assert.Contains(run.Controller.State.Coverage.NavigationRoutes, route => route.SourceKey == source && route.DestinationKey == retired.ActualDestinationKey && route.EventId == saved.ActiveEvent);
        Assert.DoesNotContain(run.Controller.State.Coverage.NavigationRoutes, route => route.SourceKey == source && route.DestinationKey == oldGoal && route.EventId == saved.ActiveEvent);
        Assert.DoesNotContain(oldGoal, run.Controller.State.InspectedVisitsSinceProgress);
        Assert.NotEqual(oldGoal, run.Controller.State.RefreshTargetVisit);
        Assert.Equal(saved.Coverage.Choices.Count, run.Controller.State.Coverage.Choices.Count);
        Assert.Equal(saved.Coverage.Choices.Sum(choice => choice.CompletedOptions.Count), run.Controller.State.Coverage.Choices.Sum(choice => choice.CompletedOptions.Count));
        string history = JsonSerializer.Serialize(run.Controller.State.Coverage.NavigationRetirements, AutoPlayController.JsonOptions);
        run.Controller.SaveAndStop();
        using InstalledRun cold = new();
        cold.Controller.ResumeFrom(run.Controller.ReproductionPath!);
        cold.Controller.Advance(1);
        Assert.Null(cold.Controller.Error);
        Assert.Equal(history, JsonSerializer.Serialize(cold.Controller.State.Coverage.NavigationRetirements, AutoPlayController.JsonOptions));
        Assert.Equal(originalManifest, File.ReadAllBytes(manifest));
        Assert.Equal(originalSnapshot, File.ReadAllBytes(snapshot));
    }

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
