using System.IO.Compression;
using Memoria.FF8.Platform;

namespace Memoria.Launcher.Tests;

public sealed class ErrorReportingTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "Memoria.ReportLauncher.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task ProcessFailureRetainsOriginalIncidentAndFullLaunchLog()
    {
        string reports = Path.Combine(directory, "reports");
        ErrorIncident original = ErrorIncident.Capture(new AggregateException(new IOException("first cause"), new InvalidOperationException("second cause")), "Field", runtime: new { Field = 42 }, launchId: "matching-launch");
        original.Persist(reports);
        ErrorIncident.Capture(new Exception("unrelated launch"), "Startup", launchId: "other-launch").Persist(reports);
        using GameProcessDiagnostics diagnostics = new(Path.Combine(directory, "logs"));
        string path = diagnostics.LogPath!;
        diagnostics.Complete(1);
        string logText = "early evidence " + new string('x', 10000) + " final evidence";
        await File.WriteAllTextAsync(path, logText, TestContext.Current.CancellationToken);
        ErrorIncident recovered = diagnostics.CaptureFailure(1, "matching-launch", reports);
        Assert.Equal(original.Id, recovered.Id);
        Assert.Contains("first cause", recovered.ExceptionText);
        Assert.Contains("second cause", recovered.ExceptionText);
        using PreparedErrorReport report = await ErrorReport.PrepareAsync(recovered, reports);
        using ZipArchive archive = ZipFile.OpenRead(report.ArchivePath);
        ZipArchiveEntry log = Assert.Single(archive.Entries, entry => entry.FullName.StartsWith("logs/", StringComparison.Ordinal));
        using StreamReader reader = new(log.Open());
        Assert.Equal(logText, await reader.ReadToEndAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void SilentNativeExitHasNoInventedManagedStackOrSave()
    {
        using GameProcessDiagnostics diagnostics = new(Path.Combine(directory, "logs"));
        diagnostics.Complete(-1);
        ErrorIncident incident = diagnostics.CaptureFailure(-1, "native-launch", Path.Combine(directory, "reports"));
        Assert.Null(incident.Exception.Stack);
        Assert.Null(incident.Save);
        Assert.Contains("No managed exception was captured", incident.ExceptionText);
        Assert.Equal("native-launch", incident.LaunchId);
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, true);
        }
    }
}
