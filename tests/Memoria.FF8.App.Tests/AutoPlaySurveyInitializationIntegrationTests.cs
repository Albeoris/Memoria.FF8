using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlaySurveyInitializationIntegrationTests
{
    [Fact]
    public void RealInitializationEstablishesItsPostInitializationBaselineBeforeDefaultCommands()
    {
        string manifest = TestResources.FixturePath("App.Tests.AutoPlaySurveyInitializationIntegrationTests.1");
        byte[] originalManifest = File.ReadAllBytes(manifest);
        AutoPlayRunState saved = JsonSerializer.Deserialize<AutoPlayRunState>(originalManifest, AutoPlayController.JsonOptions)!;
        string snapshot = Path.Combine(Path.GetDirectoryName(manifest)!, saved.Snapshot!);
        byte[] originalSnapshot = File.ReadAllBytes(snapshot);
        using InstalledRun run = new();
        run.Controller.ResumeFrom(manifest);
        run.World.EnsureField();
        Assert.Equal(174, run.Session.State.FieldId);
        Assert.False(run.Controller.State.SurveyInvalidationUnknown);
        AutoPlayTarget exit = run.World.DiscoverAutoPlayTargets().First(target => target.CoverageId == "174:line:26");
        FieldTeleportPosition approach = new(exit.ApproachX!.Value, exit.ApproachY!.Value, exit.ApproachZ!.Value, exit.ApproachTriangle!.Value, exit.Direction);
        FieldTeleportContactResult contact = run.World.Events.TeleportPlayerAndDispatchContacts(new(exit.X, exit.Y, exit.Z, exit.Triangle, exit.Direction), approach);
        Assert.True(contact.ContactTriggered);
        for (int callback = 0; callback < 16 && run.Session.State.FieldId == 174; callback++)
        {
            Assert.True(run.World.Apply(new(PlayerActionKind.Wait)).Accepted);
        }
        Assert.Equal(224, run.Session.State.FieldId);
        Assert.Equal(174, run.World.Events.FieldId);
        for (int tick = 0; tick < 16 && !run.FirstDefaultSeen && run.Controller.Error is null; tick++)
        {
            run.Controller.Advance(1);
        }

        Assert.Null(run.Controller.Error);
        Assert.True(run.FirstDefaultSeen);
        Assert.NotNull(run.AllocatedInspections);
        Assert.NotEmpty(run.AllocatedInspections);
        Dictionary<string, string> completedFingerprints = Assert.IsType<Dictionary<string, string>>(run.CompletedFingerprints);
        Assert.Equal(1, run.CompletionFrame);
        Assert.Equal(0, run.DefaultsAtCompletion);
        Assert.Equal(run.AllocatedInspections, run.CompletedInspections);
        Assert.Equal(run.AllocatedInspections, run.FirstDefaultInspections);
        AutoPlaySurveyBaseline baseline = Assert.IsType<AutoPlaySurveyBaseline>(run.FirstDefaultBaseline);
        Assert.Equal(1, baseline.Version);
        Assert.Equal(224, baseline.FieldId);
        Assert.Equal(run.World.ReadAutoPlayFieldProgramHash(224), baseline.ProgramHash);
        Assert.NotEmpty(baseline.Guards);
        Assert.All(baseline.Guards, pair => Assert.Equal(completedFingerprints[pair.Key], pair.Value.Fingerprint));
        Assert.False(run.Controller.State.SurveyInvalidationUnknown);
        string persisted = JsonSerializer.Serialize(run.Controller.State.SurveyBaseline, AutoPlayController.JsonOptions);
        Assert.NotNull(run.Controller.State.SurveyBaseline);
        string[] inspected = run.Controller.State.InspectedVisitsSinceProgress.Order(StringComparer.Ordinal).ToArray();
        run.Controller.SaveAndStop();
        using InstalledRun cold = new();
        cold.Controller.ResumeFrom(run.Controller.ReproductionPath!);
        cold.World.EnsureField();
        Assert.Equal(persisted, JsonSerializer.Serialize(cold.Controller.State.SurveyBaseline, AutoPlayController.JsonOptions));
        Assert.Equal(inspected, cold.Controller.State.InspectedVisitsSinceProgress.Order(StringComparer.Ordinal));
        Assert.False(cold.Controller.State.SurveyInvalidationUnknown);
        Assert.True(JsonElement.DeepEquals(run.World.Events.Capture(), cold.World.Events.Capture()));
        Assert.False(cold.FirstDefaultSeen);
        Assert.Equal(originalManifest, File.ReadAllBytes(manifest));
        Assert.Equal(originalSnapshot, File.ReadAllBytes(snapshot));
    }

    [Fact]
    public void AnAuthenticLegacyActiveArrivalInvalidatesOnceAndPersistsTheKnownSurveyDomain()
    {
        string manifest = TestResources.FixturePath("App.Tests.AutoPlaySurveyInitializationIntegrationTests.2");
        byte[] originalManifest = File.ReadAllBytes(manifest);
        AutoPlayRunState saved = JsonSerializer.Deserialize<AutoPlayRunState>(originalManifest, AutoPlayController.JsonOptions)!;
        string snapshot = Path.Combine(Path.GetDirectoryName(manifest)!, saved.Snapshot!);
        byte[] originalSnapshot = File.ReadAllBytes(snapshot);
        using InstalledRun run = new();
        run.Controller.ResumeFrom(manifest);
        run.World.EnsureField();
        Assert.Equal(224, run.Session.State.FieldId);
        Assert.Equal(2, run.World.Events.Frame);
        Assert.True(run.Controller.State.SurveyInvalidationUnknown);
        Assert.NotEmpty(run.Controller.State.InspectedVisitsSinceProgress);
        for (int tick = 0; tick < 8 && run.Controller.State.SurveyInvalidationUnknown && run.Controller.Error is null; tick++)
        {
            run.Controller.Advance(1);
        }
        Assert.Null(run.Controller.Error);
        Assert.False(run.Controller.State.SurveyInvalidationUnknown);
        Assert.Empty(run.Controller.State.InspectedVisitsSinceProgress);
        for (int tick = 0; tick < 32 && (run.Controller.State.InspectedVisitsSinceProgress.Count == 0 || run.Controller.State.SurveyBaseline is null) && run.Controller.Error is null; tick++)
        {
            run.Controller.Advance(1);
            Assert.False(run.Controller.State.SurveyInvalidationUnknown);
        }
        Assert.Null(run.Controller.Error);
        Assert.NotEmpty(run.Controller.State.InspectedVisitsSinceProgress);
        Assert.NotNull(run.Controller.State.SurveyBaseline);
        string baseline = JsonSerializer.Serialize(run.Controller.State.SurveyBaseline, AutoPlayController.JsonOptions);
        string[] inspected = run.Controller.State.InspectedVisitsSinceProgress.Order(StringComparer.Ordinal).ToArray();
        run.Controller.SaveAndStop();
        using InstalledRun cold = new();
        cold.Controller.ResumeFrom(run.Controller.ReproductionPath!);
        cold.World.EnsureField();
        Assert.False(cold.Controller.State.SurveyInvalidationUnknown);
        Assert.Equal(baseline, JsonSerializer.Serialize(cold.Controller.State.SurveyBaseline, AutoPlayController.JsonOptions));
        Assert.Equal(inspected, cold.Controller.State.InspectedVisitsSinceProgress.Order(StringComparer.Ordinal));
        Assert.True(JsonElement.DeepEquals(run.World.Events.Capture(), cold.World.Events.Capture()));
        Assert.Equal(originalManifest, File.ReadAllBytes(manifest));
        Assert.Equal(originalSnapshot, File.ReadAllBytes(snapshot));
    }

    private sealed class InstalledRun : IDisposable
    {
        private readonly string gameDirectory = TestResources.GameDirectory;
        private GameWorld? world;
        private int defaultCommands;

        public InstalledRun()
        {
            Controller = new(Session, () => World, Restore, Output);
        }

        public GameSession Session { get; } = GameSession.CreateColdProcess();
        public string Output { get; } = Path.Combine(Path.GetTempPath(), "ff8-autoplay-survey-initialization-" + Guid.NewGuid().ToString("N"));
        public AutoPlayController Controller { get; }
        public string[]? AllocatedInspections { get; private set; }
        public string[]? CompletedInspections { get; private set; }
        public Dictionary<string, string>? CompletedFingerprints { get; private set; }
        public long CompletionFrame { get; private set; }
        public int DefaultsAtCompletion { get; private set; }
        public bool FirstDefaultSeen { get; private set; }
        public AutoPlaySurveyBaseline? FirstDefaultBaseline { get; private set; }
        public string[]? FirstDefaultInspections { get; private set; }

        public GameWorld World
        {
            get
            {
                if (world is null)
                {
                    world = new(Session, gameDirectory, validateWorldPresentation: false);
                    GameWorld observed = world;
                    observed.Events.OperationCompleted += () => ObserveOperation(observed);
                    observed.Events.CommandExecuted += _ =>
                    {
                        if (observed.Events.FieldId == 224 && observed.Events.Scene.NativeFieldInitialization is { Stage: NativeFieldInitializationStage.Completed })
                        {
                            if (!FirstDefaultSeen)
                            {
                                FirstDefaultSeen = true;
                                FirstDefaultBaseline = Controller.State.SurveyBaseline;
                                FirstDefaultInspections = Controller.State.InspectedVisitsSinceProgress.Order(StringComparer.Ordinal).ToArray();
                            }
                            defaultCommands++;
                        }
                    };
                }
                return world;
            }
        }

        private void ObserveOperation(GameWorld observed)
        {
            if (observed.Events.FieldId != 224)
            {
                return;
            }
            if (observed.Events.Frame == 0 && observed.Events.Scene.NativeFieldInitialization is { Stage: NativeFieldInitializationStage.Allocated })
            {
                AllocatedInspections ??= Controller.State.InspectedVisitsSinceProgress.Order(StringComparer.Ordinal).ToArray();
            }
            if (CompletedFingerprints is null && observed.Events.Frame == 1 && observed.Events.Scene.NativeFieldInitialization is { Stage: NativeFieldInitializationStage.Completed })
            {
                CompletionFrame = observed.Events.Frame;
                DefaultsAtCompletion = defaultCommands;
                CompletedInspections = Controller.State.InspectedVisitsSinceProgress.Order(StringComparer.Ordinal).ToArray();
                CompletedFingerprints = new(StringComparer.Ordinal);
                foreach ((string key, AutoPlayVisitGuard guard) in Controller.State.VisitGuards)
                {
                    if (guard.Version == AutoPlayVisitGuard.CurrentVersion && observed.Events.ReadPersistentPredicateFingerprint(guard.Reads) is string fingerprint)
                    {
                        CompletedFingerprints.Add(key, fingerprint);
                    }
                }
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
