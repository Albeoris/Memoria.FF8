using System.Text.Json.Nodes;
using Memoria.FF8.Platform;

namespace Memoria.FF8.Platform.Tests;

public sealed class ErrorReportDiscoveryTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "memoria-discovery-tests-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void OrdersByIncidentTimeRatherThanStorageModificationTime()
    {
        ErrorIncident older = ErrorIncident.Capture(new Exception("older"), "test");
        ErrorIncident newer = ErrorIncident.Capture(new Exception("newer"), "test");
        string olderPath = Path.Combine(older.Persist(root), "incident.json");
        string newerPath = Path.Combine(newer.Persist(root), "incident.json");
        SetTimestamp(olderPath, DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
        SetTimestamp(newerPath, DateTimeOffset.Parse("2026-01-02T00:00:00Z"));
        File.SetLastWriteTimeUtc(olderPath, DateTime.UtcNow.AddMinutes(1));

        Assert.Equal(new[] { newer.Id, older.Id }, ErrorIncident.DiscoverAll(root).Select(incident => incident.Id));
        Assert.Equal(newer.Id, ErrorIncident.DiscoverLatest(root)?.Id);
    }

    [Fact]
    public void IncludesPresentedIncidentsUnlessPendingOnlyIsRequestedAndFiltersLaunch()
    {
        ErrorIncident presented = ErrorIncident.Capture(new Exception("shown"), "test", launchId: "selected");
        ErrorIncident pending = ErrorIncident.Capture(new Exception("pending"), "test", launchId: "selected");
        ErrorIncident other = ErrorIncident.Capture(new Exception("other"), "test", launchId: "other");
        presented.Persist(root);
        presented.MarkPresented(root);
        pending.Persist(root);
        other.Persist(root);

        Assert.Equal(3, ErrorIncident.DiscoverAll(root).Count);
        Assert.Equal(2, ErrorIncident.DiscoverAll(root, "selected").Count);
        Assert.Equal(pending.Id, Assert.Single(ErrorIncident.DiscoverAll(root, "selected", pendingOnly: true)).Id);
        Assert.Empty(ErrorIncident.DiscoverAll(root, "missing"));
    }

    [Fact]
    public async Task DeduplicatesPendingAndCompletedCopiesAndSkipsCorruptSiblings()
    {
        ErrorIncident incident = ErrorIncident.Capture(new Exception("retained"), "test");
        using PreparedErrorReport report = await ErrorReport.PrepareAsync(incident, root);
        incident.Persist(root);
        string corrupt = Path.Combine(root, "corrupt");
        Directory.CreateDirectory(corrupt);
        File.WriteAllText(Path.Combine(corrupt, "incident.json"), "{incomplete");
        File.WriteAllText(Path.Combine(root, "corrupt.zip"), "incomplete archive");

        Assert.Equal(incident.Id, Assert.Single(ErrorIncident.DiscoverAll(root)).Id);
    }

    [Fact]
    public void PrefersDuplicateWithRecoverableSaveOverNewerIncompleteCopy()
    {
        byte[] bytes = "MFF8SAVE"u8.ToArray();
        ErrorIncident incident = ErrorIncident.Capture(new Exception("retained"), "test", save: new ReportSave(bytes, "validated test save", DateTimeOffset.UtcNow, "synthetic validated"));
        string pending = incident.Persist(root);
        string legacy = Path.Combine(root, "legacy");
        Directory.CreateDirectory(legacy);
        File.Copy(Path.Combine(pending, "incident.json"), Path.Combine(legacy, "incident.json"));
        File.Copy(Path.Combine(pending, "state.ff8save"), Path.Combine(legacy, "state.ff8save"));
        File.Delete(Path.Combine(pending, "state.ff8save"));
        File.SetLastWriteTimeUtc(Path.Combine(pending, "incident.json"), DateTime.UtcNow.AddMinutes(1));

        ErrorIncident recovered = Assert.Single(ErrorIncident.DiscoverAll(root));
        Assert.Equal(bytes, Assert.IsType<ReportSave>(recovered.Save).CopyBytes());
    }

    private static void SetTimestamp(string path, DateTimeOffset timestamp)
    {
        JsonNode data = JsonNode.Parse(File.ReadAllText(path))!;
        data["Timestamp"] = timestamp;
        File.WriteAllText(path, data.ToJsonString());
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }
    }
}
