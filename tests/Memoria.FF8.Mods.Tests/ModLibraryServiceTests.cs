using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Memoria.FF8.Mods.Tests;

public sealed class ModLibraryServiceTests
{
    [Fact]
    public async Task DownloadCheckAndInstallAreSeparateAndDoNotPublishDuringGameplay()
    {
        string root = Path.Combine(Path.GetTempPath(), "mod-library-" + Guid.NewGuid().ToString("N"));
        Guid id = Guid.NewGuid();
        using MemoryStream bytes = new();
        using (ZipArchive archive = new(bytes, ZipArchiveMode.Create, true))
        {
            using StreamWriter writer = new(archive.CreateEntry("mod.xml").Open());
            writer.Write($"<ModInfo><ID>{id}</ID><Name>Example</Name><Version>1</Version></ModInfo>");
        }
        byte[] package = bytes.ToArray();
        int requests = 0;
        using HttpClient client = new(new Handler(request =>
        {
            Interlocked.Increment(ref requests);
            byte[] body = request.RequestUri!.Host == "codeload.github.com"
                ? Encoding.UTF8.GetBytes($"<Catalog><Mods><Mod><ID>{id}</ID><Name>Example</Name><LatestVersion><Version>1</Version><Link>iroj://Url/https$example.test/package</Link></LatestVersion></Mod></Mods></Catalog>")
                : package;
            return new(HttpStatusCode.OK) { Content = new ByteArrayContent(body) };
        }));
        using ModRuntime mods = new(root, watch: false);
        ModPackageInstaller installer = new(Path.Combine(root, "state"), Path.Combine(root, "Mods"), "test");
        using ModLibraryService library = new(Path.Combine(root, "cache"), installer, mods, "test", true, client);
        Assert.Equal(0, requests);
        library.RefreshCatalog();
        await Until(() => library.View.Catalog.Entries.Count == 1);
        library.Queue(Assert.Single(library.View.Catalog.Entries));
        await Until(() => library.View.Jobs.Any(job => job.CanInstall));
        ModLibraryJob job = Assert.Single(library.View.Jobs);
        Assert.Equal("Ready to install", job.Phase);
        Assert.Null(job.Error);
        Assert.False(Directory.Exists(Path.Combine(root, "Mods", "Junction")));
        string blockedTransactions = Path.Combine(root, "state", "transactions");
        await File.WriteAllTextAsync(blockedTransactions, "blocked", TestContext.Current.CancellationToken);
        library.Install(job.Id);
        await Until(() => library.View.Jobs.Single().CanRetry);
        library.Retry(job.Id);
        await Until(() => library.View.Jobs.Single().CanInstall);
        await Until(() => Directory.GetDirectories(Path.Combine(root, "state", "prepared")).Length == 1);
        File.Delete(blockedTransactions);
        library.Install(job.Id);
        await Until(() => library.View.Jobs.Single().Phase == "Restart required");
        Assert.Null(library.View.Jobs.Single().Error);
        Assert.False(Directory.Exists(Path.Combine(root, "Mods", "Junction")));
        Assert.Empty(mods.Catalog.Packages);
        Assert.True(installer.GetStatus(job.Entry)!.RestartRequired);
        Assert.Equal(2, requests);
        installer.RecoverAndApply();
        ModPackageInstallResult installed = installer.GetStatus(job.Entry)!;
        installer.CompleteStartupApplications([installed.PackageId]);
        string destination = Path.Combine(root, "Mods", installed.PackageId.Replace('/', Path.DirectorySeparatorChar));
        Directory.Delete(destination, true);
        Assert.False(installer.GetStatus(job.Entry)!.Installed);
        library.Queue(job.Entry);
        await Until(() => library.View.Jobs.Single().CanInstall);
        Assert.Null(library.View.Jobs.Single().Error);
        library.Install(job.Id);
        await Until(() => library.View.Jobs.Single().Phase == "Restart required");
        Assert.Equal(2, requests);
        // The service cancels without waiting on network or package workers. Temporary data is outside discovery.
    }

    [Fact]
    public async Task UnsupportedTargetCanBrowseButCannotQueue()
    {
        string root = Path.Combine(Path.GetTempPath(), "mod-library-" + Guid.NewGuid().ToString("N"));
        using HttpClient client = new(new Handler(_ => throw new InvalidOperationException("No network request was expected.")));
        using ModRuntime mods = new(root, watch: false);
        ModPackageInstaller installer = new(Path.Combine(root, "state"), Path.Combine(root, "Mods"), "remastered", false);
        using ModLibraryService library = new(Path.Combine(root, "cache"), installer, mods, "remastered", false, client);
        library.Queue(new("synthetic", Guid.NewGuid(), "Example", "1"));
        await Until(() => library.View.Error is not null);
        Assert.Empty(library.View.Jobs);
        Assert.False(library.View.TargetSupported);
    }

    [Fact]
    public async Task RecoveredPendingAndInstalledJobsNeedNoNetworkAndCacheRemovalPreservesInstallation()
    {
        string root = Path.Combine(Path.GetTempPath(), "mod-library-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        RemoteModEntry entry = new("synthetic", Guid.NewGuid(), "Example", "1");
        string package = Path.Combine(root, "source.zip");
        using (ZipArchive archive = ZipFile.Open(package, ZipArchiveMode.Create))
        {
            using StreamWriter writer = new(archive.CreateEntry("mod.xml").Open());
            writer.Write($"<ModInfo><ID>{entry.Id}</ID><Name>Example</Name><Version>1</Version></ModInfo>");
        }
        byte[] bytes = await File.ReadAllBytesAsync(package, TestContext.Current.CancellationToken);
        ModPackageInstaller installer = new(Path.Combine(root, "state"), Path.Combine(root, "Mods"), "test");
        ModPackageInstallPlan plan = await installer.InspectAsync(package, entry, TestContext.Current.CancellationToken);
        await installer.RequestInstallAsync(plan, false, TestContext.Current.CancellationToken);
        int requests = 0;
        using HttpClient client = new(new Handler(_ =>
        {
            Interlocked.Increment(ref requests);
            throw new InvalidOperationException("No network request was expected.");
        }));
        using ModRuntime mods = new(root, watch: false);
        async Task<(Guid Id, string Path)> WriteRecovered(string cache)
        {
            Guid id = Guid.NewGuid();
            string directory = Path.Combine(cache, "downloads", id.ToString("N"));
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "package.download");
            await File.WriteAllBytesAsync(path, bytes, TestContext.Current.CancellationToken);
            DownloadJobSnapshot job = new(id, entry, "test", false, ModDownloadPhase.ReadyToInstall) { ArchivePath = path, CompletedBytes = bytes.Length, Sha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant() };
            await File.WriteAllTextAsync(Path.Combine(directory, "job.json"), JsonSerializer.Serialize(job), TestContext.Current.CancellationToken);
            return (id, path);
        }
        string pendingCache = Path.Combine(root, "pending-cache");
        await WriteRecovered(pendingCache);
        using (ModLibraryService pending = new(pendingCache, installer, mods, "test", true, client))
        {
            await Until(() => pending.View.Jobs.FirstOrDefault()?.Phase == "Restart required");
            Assert.False(pending.View.Jobs.Single().CanResume);
            Assert.False(pending.View.Jobs.Single().CanCancel);
        }
        installer.RecoverAndApply();
        installer.CompleteStartupApplications([plan.PackageId]);
        string installedCache = Path.Combine(root, "installed-cache");
        (Guid jobId, string cachedPath) = await WriteRecovered(installedCache);
        using ModLibraryService installed = new(installedCache, installer, mods, "test", true, client);
        await Until(() => installed.View.Jobs.FirstOrDefault()?.Phase == "Installed");
        Assert.True(installed.View.Jobs.Single().CanCancel);
        installed.Cancel(jobId);
        await Until(() => !File.Exists(cachedPath));
        Assert.True(Directory.Exists(plan.Destination));
        Assert.True(installer.GetStatus(entry)!.Installed);
        Assert.Equal(0, requests);
    }

    private static async Task Until(Func<bool> condition)
    {
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        while (!condition())
        {
            await Task.Delay(20, timeout.Token);
        }
    }

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> handle) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(handle(request));
    }
}
