using System.IO.Compression;
using System.Text.Json;
using Memoria.FF8.LinuxTesting;

namespace Memoria.FF8.LinuxTesting.Tests;

public sealed class CliTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "Memoria Linux CLI " + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task PlanAcceptCommitAndUnchangedRerunUseTheSameProtocol()
    {
        string root = Path.Combine(directory, "target");
        string source = Path.Combine(directory, "source");
        Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, "tool"), "new build");
        Assert.Equal(0, await Cli.RunAsync(["init", root], TestContext.Current.CancellationToken));
        string request = Path.Combine(directory, "sync.json");
        Dictionary<string, string> manifest = Deployment.Manifest(source);
        File.WriteAllText(request, JsonSerializer.Serialize(new { root, manifest, source }));
        Assert.Equal(0, await Cli.RunAsync(["plan", request], TestContext.Current.CancellationToken));
        string inbox = Path.Combine(root, "inbox");
        Directory.CreateDirectory(inbox);
        string archive = Path.Combine(inbox, "incoming.zip");
        ZipFile.CreateFromDirectory(source, archive);
        Assert.Equal(0, await Cli.RunAsync(["accept", request, "--archive", archive], TestContext.Current.CancellationToken));
        Assert.Equal(0, await Cli.RunAsync(["commit", request], TestContext.Current.CancellationToken));
        Assert.Equal(0, await Cli.RunAsync(["sync", request], TestContext.Current.CancellationToken));
        Assert.Equal("new build", File.ReadAllText(Path.Combine(root, "payload", "tool")));
    }

    [Fact]
    public void StopRequestsCancellationOnlyInsideAnOwnedRunDirectory()
    {
        string root = Path.Combine(directory, "target");
        Deployment.Initialize(root);
        string run = Path.Combine(root, "runs", "test-1");
        Directory.CreateDirectory(run);
        Cli.RequestStop(run);
        Assert.True(File.Exists(Path.Combine(run, "stop.request")));
        Assert.Throws<ArgumentException>(() => Cli.RequestStop(root));
        string unownedRun = Path.Combine(directory, "unowned", "runs", "test-2");
        Directory.CreateDirectory(unownedRun);
        Assert.Throws<InvalidDataException>(() => Cli.RequestStop(unownedRun));
        Assert.False(File.Exists(Path.Combine(unownedRun, "stop.request")));
    }

    [Fact]
    public async Task StopWaitsUntilTheResultIsAvailableForRetrieval()
    {
        string root = Path.Combine(directory, "target");
        Deployment.Initialize(root);
        string run = Path.Combine(root, "runs", "test-1");
        Directory.CreateDirectory(run);
        Task<int> stop = Cli.RunAsync(["stop", run], TestContext.Current.CancellationToken);
        Assert.True(File.Exists(Path.Combine(run, "stop.request")));
        Assert.False(stop.IsCompleted);
        await File.WriteAllTextAsync(Path.Combine(run, "result.json"), "{}", TestContext.Current.CancellationToken);
        Assert.Equal(0, await stop);
    }

    [Fact]
    public async Task InvalidCommandsAndExtraArgumentsFail()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => Cli.RunAsync([], TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentException>(() => Cli.RunAsync(["unknown", directory], TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentException>(() => Cli.RunAsync(["init", directory, "extra"], TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentException>(() => Cli.RunAsync(["accept", directory], TestContext.Current.CancellationToken));
        Assert.False(Directory.Exists(directory));
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, true);
        }
    }
}
