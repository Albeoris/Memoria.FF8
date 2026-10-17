using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlayAcceptanceTests
{
    [Fact]
    public void AnUnsampledContinuousContactInputDoesNotCompleteItsAlternative()
    {
        string gameDirectory = TestResources.GameDirectory;
        string manifest = TestResources.FixturePath("App.Tests.AutoPlayAcceptanceTests.5");
        using InstalledRun run = new(gameDirectory);
        run.Controller.ResumeFrom(manifest);
        ScopeNativeInputCoverageToWardrobe(run.Controller);
        for (int step = 0; step < 8 && run.Controller.State.ActiveObservedInput is null; step++)
        {
            run.Controller.Advance(1);
            Assert.Null(run.Controller.Error);
        }
        ScriptedInputMetadata input = Assert.IsType<ScriptedInputMetadata>(run.Controller.State.ActiveObservedInput);
        run.Controller.State.ActiveObservedInput = input with { Queries = input.Queries.Select(query => query with { Operation = int.MaxValue }).ToArray() };
        run.Controller.Advance(1);
        Assert.Contains("did not sample physical mask", run.Controller.Error);
        AutoPlayChoiceCoverage choice = Assert.Single(run.Controller.State.Coverage.Choices, choice => choice.Key == "input:240:" + input.Fingerprint);
        Assert.Empty(choice.CompletedOptions);
    }

    [Fact]
    public void ContinuousWardrobeContactReceivesOrdinaryInputAndExploresGetChanged()
    {
        string gameDirectory = TestResources.GameDirectory;
        string manifest = TestResources.FixturePath("App.Tests.AutoPlayAcceptanceTests.5");
        using InstalledRun run = new(gameDirectory);
        run.Controller.ResumeFrom(manifest);
        ScopeNativeInputCoverageToWardrobe(run.Controller);
        bool sawWardrobe = false;
        bool choseGetChanged = false;
        bool WardrobeChoicesFinished() => run.Controller.State.Coverage.Choices.Where(choice => choice.Key.StartsWith("240:", StringComparison.Ordinal) || choice.Key.StartsWith("input:240:", StringComparison.Ordinal)).All(choice => choice.Finished);
        for (int step = 0; step < 128 && (run.Variables.GetValueOrDefault(256) != 34 || !WardrobeChoicesFinished() || run.Controller.State.PrimaryContinuation is not null); step++)
        {
            run.Controller.Advance(1);
            Assert.Null(run.Controller.Error);
            choseGetChanged |= run.DialogueOutcomes.Any(outcome => outcome.ObjectId == 12 && outcome.ScriptId == 53 && outcome.Operation == 316 && outcome.Choice == 2);
            JsonElement snapshot = run.Snapshot();
            if (snapshot.TryGetProperty("EventState", out JsonElement events) && events.ValueKind == JsonValueKind.Object)
            {
                sawWardrobe |= events.GetProperty("Dialogues").EnumerateArray().Any(dialogue => dialogue.GetProperty("ObjectId").GetInt32() == 12 && dialogue.GetProperty("ScriptId").GetInt32() == 53 && dialogue.GetProperty("Operation").GetInt32() == 316 && dialogue.GetProperty("MessageId").GetInt32() == 1);
            }
        }
        Assert.True(sawWardrobe, "The admitted continuous contact never opened its original wardrobe prompt.");
        Assert.True(choseGetChanged, "The original wardrobe prompt never received its visible Get changed answer.");
        Assert.True(WardrobeChoicesFinished());
        Assert.Null(run.Controller.State.PrimaryContinuation);
        Assert.Equal(34, run.Variables.GetValueOrDefault(256));
        Assert.Equal(1, run.Variables.GetValueOrDefault(720));
    }

    internal static void ScopeNativeInputCoverageToWardrobe(AutoPlayController controller)
    {
        // These native input regressions isolate the current field; remote legacy visit refresh has separate coverage.
        Assert.Contains(controller.State.Coverage.Visits, visit => visit.Visit.Key == "240:33");
        controller.State.Coverage.Visits.RemoveAll(visit => visit.Visit.Key != "240:33");
    }

    [Fact]
    public void ResumedChoiceReplayIsIndependentOfItsOriginalDirectory()
    {
        string gameDirectory = TestResources.GameDirectory;
        string fixture = TestResources.FixturePath("App.Tests.AutoPlayAcceptanceTests.2");
        string fixtureDirectory = Path.GetDirectoryName(fixture)!;
        string temporary = Path.Combine(Path.GetTempPath(), "ff8-checkpoint-import-" + Guid.NewGuid().ToString("N"));
        string sourceDirectory = Path.Combine(temporary, "source");
        Directory.CreateDirectory(sourceDirectory);
        try
        {
            AutoPlayRunState source = JsonSerializer.Deserialize<AutoPlayRunState>(File.ReadAllText(fixture), AutoPlayController.JsonOptions)!;
            Dictionary<string, string> copied = new(StringComparer.OrdinalIgnoreCase);
            string? Copy(string? reference)
            {
                if (reference is null)
                {
                    return null;
                }
                string path = Path.GetFullPath(reference, fixtureDirectory);
                if (!copied.TryGetValue(path, out string? filename))
                {
                    filename = $"snapshot-{copied.Count}.json";
                    File.Copy(path, Path.Combine(sourceDirectory, filename));
                    copied.Add(path, filename);
                }
                return filename;
            }
            source.Snapshot = Copy(source.Snapshot);
            source.PrimaryContinuation = Copy(source.PrimaryContinuation);
            foreach (AutoPlayChoiceCoverage choice in source.Coverage.Choices)
            {
                if (choice.Finished)
                {
                    choice.BeforeCheckpoint = "retired-before.json";
                    choice.ContinuationCheckpoint = "retired-continuation.json";
                }
                else
                {
                    choice.BeforeCheckpoint = Copy(choice.BeforeCheckpoint);
                    choice.ContinuationCheckpoint = Copy(choice.ContinuationCheckpoint);
                }
            }
            string manifest = Path.Combine(sourceDirectory, "source.run.json");
            File.WriteAllText(manifest, JsonSerializer.Serialize(source, AutoPlayController.JsonOptions));
            using InstalledRun run = new(gameDirectory);
            run.Controller.ResumeFrom(manifest);
            string imported = Assert.Single(Directory.GetFiles(run.OutputDirectory, "resume-start.run.json", SearchOption.AllDirectories));
            string importedDirectory = Path.GetDirectoryName(imported)!;
            foreach (string reference in run.Controller.State.Coverage.Choices.Where(choice => !choice.Finished).SelectMany(choice => new[] { choice.BeforeCheckpoint, choice.ContinuationCheckpoint }).Append(run.Controller.State.PrimaryContinuation).OfType<string>())
            {
                Assert.False(Path.IsPathRooted(reference));
                Assert.True(File.Exists(Path.Combine(importedDirectory, reference)));
            }
            Assert.All(run.Controller.State.Coverage.Choices.Where(choice => choice.Finished), choice =>
            {
                Assert.Null(choice.BeforeCheckpoint);
                Assert.Null(choice.ContinuationCheckpoint);
            });
            Directory.Move(sourceDirectory, Path.Combine(temporary, "detached"));
            run.Controller.Advance(1);
            Assert.Null(run.Controller.Error);
            AssertRequalifiedInputDomain(run.Controller.State);
        }
        finally
        {
            Directory.Delete(temporary, true);
        }
    }

    [Fact]
    public void InitialFieldRegistrationResumesBeforeItsFirstSkinCallback()
    {
        string gameDirectory = TestResources.GameDirectory;
        string manifest = TestResources.FixturePath("App.Tests.AutoPlayAcceptanceTests.4");
        AutoPlayRunState saved = JsonSerializer.Deserialize<AutoPlayRunState>(File.ReadAllText(manifest), AutoPlayController.JsonOptions)!;
        GameState initial = GameSession.Load(Path.Combine(Path.GetDirectoryName(manifest)!, saved.Snapshot!));
        GameSession session = new(initial);
        using GameWorld world = new(session, gameDirectory, validateWorldPresentation: false);
        world.EnsureField();
        Assert.Equal(1, world.Events.Frame);
        Assert.True(world.Events.IsNativeModelDrawPending);
        ActorState[] actors = world.Events.Actors.Where(actor => actor.Active && actor.Visible && actor.RenderModelId >= 0).ToArray();
        Assert.NotEmpty(actors);
        JsonElement beforeVisuals = world.Events.Capture();
        Assert.All(actors, actor => Assert.False(world.CreateVisual(actor).Visible));
        if (world.Events.PlayerActor is { } player)
        {
            Assert.False(world.CreateVisual(player).Visible);
        }
        Assert.True(JsonElement.DeepEquals(beforeVisuals, world.Events.Capture()));
        world.Events.Step();
        Assert.False(world.Events.IsNativeModelDrawPending);
        Assert.Contains(world.Events.Actors, actor => actor.Active && actor.Visible && actor.RenderModelId >= 0 && world.CreateVisual(actor).Visible);
        using InstalledRun run = new(gameDirectory);
        run.Controller.ResumeFrom(manifest);
        run.Controller.Advance(1);
        Assert.Null(run.Controller.Error);
        JsonElement after = run.Snapshot();
        Assert.Equal(139, after.GetProperty("FieldId").GetInt32());
        JsonElement events = after.GetProperty("EventState");
        long frame = events.GetProperty("Frame").GetInt64();
        Assert.True(frame > 1);
        JsonElement scene = events.GetProperty("Scene");
        Assert.True(scene.GetProperty("FieldCallbackClock").GetProperty("OrdinaryCallbacks").GetInt64() > 0);
        JsonProperty[] draws = scene.GetProperty("ModelParts").GetProperty("Draws").EnumerateObject().ToArray();
        Assert.NotEmpty(draws);
        Assert.All(draws, draw => Assert.Equal(frame, draw.Value.GetProperty("Frame").GetInt64()));
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), events));
    }

    [Fact]
    public void OrdinaryLibrarianTalkKeepsTheNativeAbsentPartyBranchClosed()
    {
        string gameDirectory = TestResources.GameDirectory;
        string manifest = TestResources.FixturePath("App.Tests.AutoPlayAcceptanceTests.3");
        using InstalledRun run = new(gameDirectory);
        run.Controller.ResumeFrom(manifest);
        JsonElement before = run.Snapshot();
        for (int step = 0; step < 32 && !run.Branches.Any(branch => branch.Operation == 2213); step++)
        {
            run.Controller.Advance(1);
            Assert.Null(run.Controller.Error);
        }
        BranchDecision partyGuard = Assert.Single(run.Branches, branch => branch.Operation == 2213);
        Assert.False(partyGuard.ConditionPassed);
        Assert.Equal(0, partyGuard.Value);
        JsonElement after = run.Snapshot();
        Assert.True(JsonElement.DeepEquals(before.GetProperty("EventState").GetProperty("Scene").GetProperty("Party"), after.GetProperty("EventState").GetProperty("Scene").GetProperty("Party")));
        Assert.Equal(16, run.Variables.GetValueOrDefault(256));
        Assert.Equal(0, run.Variables.GetValueOrDefault(1054));
    }

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
        AssertRequalifiedInputDomain(run.Controller.State);
    }

    private static void AssertRequalifiedInputDomain(AutoPlayRunState state)
    {
        AutoPlayChoiceCoverage legacy = Assert.Single(state.Coverage.Choices, choice => choice.SupersededInputDomain is not null);
        Assert.Null(legacy.ActiveOption);
        Assert.Contains(0, legacy.CompletedOptions);
        Assert.False(legacy.Finished);
        AutoPlayChoiceCoverage observed = Assert.Single(state.Coverage.Choices, choice => choice.Key == legacy.SupersededInputDomain);
        Assert.Equal(0, observed.ActiveOption);
        Assert.Empty(observed.CompletedOptions);
        Assert.False(observed.Finished);
        Assert.True(state.ScriptedChoiceInputs[observed.Key].ObservedQueriesProven);
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
        public IReadOnlyList<BranchDecision> Branches => GetWorld().Events.Decisions;
        public IReadOnlyDictionary<int, int> Variables => GetWorld().Events.Variables;
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
