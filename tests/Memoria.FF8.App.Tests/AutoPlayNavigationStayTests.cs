using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlayNavigationStayTests
{
    [Fact]
    public void AnActualReturnToTheSameNavigationVertexConfirmsItsTravelProbeWithoutRepeatingTheCompletedEvent()
    {
        string manifest = TestResources.FixturePath("App.Tests.AutoPlayNavigationStayTests.1");
        byte[] originalManifest = File.ReadAllBytes(manifest);
        AutoPlayRunState saved = JsonSerializer.Deserialize<AutoPlayRunState>(originalManifest, AutoPlayController.JsonOptions)!;
        string snapshot = Path.Combine(Path.GetDirectoryName(manifest)!, saved.Snapshot!);
        byte[] originalSnapshot = File.ReadAllBytes(snapshot);
        using InstalledRun run = new();
        run.Controller.ResumeFrom(manifest);
        run.World.EnsureField();
        Assert.Equal(741, run.Session.State.FieldId);
        Assert.Equal("741:area:0", run.Controller.State.ActiveEvent);
        string source = Assert.IsType<string>(run.Controller.State.ActiveNavigationKey);
        string eventId = Assert.IsType<string>(run.Controller.State.ActiveEvent);
        AutoPlayEventRoute route = Assert.IsType<AutoPlayEventRoute>(run.Controller.State.ActiveTravelRoute);
        Assert.Equal("741:223", route.DestinationVisitKey);
        JsonElement beforeRead = run.World.Events.Capture();
        GameState stateBeforeRead = run.Session.State;
        Assert.Equal(source, run.World.CaptureAutoPlayNavigationReceipt().Key("741:223"));
        Assert.True(JsonElement.DeepEquals(beforeRead, run.World.Events.Capture()));
        Assert.Same(stateBeforeRead, run.Session.State);
        AutoPlayEventCoverage originalEvent = Entry(run);
        Assert.True(originalEvent.Completed);
        AutoPlayConditionCoverage originalConditions = Assert.IsType<AutoPlayConditionCoverage>(originalEvent.Conditions);
        string[] entries = originalConditions.ConsumedEntries.Order(StringComparer.Ordinal).ToArray();
        string[] exits = originalConditions.ConsumedExits.Order(StringComparer.Ordinal).ToArray();
        string routes = JsonSerializer.Serialize(originalEvent.Routes, AutoPlayController.JsonOptions);
        int completedOptions = run.Controller.State.Coverage.Choices.Sum(choice => choice.CompletedOptions.Count);
        AutoPlayNavigationRoute[] previousEdges = run.Controller.State.Coverage.NavigationRoutes.Where(edge => edge.EventId == eventId).ToArray();
        Assert.NotEmpty(previousEdges);
        Assert.DoesNotContain(previousEdges, edge => edge.SourceKey == source);

        run.Controller.Advance(1);

        Assert.True(run.Controller.Error is null, $"{run.Controller.Error}; {run.Controller.ReproductionPath}");
        Assert.Null(run.World.Events.LastFault);
        AutoPlayNavigationRoute confirmed = Assert.Single(run.Controller.State.Coverage.NavigationRoutes, edge => edge.SourceKey == source && edge.EventId == eventId);
        Assert.Equal(source, confirmed.DestinationKey);
        Assert.Equal(JsonSerializer.Serialize(route, AutoPlayController.JsonOptions), JsonSerializer.Serialize(confirmed.Route, AutoPlayController.JsonOptions));
        Assert.All(previousEdges, edge => Assert.Contains(edge, run.Controller.State.Coverage.NavigationRoutes));
        Assert.True(Entry(run).Completed);
        AutoPlayConditionCoverage completedConditions = Assert.IsType<AutoPlayConditionCoverage>(Entry(run).Conditions);
        Assert.Equal(entries, completedConditions.ConsumedEntries.Order(StringComparer.Ordinal));
        Assert.Equal(exits, completedConditions.ConsumedExits.Order(StringComparer.Ordinal));
        Assert.Equal(routes, JsonSerializer.Serialize(Entry(run).Routes, AutoPlayController.JsonOptions));
        Assert.Equal(completedOptions, run.Controller.State.Coverage.Choices.Sum(choice => choice.CompletedOptions.Count));
        Assert.NotEqual(eventId, run.Controller.State.ActiveEvent);

        // A following ordinary tick may allocate the next field. Save only after that allocation is complete.
        for (int tick = 0; tick < 8 && !HasCompletedCurrentAllocation(run) && run.Controller.Error is null; tick++)
        {
            run.Controller.Advance(1);
            Assert.NotEqual(eventId, run.Controller.State.ActiveEvent);
        }
        Assert.True(run.Controller.Error is null, $"{run.Controller.Error}; {run.Controller.ReproductionPath}");
        Assert.True(HasCompletedCurrentAllocation(run));
        Assert.Single(run.Controller.State.Coverage.NavigationRoutes, edge => edge.SourceKey == source && edge.EventId == eventId);
        run.Controller.SaveAndStop();
        string navigation = NavigationMetadata(run.Controller.State);
        using InstalledRun cold = new();
        cold.Controller.ResumeFrom(run.Controller.ReproductionPath!);
        cold.World.EnsureField();
        Assert.Equal(navigation, NavigationMetadata(cold.Controller.State));
        Assert.True(JsonElement.DeepEquals(run.World.Events.Capture(), cold.World.Events.Capture()));
        Assert.Equal(run.Session.State.FieldId, cold.Session.State.FieldId);
        Assert.Equal(run.World.Events.Scene.Party, cold.World.Events.Scene.Party);
        Assert.Equal(originalManifest, File.ReadAllBytes(manifest));
        Assert.Equal(originalSnapshot, File.ReadAllBytes(snapshot));
        run.Verified = true;
        cold.Verified = true;
    }

    private static bool HasCompletedCurrentAllocation(InstalledRun run)
    {
        return run.World.HasCurrentFieldAllocation && run.World.Events.Scene.NativeFieldInitialization is { Stage: NativeFieldInitializationStage.Completed };
    }

    private static AutoPlayEventCoverage Entry(InstalledRun run)
    {
        return run.Controller.State.Coverage.Visits.Single(visit => visit.Visit.Key == "741:223").Events.Single(item => item.Event.Id == "741:area:0");
    }

    private static string NavigationMetadata(AutoPlayRunState state)
    {
        return JsonSerializer.Serialize(new { state.Coverage.NavigationVisits, state.Coverage.NavigationRoutes, state.Coverage.NavigationRetirements, state.NavigationReceipts, state.ActiveNavigationKey, state.RefreshTargetVisit, Inspected = state.InspectedVisitsSinceProgress.Order(StringComparer.Ordinal).ToArray() }, AutoPlayController.JsonOptions);
    }

    private sealed class InstalledRun : IDisposable
    {
        private readonly string gameDirectory = TestResources.GameDirectory;
        private GameWorld? world;

        public InstalledRun()
        {
            Controller = new(Session, () => World, Restore, Output);
        }

        public GameSession Session { get; } = GameSession.CreateColdProcess();
        public string Output { get; } = Path.Combine(Path.GetTempPath(), "ff8-autoplay-navigation-stay-" + Guid.NewGuid().ToString("N"));
        public AutoPlayController Controller { get; }
        public bool Verified { get; set; }
        public GameWorld World => world ??= new(Session, gameDirectory, validateWorldPresentation: false);

        private void Restore(GameState state)
        {
            world?.Dispose();
            world = null;
            Session.Restore(state);
        }

        public void Dispose()
        {
            world?.Dispose();
            if (Verified && Directory.Exists(Output))
            {
                Directory.Delete(Output, true);
            }
        }
    }
}
