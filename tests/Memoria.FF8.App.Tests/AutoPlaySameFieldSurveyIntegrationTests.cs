using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlaySameFieldSurveyIntegrationTests
{
    [Fact]
    public void AnOrdinaryWardrobeReloadInvalidatesTheRemoteSurveyBeforeTheSameFieldInitializesAgain()
    {
        string manifest = TestResources.FixturePath("App.Tests.AutoPlaySameFieldSurveyIntegrationTests.1");
        byte[] originalManifest = File.ReadAllBytes(manifest);
        AutoPlayRunState saved = JsonSerializer.Deserialize<AutoPlayRunState>(originalManifest, AutoPlayController.JsonOptions)!;
        string snapshot = Path.Combine(Path.GetDirectoryName(manifest)!, saved.Snapshot!);
        byte[] originalSnapshot = File.ReadAllBytes(snapshot);
        using InstalledRun run = new();
        run.Controller.ResumeFrom(manifest);
        run.World.EnsureField();
        Assert.Equal(241, run.Session.State.FieldId);
        Assert.Equal(6, run.World.Events.Frame);
        Assert.Equal(1, run.World.Events.EventMemory.KnownValues[720]);
        string staleNavigationKey = Assert.Single(run.Controller.State.InspectedVisitsSinceProgress, key => key.StartsWith("243:145:nav:", StringComparison.Ordinal));
        run.StaleNavigationKey = staleNavigationKey;
        AutoPlaySurveyBaseline originalBaseline = Assert.IsType<AutoPlaySurveyBaseline>(run.Controller.State.SurveyBaseline);
        AutoPlayVisitGuard remoteGuard = originalBaseline.Guards[staleNavigationKey];
        Assert.Contains(remoteGuard.Reads, read => read.Address == 720 && read.Width == 1);
        string originalFingerprint = remoteGuard.Fingerprint;

        for (int tick = 0; tick < 128 && run.FirstDefaultBaseline is null && run.Controller.Error is null; tick++)
        {
            run.Controller.Advance(1);
        }

        Assert.True(run.Controller.Error is null, $"{run.Controller.Error}; {run.Controller.ReproductionPath}");
        Assert.Null(run.World.Events.LastFault);
        Assert.True(run.BeforeSameFieldAllocationSeen);
        Assert.Equal(2, run.WardrobeBeforeAllocation);
        Assert.NotNull(run.FingerprintBeforeAllocation);
        Assert.NotEqual(originalFingerprint, run.FingerprintBeforeAllocation);
        Assert.True(run.AllocatedSeen);
        Assert.Equal(0, run.AllocationFrame);
        Assert.False(run.StaleInspectedAtAllocation);
        Assert.True(run.CompletedSeen);
        Assert.Equal(1, run.CompletionFrame);
        Assert.False(run.StaleInspectedAtCompletion);
        Assert.NotEqual(originalFingerprint, run.CompletedFingerprint);
        AutoPlaySurveyBaseline replacement = Assert.IsType<AutoPlaySurveyBaseline>(run.FirstDefaultBaseline);
        Assert.Equal(241, replacement.FieldId);
        Assert.Equal(originalBaseline.ProgramHash, replacement.ProgramHash);
        Assert.Equal(run.CompletedFingerprint, replacement.Guards[staleNavigationKey].Fingerprint);
        Assert.False(run.StaleInspectedAtFirstDefault);
        Assert.Contains(run.Commands, command => command.FieldId == 241 && command.Name.StartsWith("MAPJUMP", StringComparison.Ordinal) && command.Arguments.Count > 0 && command.Arguments[0] == 241);

        string survey = SurveyMetadata(run.Controller.State);
        run.Controller.SaveAndStop();
        using InstalledRun cold = new();
        cold.Controller.ResumeFrom(run.Controller.ReproductionPath!);
        cold.World.EnsureField();
        Assert.Equal(survey, SurveyMetadata(cold.Controller.State));
        Assert.True(JsonElement.DeepEquals(run.World.Events.Capture(), cold.World.Events.Capture()));
        Assert.Equal(2, cold.World.Events.EventMemory.KnownValues[720]);

        List<int> visited = [cold.Session.State.FieldId];
        for (int tick = 0; tick < 256 && cold.Session.State.FieldId != 221 && cold.Controller.Error is null; tick++)
        {
            cold.Controller.Advance(1);
            visited.Add(cold.Session.State.FieldId);
        }
        Assert.True(cold.Controller.Error is null, $"{cold.Controller.Error}; {cold.Controller.ReproductionPath}");
        Assert.Null(cold.World.Events.LastFault);
        Assert.Contains(243, visited);
        Assert.Equal(221, cold.Session.State.FieldId);
        Assert.Contains(cold.Commands, command => command is { FieldId: 243, Name: "MAPJUMPO" } && command.Arguments[0] == 221);
        Assert.Equal(originalManifest, File.ReadAllBytes(manifest));
        Assert.Equal(originalSnapshot, File.ReadAllBytes(snapshot));
    }

    private static string SurveyMetadata(AutoPlayRunState state)
    {
        return JsonSerializer.Serialize(new { state.SurveyBaseline, state.SurveyInvalidationUnknown, state.SurveyProgressBeforeInitialization, Inspected = state.InspectedVisitsSinceProgress.Order(StringComparer.Ordinal).ToArray(), state.RefreshTargetVisit, state.VisitGuards, state.NavigationReceipts }, AutoPlayController.JsonOptions);
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
        public string Output { get; } = Path.Combine(Path.GetTempPath(), "ff8-autoplay-same-field-survey-" + Guid.NewGuid().ToString("N"));
        public AutoPlayController Controller { get; }
        public string? StaleNavigationKey { get; set; }
        public List<ScriptCommand> Commands { get; } = [];
        public bool BeforeSameFieldAllocationSeen { get; private set; }
        public int WardrobeBeforeAllocation { get; private set; }
        public string? FingerprintBeforeAllocation { get; private set; }
        public bool AllocatedSeen { get; private set; }
        public long AllocationFrame { get; private set; }
        public bool StaleInspectedAtAllocation { get; private set; }
        public bool CompletedSeen { get; private set; }
        public long CompletionFrame { get; private set; }
        public bool StaleInspectedAtCompletion { get; private set; }
        public string? CompletedFingerprint { get; private set; }
        public AutoPlaySurveyBaseline? FirstDefaultBaseline { get; private set; }
        public bool StaleInspectedAtFirstDefault { get; private set; }

        public GameWorld World
        {
            get
            {
                if (world is null)
                {
                    world = new(Session, gameDirectory, validateWorldPresentation: false);
                    GameWorld observed = world;
                    // These listeners precede the controller's scoped subscriptions. Frame zero observes the completed preallocation hook chain.
                    observed.BeforeFieldAllocation += ObserveBeforeAllocation;
                    observed.Events.OperationCompleted += () => ObserveOperation(observed);
                    observed.Events.CommandExecuted += command =>
                    {
                        Commands.Add(command);
                        if (CompletedSeen && FirstDefaultBaseline is null && observed.Events.FieldId == 241 && observed.Events.Scene.NativeFieldInitialization is { Stage: NativeFieldInitializationStage.Completed })
                        {
                            FirstDefaultBaseline = Controller.State.SurveyBaseline;
                            StaleInspectedAtFirstDefault = IsStaleInspected();
                        }
                    };
                }
                return world;
            }
        }

        private void ObserveBeforeAllocation(GameWorld observed)
        {
            if (BeforeSameFieldAllocationSeen || StaleNavigationKey is null || observed.Events.FieldId != 241 || Session.State.FieldId != 241 || observed.Events.EventMemory.KnownValues[720] != 2)
            {
                return;
            }
            BeforeSameFieldAllocationSeen = true;
            WardrobeBeforeAllocation = observed.Events.EventMemory.KnownValues[720];
            FingerprintBeforeAllocation = observed.Events.ReadPersistentPredicateFingerprint(Controller.State.VisitGuards[StaleNavigationKey].Reads);
        }

        private void ObserveOperation(GameWorld observed)
        {
            if (!BeforeSameFieldAllocationSeen || observed.Events.FieldId != 241)
            {
                return;
            }
            if (!AllocatedSeen && observed.Events.Frame == 0 && observed.Events.Scene.NativeFieldInitialization is { Stage: NativeFieldInitializationStage.Allocated })
            {
                AllocatedSeen = true;
                AllocationFrame = observed.Events.Frame;
                StaleInspectedAtAllocation = IsStaleInspected();
            }
            if (AllocatedSeen && !CompletedSeen && observed.Events.Frame == 1 && observed.Events.Scene.NativeFieldInitialization is { Stage: NativeFieldInitializationStage.Completed })
            {
                CompletedSeen = true;
                CompletionFrame = observed.Events.Frame;
                StaleInspectedAtCompletion = IsStaleInspected();
                CompletedFingerprint = observed.Events.ReadPersistentPredicateFingerprint(Controller.State.VisitGuards[StaleNavigationKey!].Reads);
            }
        }

        private bool IsStaleInspected()
        {
            return StaleNavigationKey is not null && Controller.State.InspectedVisitsSinceProgress.Contains(StaleNavigationKey);
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
