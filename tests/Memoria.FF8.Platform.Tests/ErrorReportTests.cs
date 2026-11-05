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
    public async Task Compact_snapshot_is_attached_without_changing_bytes()
    {
        byte[] snapshot = "MFF8SAVE"u8.ToArray();
        ErrorIncident incident = ErrorIncident.Capture(new Exception("failed"), "test", save: new ReportSave(snapshot, "earlier validated player save", DateTimeOffset.UtcNow.AddDays(-2), "synthetic validated"));

        using PreparedErrorReport report = await ErrorReport.PrepareAsync(incident, root);
        using ZipArchive archive = ZipFile.OpenRead(report.ArchivePath);
        ZipArchiveEntry save = Assert.IsType<ZipArchiveEntry>(archive.GetEntry("repro/state.ff8save"));
        using Stream source = save.Open();
        using MemoryStream copy = new();
        source.CopyTo(copy);
        Assert.Equal(snapshot, copy.ToArray());
        Assert.Null(archive.GetEntry("repro/state.json"));
        Assert.Contains("earlier validated player save", report.Description);
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
        Assert.NotNull(archive.GetEntry("repro/state.json"));
    }

    private sealed class SourceStackException(string path) : Exception("failure")
    {
        public override string StackTrace => "   at BattleSession.GetActor(String id) in " + path + ":line 42";
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
            using StreamReader saveReader = new(archive.GetEntry("repro/state.json")!.Open());
            Assert.Equal("{\"validated\":true}", saveReader.ReadToEnd());
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
        File.WriteAllText(log, "process output ../fixtures process output");
        using PreparedErrorReport enriched = await ErrorReport.PrepareAsync(incident.WithLogPaths([log]), root);
        using ZipArchive archive = ZipFile.OpenRead(enriched.ArchivePath);
        using StreamReader reader = new(archive.GetEntry("logs/00.log")!.Open());
        string content = reader.ReadToEnd();
        Assert.Contains("process output", content);
        Assert.DoesNotContain("ReportFixture", content);
        Assert.DoesNotContain("launcher.log", enriched.Description);
        Assert.Equal("process output ../fixtures process output", File.ReadAllText(log));
    }

    [Fact]
    public async Task RecoveredReportPreservesAndResanitizesArchivedLogsWithoutDuplicates()
    {
        string inputs = Path.Combine(root, "inputs");
        Directory.CreateDirectory(inputs);
        string originalLog = Path.Combine(inputs, "original.log");
        string additionalLog = Path.Combine(inputs, "additional.log");
        string originalText = @"original marker ../fixtures/external-fixture.log" + Environment.NewLine + "password=ArchiveSecret";
        string additionalText = @"additional marker ../fixtures/external-fixture.log";
        File.WriteAllText(originalLog, originalText);
        File.WriteAllText(additionalLog, additionalText);
        ErrorIncident incident = ErrorIncident.Capture(new Exception("recovered logs"), "test", logPaths: [originalLog]);
        string archivePath;
        using (PreparedErrorReport initial = await ErrorReport.PrepareAsync(incident, root))
        {
            archivePath = initial.ArchivePath;
        }
        using (ZipArchive archive = ZipFile.Open(archivePath, ZipArchiveMode.Update))
        {
            archive.GetEntry("logs/00.log")!.Delete();
            using StreamWriter writer = new(archive.CreateEntry("logs/UnknownArchivedIdentity.log").Open());
            writer.Write(originalText);
        }
        ErrorIncident recovered = Assert.IsType<ErrorIncident>(ErrorIncident.DiscoverLatest(root));
        ErrorIncident enriched = recovered.WithLogPaths([additionalLog]);
        using (PreparedErrorReport report = await ErrorReport.PrepareAsync(enriched, root))
        using (ZipArchive archive = ZipFile.OpenRead(report.ArchivePath))
        {
            Assert.Equal(2, archive.Entries.Count(entry => entry.FullName.StartsWith("logs/", StringComparison.Ordinal)));
            using StreamReader originalReader = new(archive.GetEntry("logs/00.log")!.Open());
            string archivedOriginal = originalReader.ReadToEnd();
            Assert.Contains("original marker", archivedOriginal);
            Assert.DoesNotContain("ArchiveFixture", archivedOriginal);
            Assert.DoesNotContain("ArchiveSecret", archivedOriginal);
            using StreamReader additionalReader = new(archive.GetEntry("logs/01.log")!.Open());
            string archivedAdditional = additionalReader.ReadToEnd();
            Assert.Contains("additional marker", archivedAdditional);
            Assert.DoesNotContain("AdditionalFixture", archivedAdditional);
            Assert.DoesNotContain(archive.Entries, entry => entry.FullName.Contains("UnknownArchivedIdentity", StringComparison.Ordinal));
            using Stream manifestStream = archive.GetEntry("manifest.json")!.Open();
            using JsonDocument manifest = JsonDocument.Parse(manifestStream);
            foreach (JsonElement entry in manifest.RootElement.GetProperty("files").EnumerateArray())
            {
                Assert.Equal(entry.GetProperty("bytes").GetInt64(), archive.GetEntry(entry.GetProperty("name").GetString()!)!.Length);
            }
        }
        DateTime modified = File.GetLastWriteTimeUtc(archivePath);
        using (PreparedErrorReport repeated = await ErrorReport.PrepareAsync(enriched, root))
        using (ZipArchive archive = ZipFile.OpenRead(repeated.ArchivePath))
        {
            Assert.Equal(2, archive.Entries.Count(entry => entry.FullName.StartsWith("logs/", StringComparison.Ordinal)));
            Assert.Equal(modified, File.GetLastWriteTimeUtc(archivePath));
        }
        Assert.Equal(originalText, File.ReadAllText(originalLog));
        Assert.Equal(additionalText, File.ReadAllText(additionalLog));
    }

    [Fact]
    public async Task OversizedIncompressibleSaveFailsWithoutPublishingAndPreservesPendingEvidence()
    {
        byte[] bytes = new byte[21 * 1024 * 1024];
        new Random(713).NextBytes(bytes);
        "MFF8SAVE"u8.CopyTo(bytes);
        ErrorIncident incident = ErrorIncident.Capture(new Exception("save too large"), "test", save: new ReportSave(bytes, "earlier checkpoint", DateTimeOffset.UtcNow.AddMinutes(-2), "synthetic validated"));
        string pending = incident.Persist(root);
        IOException failure = await Assert.ThrowsAsync<IOException>(() => ErrorReport.PrepareAsync(incident, root));
        Assert.Contains("validated reproduction save cannot fit", failure.Message);
        Assert.Empty(Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories));
        Assert.Equal(bytes, File.ReadAllBytes(Path.Combine(pending, "state.ff8save")));
        ErrorIncident recovered = Assert.IsType<ErrorIncident>(ErrorIncident.DiscoverLatest(root));
        Assert.Equal(incident.Id, recovered.Id);
        Assert.Equal(bytes.Length, recovered.Save!.Length);
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
        File.WriteAllText(log, new string('a', 2 * 1024 * 1024) + "\nend\n");
        ErrorIncident incident = ErrorIncident.Capture(new IOException("primary"), "test", new BrokenContributor(), logPaths: [log, Path.Combine(root, "missing.log")], breadcrumbs: breadcrumbs);
        Assert.Equal(1000, incident.Events.Count);
        Assert.Single(incident.CollectionErrors);
        using PreparedErrorReport report = await ErrorReport.PrepareAsync(incident, root);
        Assert.Contains("primary", report.Description);
        Assert.Contains("older bytes", report.Description);
        Assert.Contains("Log 02 unavailable", report.Description);
        using ZipArchive archive = ZipFile.OpenRead(report.ArchivePath);
        using StreamReader reader = new(archive.GetEntry("logs/00.log")!.Open());
        Assert.Equal("end\n", reader.ReadToEnd());
    }

    [Theory]
    [InlineData("password=\"", "QuotedSecretSuffix\"")]
    [InlineData(@"../fixtures", "PrivatePathSuffix")]
    public async Task BoundedLogTailDiscardsPartialPrivateRecord(string prefix, string suffix)
    {
        Directory.CreateDirectory(root);
        string log = Path.Combine(root, "private-tail.log");
        File.WriteAllText(log, prefix + new string('q', 2 * 1024 * 1024) + suffix + "\ncomplete diagnostic record\n");
        ErrorIncident incident = ErrorIncident.Capture(new Exception("log tail"), "test", logPaths: [log]);
        using PreparedErrorReport report = await ErrorReport.PrepareAsync(incident, root);
        using ZipArchive archive = ZipFile.OpenRead(report.ArchivePath);
        using StreamReader reader = new(archive.GetEntry("logs/00.log")!.Open());
        string content = reader.ReadToEnd();
        Assert.Equal("complete diagnostic record\n", content);
        Assert.DoesNotContain(suffix, content);
        Assert.Contains("older bytes", report.Description);
    }

    [Fact]
    public async Task OversizedSingleLogRecordIsExplicitlyOmitted()
    {
        Directory.CreateDirectory(root);
        string log = Path.Combine(root, "single-record.log");
        File.WriteAllText(log, "password=\"" + new string('q', 2 * 1024 * 1024) + "PrivateSuffix\"");
        ErrorIncident incident = ErrorIncident.Capture(new Exception("single log record"), "test", logPaths: [log]);
        using PreparedErrorReport report = await ErrorReport.PrepareAsync(incident, root);
        using ZipArchive archive = ZipFile.OpenRead(report.ArchivePath);
        Assert.DoesNotContain(archive.Entries, entry => entry.FullName.StartsWith("logs/", StringComparison.Ordinal));
        Assert.Contains("oversized incomplete record", report.Description);
    }

    [Fact]
    public async Task BoundedLogTailPreservesRecordBeginningExactlyAtLimit()
    {
        Directory.CreateDirectory(root);
        string log = Path.Combine(root, "record-boundary.log");
        string tail = new string('a', 1024 * 1024 - 1) + "\n";
        File.WriteAllText(log, "older record\n" + tail);
        ErrorIncident incident = ErrorIncident.Capture(new Exception("record boundary"), "test", logPaths: [log]);
        using PreparedErrorReport report = await ErrorReport.PrepareAsync(incident, root);
        using ZipArchive archive = ZipFile.OpenRead(report.ArchivePath);
        using StreamReader reader = new(archive.GetEntry("logs/00.log")!.Open());
        Assert.Equal(tail, reader.ReadToEnd());
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
        Assert.NotNull(recovered.Save);
        Assert.Equal(incident.LaunchId, recovered.LaunchId);
        Assert.Equal(incident.Timestamp, recovered.Timestamp);
        recovered.MarkPresented(root);
        Assert.Null(ErrorIncident.DiscoverLatest(root, "archive-launch", true));
        Assert.NotNull(ErrorIncident.DiscoverLatest(root, "archive-launch"));
        Assert.Equal([report.ArchivePath], Directory.GetFiles(Path.Combine(root, incident.Id)));
    }

    [Fact]
    public async Task CompactSaveIsRecoveredWithoutLooseCopies()
    {
        ErrorIncident incident = ErrorIncident.Capture(new Exception("unsupported save"), "test", save: new ReportSave("MFF8SAVE"u8.ToArray().Concat(new byte[] { 0, 1, 2, 255 }).ToArray(), "current", DateTimeOffset.UtcNow, "synthetic validated"));
        using PreparedErrorReport report = await ErrorReport.PrepareAsync(incident, root);
        using ZipArchive archive = ZipFile.OpenRead(report.ArchivePath);
        Assert.NotNull(archive.GetEntry("repro/state.ff8save"));
        Assert.Null(archive.GetEntry("repro/state.json"));
        Assert.NotNull(ErrorIncident.DiscoverLatest(root)?.Save);
        Assert.Equal([report.ArchivePath], Directory.GetFiles(Path.Combine(root, incident.Id)));
        Assert.False(Directory.Exists(root + ".pending/" + incident.Id));
    }

    [Theory]
    [InlineData("76561198012345678")]
    [InlineData("username=OriginalLaunch")]
    public async Task PublishedMetadataPreservesLaunchIdentityAndExactCaptureTimes(string launchId)
    {
        DateTimeOffset captured = DateTimeOffset.Parse("2024-02-03T04:05:06.1234567+02:00");
        ErrorIncident incident = ErrorIncident.Capture(new Exception("metadata"), "test", launchId: launchId, save: new ReportSave("MFF8SAVE"u8.ToArray(), "current", captured, "synthetic validated"));
        using PreparedErrorReport report = await ErrorReport.PrepareAsync(incident, root);
        using ZipArchive archive = ZipFile.OpenRead(report.ArchivePath);
        using Stream evidence = archive.GetEntry("incident.json")!.Open();
        using JsonDocument metadata = JsonDocument.Parse(evidence);
        Assert.Equal(launchId, metadata.RootElement.GetProperty("LaunchId").GetString());
        Assert.Equal(incident.Timestamp, metadata.RootElement.GetProperty("Timestamp").GetDateTimeOffset());
        Assert.Equal(captured, metadata.RootElement.GetProperty("Save").GetProperty("CapturedUtc").GetDateTimeOffset());
        Assert.Contains("Launch: " + launchId + "\n", report.Description);
        ErrorIncident restored = Assert.IsType<ErrorIncident>(ErrorIncident.DiscoverLatest(root, launchId));
        Assert.Equal(launchId, restored.LaunchId);
        Assert.Equal(incident.Timestamp, restored.Timestamp);
        Assert.Equal(captured, restored.Save!.CapturedUtc);
    }

    [Fact]
    public async Task LogsRetainLatestEightWithoutChangingOriginalInputs()
    {
        string inputs = Path.Combine(root, "inputs");
        Directory.CreateDirectory(inputs);
        string[] paths = Enumerable.Range(0, 10).Select(index => Path.Combine(inputs, $"original-{index}.log")).ToArray();
        for (int index = 0; index < paths.Length; index++)
        {
            File.WriteAllText(paths[index], $"log marker {index}");
        }
        ErrorIncident incident = ErrorIncident.Capture(new Exception("logs"), "test", logPaths: paths);
        using PreparedErrorReport report = await ErrorReport.PrepareAsync(incident, root);
        using ZipArchive archive = ZipFile.OpenRead(report.ArchivePath);
        Assert.Equal(8, archive.Entries.Count(entry => entry.FullName.StartsWith("logs/", StringComparison.Ordinal)));
        using StreamReader first = new(archive.GetEntry("logs/00.log")!.Open());
        Assert.Equal("log marker 2", first.ReadToEnd());
        using StreamReader last = new(archive.GetEntry("logs/07.log")!.Open());
        Assert.Equal("log marker 9", last.ReadToEnd());
        Assert.Contains("Omitted 2 older log files", report.Description);
        for (int index = 0; index < paths.Length; index++)
        {
            Assert.Equal($"log marker {index}", File.ReadAllText(paths[index]));
        }
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
