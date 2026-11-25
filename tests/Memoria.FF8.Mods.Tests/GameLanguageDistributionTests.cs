using System.Collections.Concurrent;
using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Memoria.FF8.Platform;

namespace Memoria.FF8.Mods.Tests;

public sealed class GameLanguageDistributionTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "memoria-languages-" + Guid.NewGuid().ToString("N"));
    private static readonly GameLanguageProfile Classic = new("Classic", "en");
    private static readonly GameLanguageProfile Remastered = new("Remastered", "en");
    private static readonly LanguageNativeVariant[] Variants = [new("Classic", "en"), new("Remastered", "en")];
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public void JunctionUsesExactMarkerAndVersionedBindingsInsteadOfTagOrder()
    {
        const string xml = """
            <Catalog><Mods>
                <Mod><ID>3d7436a1-2cd7-4503-821a-910b0f7aa7e3</ID><Name>ILP</Name>
                <LatestVersion><Version>1.8</Version><Link>https://example.test/one.zip</Link></LatestVersion>
                <Tags><string>language</string><string>italian</string><string>french</string><string>german</string><string>spanish</string><string>english</string></Tags></Mod>
                <Mod><ID>bdd7d71d-8ffd-4aa5-93c1-bf5ae182ad66</ID><Name>Succession</Name>
                <LatestVersion><Version>1.10</Version><Link>https://example.test/two.zip</Link></LatestVersion><Tags><string>story</string></Tags></Mod>
                <Mod><ID>e16434be-c49e-43c9-9fb6-a5c2881f07f6</ID><Name>Ordinary mod</Name>
                <LatestVersion><Version>1.0</Version><Link>https://example.test/three.zip</Link></LatestVersion>
                <Tags><string>languages</string><string>english</string></Tags></Mod>
            </Mods></Catalog>
            """;
        IReadOnlyList<RemoteModEntry> entries = RemoteModCatalogAdapter.Parse(Encoding.UTF8.GetBytes(xml));
        Assert.Equal(2, entries.Count(LanguagePackages.IsLanguage));
        GameLanguageInventory inventory = GameLanguageInventory.Create([Classic], Classic, [], entries, []);
        Assert.Equal(["1", "2", "3", "4", "5"], inventory.Groups.SelectMany(group => group.Offers).Where(offer => offer.Title.StartsWith("ILP", StringComparison.Ordinal)).Select(offer => offer.Option).Order());
        GameLanguageGroup english = inventory.Groups.Single(group => group.DisplayTag == "en");
        Assert.True(english.Offers[0].IsOriginal);
        Assert.Contains(english.Offers, offer => offer.Title.Contains("story rewrite", StringComparison.Ordinal) && offer.Option is null);
        GameLanguageProfile german = new("Classic", "de");
        GameLanguageOffer germanIlp = GameLanguageInventory.Create([german], german, [], entries, []).Groups.Single(group => group.DisplayTag == "de").Offers[1];
        Assert.False(germanIlp.CanSelect);
        Assert.Contains("Classic English", germanIlp.Requirement);
        Assert.Equal("German (Deutsch)", inventory.Groups.Single(group => group.DisplayTag == "de").Label);
        Assert.Equal("fr", inventory.OfferForDisplayCulture(CultureInfo.GetCultureInfo("fr-FR"), "en")?.DisplayTag);
        Assert.Null(inventory.OfferForDisplayCulture(CultureInfo.GetCultureInfo("en-GB"), "en"));
    }

    [Fact]
    public void PreferencesRetainDeclineAndScopeExplicitChoiceToTheInstallation()
    {
        string directory = Path.Combine(root, "installed");
        string path = Path.Combine(root, "launcher.json");
        new LauncherSettings(directory, ResourceLanguage: "fr").Save(path);
        Assert.Equal("fr", LauncherSettings.Load(path).LegacyResourceLanguageFor(directory));
        GameLanguageChoice choice = new("ru", "memoria-official/translation", "ru (Literary edition)", Remastered, "Memoria/translation");
        (LauncherSettings.Load(path).WithLanguageChoice(choice, directory) with { LanguageOfferDeclined = true }).Save(path);
        LauncherSettings restored = LauncherSettings.Load(path);
        Assert.True(restored.LanguageOfferDeclined);
        Assert.Equal(choice with { InstallationDirectory = directory }, restored.LanguageChoiceFor(directory));
        Assert.Null(restored.LanguageChoiceFor(Path.Combine(root, "other")));
        Assert.Null(restored.LegacyResourceLanguageFor(Path.Combine(root, "other")));
        Assert.Equal("ja", GameResourceLanguages.Normalize("jp"));
    }

    [Fact]
    public void InstalledJunctionSelectionUsesTheDeclaredValueAndRestoresOnFailure()
    {
        string mods = Path.Combine(root, "Mods");
        string install = Path.Combine(mods, "Junction", "ILP");
        Directory.CreateDirectory(Path.Combine(install, "Assets", "movies"));
        File.WriteAllText(Path.Combine(install, "Assets", "movies", "intro.cam"), "fixture");
        File.WriteAllText(Path.Combine(install, "mod.xml"), """
            <ModInfo><Name>ILP</Name><ID>3d7436a1-2cd7-4503-821a-910b0f7aa7e3</ID><Version>1.8</Version>
            <GameLanguage>EN</GameLanguage><ModFolder Folder="Assets" />
            <ConfigOption><Type>List</Type><Default>5</Default><ID>Language</ID><Name>Language</Name>
            <Option Value="5" Name="Italian" /><Option Value="3" Name="Spanish" /><Option Value="2" Name="French" />
            <Option Value="1" Name="English retranslation" /><Option Value="4" Name="German" />
            </ConfigOption></ModInfo>
            """);
        using ModRuntime runtime = new(root, watch: false, modsDirectory: mods);
        Assert.True(runtime.PreparePending(force: true));
        Assert.True(runtime.CommitPending());
        ModPackage package = Assert.Single(runtime.Catalog.Packages);
        Assert.Equal(ModEffectiveStatus.Disabled, package.Status);
        Assert.Equal(5, package.JunctionMetadata!.Options["Language"]);
        GameLanguageInventory inventory = GameLanguageInventory.Create([Classic], Classic, runtime.InstalledPackages, [], []);
        GameLanguageOffer french = inventory.Groups.Single(group => group.DisplayTag == "fr").Offers.Single();
        Assert.All(inventory.Groups.SelectMany(group => group.Offers).Where(offer => !offer.IsOriginal), offer => Assert.Same(package, offer.InstalledPackage));
        string ini = Path.Combine(install, "mod.ini");
        using (runtime.StageLanguageSelection(french, package.Id, null))
        {
            Assert.Contains("option.Language = 2", File.ReadAllText(ini));
        }
        Assert.False(File.Exists(ini));
        using LanguageSelectionChange change = runtime.StageLanguageSelection(french, package.Id, null);
        Assert.True(runtime.PreparePending(force: true));
        Assert.True(runtime.CommitPending());
        change.Commit();
        Assert.Equal(2, Assert.Single(runtime.Catalog.Packages).JunctionMetadata!.Options["Language"]);
        Assert.Single(runtime.Catalog.Files);
    }

    [Fact]
    public void OrdinaryPackageWithRelatedTextStaysInMods()
    {
        string descriptorPath = Path.Combine(root, "mod.json");
        Directory.CreateDirectory(root);
        File.WriteAllText(descriptorPath, """{"id":"item-expansion","name":"Item Expansion","package":"Memoria","description":{"en":"Adds items.","ru":"Новые предметы."}}""");
        ModPackage package = new("Memoria/item-expansion", "Item Expansion", ModFormat.Memoria, true, true, ModEffectiveStatus.Active, root)
        {
            Descriptor = NativeModDescriptor.Read(descriptorPath),
            HasLocalization = true
        };
        Assert.False(LanguagePackages.IsLanguage(package));
        Assert.True(LanguagePackages.IsLanguage(package with { LocalizationOnly = true }));
    }

    [Fact]
    public async Task VerifiedLanguageAcquisitionKeepsDependenciesHiddenAndReusesTheirReceipts()
    {
        OfficialModComponent media = Component("opening-media", "1.0.0", dependency: true);
        OfficialModComponent translation = Component("translation", "3.2.0", dependency: false, new LanguageDependencyPin(media.Id, media.Latest!.Version));
        OfficialModComponent update = Component("translation", "3.3.0", dependency: false, new LanguageDependencyPin(media.Id, media.Latest.Version));
        Dictionary<string, byte[]> downloads = [];
        media = BuildArchive(media, downloads);
        translation = BuildArchive(translation, downloads);
        update = BuildArchive(update, downloads);
        await AcquireAndVerify(translation, media, downloads.ToDictionary(pair => pair.Key, pair => (Func<Stream>)(() => new MemoryStream(pair.Value, writable: false))), update);
    }

    [Fact]
    public async Task CancelledOrCorruptedDependencyDoesNotReplaceTheActiveLanguage()
    {
        string modsRoot = Path.Combine(root, "Mods");
        string priorRoot = Path.Combine(modsRoot, "Memoria", "preferred-language");
        Directory.CreateDirectory(priorRoot);
        File.WriteAllText(Path.Combine(priorRoot, "mod.json"), """
            {"id":"preferred-language","name":"Preferred Language","package":"Localization","description":{"en":"Existing translation."},"kind":"language",
            "languageOffers":[{"locale":"it","label":"Preferred","option":"it","nativeVariants":[{"edition":"Remastered","language":"en"}]}]}
            """);
        File.WriteAllText(Path.Combine(priorRoot, "mod.ini"), "enabled = 1");
        File.WriteAllText(Path.Combine(priorRoot, "content.txt"), "already enabled");
        OfficialModComponent media = Component("opening-media", "1.0.0", dependency: true);
        OfficialModComponent translation = Component("translation", "3.2.0", dependency: false, new LanguageDependencyPin(media.Id, media.Latest!.Version));
        Dictionary<string, byte[]> downloads = [];
        media = BuildArchive(media, downloads);
        translation = BuildArchive(translation, downloads);
        string cache = Path.Combine(root, "catalog-cache");
        Directory.CreateDirectory(cache);
        File.WriteAllBytes(Path.Combine(cache, "languages-v1.json"), Catalog(1, [translation], [media]));
        string mediaUrl = media.Latest!.Artifact.Url;
        string translationUrl = translation.Latest!.Artifact.Url;
        int mediaRequests = 0;
        int translationRequests = 0;
        TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using HttpClient client = new(new FixtureServer(async (request, token) =>
        {
            string uri = request.RequestUri!.AbsoluteUri;
            if (uri == mediaUrl)
            {
                if (Interlocked.Increment(ref mediaRequests) == 1)
                {
                    started.TrySetResult();
                    await Task.Delay(Timeout.Infinite, token);
                }
                return new(HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[downloads[mediaUrl].Length]) };
            }
            if (uri == translationUrl)
            {
                Interlocked.Increment(ref translationRequests);
            }
            return new(HttpStatusCode.NotFound);
        }));
        using ModRuntime runtime = new(root, watch: false, modsDirectory: modsRoot, edition: Remastered.Edition, language: Remastered.ResourceLanguage);
        runtime.PreparePending(force: true);
        runtime.CommitPending();
        ModPackage prior = Assert.Single(runtime.Catalog.Packages);
        Assert.Equal(ModEffectiveStatus.Active, prior.Status);
        ModPackageInstaller installer = new(ModPackageInstaller.StateDirectoryFor(modsRoot), modsRoot, "Remastered/en/fixture", junctionSupported: false);
        await using ModLibraryService library = new(Path.Combine(root, "library"), installer, runtime, "Remastered/en/fixture", false, client, officialCacheDirectory: cache, officialSupported: true);
        await WaitForOffer(library, translation.Latest.Version);
        GameLanguageOffer offer = Inventory(library, runtime).Groups.Single(group => group.DisplayTag == "ru").Offers.Single();
        using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(Token);
        Task<string?> attempt = library.AcquireLanguageAsync(offer, installer, cancellation.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10), Token);
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => attempt);
        using CancellationTokenSource retryTimeout = CancellationTokenSource.CreateLinkedTokenSource(Token);
        retryTimeout.CancelAfter(TimeSpan.FromSeconds(15));
        await Assert.ThrowsAsync<IOException>(() => library.AcquireLanguageAsync(offer, installer, retryTimeout.Token));
        Assert.Equal(0, translationRequests);
        Assert.Equal(prior, Assert.Single(runtime.Catalog.Packages));
        Assert.False(Directory.Exists(Path.Combine(modsRoot, "Memoria", translation.Id)));
        Assert.False(Directory.Exists(Path.Combine(modsRoot, "Memoria", media.Id)));
    }

    [Fact]
    public async Task ExternalVerifiedLanguageArtifactsInstallTogether()
    {
        string? fixtureRoot = Environment.GetEnvironmentVariable("MEMORIA_TEST_LANGUAGE_ARTIFACTS");
        if (string.IsNullOrWhiteSpace(fixtureRoot))
        {
            Assert.Skip("Set MEMORIA_TEST_LANGUAGE_ARTIFACTS to a verified root and dependency artifact fixture directory.");
        }
        Dictionary<string, Func<Stream>> archives = [];
        List<OfficialModComponent> components = [];
        foreach (string path in Directory.EnumerateFiles(fixtureRoot!, "*.catalog-entry.json"))
        {
            OfficialModComponent component = JsonSerializer.Deserialize<OfficialModComponent>(File.ReadAllBytes(path), Json)!;
            string archive = path[..^".catalog-entry.json".Length] + ".zip";
            archives.Add(component.Latest!.Artifact.Url, () => File.OpenRead(archive));
            components.Add(component);
        }
        OfficialModComponent translation = components.Single(component => component.LanguageOffers is { Count: > 0 });
        OfficialModComponent media = components.Single(component => component.IsLanguageDependency);
        await AcquireAndVerify(translation, media, archives, update: null);
    }

    private async Task AcquireAndVerify(OfficialModComponent translation, OfficialModComponent media, IReadOnlyDictionary<string, Func<Stream>> archives, OfficialModComponent? update)
    {
        string modsRoot = Path.Combine(root, "Mods");
        string cache = Path.Combine(root, "catalog-cache");
        Directory.CreateDirectory(cache);
        byte[] feed = Catalog(1, [translation], [media]);
        File.WriteAllBytes(Path.Combine(cache, "languages-v1.json"), feed);
        Assert.Throws<InvalidDataException>(() => OfficialModCatalog.Parse(feed));
        Assert.Throws<InvalidDataException>(() => OfficialModCatalog.Parse(Catalog(1, [translation], []), languageIndex: true));
        Assert.Throws<InvalidDataException>(() => OfficialModCatalog.Parse(Catalog(1, [translation], [media with { Dependencies = [new(media.Id, media.Latest!.Version)] }]), languageIndex: true));
        ConcurrentDictionary<string, int> requests = new();
        using HttpClient client = new(new FixtureServer(request =>
        {
            string uri = request.RequestUri!.AbsoluteUri;
            if (archives.TryGetValue(uri, out Func<Stream>? open))
            {
                requests.AddOrUpdate(uri, 1, (_, count) => count + 1);
                return new(HttpStatusCode.OK) { Content = new StreamContent(open()) };
            }
            return new(HttpStatusCode.NotFound);
        }));
        ModPackageInstaller installer = new(ModPackageInstaller.StateDirectoryFor(modsRoot), modsRoot, "Remastered/en/fixture", junctionSupported: false, preferenceRoot: modsRoot);
        using ModRuntime runtime = new(root, watch: false, modsDirectory: modsRoot, edition: Remastered.Edition, language: Remastered.ResourceLanguage);
        await using ModLibraryService library = new(Path.Combine(root, "library"), installer, runtime, "Remastered/en/fixture", false, client, officialCacheDirectory: cache, officialSupported: true);
        RemoteModEntry offerEntry = await WaitForOffer(library, translation.Latest!.Version);
        Assert.Empty(requests);
        Assert.DoesNotContain(library.View.Catalog.Entries, LanguagePackages.IsLanguage);
        GameLanguageOffer offer = Inventory(library, runtime).Groups.Single(group => group.DisplayTag == "ru").Offers.Single();
        Assert.Equal(2, offer.DownloadMembers.Count);
        Assert.Equal(translation.Latest.Artifact.Size + media.Latest!.Artifact.Size, offer.DownloadBytes);
        Assert.Throws<NotSupportedException>(() => library.AcquireLanguageAsync(offer with { Profile = null }, installer, Token).GetAwaiter().GetResult());
        string? packageId = await library.AcquireLanguageAsync(offer, installer, Token);
        Assert.Equal(translation.RuntimePackageId, packageId);
        Assert.Equal(1, requests[offerEntry.Official!.Latest!.Artifact.Url]);
        Assert.Equal(1, requests[media.Latest.Artifact.Url]);
        RemoteModEntry mediaEntry = new(OfficialModCatalog.SourceId, media.CatalogId, media.Name, media.Latest.Version) { Official = media };
        Assert.True(installer.GetStatus(mediaEntry)?.Installed);
        Assert.DoesNotContain(library.View.Jobs.Concat(library.View.LanguageJobs), job => job.Entry.Id == media.CatalogId);

        using (ModRuntime activated = new(root, watch: false, modsDirectory: modsRoot, edition: Remastered.Edition, language: Remastered.ResourceLanguage))
        {
            activated.PreparePending(force: true);
            activated.CommitPending();
            Assert.DoesNotContain(activated.Catalog.Packages, package => package.Status == ModEffectiveStatus.Active);
            using (activated.StageLanguageSelection(offer, packageId, null))
            {
                Assert.True(activated.PreparePending(force: true));
                Assert.True(activated.PendingLanguageMatches(packageId, null));
            }
            Assert.DoesNotContain(activated.Catalog.Packages, package => package.Status == ModEffectiveStatus.Active);
            using (LanguageSelectionChange switchToTranslation = activated.StageLanguageSelection(offer, packageId, null))
            {
                Assert.True(activated.PreparePending(force: true));
                Assert.True(activated.CommitPending());
                switchToTranslation.Commit();
            }
            Assert.Contains(activated.Catalog.Packages, package => package.Id == media.RuntimePackageId && package.Status == ModEffectiveStatus.Active);
            GameLanguageOffer original = Inventory(library, activated).Groups.Single(group => group.DisplayTag == "en").Offers[0];
            using LanguageSelectionChange switchBack = activated.StageLanguageSelection(original, null, offer.ToChoice(packageId));
            Assert.True(activated.PreparePending(force: true));
            Assert.True(activated.CommitPending());
            switchBack.Commit();
            Assert.Contains(activated.Catalog.Packages, package => package.Id == media.RuntimePackageId && package.Status == ModEffectiveStatus.Disabled);
        }
        if (update is null)
        {
            return;
        }
        feed = Catalog(2, [update], [media]);
        File.WriteAllBytes(Path.Combine(cache, "languages-v1.json"), feed);
        library.RefreshCatalog();
        await WaitForOffer(library, update.Latest!.Version);
        using ModRuntime installedRuntime = new(root, watch: false, modsDirectory: modsRoot, edition: Remastered.Edition, language: Remastered.ResourceLanguage);
        installedRuntime.PreparePending(force: true);
        installedRuntime.CommitPending();
        GameLanguageOffer updated = Inventory(library, installedRuntime).Groups.Single(group => group.DisplayTag == "ru").Offers.Single();
        Assert.True(updated.CanUpdate);
        Assert.False(updated.RequiresAcquisition);
        Assert.Equal(update.Latest.Artifact.Size, updated.DownloadBytes);
        Assert.Equal(packageId, await library.AcquireLanguageAsync(updated, installer, Token, update: true));
        Assert.Equal(1, requests[media.Latest.Artifact.Url]);
        Assert.Equal(1, requests[update.Latest.Artifact.Url]);
    }

    private static GameLanguageInventory Inventory(ModLibraryService library, ModRuntime runtime)
    {
        return GameLanguageInventory.Create([Remastered], Remastered, runtime.InstalledPackages, [], library.View.LanguageEntries);
    }

    private static async Task<RemoteModEntry> WaitForOffer(ModLibraryService library, string version)
    {
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(Token);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        while (true)
        {
            if (library.View.LanguageEntries.SingleOrDefault(entry => entry.Version == version) is { } entry)
            {
                return entry;
            }
            await Task.Delay(25, timeout.Token);
        }
    }

    private static OfficialModComponent Component(string id, string version, bool dependency, params LanguageDependencyPin[] pins)
    {
        return new(id, "Memoria", "Memoria/" + id, id, id, "Language package fixture", "Localization", "memoria-native", "download", false, new("FFVIII", ["Classic", "Remastered"], ["en"]),
            new(version, "[0.8.16,)", id + "-v" + version, "", new OfficialModArtifact("https://example.test/" + id + "-" + version + ".zip", 0, 0, "")))
        {
            LanguageOffers = dependency ? null : [new("ru", "Translation", "ru", Variants)],
            NativeVariants = dependency ? Variants : null,
            Dependencies = pins
        };
    }

    private static OfficialModComponent BuildArchive(OfficialModComponent component, Dictionary<string, byte[]> downloads)
    {
        Dictionary<string, object> descriptor = new()
        {
            ["id"] = component.Id, ["name"] = component.Name, ["package"] = "Localization",
            ["description"] = new { en = component.Description }, ["kind"] = component.IsLanguageDependency ? "dependency" : "language"
        };
        if (component.IsLanguageDependency)
        {
            descriptor["nativeVariants"] = component.NativeVariants!;
        }
        else
        {
            descriptor["languageOffers"] = component.LanguageOffers!;
        }
        descriptor["dependencies"] = component.Dependencies!.Select(pin => pin.Id).ToArray();
        byte[] mod = JsonSerializer.SerializeToUtf8Bytes(descriptor, Json);
        byte[] payload = Encoding.UTF8.GetBytes("fixture-" + component.Latest!.Version);
        OfficialModFile[] files = [new("mod.json", mod.Length, Digest(mod), false), new("asset.txt", payload.Length, Digest(payload), false)];
        byte[] manifest = JsonSerializer.SerializeToUtf8Bytes(new
        {
            schemaVersion = 1, sourceId = OfficialModCatalog.SourceId, componentId = component.Id, runtimePackageId = component.RuntimePackageId,
            component.Latest.Version, component.Latest.EngineVersionRange, component.Target, files
        }, Json);
        using MemoryStream stream = new();
        using (ZipArchive zip = new(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            Add(zip, "mod.manifest.json", manifest);
            Add(zip, "payload/mod.json", mod);
            Add(zip, "payload/asset.txt", payload);
        }
        byte[] archive = stream.ToArray();
        downloads[component.Latest.Artifact.Url] = archive;
        return component with { Latest = component.Latest with { Artifact = component.Latest.Artifact with { Size = archive.Length, UnpackedSize = manifest.Length + mod.Length + payload.Length, Sha256 = Digest(archive) } } };
    }

    private static byte[] Catalog(int revision, OfficialModComponent[] components, OfficialModComponent[] dependencies)
    {
        return JsonSerializer.SerializeToUtf8Bytes(new { schemaVersion = 1, sourceId = OfficialModCatalog.SourceId, revision, publishedAt = DateTimeOffset.UnixEpoch, components, dependencies }, Json);
    }

    private static string Digest(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static void Add(ZipArchive zip, string name, byte[] bytes)
    {
        using Stream entry = zip.CreateEntry(name).Open();
        entry.Write(bytes);
    }

    private sealed class FixtureServer(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        public FixtureServer(Func<HttpRequestMessage, HttpResponseMessage> respond) : this((request, _) => Task.FromResult(respond(request)))
        {
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return respond(request, cancellationToken);
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
