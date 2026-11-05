using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Memoria.FF8.Platform;

namespace Memoria.FF8.Platform.Tests;

public sealed class ErrorReportTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "memoria-report-tests-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void CaptureDetachesContextAndAllExceptionBranches()
    {
        Dictionary<string, int> runtime = new() { ["frame"] = 12 };
        ErrorIncident incident = ErrorIncident.Capture(new AggregateException(new IOException("first"), new InvalidOperationException("second", new Exception("third"))), "test", runtime, new { renderer = "cached" });
        runtime["frame"] = 40;
        Assert.Equal(12, incident.Runtime.GetProperty("frame").GetInt32());
        Assert.Equal(2, incident.Exception.Causes.Count);
        Assert.Contains("third", incident.ExceptionText);
        Assert.True(incident.Environment.TryGetProperty("build", out _));
        Assert.Equal("cached", incident.Environment.GetProperty("details").GetProperty("renderer").GetString());
    }

    [Theory]
    [InlineData(@"C:\build\private-checkout\BattleSession.cs")]
    [InlineData("/home/builder/private-checkout/BattleSession.cs")]
    public void Captured_stack_preserves_source_location_without_machine_path(string path)
    {
        ErrorIncident incident = ErrorIncident.Capture(new SourceStackException(path), "test");

        Assert.DoesNotContain(path, incident.ExceptionText);
        Assert.Contains("BattleSession.GetActor", incident.ExceptionText);
        Assert.Contains("BattleSession.cs:line 42", incident.ExceptionText);
    }

    [Fact]
    public async Task Compact_snapshot_is_excluded_from_public_report()
    {
        byte[] snapshot = "MFF8SAVE"u8.ToArray();
        ErrorIncident incident = ErrorIncident.Capture(new Exception("failed"), "test", save: new ReportSave(snapshot, "earlier validated player save", DateTimeOffset.UtcNow.AddDays(-2), "synthetic validated"));

        using PreparedErrorReport report = await ErrorReport.PrepareAsync(incident, root);
        using ZipArchive archive = ZipFile.OpenRead(report.ArchivePath);
        Assert.Null(archive.GetEntry("repro/state.ff8save"));
        Assert.Null(archive.GetEntry("repro/state.json"));
        Assert.Contains("cannot be safely sanitized", report.Description);
    }

    [Fact]
    public async Task Earlier_save_keeps_current_snapshot_omission_in_public_report()
    {
        const string reason = "Current snapshot unavailable: Faulted. Reproduction has not been verified.";
        ErrorIncident incident = ErrorIncident.Capture(new Exception("failed"), "test", save: new ReportSave("{}"u8.ToArray(), "earlier validated player save", DateTimeOffset.UtcNow.AddDays(-2), "storage and continuation validated"), saveUnavailableReason: reason);

        using PreparedErrorReport report = await ErrorReport.PrepareAsync(incident, root);
        using ZipArchive archive = ZipFile.OpenRead(report.ArchivePath);
        using StreamReader manifestReader = new(archive.GetEntry("manifest.json")!.Open());
        using JsonDocument manifest = JsonDocument.Parse(manifestReader.ReadToEnd());

        Assert.Contains(reason, report.Description);
        Assert.Contains(manifest.RootElement.GetProperty("omissions").EnumerateArray(), omission => omission.GetString() == reason);
        Assert.Null(archive.GetEntry("repro/state.json"));
    }

    private sealed class SourceStackException(string path) : Exception("failure")
    {
        public override string StackTrace => "   at BattleSession.GetActor(String id) in " + path + ":line 42";
    }

    [Fact]
    public async Task PackagingCoalescesReusesAndOmitsPrivateSave()
    {
        byte[] save = Encoding.UTF8.GetBytes("{\"validated\":true}");
        ErrorIncident incident = ErrorIncident.Capture(new IOException("original failure"), "test", save: new ReportSave(save, "current", DateTimeOffset.UtcNow, "synthetic continuation passed"));
        save[0] = 0;
        Task<PreparedErrorReport> firstTask = ErrorReport.PrepareAsync(incident, root);
        Task<PreparedErrorReport> secondTask = ErrorReport.PrepareAsync(incident, root);
        using PreparedErrorReport first = await firstTask;
        using PreparedErrorReport second = await secondTask;
        Assert.NotSame(first, second);
        Assert.Equal(first.ArchivePath, second.ArchivePath);
        Assert.True(first.Size < ErrorReport.MaximumArchiveBytes);
        using (ZipArchive archive = ZipFile.OpenRead(first.ArchivePath))
        {
            Assert.Null(archive.GetEntry("repro/state.json"));
            Assert.NotNull(archive.GetEntry("manifest.json"));
            Assert.NotNull(archive.GetEntry("exception.txt"));
            using Stream metadata = archive.GetEntry("completed.json")!.Open();
            using JsonDocument completed = JsonDocument.Parse(metadata);
            Assert.Equal(first.IssueUrl, completed.RootElement.GetProperty("issueUrl").GetString());
            using Stream manifestStream = archive.GetEntry("manifest.json")!.Open();
            using JsonDocument manifest = JsonDocument.Parse(manifestStream);
            foreach (JsonElement entry in manifest.RootElement.GetProperty("files").EnumerateArray())
            {
                Assert.Equal(entry.GetProperty("bytes").GetInt64(), archive.GetEntry(entry.GetProperty("name").GetString()!)!.Length);
            }
        }
        DateTime modified = File.GetLastWriteTimeUtc(first.ArchivePath);
        first.Dispose();
        first.Dispose();
        second.Dispose();
        using PreparedErrorReport reopened = await ErrorReport.PrepareAsync(incident, root);
        Assert.Equal(modified, File.GetLastWriteTimeUtc(reopened.ArchivePath));
    }

    [Fact]
    public async Task AdditionalProcessLogInvalidatesPreviouslyCompletedPackage()
    {
        ErrorIncident incident = ErrorIncident.Capture(new Exception("process failed"), "test");
        using (PreparedErrorReport initial = await ErrorReport.PrepareAsync(incident, root))
        {
            Assert.True(File.Exists(initial.ArchivePath));
        }
        string log = Path.Combine(root, "launcher.log");
        File.WriteAllText(log, "PrivateUnlabelledAccountIdentity");
        using PreparedErrorReport enriched = await ErrorReport.PrepareAsync(incident.WithLogPaths([log]), root);
        using ZipArchive archive = ZipFile.OpenRead(enriched.ArchivePath);
        Assert.DoesNotContain(archive.Entries, entry => entry.FullName.StartsWith("logs/", StringComparison.Ordinal));
        Assert.Contains("Excluded 1 raw log file", enriched.Description);
        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            using StreamReader reader = new(entry.Open());
            string content = reader.ReadToEnd();
            Assert.DoesNotContain("launcher.log", content);
            Assert.DoesNotContain("PrivateUnlabelledAccountIdentity", content);
        }
    }

    [Fact]
    public async Task OversizedIncompressibleSaveIsOmittedWithoutLooseCopies()
    {
        byte[] bytes = new byte[21 * 1024 * 1024];
        new Random(713).NextBytes(bytes);
        ErrorIncident incident = ErrorIncident.Capture(new Exception("save too large"), "test", save: new ReportSave(bytes, "earlier checkpoint", DateTimeOffset.UtcNow.AddMinutes(-2), "synthetic validated"));
        using PreparedErrorReport report = await ErrorReport.PrepareAsync(incident, root);
        Assert.True(report.Size <= ErrorReport.MaximumArchiveBytes);
        Assert.Contains("Excluded from ZIP", report.Description);
        using ZipArchive archive = ZipFile.OpenRead(report.ArchivePath);
        Assert.Null(archive.GetEntry("repro/state.json"));
        Assert.Equal([report.ArchivePath], Directory.GetFiles(Path.Combine(root, incident.Id)));
        Assert.False(Directory.Exists(root + ".pending/" + incident.Id));
    }

    [Fact]
    public void UnicodeIssueUrlIsEncodedWithinCompleteBudget()
    {
        string text = string.Concat(Enumerable.Repeat("ðŸ˜€Ð– &?%\n", 1000));
        ErrorIncident incident = ErrorIncident.Capture(new Exception(text), "unicode", new { scene = text });
        string url = ErrorReport.BuildIssueUrl(incident, "report.zip", false);
        Assert.True(url.Length <= 2000);
        Assert.All(url, character => Assert.True(character <= 127));
        Assert.Contains("report.zip", Uri.UnescapeDataString(url));
        Assert.DoesNotContain("ï¿½", Uri.UnescapeDataString(url));
    }

    [Fact]
    public void PersistedEvidenceMatchesLaunchAndPresentationState()
    {
        ErrorIncident incident = ErrorIncident.Capture(new Exception("persisted"), "test", launchId: "selected-launch");
        incident.Persist(root);
        Assert.Null(ErrorIncident.DiscoverLatest(root, "different-launch"));
        ErrorIncident loaded = Assert.IsType<ErrorIncident>(ErrorIncident.DiscoverLatest(root, "selected-launch", true));
        Assert.Equal(incident.Id, loaded.Id);
        Assert.Contains("persisted", loaded.ExceptionText);
        loaded.MarkPresented(root);
        Assert.Null(ErrorIncident.DiscoverLatest(root, "selected-launch", true));
        Assert.NotNull(ErrorIncident.DiscoverLatest(root, "selected-launch"));
    }

    [Fact]
    public void LegacyAndDamagedArchivesDoNotHideRecoverableIncidents()
    {
        ErrorIncident incident = ErrorIncident.Capture(new Exception("pending evidence"), "test");
        incident.Persist(root);
        Directory.CreateDirectory(root);
        using (ZipArchive legacy = ZipFile.Open(Path.Combine(root, "legacy.zip"), ZipArchiveMode.Create))
        {
            legacy.CreateEntry("report.md");
        }
        File.WriteAllText(Path.Combine(root, "damaged.zip"), "not a zip");
        Assert.Equal(incident.Id, ErrorIncident.DiscoverLatest(root)?.Id);
    }

    [Fact]
    public async Task RetentionPreservesAnotherActiveLease()
    {
        ErrorIncident original = ErrorIncident.Capture(new Exception("protected"), "test");
        PreparedErrorReport first = await ErrorReport.PrepareAsync(original, root);
        using PreparedErrorReport second = await ErrorReport.PrepareAsync(original, root);
        first.Dispose();
        for (int i = 0; i < 12; i++)
        {
            using PreparedErrorReport report = await ErrorReport.PrepareAsync(ErrorIncident.Capture(new Exception("later"), "test"), root);
        }
        Assert.True(File.Exists(second.ArchivePath));
        Assert.True(Directory.EnumerateFiles(root, "*.zip", SearchOption.AllDirectories).Count() <= 11);
    }

    [Fact]
    public async Task BreadcrumbsAndLogsAreBoundedAndCollectionFailureIsSecondary()
    {
        ErrorBreadcrumbs breadcrumbs = new();
        for (int i = 0; i < 1100; i++)
        {
            breadcrumbs.Add("transition", i.ToString());
        }
        Directory.CreateDirectory(root);
        string log = Path.Combine(root, "game.log");
        File.WriteAllText(log, new string('a', 2 * 1024 * 1024) + "end");
        ErrorIncident incident = ErrorIncident.Capture(new IOException("primary"), "test", new BrokenContributor(), logPaths: [log, Path.Combine(root, "missing.log")], breadcrumbs: breadcrumbs);
        Assert.Equal(1000, incident.Events.Count);
        Assert.Single(incident.CollectionErrors);
        using PreparedErrorReport report = await ErrorReport.PrepareAsync(incident, root);
        Assert.Contains("primary", report.Description);
        Assert.Contains("Excluded 2 raw log file", report.Description);
        using ZipArchive archive = ZipFile.OpenRead(report.ArchivePath);
        Assert.DoesNotContain(archive.Entries, entry => entry.FullName.StartsWith("logs/", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CompletedReportContainsOnlyArchiveAndRemainsDiscoverable()
    {
        ErrorIncident incident = ErrorIncident.Capture(new Exception("recover completed"), "test", launchId: "archive-launch", save: new ReportSave(Encoding.UTF8.GetBytes("{\"frame\":12}"), "current", DateTimeOffset.UtcNow, "synthetic validated"));
        string pending = incident.Persist(root);
        Assert.False(pending.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
        using PreparedErrorReport report = await ErrorReport.PrepareAsync(incident, root);
        Assert.Equal([report.ArchivePath], Directory.GetFiles(Path.Combine(root, incident.Id)));
        Assert.False(Directory.Exists(pending));
        ErrorIncident recovered = Assert.IsType<ErrorIncident>(ErrorIncident.DiscoverLatest(root, "archive-launch", true));
        Assert.Equal(incident.Id, recovered.Id);
        Assert.Null(recovered.Save);
        recovered.MarkPresented(root);
        Assert.Null(ErrorIncident.DiscoverLatest(root, "archive-launch", true));
        Assert.NotNull(ErrorIncident.DiscoverLatest(root, "archive-launch"));
        Assert.Equal([report.ArchivePath], Directory.GetFiles(Path.Combine(root, incident.Id)));
    }

    [Fact]
    public async Task UnsupportedBinarySaveIsOmittedWithoutLooseCopies()
    {
        ErrorIncident incident = ErrorIncident.Capture(new Exception("unsupported save"), "test", save: new ReportSave([0, 1, 2, 255], "current", DateTimeOffset.UtcNow, "synthetic validated"));
        using PreparedErrorReport report = await ErrorReport.PrepareAsync(incident, root);
        using ZipArchive archive = ZipFile.OpenRead(report.ArchivePath);
        Assert.Null(archive.GetEntry("repro/state.json"));
        Assert.Contains("cannot be safely sanitized", report.Description);
        Assert.Equal([report.ArchivePath], Directory.GetFiles(Path.Combine(root, incident.Id)));
        Assert.False(Directory.Exists(root + ".pending/" + incident.Id));
    }

    private sealed class BrokenContributor
    {
        public string Value => throw new InvalidOperationException("collection failed");
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }
        if (Directory.Exists(root + ".pending"))
        {
            Directory.Delete(root + ".pending", true);
        }
    }
}
