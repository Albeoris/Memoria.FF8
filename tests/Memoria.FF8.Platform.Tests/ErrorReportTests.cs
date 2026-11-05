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

    [Fact]
    public async Task PackagingCoalescesReusesAndPreservesDetachedSave()
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
            using StreamReader reader = new(archive.GetEntry("repro/state.json")!.Open());
            Assert.Equal("{\"validated\":true}", reader.ReadToEnd());
            Assert.NotNull(archive.GetEntry("manifest.json"));
            Assert.NotNull(archive.GetEntry("exception.txt"));
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
        File.WriteAllText(log, "full process output");
        using PreparedErrorReport enriched = await ErrorReport.PrepareAsync(incident.WithLogPaths([log]), root);
        using ZipArchive archive = ZipFile.OpenRead(enriched.ArchivePath);
        Assert.NotNull(archive.GetEntry("logs/00-launcher.log"));
    }

    [Fact]
    public async Task OversizedIncompressibleSaveIsRetainedOutsideBoundedArchive()
    {
        byte[] bytes = new byte[21 * 1024 * 1024];
        new Random(713).NextBytes(bytes);
        ErrorIncident incident = ErrorIncident.Capture(new Exception("save too large"), "test", save: new ReportSave(bytes, "earlier checkpoint", DateTimeOffset.UtcNow.AddMinutes(-2), "synthetic validated"));
        using PreparedErrorReport report = await ErrorReport.PrepareAsync(incident, root);
        Assert.True(report.Size <= ErrorReport.MaximumArchiveBytes);
        Assert.Contains("Excluded from ZIP", report.Description);
        using ZipArchive archive = ZipFile.OpenRead(report.ArchivePath);
        Assert.Null(archive.GetEntry("repro/state.json"));
        Assert.Equal(bytes.Length, new FileInfo(Path.Combine(root, incident.Id, "state.json")).Length);
    }

    [Fact]
    public void UnicodeIssueUrlIsEncodedWithinCompleteBudget()
    {
        string text = string.Concat(Enumerable.Repeat("😀Ж &?%\n", 1000));
        ErrorIncident incident = ErrorIncident.Capture(new Exception(text), "unicode", new { scene = text });
        string url = ErrorReport.BuildIssueUrl(incident, "report.zip", false);
        Assert.True(url.Length <= 2000);
        Assert.All(url, character => Assert.True(character <= 127));
        Assert.Contains("report.zip", Uri.UnescapeDataString(url));
        Assert.DoesNotContain("�", Uri.UnescapeDataString(url));
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
        Assert.True(Directory.EnumerateFiles(root, "completed.json", SearchOption.AllDirectories).Count() <= 11);
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
        Assert.Contains("older bytes", report.Description);
        using ZipArchive archive = ZipFile.OpenRead(report.ArchivePath);
        Assert.Equal(1024 * 1024, archive.GetEntry("logs/00-game.log")!.Length);
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
    }
}
