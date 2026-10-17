using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlayControllerTests
{
    [Fact]
    public void RollbackRetainsExactCompactBytesThroughStateMutationAndFailedReplacement()
    {
        using TestRun run = new();
        AutoPlayController controller = run.Create(() => throw new InvalidOperationException("Resources are unavailable."));
        controller.State.Steps = 17;
        JsonSerializerOptions compact = new(AutoPlayController.JsonOptions) { WriteIndented = false };
        byte[] expected = JsonSerializer.SerializeToUtf8Bytes(controller.State, compact);
        ReadOnlyMemory<byte> retained = controller.CaptureControllerRollback();
        controller.State.Steps = 18;
        controller.State.ObservedMessages.Add("A later native callback.");

        Assert.Equal(expected, retained.ToArray());
        controller.State.ActiveTravelRoute = new("destination", null, null, new ThrowingSelections());
        Assert.Throws<InvalidOperationException>(() => controller.CaptureControllerRollback());
        Assert.Equal(expected, retained.ToArray());

        controller.State.ActiveTravelRoute = null;
        ReadOnlyMemory<byte> replacement = controller.CaptureControllerRollback();
        AutoPlayRunState restored = JsonSerializer.Deserialize<AutoPlayRunState>(replacement.Span, AutoPlayController.JsonOptions)!;
        Assert.Equal(18, restored.Steps);
        Assert.Contains("A later native callback.", restored.ObservedMessages);
        Assert.Equal(expected, retained.ToArray());
    }

    [Fact]
    public void WarmRollbackBuffersAvoidAllocatingAnotherManifestSizedArray()
    {
        using TestRun run = new();
        AutoPlayController controller = run.Create(() => throw new InvalidOperationException("Resources are unavailable."));
        controller.State.Error = new string('x', 256 * 1024);
        controller.CaptureControllerRollback();
        controller.CaptureControllerRollback();
        long before = GC.GetAllocatedBytesForCurrentThread();
        ReadOnlyMemory<byte> snapshot = default;
        for (int capture = 0; capture < 4; capture++)
        {
            snapshot = controller.CaptureControllerRollback();
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.True(allocated < snapshot.Length, $"Four warmed captures allocated {allocated} bytes for a {snapshot.Length}-byte manifest.");
        AutoPlayRunState restored = JsonSerializer.Deserialize<AutoPlayRunState>(snapshot.Span, AutoPlayController.JsonOptions)!;
        Assert.Equal(controller.State.Error, restored.Error);
    }

    [Theory]
    [InlineData(33, "172:33", 33, false)]
    [InlineData(33, "172:33", 34, true)]
    [InlineData(null, "172:33", 33, false)]
    [InlineData(null, "world:33", 34, true)]
    [InlineData(null, "unknown", 33, null)]
    public void Transition_story_observation_uses_recorded_entry_and_actual_exit_without_inventing_missing_metadata(int? entry, string visit, int exit, bool? expected)
    {
        Assert.Equal(expected, AutoPlayController.ObserveStoryChange(entry, visit, exit));
    }

    [Fact]
    public void PresentationFailureStopsTheControllerAndPreservesItsCurrentStandaloneState()
    {
        using TestRun run = new();
        AutoPlayController controller = run.Create(() => throw new InvalidOperationException("Capture unavailable."));
        controller.Start();
        controller.State.Steps = 17;
        controller.ReportHostFailure(new InvalidDataException("Synthetic rasterizer fault."));
        Assert.False(controller.Running);
        Assert.Contains("Synthetic rasterizer fault.", controller.Error);
        Assert.NotNull(controller.ReproductionPath);
        AutoPlayRunState saved = ReadManifest(controller.ReproductionPath);
        Assert.Equal(17, saved.Steps);
        Assert.Equal(74, GameSession.Load(Path.Combine(Path.GetDirectoryName(controller.ReproductionPath)!, saved.Snapshot!)).FieldId);
    }

    [Fact]
    public void IdleDeadlineFailsAtTenSecondsAndCanBeResetWithoutSleeping()
    {
        using TestRun run = new();
        ManualClock clock = new();
        AutoPlayController controller = new(run.Session, () => throw new InvalidOperationException(), run.Session.Restore, run.Directory, deadlineTime: clock);
        clock.Advance(TimeSpan.FromMilliseconds(9999));
        controller.CheckIdleDeadline();
        clock.Advance(TimeSpan.FromMilliseconds(1));
        Assert.Throws<TimeoutException>(controller.CheckIdleDeadline);
        controller.ResetIdleDeadline();
        controller.CheckIdleDeadline();
    }

    [Fact]
    public void EndingRequiresTheExplicitTerminalSceneMovieAndHaltedEntryScript()
    {
        GameState ending = new() { Scene = SceneKind.Field, FieldId = 76, MovieSource = new NativeMovieSource(3, 5), MoviePlayback = new MoviePlaybackState(100, 100, false, true, false) };
        JsonElement haltedEntry = JsonSerializer.SerializeToElement(new { Fibers = new[] { new { ObjectId = 0, ScriptId = 1, WaitReason = "NativeHalt" } } });
        Assert.True(AutoPlayController.IsEnding(ending, haltedEntry));
        Assert.False(AutoPlayController.IsEnding(ending with { Scene = SceneKind.Movie }, haltedEntry));
        Assert.False(AutoPlayController.IsEnding(ending with { FieldId = 74 }, haltedEntry));
        Assert.False(AutoPlayController.IsEnding(ending with { MovieSource = new NativeMovieSource(2, 5) }, haltedEntry));
        Assert.False(AutoPlayController.IsEnding(ending with { MovieSource = new NativeMovieSource(3, 4) }, haltedEntry));
        Assert.False(AutoPlayController.IsEnding(ending with { MovieSource = null }, haltedEntry));
        Assert.False(AutoPlayController.IsEnding(ending with { MoviePlayback = ending.MoviePlayback! with { Completed = false } }, haltedEntry));
        Assert.False(AutoPlayController.IsEnding(ending with { MoviePlayback = ending.MoviePlayback! with { IsPlaying = true } }, haltedEntry));
        Assert.False(AutoPlayController.IsEnding(ending with { MoviePlayback = null }, haltedEntry));
        Assert.False(AutoPlayController.IsEnding(ending, JsonSerializer.SerializeToElement(new { Fibers = Array.Empty<object>() })));
    }

    [Theory]
    [InlineData(1, 1, "NativeHalt")]
    [InlineData(0, 2, "NativeHalt")]
    [InlineData(0, 1, "NativeWait")]
    [InlineData(0, 1, null)]
    public void UnrelatedOrNonterminalFibersCannotEstablishCompletion(int objectId, int scriptId, string? waitReason)
    {
        GameState ending = new() { Scene = SceneKind.Field, FieldId = 76, MovieSource = new NativeMovieSource(3, 5), MoviePlayback = new MoviePlaybackState(100, 100, false, true, false) };
        JsonElement vm = JsonSerializer.SerializeToElement(new { Fibers = new[] { new { ObjectId = objectId, ScriptId = scriptId, WaitReason = waitReason } } });
        Assert.False(AutoPlayController.IsEnding(ending, vm));
    }

    [Fact]
    public void StartSavesNativeColdNewGameBeforeAccessingResources()
    {
        using TestRun run = new();
        int worldRequests = 0;
        AutoPlayController controller = run.Create(() =>
        {
            worldRequests++;
            throw new InvalidOperationException("Resources are unavailable.");
        });
        controller.Start();
        Assert.True(controller.Running);
        Assert.Equal(0, worldRequests);
        string manifest = Assert.Single(Directory.GetFiles(run.Directory, "start.run.json", SearchOption.AllDirectories));
        AutoPlayRunState state = ReadManifest(manifest);
        Assert.Equal(1, state.Seed);
        Assert.Equal(0, state.Steps);
        GameState saved = GameSession.Load(Path.Combine(Path.GetDirectoryName(manifest)!, state.Snapshot!));
        Assert.Equal(SceneKind.Field, saved.Scene);
        Assert.Equal(74, saved.FieldId);
        Assert.Equal("start0", saved.FieldName);
        GameSession expected = GameSession.CreateColdProcess();
        expected.StartNewGame();
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(expected.State), JsonSerializer.SerializeToElement(saved)));
    }

    [Fact]
    public void ColdResourceFailureKeepsReplayableSnapshotAndRepeatsTheSameFault()
    {
        using TestRun run = new();
        AutoPlayController controller = run.Create(() => throw new InvalidOperationException("Synthetic allocation failure."));
        controller.Start();
        controller.Advance(1);
        Assert.False(controller.Running);
        Assert.Contains("Synthetic allocation failure.", controller.Error);
        Assert.NotNull(controller.ReproductionPath);
        Assert.True(File.Exists(controller.ReproductionPath));
        AutoPlayRunState saved = ReadManifest(controller.ReproductionPath);
        Assert.Equal(0, saved.Steps);
        Assert.Equal(74, GameSession.Load(Path.Combine(Path.GetDirectoryName(controller.ReproductionPath)!, saved.Snapshot!)).FieldId);
        controller.ReplayFailure();
        Assert.True(controller.Running);
        Assert.Null(controller.Error);
        controller.Advance(1);
        Assert.False(controller.Running);
        Assert.Contains("Synthetic allocation failure.", controller.Error);
        Assert.Equal(0, controller.State.Steps);
    }

    [Theory]
    [InlineData(2, 1, "snapshot.json")]
    [InlineData(1, 2, "snapshot.json")]
    [InlineData(1, 1, null)]
    public void ResumeRejectsUnsupportedManifestBeforeReplacingSession(int version, int seed, string? snapshot)
    {
        using TestRun run = new();
        string path = Path.Combine(run.Directory, "invalid.run.json");
        File.WriteAllText(path, JsonSerializer.Serialize(new AutoPlayRunState { Version = version, Seed = seed, Snapshot = snapshot }, AutoPlayController.JsonOptions));
        int restores = 0;
        AutoPlayController controller = new(run.Session, () => throw new InvalidOperationException(), _ => restores++, run.Directory);
        Assert.Throws<InvalidDataException>(() => controller.ResumeFrom(path));
        Assert.Equal(0, restores);
        Assert.False(controller.Running);
    }

    [Fact]
    public void ResumeRestoresSavedPlannerStateAndUsesManifestRelativeSnapshot()
    {
        using TestRun run = new();
        AutoPlayController controller = run.Create(() => throw new InvalidOperationException("Resources are unavailable."));
        controller.Start();
        string manifest = Assert.Single(Directory.GetFiles(run.Directory, "start.run.json", SearchOption.AllDirectories));
        AutoPlayRunState state = ReadManifest(manifest);
        state.Steps = 42;
        state.SettleFrames = 2;
        state.Coverage.Visits.Add(new AutoPlayVisitCoverage { Visit = new AutoPlayVisit("74:0", "region"), Events = [new AutoPlayEventCoverage { Event = new AutoPlayEvent("talk", AutoPlayEventKind.Interaction), Completed = true }] });
        state.Coverage.Choices.Add(new AutoPlayChoiceCoverage { Key = "choice", OptionCount = 2, BeforeCheckpoint = "choice.json", ContinuationCheckpoint = "continuation.json" });
        state.PrimaryContinuation = "continuation.json";
        string sourceDirectory = Path.GetDirectoryName(manifest)!;
        File.Copy(Path.Combine(sourceDirectory, state.Snapshot!), Path.Combine(sourceDirectory, "choice.json"));
        File.Copy(Path.Combine(sourceDirectory, state.Snapshot!), Path.Combine(sourceDirectory, "continuation.json"));
        File.WriteAllText(manifest, JsonSerializer.Serialize(state, AutoPlayController.JsonOptions));
        Dictionary<string, byte[]> originals = Directory.GetFiles(sourceDirectory).ToDictionary(path => path, File.ReadAllBytes);
        run.Session.Restore(new GameState());
        controller.ResumeFrom(manifest);
        Assert.Equal(74, run.Session.State.FieldId);
        Assert.Equal(42, controller.State.Steps);
        Assert.Equal(2, controller.State.SettleFrames);
        Assert.True(Assert.Single(Assert.Single(controller.State.Coverage.Visits).Events).Completed);
        Assert.Equal(manifest, controller.ReproductionPath);
        AutoPlayChoiceCoverage choice = Assert.Single(controller.State.Coverage.Choices);
        string resumed = Assert.Single(Directory.GetFiles(run.Directory, "resume-start.run.json", SearchOption.AllDirectories));
        string resumedDirectory = Path.GetDirectoryName(resumed)!;
        Assert.NotEqual(sourceDirectory, resumedDirectory);
        foreach ((string? imported, string original) in new[] { (choice.BeforeCheckpoint, "choice.json"), (choice.ContinuationCheckpoint, "continuation.json"), (controller.State.PrimaryContinuation, "continuation.json") })
        {
            Assert.NotNull(imported);
            Assert.False(Path.IsPathRooted(imported));
            Assert.Equal(originals[Path.Combine(sourceDirectory, original)], File.ReadAllBytes(Path.Combine(resumedDirectory, imported)));
        }
        controller.Advance(1);
        Assert.False(controller.Running);
        Assert.Contains("Resources are unavailable.", controller.Error);
        foreach ((string path, byte[] bytes) in originals)
        {
            Assert.Equal(bytes, File.ReadAllBytes(path));
        }
    }

    [Fact]
    public void PausedControllerDoesNotTouchResourcesEvenWhenMinuteCheckpointIsDue()
    {
        using TestRun run = new();
        ManualClock clock = new();
        int requests = 0;
        AutoPlayController controller = new(run.Session, () =>
        {
            requests++;
            throw new InvalidOperationException("Resources are unavailable.");
        }, run.Session.Restore, run.Directory, clock);
        controller.Start();
        controller.Pause();
        clock.Advance(TimeSpan.FromMinutes(2));
        controller.Advance(10);
        Assert.Equal(0, requests);
        Assert.Equal(0, controller.State.Steps);
        Assert.Empty(Directory.GetFiles(run.Directory, "minute-*.run.json", SearchOption.AllDirectories));
        controller.Resume();
        controller.Advance(1);
        Assert.True(requests > 0);
        Assert.False(controller.Running);
    }

    private sealed class ThrowingSelections : IReadOnlyList<AutoPlayChoiceSelection>
    {
        public int Count => 1;
        public AutoPlayChoiceSelection this[int index] => throw new InvalidOperationException("Synthetic serialization failure.");

        public IEnumerator<AutoPlayChoiceSelection> GetEnumerator()
        {
            throw new InvalidOperationException("Synthetic serialization failure.");
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    private static AutoPlayRunState ReadManifest(string path)
    {
        return JsonSerializer.Deserialize<AutoPlayRunState>(File.ReadAllText(path), AutoPlayController.JsonOptions)!;
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

    private sealed class TestRun : IDisposable
    {
        public TestRun()
        {
            System.IO.Directory.CreateDirectory(Directory);
        }

        public string Directory { get; } = Path.Combine(Path.GetTempPath(), "ff8-autoplay-controller-" + Guid.NewGuid().ToString("N"));
        public GameSession Session { get; } = GameSession.CreateColdProcess();

        public AutoPlayController Create(Func<GameWorld> getWorld)
        {
            return new AutoPlayController(Session, getWorld, Session.Restore, Directory);
        }

        public void Dispose()
        {
            System.IO.Directory.Delete(Directory, true);
        }
    }
}
