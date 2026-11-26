using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Memoria.FF8.Mods.Tests;

public sealed class ModLibraryServiceTests
{
    [Fact]
    public async Task StoppingReleasesQueueOwnershipBeforeAnotherLibraryStarts()
    {
        string root = Path.Combine(Path.GetTempPath(), "mod-library-stop-" + Guid.NewGuid().ToString("N"));
        try
        {
            using ModRuntime mods = new(root, watch: false);
            ModPackageInstaller installer = new(Path.Combine(root, "state"), Path.Combine(root, "Mods"), "Classic/en/test");
            using ModLibraryService first = new(Path.Combine(root, "cache"), installer, mods, "Classic/en/test", false);
            first.Queue(new("test", Guid.NewGuid(), "Example", "1"));
            await Until(() => first.View.Error is not null);
            await first.StopAsync();
            await first.StopAsync();
            using ModLibraryService second = new(Path.Combine(root, "cache"), installer, mods, "Classic/en/test", false);
            second.Queue(new("test", Guid.NewGuid(), "Example", "1"));
            await Until(() => second.View.Error is not null);
            Assert.Contains("Downloads cannot be installed", second.View.Error);
            await second.StopAsync();
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    [Fact]
    public async Task DownloadCheckAndInstallAreSeparateAndDoNotPublishDuringGameplay()
    {
        string root = TestResources.ArtifactFile("mod-library-install");
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
        await Until(() => library.View.Jobs.Single().Phase == "Waiting to apply");
        Assert.Null(library.View.Jobs.Single().Error);
        Assert.False(Directory.Exists(Path.Combine(root, "Mods", "Junction")));
        Assert.Empty(mods.Catalog.Packages);
        Assert.True(installer.GetStatus(job.Entry)!.RestartRequired);
        Assert.Equal(3, requests);
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
        await Until(() => library.View.Jobs.Single().Phase == "Waiting to apply");
        Assert.Equal(3, requests);
        await library.StopAsync();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StartupRefreshesCatalogAndRetryRechecksAgainstRefreshedRelease(bool hasCompatibilityRules)
    {
        string root = Path.Combine(Path.GetTempPath(), "mod-library-version-" + Guid.NewGuid().ToString("N"));
        Guid id = Guid.NewGuid();
        using MemoryStream bytes = new();
        using (ZipArchive archive = new(bytes, ZipArchiveMode.Create, true))
        {
            using StreamWriter writer = new(archive.CreateEntry("mod.xml").Open());
            string compatibility = hasCompatibilityRules ? $"<Compatibility><Forbid ModID=\"{Guid.NewGuid()}\">Other mod</Forbid></Compatibility>" : "";
            writer.Write($"<ModInfo><ID>{id}</ID><Name>Example</Name><Version>2.11</Version>{compatibility}</ModInfo>");
        }
        byte[] package = bytes.ToArray();
        int catalogRequests = 0;
        int downloads = 0;
        using HttpClient client = new(new Handler(request =>
        {
            if (request.RequestUri!.Host == "codeload.github.com")
            {
                string version = Interlocked.Increment(ref catalogRequests) == 1 ? "2.10" : "2.11";
                byte[] catalog = Encoding.UTF8.GetBytes($"<Catalog><Mods><Mod><ID>{id}</ID><Name>Example</Name><LatestVersion><Version>{version}</Version><Link>iroj://Url/https$example.test/package</Link></LatestVersion></Mod></Mods></Catalog>");
                return new(HttpStatusCode.OK) { Content = new ByteArrayContent(catalog) };
            }
            Interlocked.Increment(ref downloads);
            return new(HttpStatusCode.OK) { Content = new ByteArrayContent(package) };
        }));
        try
        {
            using ModRuntime mods = new(root, watch: false);
            ModPackageInstaller installer = new(Path.Combine(root, "state"), Path.Combine(root, "Mods"), "test");
            using ModLibraryService library = new(Path.Combine(root, "cache"), installer, mods, "test", true, client, refreshCatalogOnStartup: true);
            await Until(() => library.View.Catalog.Entries.Count == 1);
            RemoteModEntry entry = Assert.Single(library.View.Catalog.Entries);
            library.DownloadAndEnable(entry);
            await Until(() => library.View.Jobs.Any(job => job.CanPreview && job.VersionWarning is not null));
            ModLibraryJob warning = Assert.Single(library.View.Jobs);
            Assert.True(warning.CanRetry);
            Assert.True(warning.CanPreview);
            Assert.True(warning.CanInstall);
            Assert.Null(warning.Error);
            Assert.Null(installer.GetStatus(entry));
            ModPreviewDocument preview = await library.OpenPreviewAsync(entry, null, TestContext.Current.CancellationToken);
            Assert.Equal("Example", preview.Name);

            library.Retry(warning.Id);
            await Until(() => library.View.Jobs.Any(job => job.Entry.Version == "2.11" && job.Phase == "Waiting to apply"));
            ModLibraryJob updated = Assert.Single(library.View.Jobs);
            Assert.Equal(warning.Id, updated.Id);
            Assert.Equal("2.11", updated.Entry.Version);
            Assert.Null(updated.VersionWarning);
            Assert.Equal(2, catalogRequests);
            Assert.Equal(1, downloads);
            await library.StopAsync();
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
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
        bool directoryAvailable = false;
        bool failFirstApplication = true;
        int applyAttempts = 0;
        using (ModLibraryService pending = new(pendingCache, installer, mods, "test", true, client, applyPendingChanges: _ =>
        {
            Interlocked.Increment(ref applyAttempts);
            if (!Volatile.Read(ref directoryAvailable))
            {
                return Task.FromResult(false);
            }
            if (failFirstApplication)
            {
                failFirstApplication = false;
                throw new IOException("Temporary application failure.");
            }
            using ModDirectoryResourceLease lease = ModDirectoryResourceLease.Acquire(Path.Combine(root, "Mods"), true);
            if (!lease.IsWriter)
            {
                return Task.FromResult(false);
            }
            installer.RecoverAndApply();
            return Task.FromResult(true);
        }))
        {
            await Until(() => pending.View.Jobs.FirstOrDefault()?.Phase == "Waiting to apply");
            Assert.False(pending.View.Jobs.Single().CanResume);
            Assert.False(pending.View.Jobs.Single().CanCancel);
            await Until(() => Volatile.Read(ref applyAttempts) > 0);
            Assert.False(Directory.Exists(plan.Destination));
            Volatile.Write(ref directoryAvailable, true);
            await Until(() => pending.View.Error is not null);
            await Until(() => pending.View.Jobs.Single().Phase == "Installed" && pending.View.Installations.Single().Pending == false);
            Assert.Null(pending.View.Error);
            Assert.False(installer.GetStatus(entry)!.RestartRequired);
            Assert.True(Directory.Exists(plan.Destination));
            Assert.Single(Directory.GetFiles(Path.Combine(root, "state", "journals"), "*.json"));
        }
        installer.RecoverAndApply();
        installer.CompleteStartupApplications([plan.PackageId]);
        string installedCache = Path.Combine(root, "installed-cache");
        (Guid jobId, string cachedPath) = await WriteRecovered(installedCache);
        using ModLibraryService installed = new(installedCache, installer, mods, "test", true, client);
        await Until(() => installed.View.Jobs.FirstOrDefault()?.Phase == "Installed");
        Assert.True(installed.View.Jobs.Single().CanCancel);
        Assert.True(installed.View.Jobs.Single().CanDeleteDownload);
        installed.DeleteDownload(jobId);
        await Until(() => !File.Exists(cachedPath));
        Assert.True(Directory.Exists(plan.Destination));
        Assert.True(installer.GetStatus(entry)!.Installed);
        Assert.Equal(0, requests);
        await Until(() => installed.View.Installations.Any(item => item.CanUninstall));
        installed.Uninstall(entry);
        await Until(() => installed.View.Installations.Any(item => item.PendingRemoval));
        Assert.True(Directory.Exists(plan.Destination));
        Assert.True(installer.GetStatus(entry)!.PendingRemoval);
        Assert.Null(Assert.Single(installer.RecoverAndApply()).Error);
        Assert.False(Directory.Exists(plan.Destination));
        await installed.StopAsync();
        Directory.Delete(root, true);
    }

    [Fact]
    public async Task UpdatingAfterChangingDestinationMovesTheVerifiedInstallation()
    {
        string root = Path.Combine(Path.GetTempPath(), "mod-library-destination-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            const string oldProfile = "Classic/en/previous";
            const string newProfile = "Classic/en/selected";
            string previousRoot = Path.Combine(root, "Mods");
            string selectedRoot = Path.Combine(root, "Selected");
            string cache = Path.Combine(root, "cache");
            ModPackageInstaller previous = new(ModPackageInstaller.StateDirectoryFor(previousRoot), previousRoot, oldProfile);
            ModPackageInstaller selected = new(ModPackageInstaller.StateDirectoryFor(selectedRoot), selectedRoot, newProfile);
            RemoteModEntry original = new("synthetic", Guid.NewGuid(), "Existing", "1");
            RemoteModEntry update = original with { Version = "2" };
            RemoteModEntry fresh = new("synthetic", Guid.NewGuid(), "New", "1");
            string originalArchive = Path.Combine(root, "original.zip");
            WritePackage(originalArchive, original);
            ModPackageInstallPlan originalPlan = await previous.InspectAsync(originalArchive, original, TestContext.Current.CancellationToken);
            ModPackageInstallResult originalInstall = await previous.RequestInstallAsync(originalPlan, false, TestContext.Current.CancellationToken);
            Assert.Null(Assert.Single(previous.RecoverAndApply()).Error);
            Assert.Null(Assert.Single(previous.CompleteStartupApplications([originalPlan.PackageId])).Error);
            Guid updateJob = await WriteJob(update);
            Guid freshJob = await WriteJob(fresh);

            using ModRuntime mods = new(root, watch: false, additionalBaseDirectories: [root], modsDirectory: selectedRoot);
            mods.PreparePending(true);
            mods.CommitPending();
            using HttpClient client = new(new Handler(_ => throw new InvalidOperationException("No network request was expected.")));
            using ModLibraryService library = new(cache, selected, mods, newProfile, true, client, previousInstallers: [previous]);
            try
            {
                await Until(() => library.View.Jobs.Count(job => job.CanResume) == 2);
                library.Resume(updateJob);
                library.Resume(freshJob);
                await Until(() => library.View.Jobs.Count(job => job.CanInstall) == 2);
                library.Install(updateJob);
                library.Install(freshJob);
                await Until(() => library.View.Jobs.Count(job => job.Phase == "Waiting to apply") == 2);
                Assert.Null(selected.GetStatus(update));
                Assert.Null(previous.GetStatus(fresh));
                Assert.True(Directory.Exists(originalPlan.Destination));

                ModPackageInstallResult updated = Assert.Single(previous.RecoverAndApply());
                ModPackageInstallResult installed = Assert.Single(selected.RecoverAndApply());
                Assert.Null(updated.Error);
                Assert.Null(installed.Error);
                Assert.Equal("2", updated.Version);
                Assert.Equal(originalPlan.PackageId, updated.PackageId);
                Assert.True(Directory.Exists(originalPlan.Destination));
                Assert.Empty(previous.RecoverRelocations([previous, selected]));
                Assert.Null(Assert.Single(previous.CompleteStartupApplications([updated.PackageId])).Error);
                Assert.Null(Assert.Single(selected.CompleteStartupApplications([installed.PackageId])).Error);
                Assert.NotNull(Assert.Single(previous.RecoverRelocations([previous])).Error);
                Assert.True(Directory.Exists(originalPlan.Destination));
                Assert.Null(selected.GetStatus(update));

                string updatedDestination = Path.Combine(selectedRoot, updated.PackageId.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(updatedDestination);
                string unrelated = Path.Combine(updatedDestination, "unrelated.txt");
                File.WriteAllText(unrelated, "Keep this file.");
                ModPackageInstallResult blocked = Assert.Single(previous.RecoverRelocations([previous, selected]));
                Assert.NotNull(blocked.Error);
                Assert.True(Directory.Exists(originalPlan.Destination));
                Assert.Equal("Keep this file.", File.ReadAllText(unrelated));
                Directory.Delete(updatedDestination, true);

                ModPackageInstaller recoveredPrevious = new(ModPackageInstaller.StateDirectoryFor(previousRoot), previousRoot, oldProfile);
                ModPackageInstallResult relocation = Assert.Single(recoveredPrevious.RecoverRelocations([recoveredPrevious, selected]));
                Assert.Null(relocation.Error);
                Assert.True(relocation.Relocated);
                Assert.False(Directory.Exists(originalPlan.Destination));
                Assert.Null(previous.GetStatus(update));
                Assert.Equal(originalInstall.InstallationId, selected.GetStatus(update)!.InstallationId);
                Assert.Equal("[Mod]\nenabled=0\n", File.ReadAllText(Path.Combine(updatedDestination, "mod.ini")));
                mods.Refresh();
                mods.PreparePending(true);
                Assert.Equal(updatedDestination, Assert.Single(mods.InstalledPackages, package => package.Id == updated.PackageId).RootDirectory);
                Assert.StartsWith(selectedRoot + Path.DirectorySeparatorChar, Assert.Single(mods.InstalledPackages, package => package.Id == installed.PackageId).RootDirectory);

                await Until(() => library.View.Installations.Any(installation => installation.Id == update.Id && installation.CanUninstall));
                library.Uninstall(update);
                await Until(() => library.View.Installations.Any(installation => installation.Id == update.Id && installation.PendingRemoval));
                Assert.True(Directory.Exists(updatedDestination));
                Assert.True(selected.GetStatus(update)!.PendingRemoval);
                Assert.Null(Assert.Single(selected.RecoverAndApply()).Error);
                Assert.False(Directory.Exists(updatedDestination));
                Assert.Null(selected.GetStatus(update));
                Assert.True(selected.GetStatus(fresh)!.Installed);
            }
            finally
            {
                await library.StopAsync();
            }

            async Task<Guid> WriteJob(RemoteModEntry entry)
            {
                Guid id = Guid.NewGuid();
                string directory = Path.Combine(cache, "downloads", id.ToString("N"));
                Directory.CreateDirectory(directory);
                string archive = Path.Combine(directory, "package.download");
                WritePackage(archive, entry);
                byte[] bytes = await File.ReadAllBytesAsync(archive, TestContext.Current.CancellationToken);
                DownloadJobSnapshot job = new(id, entry, newProfile, false, ModDownloadPhase.ReadyToInstall) { ArchivePath = archive, CompletedBytes = bytes.Length, Sha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant() };
                await File.WriteAllTextAsync(Path.Combine(directory, "job.json"), JsonSerializer.Serialize(job), TestContext.Current.CancellationToken);
                return id;
            }
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static void WritePackage(string path, RemoteModEntry entry)
    {
        using ZipArchive archive = ZipFile.Open(path, ZipArchiveMode.Create);
        using StreamWriter writer = new(archive.CreateEntry("mod.xml").Open());
        writer.Write($"<ModInfo><ID>{entry.Id}</ID><Name>{entry.Name}</Name><Version>{entry.Version}</Version></ModInfo>");
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
