using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.FlArchives;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Memoria.FF8.Mods;
using Memoria.FF8.Platform;

namespace Memoria.FF8.App.Tests;

public sealed class ModLibraryStartupTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "library-startup-" + Guid.NewGuid().ToString("N"));
    private readonly Guid modId = Guid.NewGuid();
    private string Mods => Path.Combine(root, "Mods");
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task OfficialIdentityUpgradeSettlesAcrossRestartsAndPreservesPreferences(bool legacyArtwork, bool enable)
    {
        CreateItemArchive();
        string destination = legacyArtwork
            ? RecordLegacyArtwork()
            : await InstallPreviousOfficialDescriptor();
        string settings = Path.Combine(Mods, "Memoria", "memoria-settings.json");
        string oldPreference = legacyArtwork ? "Girl Next Door" : "girl-next-door";
        File.WriteAllText(settings, JsonSerializer.Serialize(new { components = new Dictionary<string, bool> { [oldPreference] = false, ["unrelated"] = false } }));
        File.WriteAllText(Path.Combine(destination, "mod.ini"), "keep = player-value\n");

        string lookalike = Path.Combine(Mods, "Memoria", Path.GetFileName(destination).ToUpperInvariant());
        if (!Directory.Exists(lookalike))
        {
            Directory.CreateDirectory(lookalike);
            File.WriteAllText(Path.Combine(lookalike, "local.txt"), "separate directory");
        }

        var (archive, entry) = OfficialPackage("1.0.4", OfficialModCatalog.GirlNextDoor, usable: true);
        Guid installation;
        using (GameResourceHost previous = Host())
        {
            installation = previous.PackageInstaller.GetStatus(entry)!.InstallationId;
            ModPackageInstallPlan upgrade = await previous.PackageInstaller.InspectAsync(archive, entry, Token);
            Assert.Equal(destination, upgrade.Destination);
            Assert.True((await previous.PackageInstaller.RequestInstallAsync(upgrade, cancellationToken: Token, enableOnAcceptance: enable)).RestartRequired);
        }

        for (int restart = 0; restart < 2; restart++)
        {
            using GameResourceHost host = Host();
            Assert.DoesNotContain(host.Mods.Diagnostics, diagnostic => diagnostic.Code is "library-activation" or "native-descriptor" or "reload-failed");
            ModPackageInstallResult status = Assert.IsType<ModPackageInstallResult>(host.PackageInstaller.GetStatus(entry));
            Assert.Equal((installation, "1.0.4", true, false), (status.InstallationId, status.Version, status.Installed, status.RestartRequired));
            ModPackage installed = Assert.Single(host.Mods.InstalledPackages, package => package.Id == entry.Official!.RuntimePackageId);
            Assert.Equal(destination, installed.RootDirectory);
            Assert.Equal(enable, installed.RequestedEnabled);
            Assert.Equal(enable ? ModEffectiveStatus.Active : ModEffectiveStatus.Disabled, installed.Status);
            Assert.Equal(enable ? ItemType.Magazine : (ItemType?)null, host.ItemOverrides.Find(163)?.Type);
        }
        Assert.Equal("keep = player-value\n", File.ReadAllText(Path.Combine(destination, "mod.ini")));
        JsonNode preferences = JsonNode.Parse(File.ReadAllText(settings))!["components"]!;
        Assert.False(preferences[oldPreference]!.GetValue<bool>());
        Assert.False(preferences["unrelated"]!.GetValue<bool>());
        Assert.Equal(enable ? true : (bool?)null, preferences[entry.Official!.SettingsKey]?.GetValue<bool>());

        if (enable)
        {
            using GameResourceHost host = Host();
            File.Delete(Path.Combine(destination, NativeModDescriptor.FileName));
            host.Mods.Refresh();
            Assert.False(host.Pump(force: true));
            Assert.Contains(host.Mods.Diagnostics, diagnostic => diagnostic.Code == "native-descriptor" && diagnostic.PackageId == entry.Official.RuntimePackageId);
            Assert.False(host.PackageInstaller.GetStatus(entry)!.Installed);
        }
    }

    [Fact]
    public async Task RejectedNativeUpgradeRediscoversTheRestoredDescriptorlessLegacyPayload()
    {
        string destination = RecordLegacyArtwork();
        var (archive, entry) = OfficialPackage("1.0.4", OfficialModCatalog.GirlNextDoor, usable: false);
        using (GameResourceHost previous = Host())
        {
            ModPackageInstallPlan upgrade = await previous.PackageInstaller.InspectAsync(archive, entry, Token);
            Assert.True((await previous.PackageInstaller.RequestInstallAsync(upgrade, cancellationToken: Token, enableOnAcceptance: true)).RestartRequired);
        }

        using (GameResourceHost rejected = Host())
        {
            Assert.Contains(rejected.Mods.Diagnostics, diagnostic => diagnostic.Code == "library-activation" && diagnostic.Message.Contains("previous installation was restored"));
            Assert.DoesNotContain(rejected.Mods.Diagnostics, diagnostic => diagnostic.Code is "reload-failed" or "native-descriptor");
            ModPackage legacy = Assert.Single(rejected.Mods.Catalog.Packages);
            Assert.Equal(OfficialModCatalog.LegacyGirlNextDoorRuntimePackageId, legacy.Id);
            Assert.Null(legacy.Descriptor);
        }
        using GameResourceHost restarted = Host();
        ModPackageInstallResult prior = Assert.IsType<ModPackageInstallResult>(restarted.PackageInstaller.GetStatus(entry));
        Assert.Equal(("1.0.1", true, false), (prior.Version, prior.Installed, prior.RestartRequired));
        Assert.Equal("legacy artwork", File.ReadAllText(Path.Combine(destination, "GirlNextDoor.png")));
        Assert.False(File.Exists(Path.Combine(destination, NativeModDescriptor.FileName)));
        Assert.False(File.Exists(Path.Combine(Mods, "Memoria", "memoria-settings.json")));
    }

    [Fact]
    public async Task DisabledInstallIsAppliedBeforeDiscoveryAndRemainsDisabled()
    {
        RemoteModEntry entry = new("synthetic", modId, "Example", "1");
        ModPackageInstallPlan plan;
        using (GameResourceHost first = Host())
        {
            plan = await first.PackageInstaller.InspectAsync(Package("1", false), entry, TestContext.Current.CancellationToken);
            await first.PackageInstaller.RequestInstallAsync(plan, false, TestContext.Current.CancellationToken);
            Assert.Empty(first.Mods.InstalledPackages);
        }
        using GameResourceHost next = Host();
        ModPackage installed = Assert.Single(next.Mods.InstalledPackages);
        Assert.Equal(plan.PackageId, installed.Id);
        Assert.False(installed.RequestedEnabled);
        Assert.Equal(ModEffectiveStatus.Disabled, installed.Status);
        Assert.True(next.PackageInstaller.GetStatus(entry)!.Installed);
    }

    [Fact]
    public async Task UnusableUpdateRestoresPreviousPayloadBeforeConsumersReadIt()
    {
        RemoteModEntry entry = new("synthetic", modId, "Example", "1");
        using (GameResourceHost initial = Host())
        {
            ModPackageInstallPlan plan = await initial.PackageInstaller.InspectAsync(Package("1", true), entry, TestContext.Current.CancellationToken);
            await initial.PackageInstaller.RequestInstallAsync(plan, true, TestContext.Current.CancellationToken);
        }
        string? destination;
        using (GameResourceHost old = Host())
        {
            Assert.Equal(ModEffectiveStatus.Active, Assert.Single(old.Mods.Catalog.Packages).Status);
            ModPackageInstallPlan plan = await old.PackageInstaller.InspectAsync(Package("2", false), entry with { Version = "2" }, TestContext.Current.CancellationToken);
            destination = plan.Destination;
            await old.PackageInstaller.RequestInstallAsync(plan, true, TestContext.Current.CancellationToken);
            Assert.True(File.Exists(Path.Combine(destination, "payload/movies/example.avi")));
        }
        using GameResourceHost restored = Host();
        Assert.Equal("1", restored.PackageInstaller.GetStatus(entry)!.Version);
        Assert.True(File.Exists(Path.Combine(destination, "payload/movies/example.avi")));
        Assert.Equal(ModEffectiveStatus.Active, Assert.Single(restored.Mods.Catalog.Packages).Status);
        Assert.Equal("retained media", File.ReadAllText(restored.ResolveGameFile("movies/example.avi")));
    }

    [Fact]
    public void AnotherLiveHostCannotApplyPendingChangesToTheSameRoot()
    {
        using GameResourceHost owner = Host();
        using GameResourceHost second = Host();
        Assert.Contains(second.Mods.Diagnostics, diagnostic => diagnostic.Code == "library-recovery");
        Assert.False(second.ModLibrary.View.TargetSupported);
        Assert.Empty(second.Mods.Catalog.Packages);
    }

    [Fact]
    public async Task ThirdHostCannotReplaceArchivesRetainedBySecondHostAfterFirstExits()
    {
        RemoteModEntry entry = new("synthetic", modId, "Example", "1");
        using (GameResourceHost setup = Host())
        {
            ModPackageInstallPlan plan = await setup.PackageInstaller.InspectAsync(Package("1", true), entry, TestContext.Current.CancellationToken);
            await setup.PackageInstaller.RequestInstallAsync(plan, true, TestContext.Current.CancellationToken);
        }
        GameResourceHost? first = Host();
        try
        {
            using (GameResourceHost second = Host())
            {
                ModPackageInstallPlan update = await first.PackageInstaller.InspectAsync(Package("2", true), entry with { Version = "2" }, TestContext.Current.CancellationToken);
                await first.PackageInstaller.RequestInstallAsync(update, true, TestContext.Current.CancellationToken);
                first.Dispose();
                first = null;
                using GameResourceHost third = Host();
                Assert.Equal("1", Assert.Single(second.Mods.InstalledPackages).JunctionMetadata?.Version);
                Assert.Equal("1", Assert.Single(third.Mods.InstalledPackages).JunctionMetadata?.Version);
                Assert.True(third.PackageInstaller.GetStatus(entry)!.RestartRequired);
                Assert.Equal("retained media", File.ReadAllText(second.ResolveGameFile("movies/example.avi")));
            }
            using GameResourceHost next = Host();
            Assert.Equal("2", Assert.Single(next.Mods.InstalledPackages).JunctionMetadata?.Version);
            Assert.True(next.PackageInstaller.GetStatus(entry with { Version = "2" })!.Installed);
        }
        finally
        {
            first?.Dispose();
        }
    }

    [Fact]
    public async Task UnleasedManagedPackagesAreExcludedWhileManualPackagesRemainVisible()
    {
        RemoteModEntry entry = new("synthetic", modId, "Example", "1");
        using (GameResourceHost setup = Host())
        {
            ModPackageInstallPlan plan = await setup.PackageInstaller.InspectAsync(Package("1", true), entry, TestContext.Current.CancellationToken);
            await setup.PackageInstaller.RequestInstallAsync(plan, true, TestContext.Current.CancellationToken);
        }
        using (GameResourceHost installed = Host())
        {
            Assert.Single(installed.Mods.InstalledPackages);
        }
        string manual = Path.Combine(root, "Mods", "Junction", "manual");
        Directory.CreateDirectory(manual);
        await File.WriteAllTextAsync(Path.Combine(manual, "mod.xml"), "<ModInfo><Name>Manual</Name></ModInfo>", TestContext.Current.CancellationToken);
        string runtime = Path.Combine(ModDirectoryResourceLease.StateDirectory(Path.Combine(root, "Mods")), "runtime.lock");
        using FileStream blocked = new(runtime, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        using GameResourceHost reader = Host();
        Assert.Contains(reader.Mods.InstalledPackages, package => package.Id == "Junction/manual");
        Assert.DoesNotContain(reader.Mods.InstalledPackages, package => package.Id.StartsWith("Junction/managed-", StringComparison.Ordinal));
        Assert.Contains(reader.Mods.Diagnostics, diagnostic => diagnostic.Code == "package-resource-lease");
    }

    private GameResourceHost Host()
    {
        Directory.CreateDirectory(root);
        return new(Path.Combine(root, "game"), root, watch: false);
    }

    private void CreateItemArchive()
    {
        string directory = Path.Combine(root, "game", "Data", "lang-en");
        Directory.CreateDirectory(directory);
        using IFlArchive archive = FlArchive.Create(Path.Combine(directory, "menu.fs"), FlArchiveRepresentation.Files);
        byte[] items = new byte[164 * 4];
        using Stream stream = archive.AddEntry("mitem.bin").OpenForWrite((uint)items.Length);
        stream.Write(items);
    }

    private string RecordLegacyArtwork()
    {
        string destination = Path.GetFullPath(OfficialModCatalog.LegacyGirlNextDoorRuntimePackageId, Mods);
        Directory.CreateDirectory(destination);
        File.WriteAllText(Path.Combine(destination, "GirlNextDoor.png"), "legacy artwork");
        Assert.True(ModPackageInstaller.RecordMigratedGirlNextDoor(Mods));

        // Give the synthetic artwork a versioned legacy receipt without bundling the real artwork.
        string receipt = Assert.Single(Directory.GetFiles(Path.Combine(ModPackageInstaller.StateDirectoryFor(Mods), "receipts"), "*.json"));
        JsonNode state = JsonNode.Parse(File.ReadAllText(receipt))!;
        state["Version"] = "1.0.1";
        state["EngineVersionRange"] = "[0.8.10,)";
        File.WriteAllText(receipt, state.ToJsonString());
        return destination;
    }

    private async Task<string> InstallPreviousOfficialDescriptor()
    {
        OfficialModComponent previous = OfficialModCatalog.GirlNextDoor with { Id = "girl-next-door", RuntimePackageId = "Memoria/girl-next-door", SettingsKey = "girl-next-door" };
        var (archive, entry) = OfficialPackage("1.0.3", previous, usable: true);
        string destination;
        using (GameResourceHost first = Host())
        {
            ModPackageInstallPlan plan = await first.PackageInstaller.InspectAsync(archive, entry, Token);
            await first.PackageInstaller.RequestInstallAsync(plan, cancellationToken: Token);
            destination = plan.Destination;
        }
        using GameResourceHost installed = Host();
        Assert.True(installed.PackageInstaller.GetStatus(entry) is { Installed: true }, string.Join(Environment.NewLine, installed.Mods.Diagnostics));
        return destination;
    }

    private (string Path, RemoteModEntry Entry) OfficialPackage(string version, OfficialModComponent component, bool usable)
    {
        byte[] descriptor = JsonSerializer.SerializeToUtf8Bytes(new
        {
            id = component.Id,
            name = component.Name,
            package = component.GroupId,
            description = new { en = component.Description }
        });
        Dictionary<string, byte[]> payload = new() { [NativeModDescriptor.FileName] = descriptor };
        if (usable)
        {
            payload.Add("items/magazine.json", """{"id":163,"type":"Magazine"}"""u8.ToArray());
        }
        OfficialModFile[] files = payload.Select(file => new OfficialModFile(file.Key, file.Value.Length, Digest(file.Value), false)).ToArray();
        string engineVersionRange = $"[{EngineBuildMetadata.CompatibilityVersion},)";
        byte[] manifest = JsonSerializer.SerializeToUtf8Bytes(new
        {
            schemaVersion = 1,
            sourceId = OfficialModCatalog.SourceId,
            componentId = component.Id,
            runtimePackageId = component.RuntimePackageId,
            version,
            engineVersionRange,
            target = component.Target,
            files
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        string path = Path.Combine(root, version + ".zip");
        using (ZipArchive archive = ZipFile.Open(path, ZipArchiveMode.Create))
        {
            Write(archive, "mod.manifest.json", manifest);
            foreach ((string name, byte[] bytes) in payload)
            {
                Write(archive, "payload/" + name, bytes);
            }
        }
        OfficialModArtifact artifact = new("https://example.invalid/mod.zip", new FileInfo(path).Length, manifest.Length + files.Sum(file => file.Size), Digest(File.ReadAllBytes(path)));
        component = component with { Latest = new(version, engineVersionRange, component.Id + "-v" + version, "Fixture", artifact) };
        return (path, new(OfficialModCatalog.SourceId, component.CatalogId, component.Name, version) { Official = component });
    }

    private static void Write(ZipArchive archive, string name, byte[] bytes)
    {
        using Stream stream = archive.CreateEntry(name).Open();
        stream.Write(bytes);
    }

    private static string Digest(byte[] content) => Convert.ToHexStringLower(SHA256.HashData(content));

    private string Package(string version, bool media)
    {
        string path = Path.Combine(root, Guid.NewGuid().ToString("N") + ".zip");
        using ZipArchive archive = ZipFile.Open(path, ZipArchiveMode.Create);
        using (StreamWriter manifest = new(archive.CreateEntry("mod.xml").Open()))
        {
            manifest.Write($"<ModInfo><ID>{modId}</ID><Name>Example</Name><Version>{version}</Version>{(media ? "<ModFolder Folder=\"payload\"/>" : "")}</ModInfo>");
        }
        if (media)
        {
            using StreamWriter writer = new(archive.CreateEntry("payload/movies/example.avi").Open());
            writer.Write("retained media");
        }
        return path;
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(root, true);
        }
        catch (IOException)
        {
            // Application-lifetime cancellation releases queue handles asynchronously.
        }
    }
}
