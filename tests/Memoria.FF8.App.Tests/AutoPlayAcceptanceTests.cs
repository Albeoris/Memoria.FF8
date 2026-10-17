using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlayAcceptanceTests
{
    [Fact]
    public void ScriptedChoiceReplayRecoversAContactOwnerBetweenNativeCallbacks()
    {
        string gameDirectory = TestResources.GameDirectory;
        string manifest = TestResources.FixturePath("App.Tests.AutoPlayAcceptanceTests.2");
        using InstalledRun run = new(gameDirectory);
        run.Controller.ResumeFrom(manifest);
        long before = run.Controller.State.Steps;
        run.Controller.Advance(1);
        Assert.Null(run.Controller.Error);
        Assert.Equal(before + 1, run.Controller.State.Steps);
        AutoPlayChoiceCoverage choice = Assert.Single(run.Controller.State.Coverage.Choices, choice => choice.Key.StartsWith("input:", StringComparison.Ordinal));
        Assert.Equal(1, choice.ActiveOption);
        Assert.Contains(0, choice.CompletedOptions);
        Assert.True(run.Controller.State.ScriptedChoiceInputs.ContainsKey(choice.Key));
    }

    [Fact]
    public void TenSecondDeadlinePreservesAStandaloneFailureAndReplayResetsTheDeadline()
    {
        string gameDirectory = TestResources.GameDirectory;
        ManualClock clock = new();
        using InstalledRun run = new(gameDirectory, deadlineTime: clock);
        run.Controller.Start();
        clock.Advance(TimeSpan.FromSeconds(10));
        run.Controller.Advance(1);
        Assert.False(run.Controller.Running);
        Assert.Contains("10 seconds", run.Controller.Error);
        Assert.NotNull(run.Controller.ReproductionPath);
        AutoPlayRunState saved = JsonSerializer.Deserialize<AutoPlayRunState>(File.ReadAllText(run.Controller.ReproductionPath), AutoPlayController.JsonOptions)!;
        GameState snapshot = GameSession.Load(Path.Combine(Path.GetDirectoryName(run.Controller.ReproductionPath)!, saved.Snapshot!));
        Assert.Equal(74, snapshot.FieldId);
        run.Controller.ReplayFailure();
        run.Pump();
        run.Controller.Advance(1);
        Assert.Null(run.Controller.Error);
        Assert.True(run.Controller.Running);
    }

    [Fact]
    public void ExhaustedNestedChoiceUsesItsBackOptionWhileParentAlternativeIsActive()
    {
        string gameDirectory = TestResources.GameDirectory;
        string manifest = TestResources.FixturePath("App.Tests.AutoPlayAcceptanceTests.1");
        using InstalledRun run = new(gameDirectory);
        run.Controller.ResumeFrom(manifest);
        run.Controller.Advance(1);
        Assert.Null(run.Controller.Error);
        DialogueOutcome outcome = Assert.Single(run.DialogueOutcomes);
        Assert.Equal(3, outcome.Choice);
    }

    [Fact]
    public void AutonomousNewGameReachesAuthenticatedEndingWithoutErrors()
    {
        Assert.SkipUnless(Environment.GetEnvironmentVariable("MEMORIA_FF8_AUTOPLAY_ACCEPTANCE") == "1", "Set MEMORIA_FF8_AUTOPLAY_ACCEPTANCE=1 to enable the full autonomous playthrough acceptance test.");
        string gameDirectory = TestResources.GameDirectory;
        using InstalledRun run = new(gameDirectory);
        run.PreserveArtifacts = true;
        run.Controller.Start();
        while (run.Controller.Running)
        {
            run.Pump();
            run.Controller.Advance(4096);
        }
        Assert.True(run.Controller.Error is null, $"{run.Controller.Error}{Environment.NewLine}Reproduction: {run.Controller.ReproductionPath ?? "unavailable"}{Environment.NewLine}Artifacts: {run.OutputDirectory}");
        Assert.True(run.Controller.State.ReachedEnding, $"{run.Controller.Status}{Environment.NewLine}Reproduction: {run.Controller.ReproductionPath ?? "unavailable"}{Environment.NewLine}Artifacts: {run.OutputDirectory}");
        run.PreserveArtifacts = false;
    }

    [Fact]
    public void FiveAutonomousStepsFromColdNewGameAreDeterministic()
    {
        string gameDirectory = TestResources.GameDirectory;
        using InstalledRun first = new(gameDirectory);
        using InstalledRun second = new(gameDirectory);
        first.Controller.Start();
        second.Controller.Start();
        for (int step = 0; step < 5; step++)
        {
            first.Pump();
            first.Controller.Advance(1);
            second.Pump();
            second.Controller.Advance(1);
        }
        Assert.Null(first.Controller.Error);
        Assert.Null(second.Controller.Error);
        Assert.Equal(5, first.Controller.State.Steps);
        Assert.Equal(5, second.Controller.State.Steps);
        Assert.True(JsonElement.DeepEquals(first.Snapshot(), second.Snapshot()));
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(first.Controller.State.Coverage), JsonSerializer.SerializeToElement(second.Controller.State.Coverage)));
    }

    [Fact]
    public void MinuteCheckpointLoadsStandaloneAndResumePreservesTheSourceBundle()
    {
        string gameDirectory = TestResources.GameDirectory;
        ManualClock clock = new();
        using InstalledRun run = new(gameDirectory, clock);
        run.Controller.Start();
        run.Controller.State.Coverage.Visits.Add(new AutoPlayVisitCoverage { Visit = new AutoPlayVisit("checkpoint", "fixture"), Events = [new AutoPlayEventCoverage { Event = new AutoPlayEvent("completed", AutoPlayEventKind.Interaction), Completed = true }] });
        clock.Advance(TimeSpan.FromSeconds(61));
        run.Pump();
        run.Controller.Advance(1);
        Assert.Null(run.Controller.Error);
        string manifestPath = Assert.Single(Directory.GetFiles(run.OutputDirectory, "minute-*.run.json", SearchOption.AllDirectories));
        string sourceDirectory = Path.GetDirectoryName(manifestPath)!;
        AutoPlayRunState saved = JsonSerializer.Deserialize<AutoPlayRunState>(File.ReadAllText(manifestPath), AutoPlayController.JsonOptions)!;
        Assert.Equal(1, saved.Steps);
        Assert.True(Assert.Single(saved.Coverage.Visits.Single(visit => visit.Visit.Key == "checkpoint").Events).Completed);
        GameState snapshot = GameSession.Load(Path.Combine(sourceDirectory, saved.Snapshot!));
        run.ValidateStandalone(snapshot);
        Dictionary<string, byte[]> originals = Directory.GetFiles(sourceDirectory).ToDictionary(path => path, File.ReadAllBytes);
        run.Controller.ResumeFrom(manifestPath);
        Assert.True(Assert.Single(run.Controller.State.Coverage.Visits.Single(visit => visit.Visit.Key == "checkpoint").Events).Completed);
        string resumedManifest = Assert.Single(Directory.GetFiles(run.OutputDirectory, "resume-start.run.json", SearchOption.AllDirectories));
        Assert.NotEqual(sourceDirectory, Path.GetDirectoryName(resumedManifest));
        run.Pump();
        run.Controller.Advance(1);
        Assert.Null(run.Controller.Error);
        Assert.Equal(2, run.Controller.State.Steps);
        foreach ((string path, byte[] bytes) in originals)
        {
            Assert.Equal(bytes, File.ReadAllBytes(path));
        }
    }

    private sealed class ManualClock : TimeProvider
    {
        private long timestamp;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => timestamp;
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch.AddTicks(timestamp);

        public void Advance(TimeSpan elapsed)
        {
            timestamp += elapsed.Ticks;
        }
    }

    private sealed class InstalledRun : IDisposable
    {
        private readonly GameSession session = GameSession.CreateColdProcess();
        private readonly GameResourceHost resources;
        private readonly string gameDirectory;
        private readonly string outputDirectory = Path.Combine(Path.GetTempPath(), "ff8-autoplay-acceptance-" + Guid.NewGuid().ToString("N"));
        private GameWorld? world;

        public InstalledRun(string gameDirectory, TimeProvider? time = null, TimeProvider? deadlineTime = null)
        {
            this.gameDirectory = gameDirectory;
            resources = new GameResourceHost(gameDirectory, watch: false);
            Controller = new AutoPlayController(session, GetWorld, Restore, outputDirectory, time, deadlineTime);
        }

        public AutoPlayController Controller { get; }
        public IReadOnlyList<Memoria.FF8.Events.DialogueOutcome> DialogueOutcomes => GetWorld().Events.DialogueOutcomes;
        public string OutputDirectory => outputDirectory;
        public bool PreserveArtifacts { get; set; }

        public void Pump()
        {
            resources.Pump();
        }

        public JsonElement Snapshot()
        {
            if (world?.Field is not null)
            {
                if (session.State.Scene == SceneKind.WorldMap || world.HasCurrentFieldAllocation)
                {
                    session.SetEventState(world.Events.Capture());
                }
                else
                {
                    session.SetDepartingFieldState(world.Events.Capture());
                }
            }
            return JsonSerializer.SerializeToElement(session.State);
        }

        public void ValidateStandalone(GameState state)
        {
            GameSession restoredSession = new(state);
            using GameWorld restored = new(restoredSession, gameDirectory, new MovieClock(restoredSession, gameDirectory, resolveGameFile: resources.ResolveGameFile), resources.Resources, validateWorldPresentation: false, resolveGameFile: resources.ResolveGameFile);
            restored.EnsureField();
            Assert.Equal(state.FieldId, restored.Events.FieldId);
        }

        private GameWorld GetWorld()
        {
            return world ??= new GameWorld(session, gameDirectory, new MovieClock(session, gameDirectory, resolveGameFile: resources.ResolveGameFile), resources.Resources, validateWorldPresentation: false, resolveGameFile: resources.ResolveGameFile);
        }

        private void Restore(GameState state)
        {
            world?.Dispose();
            world = null;
            session.Restore(state);
        }

        public void Dispose()
        {
            world?.Dispose();
            resources.Dispose();
            if (!PreserveArtifacts && Directory.Exists(outputDirectory))
            {
                Directory.Delete(outputDirectory, true);
            }
        }
    }
}
