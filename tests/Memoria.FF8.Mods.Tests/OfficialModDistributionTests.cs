using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Memoria.FF8.Platform;

namespace Memoria.FF8.Mods.Tests;

public sealed class OfficialModDistributionTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "official-mod-tests-" + Guid.NewGuid().ToString("N"));
    private string Mods => Path.Combine(root, "Mods");
    private ModPackageInstaller Installer(string profile = "Classic/fr/fixture") => new(ModPackageInstaller.StateDirectoryFor(Mods), Mods, profile, junctionSupported: false);
    private static string Range => $"[{EngineBuildMetadata.CompatibilityVersion},)";
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task StageEnableAndRollbackUseIndependentNativeReceiptAndKeepPreferences()
    {
        Directory.CreateDirectory(Mods);
        (string zip, RemoteModEntry initial) = Package("1.9.0");
        ModPackageInstaller installer = Installer();
        var plan = await installer.InspectAsync(zip, initial, Token);
        Assert.True((await installer.RequestInstallAsync(plan, false, Token)).RestartRequired);
        Assert.False(Directory.Exists(plan.Destination));
        Assert.True(Assert.Single(Installer("Classic/en/other-profile").RecoverAndApply()).Installed);
        Assert.True(Assert.Single(Installer().CompleteStartupApplications([plan.PackageId])).Installed);
        string playerIni = Path.Combine(plan.Destination, "mod.ini");
        Assert.False(File.Exists(playerIni));
        using (ModRuntime fresh = new(root, watch: false, modsDirectory: Mods))
        {
            Assert.True(fresh.PreparePending(force: true));
            Assert.True(fresh.CommitPending());
            Assert.True(Assert.Single(fresh.InstalledPackages).RequestedEnabled);
        }
        const string playerSettings = "enabled = 0\nkeep=example\n";
        File.WriteAllText(playerIni, playerSettings);

        string preferences = Path.Combine(Mods, "Memoria", "memoria-settings.json");
        File.WriteAllText(preferences, """{"components":{"Fixture":false,"Other":false},"meltdown":{}}""");
        var (update, release) = Package("1.10.0");
        Assert.True(release.CompareVersion(initial.Version) > 0);
        ModPackageInstallPlan next = await installer.InspectAsync(update, release, Token);
        Assert.True((await installer.RequestInstallAsync(next, true, Token, enableOnAcceptance: true)).RestartRequired);
        Assert.True(Assert.Single(Installer().RecoverAndApply()).Installed);
        Assert.False(Assert.Single(Installer().CompleteStartupApplications([])).Installed);
        Assert.Equal("1.9.0", ModPackageInstaller.ReadOfficialInstallation(Mods, initial.Official!)!.Version);
        Assert.Contains("\"Fixture\":false", File.ReadAllText(preferences));

        next = await installer.InspectAsync(update, release, Token);
        Assert.True((await installer.RequestInstallAsync(next, true, Token, enableOnAcceptance: true)).RestartRequired);
        Assert.True(Assert.Single(Installer().RecoverAndApply()).Installed);
        using ModRuntime runtime = new(root, watch: false, modsDirectory: Mods);
        Assert.True(runtime.PreparePending(force: true));
        Assert.True(runtime.CommitPending());
        Assert.True(Assert.Single(runtime.InstalledPackages).RequestedEnabled);
        Assert.True(Assert.Single(Installer().CompleteStartupApplications([next.PackageId])).Installed);
        Assert.Equal("1.10.0", ModPackageInstaller.ReadOfficialInstallation(Mods, release.Official!)!.Version);
        Assert.Contains("\"Fixture\":true", File.ReadAllText(preferences));
        Assert.Contains("\"Other\":false", File.ReadAllText(preferences));
        Assert.Equal(playerSettings, File.ReadAllText(playerIni));

        var (_, incompatible) = Package("1.11.0", "[99.0.0,)");
        Assert.Contains("99.0.0", Installer().CompatibilityError(incompatible));
        Assert.Equal("1.10.0", Installer().GetStatus(incompatible)!.Version);
        Assert.Throws<InvalidDataException>(() => OfficialModCatalog.Parse(Catalog(1, Package("1.12.0", "[0.8.9,1.0.0)").Entry.Official!)));
    }

    [Fact]
    public async Task CatalogCacheRejectsStaleOrInvalidRevisionsAndBootstrapsOffline()
    {
        string cache = Path.Combine(root, "cache");
        Assert.Contains(OfficialModCatalog.ReadCached(cache).Components, component => component.Id == "girl-next-door");
        var (_, entry) = Package("1.0.0");
        using HttpClient client = new(new Response(_ => new(HttpStatusCode.OK) { Content = new ByteArrayContent(Catalog(2, entry.Official!)) }));
        OfficialCatalogSnapshot official = await OfficialModCatalog.RefreshAsync(cache, client, cancellationToken: Token);
        Assert.Equal(2, official.Revision);
        Assert.Null(official.Error);
        Assert.Contains(official.Components, component => component.Id == "fixture");
        using HttpClient old = new(new Response(_ => new(HttpStatusCode.OK) { Content = new ByteArrayContent(Catalog(1, entry.Official!)) }));
        OfficialCatalogSnapshot rejected = await OfficialModCatalog.RefreshAsync(cache, old, force: true, cancellationToken: Token, bypassDebounce: true);
        Assert.Equal(2, rejected.Revision);
        Assert.NotNull(rejected.Error);
        Assert.Equal(2, OfficialModCatalog.ReadCached(cache).Revision);
    }

    [Fact]
    public async Task DownloadRequiresPublisherDigestAndStoresVerifiedArchiveAnonymously()
    {
        var (path, entry) = Package("1.10.0");
        byte[] archive = File.ReadAllBytes(path);
        using HttpClient client = new(new Response(request =>
        {
            Assert.Null(request.Headers.Authorization);
            return new(HttpStatusCode.OK) { Content = new ByteArrayContent(archive) };
        }));
        using ModDownloadCoordinator downloads = new(Path.Combine(root, "downloads"), client);
        await downloads.InitializeAsync(Token);
        Guid valid = downloads.Enqueue(entry, "Classic/en");
        DownloadJobSnapshot verified = await Finish(downloads, valid);
        Assert.True(verified.Phase == ModDownloadPhase.ReadyToInstall, verified.Details);
        OfficialModComponent descriptor = entry.Official!;
        OfficialModRelease release = descriptor.Latest!;
        RemoteModEntry changedDigest = Entry(descriptor with { Latest = release with { Artifact = release.Artifact with { Sha256 = new('0', 64) } } });
        Guid invalid = downloads.Enqueue(changedDigest, "Classic/en");
        Assert.NotEqual(valid, invalid);
        DownloadJobSnapshot rejected = await Finish(downloads, invalid);
        Assert.Equal(ModDownloadPhase.Failed, rejected.Phase);
        Assert.Null(rejected.ArchivePath);
        await Task.Delay(100, Token);
    }

    [Fact]
    public async Task DownloadCacheSurvivesSwitchingProfilesOnTheSamePhysicalRoot()
    {
        var (path, entry) = Package("1.0.0");
        byte[] archive = File.ReadAllBytes(path);
        byte[] catalog = Catalog(1, entry.Official!);
        int artifactRequests = 0;
        using HttpClient client = new(new Response(request =>
        {
            if (request.RequestUri == OfficialModCatalog.Feed)
            {
                return new(HttpStatusCode.OK) { Content = new ByteArrayContent(catalog) };
            }
            Interlocked.Increment(ref artifactRequests);
            return new(HttpStatusCode.OK) { Content = new ByteArrayContent(archive) };
        }));
        using ModRuntime runtime = new(root, watch: false, modsDirectory: Mods);
        string cache = Path.Combine(root, "catalog");
        Guid id;
        using (ModLibraryService first = new(Path.Combine(root, "first"), Installer("Classic/fr/first"), runtime, "Classic/fr/first", false, client, officialCacheDirectory: cache, officialSupported: true))
        {
            await WaitFor(() => first.View.OfficialSupported && first.View.Catalog.Entries.Any(item => item.Id == entry.Id));
            first.Queue(first.View.Catalog.Entries.Single(item => item.Id == entry.Id));
            await WaitFor(() => first.View.Jobs.Any(job => job.CanInstall));
            id = Assert.Single(first.View.Jobs).Id;
        }

        string queueLock = Path.Combine(ModPackageInstaller.StateDirectoryFor(Mods), "official-downloads", "queue.lock");
        await WaitFor(() => QueueReleased(queueLock));
        using (ModLibraryService second = new(Path.Combine(root, "second"), Installer("Classic/en/second"), runtime, "Classic/en/second", false, client, officialCacheDirectory: cache, officialSupported: true))
        {
            await WaitFor(() => second.View.Jobs.Any(job => job.CanInstall));
            Assert.Equal(id, Assert.Single(second.View.Jobs).Id);
            Assert.Equal(1, artifactRequests);
        }
        await WaitFor(() => QueueReleased(queueLock));
    }

    private static bool QueueReleased(string path)
    {
        try
        {
            using FileStream file = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
    }

    private static async Task WaitFor(Func<bool> predicate)
    {
        for (int attempt = 0; attempt < 100; attempt++)
        {
            if (predicate())
            {
                return;
            }
            await Task.Delay(50, Token);
        }
        throw new TimeoutException("The mod library did not reach the expected state.");
    }

    [Theory]
    [InlineData("mod.ini")]
    [InlineData("settings/MOD.INI")]
    public async Task OfficialPackageCannotSupplyPlayerSettings(string settingsPath)
    {
        var (path, entry) = Package("1.0.0", settingsPath: settingsPath);
        await Assert.ThrowsAsync<InvalidDataException>(() => Installer().InspectAsync(path, entry, Token));
        Assert.False(Directory.Exists(Mods));
    }

    [Fact]
    public async Task NativeInspectionRejectsModifiedArchiveAndUnownedDestination()
    {
        var (path, entry) = Package("1.0.0");
        ModPackageInstaller installer = Installer();
        ModPackageInstallPlan plan = await installer.InspectAsync(path, entry, Token);
        Directory.CreateDirectory(plan.Destination);
        File.WriteAllText(Path.Combine(plan.Destination, "local.txt"), "keep");
        await Assert.ThrowsAsync<InvalidDataException>(() => installer.RequestInstallAsync(plan, cancellationToken: Token));
        Assert.Equal("keep", File.ReadAllText(Path.Combine(plan.Destination, "local.txt")));
        using (FileStream stream = new(path, FileMode.Append))
        {
            stream.WriteByte(1);
        }
        await Assert.ThrowsAsync<InvalidDataException>(() => installer.InspectAsync(path, entry, Token));
    }

    private static async Task<DownloadJobSnapshot> Finish(ModDownloadCoordinator downloads, Guid id)
    {
        for (int attempt = 0; attempt < 100; attempt++)
        {
            DownloadJobSnapshot job = downloads.Snapshot.Single(job => job.JobId == id);
            if (job.Phase is ModDownloadPhase.ReadyToInstall or ModDownloadPhase.Failed)
            {
                return job;
            }
            await Task.Delay(25, Token);
        }
        throw new TimeoutException("The download did not complete.");
    }

    private (string Path, RemoteModEntry Entry) Package(string version, string? engineRange = null, string? settingsPath = null)
    {
        Directory.CreateDirectory(root);
        engineRange ??= Range;
        OfficialModTarget target = new("FFVIII", ["Classic"], ["*"]);
        byte[] payload = Encoding.UTF8.GetBytes("fixture " + version);
        byte[] ini = "enabled = 0\n"u8.ToArray();
        List<OfficialModFile> files = [new("asset.txt", payload.Length, Hash(payload), false)];
        if (settingsPath is not null)
        {
            files.Add(new(settingsPath, ini.Length, Hash(ini), false));
        }
        var manifest = new
        {
            schemaVersion = 1,
            sourceId = OfficialModCatalog.SourceId,
            componentId = "fixture",
            runtimePackageId = "Memoria/Fixture",
            version,
            engineVersionRange = engineRange,
            target,
            files
        };
        byte[] metadata = JsonSerializer.SerializeToUtf8Bytes(manifest, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        string path = Path.Combine(root, version + ".zip");
        using (ZipArchive zip = ZipFile.Open(path, ZipArchiveMode.Create))
        {
            Write(zip, "mod.manifest.json", metadata);
            Write(zip, "payload/asset.txt", payload);
            if (settingsPath is not null)
            {
                Write(zip, "payload/" + settingsPath, ini);
            }
        }
        OfficialModArtifact artifact = new("https://example.test/mod.zip", new FileInfo(path).Length, metadata.Length + payload.Length + (settingsPath is null ? 0 : ini.Length), Hash(File.ReadAllBytes(path)));
        OfficialModComponent descriptor = new("fixture", "Memoria", "Memoria/Fixture", "Fixture", "Fixture", "Test artwork", "Items", "memoria-native", "download", true, target, new(version, engineRange, "fixture-v" + version, "New", artifact));
        return (path, Entry(descriptor));
    }

    private static RemoteModEntry Entry(OfficialModComponent descriptor) => new(OfficialModCatalog.SourceId, descriptor.CatalogId, descriptor.Name, descriptor.Latest!.Version)
    {
        Official = descriptor,
        Links = [descriptor.Latest.Artifact.Url]
    };

    private static byte[] Catalog(int revision, OfficialModComponent component)
    {
        return JsonSerializer.SerializeToUtf8Bytes(new { schemaVersion = 1, sourceId = OfficialModCatalog.SourceId, revision, publishedAt = DateTimeOffset.UnixEpoch, components = new[] { component } }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }

    private static void Write(ZipArchive zip, string path, byte[] bytes)
    {
        using Stream stream = zip.CreateEntry(path).Open();
        stream.Write(bytes);
    }

    private static string Hash(byte[] data) => Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();

    private sealed class Response(Func<HttpRequestMessage, HttpResponseMessage> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(send(request));
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }
    }
}
