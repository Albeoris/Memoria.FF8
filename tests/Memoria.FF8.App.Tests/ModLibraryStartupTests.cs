using System.IO.Compression;
using Memoria.FF8.Mods;

namespace Memoria.FF8.App.Tests;

public sealed class ModLibraryStartupTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "library-startup-" + Guid.NewGuid().ToString("N"));
    private readonly Guid modId = Guid.NewGuid();

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

    private GameResourceHost Host()
    {
        Directory.CreateDirectory(root);
        return new(Path.Combine(root, "game"), root, watch: false);
    }

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
