using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlayConditionEntryTests
{
    [Fact]
    public void AConditionObservedOnlyAfterReturnRemainsAnUnexecutedEntry()
    {
        AutoPlayPlanner planner = CompleteInitialEntry("entry-zero", "exit-two");
        Observe(planner, "exit-two");

        AutoPlayPlanStep next = Assert.IsType<AutoPlayPlanStep>(planner.Plan("room"));
        Assert.Equal("talk", next.Event.Id);
        Assert.False(next.IsTravel);
        AutoPlayConditionCoverage conditions = Entry(planner).Conditions!;
        Assert.Equal(new[] { "entry-zero" }, conditions.ConsumedEntries);
        Assert.Equal(new[] { "exit-two" }, conditions.ConsumedExits);
        Assert.True(Entry(planner).Completed);

        planner.BeginEvent("room", "talk");
        planner.Completed("room", "talk", exitSignature: "exit-three");
        Observe(planner, "exit-two");
        Assert.Null(planner.Plan("room"));
        Assert.Contains("exit-two", conditions.ConsumedEntries);
        Assert.Contains("exit-three", conditions.ConsumedExits);
    }

    [Fact]
    public void ColdCoveragePreservesExitHistoryAndTheStillPendingEntry()
    {
        AutoPlayPlanner planner = CompleteInitialEntry("entry-zero", "exit-two");
        Observe(planner, "exit-two");
        planner = Restore(planner);

        Assert.NotNull(planner.Plan("room"));
        Assert.Equal(new[] { "entry-zero" }, Entry(planner).Conditions!.ConsumedEntries);
        Assert.Equal(new[] { "exit-two" }, Entry(planner).Conditions!.ConsumedExits);
        Observe(planner, "entry-zero");
        Assert.Null(planner.Plan("room"));
        planner = Restore(planner);
        Assert.Null(planner.Plan("room"));
        Observe(planner, "exit-two");
        Assert.NotNull(planner.Plan("room"));
    }

    [Fact]
    public void AnUnchangedExecutedEntryDoesNotRepeatEvenWhenItsExitWasIdentical()
    {
        AutoPlayPlanner planner = CompleteInitialEntry("unchanged", "unchanged");
        for (int observation = 0; observation < 3; observation++)
        {
            planner = Restore(planner);
            Observe(planner, "unchanged");
            Assert.Null(planner.Plan("room"));
        }
        Assert.Equal(new[] { "unchanged" }, Entry(planner).Conditions!.ConsumedEntries);
        Assert.Equal(new[] { "unchanged" }, Entry(planner).Conditions!.ConsumedExits);
    }

    [Fact]
    public void AnUnexecutedExitDoesNotBypassAvailabilityOrCreateADisconnectedRoute()
    {
        AutoPlayPlanner planner = CompleteInitialEntry("entry-zero", "exit-two");
        Observe(planner, "exit-two");
        planner.Observe(new("room", "region"), []);
        Assert.Null(planner.Plan("room"));
        Assert.False(Entry(planner).Available);
        Observe(planner, "exit-two");
        Assert.NotNull(planner.Plan("room"));

        planner.Observe(new("isolated", "region"), []);
        Assert.Null(planner.Plan("isolated"));
        Assert.Empty(Entry(planner).Routes);
        Assert.Empty(planner.State.NavigationRoutes);
        Assert.DoesNotContain("exit-two", Entry(planner).Conditions!.ConsumedEntries);
    }

    [Fact]
    public void AnActualExitOnlyPredicateAllowsTheNextNativeTalkBranchAndColdContinuation()
    {
        string manifest = TestResources.FixturePath("App.Tests.AutoPlayConditionEntryTests.1");
        byte[] originalManifest = File.ReadAllBytes(manifest);
        AutoPlayRunState saved = JsonSerializer.Deserialize<AutoPlayRunState>(originalManifest, AutoPlayController.JsonOptions)!;
        string snapshot = Path.Combine(Path.GetDirectoryName(manifest)!, saved.Snapshot!);
        byte[] originalSnapshot = File.ReadAllBytes(snapshot);
        using InstalledRun run = new();
        run.Controller.ResumeFrom(manifest);
        run.World.EnsureField();
        Assert.Equal(737, run.Session.State.FieldId);
        Assert.True(run.World.Events.IsUserControlEnabled);
        Assert.NotNull(run.World.Events.PlayerActor);
        Assert.Equal(new[] { 8, 255, 255 }, run.World.Events.Scene.Party);
        Assert.Equal(2, run.Session.State.EventMemory.KnownValues[1025]);
        JsonElement beforeRead = run.World.Events.Capture();
        GameState beforeState = run.Session.State;
        string signature = run.World.ReadAutoPlayConditions("737:talk:8", 8).Fingerprint;
        AutoPlayEventCoverage coverage = run.Controller.State.Coverage.Visits.Single(visit => visit.Visit.Key == "737:228").Events.Single(item => item.Event.Id == "737:talk:8");
        Assert.Contains(signature, coverage.Conditions!.ConsumedExits);
        Assert.DoesNotContain(signature, coverage.Conditions.ConsumedEntries);
        Assert.True(JsonElement.DeepEquals(beforeRead, run.World.Events.Capture()));
        Assert.Same(beforeState, run.Session.State);

        for (int tick = 0; tick < 20 && !run.WroteNextBranch && run.Controller.Error is null; tick++)
        {
            run.Controller.Advance(1);
        }

        Assert.True(run.Controller.Error is null, $"{run.Controller.Error}; {run.Controller.ReproductionPath}");
        Assert.Null(run.World.Events.LastFault);
        Assert.True(run.WroteNextBranch);
        Assert.All(run.PartySamples, party => Assert.Equal(new[] { 8, 255, 255 }, party));
        // The native talk may transfer fields in the same callback as its write. Save after the actual destination initialization.
        for (int callback = 0; callback < 8 && run.Controller.Error is null; callback++)
        {
            run.World.EnsureField();
            if (run.World.HasCurrentFieldAllocation && run.World.Events.Scene.NativeFieldInitialization is { Stage: NativeFieldInitializationStage.Completed })
            {
                break;
            }
            Assert.True(run.World.Apply(new(PlayerActionKind.Wait)).Accepted);
            Assert.Null(run.World.Events.LastFault);
        }
        Assert.True(run.Controller.Error is null, $"{run.Controller.Error}; {run.Controller.ReproductionPath}");
        Assert.True(run.World.HasCurrentFieldAllocation);
        Assert.Equal(run.Session.State.FieldId, run.World.Events.FieldId);
        Assert.Equal(NativeFieldInitializationStage.Completed, run.World.Events.Scene.NativeFieldInitialization!.Stage);
        run.Controller.SaveAndStop();
        JsonElement native = run.World.Events.Capture();
        using InstalledRun cold = new();
        cold.Controller.ResumeFrom(run.Controller.ReproductionPath!);
        cold.World.EnsureField();
        Assert.True(JsonElement.DeepEquals(native, cold.World.Events.Capture()));
        Assert.Equal(JsonSerializer.Serialize(run.Controller.State.Coverage, AutoPlayController.JsonOptions), JsonSerializer.Serialize(cold.Controller.State.Coverage, AutoPlayController.JsonOptions));
        cold.Controller.Advance(1);
        Assert.True(cold.Controller.Error is null, $"{cold.Controller.Error}; {cold.Controller.ReproductionPath}");
        Assert.Null(cold.World.Events.LastFault);
        Assert.Equal(originalManifest, File.ReadAllBytes(manifest));
        Assert.Equal(originalSnapshot, File.ReadAllBytes(snapshot));
        run.Verified = true;
        cold.Verified = true;
    }

    private static AutoPlayPlanner CompleteInitialEntry(string entry, string exit)
    {
        AutoPlayPlanner planner = new();
        Observe(planner, entry);
        planner.BeginEvent("room", "talk");
        planner.Completed("room", "talk", exitSignature: exit);
        return planner;
    }

    private static void Observe(AutoPlayPlanner planner, string signature)
    {
        planner.Observe(new("room", "region"), [new("talk", AutoPlayEventKind.Interaction, ConditionSignature: signature)]);
    }

    private static AutoPlayEventCoverage Entry(AutoPlayPlanner planner)
    {
        return planner.State.Visits.Single(visit => visit.Visit.Key == "room").Events.Single();
    }

    private static AutoPlayPlanner Restore(AutoPlayPlanner planner)
    {
        return new(JsonSerializer.Deserialize<AutoPlayPlannerState>(JsonSerializer.Serialize(planner.State))!);
    }

    private sealed class InstalledRun : IDisposable
    {
        private GameWorld? world;
        private readonly string output = Path.Combine(Path.GetTempPath(), "ff8-condition-entry-" + Guid.NewGuid().ToString("N"));

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
        internal AutoPlayController Controller { get; }
        internal bool WroteNextBranch { get; private set; }
        internal bool Verified { get; set; }
        internal List<int[]> PartySamples { get; } = [];

        internal GameWorld World
        {
            get
            {
                if (world is null)
                {
                    world = new(Session, TestResources.GameDirectory, validateWorldPresentation: false);
                    GameWorld observed = world;
                    observed.Events.CommandExecuted += command =>
                    {
                        if (command.FieldId == 737 && !WroteNextBranch)
                        {
                            PartySamples.Add(observed.Events.Scene.Party.ToArray());
                        }
                        if (command.FieldId == 737 && command.ObjectId == 8 && command.ScriptId == 115 && command.Operation == 690 && command.Name == "POPM_B" && command.Arguments.SequenceEqual(new[] { 3 }))
                        {
                            WroteNextBranch = true;
                        }
                    };
                }
                return world;
            }
        }

        public void Dispose()
        {
            world?.Dispose();
            if (Verified && Directory.Exists(output))
            {
                Directory.Delete(output, true);
            }
        }
    }
}
