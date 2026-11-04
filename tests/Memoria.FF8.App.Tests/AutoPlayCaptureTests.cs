using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlayCaptureTests(ITestOutputHelper output)
{
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
}
