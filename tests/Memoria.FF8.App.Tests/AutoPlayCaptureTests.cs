using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlayCaptureTests(ITestOutputHelper output)
{
    [Fact]
    public void IdleFailurePreservesCurrentNativeFrameAndEntryCheckpoint()
    {
        string gameDirectory = TestResources.GameDirectory;
        GameSession session = GameWorld.CreateColdProcess(gameDirectory);
        GameWorld? world = null;
        ManualClock clock = new();
        string directory = Path.Combine(Path.GetTempPath(), "autoplay-failure-" + Guid.NewGuid().ToString("N"));
        try
        {
            GameWorld GetWorld() => world ??= new(session, gameDirectory, validateWorldPresentation: false);
            AutoPlayController controller = new(session, GetWorld, session.Restore, directory, deadlineTime: clock);
            session.StartNewGame();
            controller.StartFromCurrentState();
            string entry = Assert.Single(Directory.GetFiles(directory, "latest.run.json", SearchOption.AllDirectories));
            GameWorld live = GetWorld();
            live.EnsureField();
            live.Events.Step();
            JsonElement liveVm = live.Events.Capture();
            Assert.True(live.Events.Frame > 0);
            Assert.Empty(Directory.GetFiles(directory, "failure-*", SearchOption.AllDirectories));

            string staged = controller.StageAttempt("active-source.json", session.State);
            controller.State.ActiveBeforeCheckpoint = staged;
            controller.State.Steps = 7;
            for (int i = 0; i < 40; i++)
            {
                Memoria.AutoPlay.AutoPlayRecording.AppendRecent(controller.State.RecentActions, new(i, session.State.FieldId, "Field", $"Input {i}"));
            }
            controller.CheckIdleDeadline();
            clock.Advance(TimeSpan.FromSeconds(10));
            controller.Advance(1);

            Assert.False(controller.Running);
            Assert.Contains("10 seconds", controller.Error);
            string manifest = Assert.IsType<string>(controller.ReproductionPath);
            Assert.Equal("failure-0000000007.run.json", Path.GetFileName(manifest));
            AutoPlayRunState failure = ReadManifest(manifest);
            Assert.Equal(7, failure.Steps);
            Assert.Equal(32, failure.RecentActions.Count);
            Assert.Equal("Input 8", failure.RecentActions[0].Action);
            Assert.Equal("Input 39", failure.RecentActions[^1].Action);
            Assert.Equal(staged, failure.ActiveBeforeCheckpoint);
            Assert.Equal(session.State.FieldId, AutoPlayCheckpointStore.Load(Path.Combine(Path.GetDirectoryName(manifest)!, staged)).FieldId);
            GameState saved = GameSession.Load(Path.Combine(Path.GetDirectoryName(manifest)!, failure.Snapshot!));
            JsonElement savedVm = Assert.IsType<JsonElement>(saved.EventState);
            Assert.Equal(live.Events.FieldId, saved.FieldId);
            Assert.Equal(live.Events.Frame, savedVm.GetProperty("Frame").GetInt64());
            Assert.True(JsonElement.DeepEquals(liveVm.GetProperty("Fibers"), savedVm.GetProperty("Fibers")));
            Assert.Equal("start.json", ReadManifest(entry).Snapshot);
            Assert.True(File.Exists(Path.Combine(Path.GetDirectoryName(entry)!, "start.json")));
            Assert.Empty(Directory.GetFiles(directory, "*.tmp", SearchOption.AllDirectories));
        }
        finally
        {
            world?.Dispose();
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
    }

    [Fact]
    public void ExhaustedCaptureBudgetKeepsEntryCheckpointAndPrimaryError()
    {
        string directory = Path.Combine(Path.GetTempPath(), "autoplay-failure-budget-" + Guid.NewGuid().ToString("N"));
        try
        {
            GameSession session = GameSession.CreateColdProcess();
            AutoPlayController controller = new(session, () => throw new InvalidOperationException("Budget should be checked before allocating a world."), session.Restore, directory);
            controller.Start();
            string entry = Assert.Single(Directory.GetFiles(directory, "start.run.json", SearchOption.AllDirectories));
            using (FileStream occupied = File.Create(Path.Combine(Path.GetDirectoryName(entry)!, "occupied")))
            {
                occupied.SetLength(128L * 1024 * 1024);
            }

            controller.ReportHostFailure(new InvalidOperationException("Primary failure."));

            Assert.False(controller.Running);
            Assert.Equal(entry, controller.ReproductionPath);
            Assert.Contains("Primary failure.", controller.Error);
            Assert.Contains("128 MiB", controller.Status);
            Assert.Empty(Directory.GetFiles(directory, "failure*", SearchOption.AllDirectories));
            Assert.Empty(Directory.GetFiles(directory, "*.tmp", SearchOption.AllDirectories));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
    }

    private static AutoPlayRunState ReadManifest(string path) =>
        JsonSerializer.Deserialize<AutoPlayRunState>(File.ReadAllText(path), AutoPlayController.JsonOptions)!;

    private sealed class ManualClock : TimeProvider
    {
        private long timestamp;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => timestamp;

        public void Advance(TimeSpan elapsed)
        {
            timestamp += elapsed.Ticks;
        }
    }

    [Fact]
    public void ConfiguredHistoricalRunImportsWithinBudgetAndPreservesCurrentState()
    {
        string? fixture = Environment.GetEnvironmentVariable("MEMORIA_FF8_AUTOPLAY_STORAGE_MANIFEST");
        if (string.IsNullOrWhiteSpace(fixture))
        {
            Assert.Skip("Configure an external automatic exploration manifest for the storage integration check.");
        }
        string directory = Path.Combine(Path.GetTempPath(), "autoplay-import-" + Guid.NewGuid().ToString("N"));
        try
        {
            AutoPlayRunState original = JsonSerializer.Deserialize<AutoPlayRunState>(File.ReadAllText(fixture), AutoPlayController.JsonOptions)!;
            int expectedField = GameSession.ReadSnapshotProjection<FieldProjection>(Path.GetFullPath(original.Snapshot!, Path.GetDirectoryName(fixture)!)).FieldId;
            GameSession session = GameSession.CreateColdProcess();
            AutoPlayController controller = new(session, () => throw new InvalidOperationException(), session.Restore, directory);
            controller.ResumeFrom(fixture);
            string importedManifest = Assert.Single(Directory.GetFiles(directory, "latest.run.json", SearchOption.AllDirectories));
            AutoPlayRunState imported = JsonSerializer.Deserialize<AutoPlayRunState>(File.ReadAllText(importedManifest), AutoPlayController.JsonOptions)!;
            Assert.Equal(original.Steps, imported.Steps);
            Assert.Equal(expectedField, GameSession.Load(Path.Combine(Path.GetDirectoryName(importedManifest)!, imported.Snapshot!)).FieldId);
            long bytes = Directory.GetFiles(directory, "*", SearchOption.AllDirectories).Sum(path => new FileInfo(path).Length);
            output.WriteLine($"Imported capture: {bytes} bytes; {Directory.GetFiles(directory, "*", SearchOption.AllDirectories).Length} files; {imported.InputSearchHistory.Count} closed search ledgers.");
            Assert.True(bytes < 128L * 1024 * 1024);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
    }

    private sealed record FieldProjection(int FieldId);

    [Fact]
    public void RepeatedCheckpointsKeepCurrentStandaloneAndBoundedFiles()
    {
        string directory = Path.Combine(Path.GetTempPath(), "autoplay-retention-" + Guid.NewGuid().ToString("N"));
        try
        {
            GameSession session = GameSession.CreateColdProcess();
            session.Restore(session.State with { Scene = SceneKind.Field, FieldId = 130 });
            AutoPlayController controller = new(session, () => throw new InvalidOperationException(), session.Restore, directory);
            controller.StartFromCurrentState();
            string manifest = string.Empty;
            for (int step = 0; step < 100; step++)
            {
                controller.State.Steps++;
                session.Restore(session.State with { X = step });
                manifest = controller.SaveBundle($"event-{step:D10}", session.PrepareSnapshot());
                controller.MaintainCapture();
            }
            AutoPlayRunState state = JsonSerializer.Deserialize<AutoPlayRunState>(File.ReadAllText(manifest), AutoPlayController.JsonOptions)!;
            Assert.Equal(100, state.Steps);
            Assert.Equal(99, GameSession.Load(Path.Combine(Path.GetDirectoryName(manifest)!, state.Snapshot!)).X);
            Assert.Equal(2, Directory.GetFiles(Path.GetDirectoryName(manifest)!, "*.run.json").Length);
            Assert.True(Directory.GetFiles(directory, "*", SearchOption.AllDirectories).Length < 30);
            Assert.True(Directory.GetFiles(directory, "*", SearchOption.AllDirectories).Sum(path => new FileInfo(path).Length) < 1024 * 1024);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
    }

    [Fact]
    public void ClosedSearchRetiresFilesWhileOpenSearchKeepsEveryRecoveryFrame()
    {
        AutoPlayInputSearch closed = new() { Closed = true, Frames = [new() { Checkpoint = "closed.json", Context = new() { BeforeCheckpoint = "closed-before.json" }, Trials = [new() { Diagnostic = "native loss" }] }] };
        AutoPlayInputSearch open = new() { Frames = [new() { Checkpoint = "root.json" }, new() { Checkpoint = "sibling.json", Context = new() { BeforeCheckpoint = "event.json" } }] };
        AutoPlayRunState state = new() { Snapshot = "current.json", InputSearchHistory = [closed], InputSearch = open };
        AutoPlayController.RetireClosedSearchSnapshots(state);
        Assert.True(closed.SnapshotsRetired);
        Assert.Equal("native loss", Assert.Single(Assert.Single(closed.Frames).Trials).Diagnostic);
        Assert.Empty(Assert.Single(closed.Frames).Checkpoint);
        Assert.Equal(new[] { "current.json", "event.json", "root.json", "sibling.json" }, AutoPlayController.CaptureDependencies(state).Order().ToArray());
        Assert.False(open.SnapshotsRetired);
    }

    [Fact]
    public void OpenChoiceRetainsTheOriginalEventCheckpoint()
    {
        AutoPlayChoiceCoverage choice = new() { BeforeCheckpoint = "choice.json", BeforeContext = new() { BeforeCheckpoint = "source.json" } };
        AutoPlayRunState state = new();
        state.Coverage.Choices.Add(choice);

        Assert.Equal(new[] { "choice.json", "source.json" }, AutoPlayController.CaptureDependencies(state).Order().ToArray());
        choice.Finished = true;
        Assert.Empty(AutoPlayController.CaptureDependencies(state));
    }
}
