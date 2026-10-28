using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;

namespace Memoria.FF8.Mods.Tests;

public sealed class ModPackageInstallerTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "mod-install-tests-" + Guid.NewGuid().ToString("N"));
    private readonly Guid id = Guid.NewGuid();
    private string State => Path.Combine(root, "state");
    private string Mods => Path.Combine(root, "Mods");
    private RemoteModEntry Entry(string version = "1.00") => new("synthetic", id, "Fixture", version);
    private ModPackageInstaller Installer(ModPackageInstallLimits? limits = null) => new(State, Mods, "Classic/EN", limits: limits);

    [Fact]
    public void CorruptJournalIsReportedWithoutDiscardingRecoveryData()
    {
        string journals = Path.Combine(State, "journals");
        Directory.CreateDirectory(journals);
        string path = Path.Combine(journals, "invalid.json");
        File.WriteAllText(path, "{\"Schema\":1,\"Next\":null}");
        ModPackageInstallResult result = Assert.Single(Installer().RecoverAndApply());
        Assert.False(result.Installed);
        Assert.Contains("Recovery needed", result.Error!);
        Assert.True(File.Exists(path));
        Assert.False(Directory.Exists(Mods));
    }

    [Fact]
    public async Task InstallDefersUntilStartupAndKeepsConditionalFolders()
    {
        ModPackageInstaller installer = Installer();
        string xml = Manifest("1.0", "<ConfigOption><ID>alternate</ID><Name>Alternate</Name><Type>Bool</Type><Default>0</Default></ConfigOption><ModFolder Folder=\"first\" /><ModFolder Folder=\"second\" ActiveWhen=\"alternate = 1\" />");
        string zip = Zip(("wrapper/mod.xml", xml), ("wrapper/first/data/a.bin", "old"), ("wrapper/second/data/a.bin", "alternate"));
        ModPackageInstallPlan plan = await installer.InspectAsync(zip, Entry(), cancellationToken: TestContext.Current.CancellationToken);
        ModPackageInstallResult pending = await installer.RequestInstallAsync(plan, false, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(pending.RestartRequired);
        Assert.False(Directory.Exists(plan.Destination));
        Assert.True(Assert.Single(Installer().RecoverAndApply()).Installed);
        Assert.Equal("alternate", File.ReadAllText(Path.Combine(plan.Destination, "second/data/a.bin")));
        Assert.Contains("enabled=0", File.ReadAllText(Path.Combine(plan.Destination, "mod.ini")));
        Assert.True(Assert.Single(Installer().CompleteStartupApplications([plan.PackageId])).Installed);
        Assert.True(Installer().GetStatus(Entry())!.Installed);
        Assert.Empty(Installer().RecoverAndApply());
    }

    [Theory]
    [InlineData("../escape")]
    [InlineData("/escape")]
    [InlineData("C:/escape")]
    [InlineData("CON.txt")]
    [InlineData("file:stream")]
    [InlineData("dir./a")]
    public async Task RejectsUnsafeWrapperPaths(string unsafePath)
    {
        string zip = Zip(("mod.xml", Manifest()), (unsafePath, "bad"));
        await Assert.ThrowsAsync<InvalidDataException>(() => Installer().InspectAsync(zip, Entry(), cancellationToken: TestContext.Current.CancellationToken));
        Assert.False(Directory.Exists(Mods));
    }

    [Theory]
    [InlineData("data/A.bin", "data/a.bin")]
    [InlineData("DATA/a.bin", "data/b.bin")]
    [InlineData("data/a.bin", "data/a.bin")]
    public async Task RejectsCaseAndDuplicateCollisions(string first, string second)
    {
        await Assert.ThrowsAsync<InvalidDataException>(() => Installer().InspectAsync(Zip(("mod.xml", Manifest()), (first, "a"), (second, "b")), Entry(), cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RejectsWrongPinnedIdentityAndVersion()
    {
        await Assert.ThrowsAsync<InvalidDataException>(() => Installer().InspectAsync(Zip(("mod.xml", Manifest())), Entry() with { Id = Guid.NewGuid() }, cancellationToken: TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<InvalidDataException>(() => Installer().InspectAsync(Zip(("mod.xml", Manifest("2"))), Entry(), cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RejectsAmbiguousComponentsAndExpansion()
    {
        await Assert.ThrowsAsync<NotSupportedException>(() => Installer().InspectAsync(Zip(("one/mod.xml", Manifest()), ("two/mod.xml", Manifest())), Entry(), cancellationToken: TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<InvalidDataException>(() => Installer(new(ExpandedBytes: 64)).InspectAsync(Zip(("mod.xml", Manifest()), ("data/a.bin", new string('a', 256))), Entry(), cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ManualIdentityDoesNotAuthorizeOverwrite()
    {
        string manual = Path.Combine(Mods, "Junction", "manual");
        Directory.CreateDirectory(manual);
        File.WriteAllText(Path.Combine(manual, "mod.xml"), Manifest());
        File.WriteAllText(Path.Combine(manual, "a.bin"), "manual");
        ModPackageInstaller installer = Installer();
        ModPackageInstallPlan plan = await installer.InspectAsync(Zip(("mod.xml", Manifest()), ("a.bin", "new")), Entry(), cancellationToken: TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<InvalidDataException>(() => installer.RequestInstallAsync(plan, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal("manual", File.ReadAllText(Path.Combine(manual, "a.bin")));
    }

    [Fact]
    public async Task UpdatePreservesSettingsAndRejectedUpdateRestoresOldPayloadAndReceipt()
    {
        ModPackageInstaller installer = Installer();
        ModPackageInstallPlan first = await installer.InspectAsync(Zip(("mod.xml", Manifest()), ("a.bin", "old")), Entry(), cancellationToken: TestContext.Current.CancellationToken);
        await installer.RequestInstallAsync(first, cancellationToken: TestContext.Current.CancellationToken);
        installer.RecoverAndApply();
        installer.CompleteStartupApplications([first.PackageId]);
        File.WriteAllText(Path.Combine(first.Destination, "mod.ini"), "enabled=0\npriority=19\n");
        ModPackageInstallPlan next = await installer.InspectAsync(Zip(("mod.xml", Manifest("2")), ("a.bin", "new")), Entry("2"), cancellationToken: TestContext.Current.CancellationToken);
        await installer.RequestInstallAsync(next, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal("old", File.ReadAllText(Path.Combine(first.Destination, "a.bin")));
        Installer().RecoverAndApply();
        Assert.Equal("new", File.ReadAllText(Path.Combine(first.Destination, "a.bin")));
        Assert.Contains("priority=19", File.ReadAllText(Path.Combine(first.Destination, "mod.ini")));
        Assert.False(Assert.Single(Installer().CompleteStartupApplications([])).Installed);
        Assert.Equal("old", File.ReadAllText(Path.Combine(first.Destination, "a.bin")));
        Assert.Equal("1.0", Installer().GetStatus(Entry())!.Version);
        Assert.Empty(Installer().RecoverAndApply());
    }

    [Fact]
    public async Task StartupStopsWhenManagedPayloadOrSettingsChanged()
    {
        ModPackageInstaller installer = Installer();
        ModPackageInstallPlan first = await installer.InspectAsync(Zip(("mod.xml", Manifest()), ("a.bin", "old")), Entry(), cancellationToken: TestContext.Current.CancellationToken);
        await installer.RequestInstallAsync(first, cancellationToken: TestContext.Current.CancellationToken);
        installer.RecoverAndApply();
        installer.CompleteStartupApplications([first.PackageId]);
        ModPackageInstallPlan next = await installer.InspectAsync(Zip(("mod.xml", Manifest("2")), ("a.bin", "new")), Entry("2"), cancellationToken: TestContext.Current.CancellationToken);
        await installer.RequestInstallAsync(next, cancellationToken: TestContext.Current.CancellationToken);
        File.WriteAllText(Path.Combine(first.Destination, "mod.ini"), "enabled=0\n");
        ModPackageInstallResult result = Assert.Single(Installer().RecoverAndApply());
        Assert.False(result.Installed);
        Assert.Contains("settings changed", result.Error!);
        Assert.Equal("old", File.ReadAllText(Path.Combine(first.Destination, "a.bin")));
    }

    [Theory]
    [InlineData("old-move")]
    [InlineData("new-move")]
    [InlineData("receipt")]
    public async Task StartupRecoveryIsIdempotentAcrossRenameAndReceiptBoundaries(string point)
    {
        ModPackageInstaller installer = Installer();
        ModPackageInstallPlan first = await installer.InspectAsync(Zip(("mod.xml", Manifest()), ("a.bin", "old")), Entry(), cancellationToken: TestContext.Current.CancellationToken);
        await installer.RequestInstallAsync(first, cancellationToken: TestContext.Current.CancellationToken);
        installer.RecoverAndApply();
        installer.CompleteStartupApplications([first.PackageId]);
        ModPackageInstallPlan next = await installer.InspectAsync(Zip(("mod.xml", Manifest("2")), ("a.bin", "new")), Entry("2"), cancellationToken: TestContext.Current.CancellationToken);
        await installer.RequestInstallAsync(next, cancellationToken: TestContext.Current.CancellationToken);
        string journalPath = Directory.GetFiles(Path.Combine(State, "journals")).Single();
        JsonObject journal = JsonNode.Parse(File.ReadAllText(journalPath))!.AsObject();
        string transaction = Path.Combine(State, "transactions", Guid.Parse(journal["TransactionId"]!.GetValue<string>()).ToString("N"));
        Directory.Move(first.Destination, Path.Combine(transaction, "previous"));
        if (point != "old-move")
        {
            Directory.Move(Path.Combine(transaction, "content"), first.Destination);
            File.WriteAllText(Path.Combine(first.Destination, "mod.ini"), journal["Settings"]!.GetValue<string>());
        }
        if (point == "receipt")
        {
            File.WriteAllText(Directory.GetFiles(Path.Combine(State, "receipts")).Single(), journal["Next"]!.ToJsonString());
        }
        Assert.True(Assert.Single(Installer().RecoverAndApply()).Installed);
        Assert.True(Assert.Single(Installer().RecoverAndApply()).Installed);
        Installer().CompleteStartupApplications([next.PackageId]);
        Assert.Empty(Installer().RecoverAndApply());
        Assert.Equal("2", Installer().GetStatus(Entry())!.Version);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RetainsDirectAndWrappedIrojWithoutFlattening(bool wrapped)
    {
        string path = Iroj();
        if (wrapped)
        {
            string outer = Zip(("readme.txt", "fixture"));
            using (ZipArchive zip = ZipFile.Open(outer, ZipArchiveMode.Update))
            {
                zip.CreateEntryFromFile(path, "nested/payload.iroj");
            }
            path = outer;
        }
        ModPackageInstaller installer = Installer();
        ModPackageInstallPlan plan = await installer.InspectAsync(path, Entry(), cancellationToken: TestContext.Current.CancellationToken);
        await installer.RequestInstallAsync(plan, false, cancellationToken: TestContext.Current.CancellationToken);
        Assert.False(File.Exists(plan.Destination));
        Assert.True(Assert.Single(Installer().RecoverAndApply()).Installed);
        Assert.True(File.Exists(plan.Destination));
        Assert.Equal(2, new JunctionArchive(plan.Destination).Entries.Count);
        Assert.Contains("enabled=0", File.ReadAllText(plan.Destination + ".ini"));
        Installer().CompleteStartupApplications([plan.PackageId]);
    }

    [Fact]
    public async Task RejectsLinkEntriesAndUnknownCodeEvenWhenUnselected()
    {
        string path = Zip(("mod.xml", Manifest()), ("link", "target"));
        using (ZipArchive zip = ZipFile.Open(path, ZipArchiveMode.Update))
        {
            zip.GetEntry("link")!.ExternalAttributes = unchecked((int)0xa1ff0000);
        }
        await Assert.ThrowsAsync<InvalidDataException>(() => Installer().InspectAsync(path, Entry(), cancellationToken: TestContext.Current.CancellationToken));
        string manifest = Manifest(extra: "<ModFolder Folder=\"off\" ActiveWhen=\"0\" />");
        await Assert.ThrowsAsync<NotSupportedException>(() => Installer().InspectAsync(Zip(("mod.xml", manifest), ("off/a.hext", "unknown")), Entry(), cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ChangedManagedPayloadCannotBeUpdated()
    {
        ModPackageInstaller installer = Installer();
        ModPackageInstallPlan first = await installer.InspectAsync(Zip(("mod.xml", Manifest()), ("a.bin", "old")), Entry(), cancellationToken: TestContext.Current.CancellationToken);
        await installer.RequestInstallAsync(first, cancellationToken: TestContext.Current.CancellationToken);
        installer.RecoverAndApply();
        installer.CompleteStartupApplications([first.PackageId]);
        File.WriteAllText(Path.Combine(first.Destination, "a.bin"), "local changes");
        ModPackageInstallPlan next = await installer.InspectAsync(Zip(("mod.xml", Manifest("2")), ("a.bin", "new")), Entry("2"), cancellationToken: TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<InvalidDataException>(() => installer.RequestInstallAsync(next, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal("local changes", File.ReadAllText(Path.Combine(first.Destination, "a.bin")));
    }

    [Fact]
    public void NoManagedStateDoesNotCreateOperationalDirectories()
    {
        Assert.Empty(Installer().RecoverAndApply());
        Assert.Empty(Installer().CompleteStartupApplications([]));
        Assert.False(Directory.Exists(State));
    }

    [Fact]
    public async Task RemovedManagedInstallationIsReportedWithoutReinstallation()
    {
        ModPackageInstaller installer = Installer();
        ModPackageInstallPlan plan = await installer.InspectAsync(Zip(("mod.xml", Manifest()), ("a.bin", "old")), Entry(), cancellationToken: TestContext.Current.CancellationToken);
        await installer.RequestInstallAsync(plan, cancellationToken: TestContext.Current.CancellationToken);
        installer.RecoverAndApply();
        installer.CompleteStartupApplications([plan.PackageId]);
        Directory.Delete(plan.Destination, true);
        Assert.False(installer.GetStatus(Entry())!.Installed);
        Assert.Contains("removed", installer.GetStatus(Entry())!.Error!);
    }

    [Fact]
    public async Task RejectsUnexpectedWrapperPayloadAndBadCrc()
    {
        await Assert.ThrowsAsync<NotSupportedException>(() => Installer().InspectAsync(Zip(("wrapper/mod.xml", Manifest()), ("wrapper/a.bin", "a"), ("helper.exe", "code")), Entry(), cancellationToken: TestContext.Current.CancellationToken));
        string path = Zip(("mod.xml", Manifest()), ("a.bin", "payload"));
        byte[] bytes = File.ReadAllBytes(path);
        for (int index = 0; index + 46 < bytes.Length; index++)
        {
            if (bytes.AsSpan(index, 4).SequenceEqual(new byte[] { 0x50, 0x4b, 0x01, 0x02 }))
            {
                bytes[index + 16] ^= 1;
            }
        }
        for (int index = 0; index + 30 < bytes.Length; index++)
        {
            if (bytes.AsSpan(index, 4).SequenceEqual(new byte[] { 0x50, 0x4b, 0x03, 0x04 }))
            {
                bytes[index + 14] ^= 1;
            }
        }
        File.WriteAllBytes(path, bytes);
        await Assert.ThrowsAsync<InvalidDataException>(() => Installer().InspectAsync(path, Entry(), cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DiscardDeletesOnlyItsUncommittedInspection()
    {
        ModPackageInstaller installer = Installer();
        ModPackageInstallPlan first = await installer.InspectAsync(Zip(("mod.xml", Manifest()), ("a.bin", "first")), Entry(), cancellationToken: TestContext.Current.CancellationToken);
        ModPackageInstallPlan second = await installer.InspectAsync(Zip(("mod.xml", Manifest()), ("a.bin", "second")), Entry(), cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(2, Directory.GetDirectories(Path.Combine(State, "prepared")).Length);
        await installer.DiscardAsync(first, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Single(Directory.GetDirectories(Path.Combine(State, "prepared")));
        await installer.RequestInstallAsync(second, cancellationToken: TestContext.Current.CancellationToken);
        await installer.DiscardAsync(second, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Single(Directory.GetFiles(Path.Combine(State, "journals")));
        Assert.True(Assert.Single(Installer().RecoverAndApply()).Installed);
    }

    private string Iroj()
    {
        Directory.CreateDirectory(root);
        string path = Path.Combine(root, Guid.NewGuid().ToString("N") + ".download");
        (string Name, byte[] Content)[] files = [("mod.xml", Encoding.UTF8.GetBytes(Manifest())), ("data/a.bin", "fixture"u8.ToArray())];
        using FileStream stream = File.Create(path);
        using BinaryWriter writer = new(stream, Encoding.Unicode);
        writer.Write(0x534f5249);
        writer.Write(0x10002);
        writer.Write(0);
        writer.Write(16);
        writer.Write(files.Length);
        long offset = 20 + files.Sum(file => 20 + Encoding.Unicode.GetByteCount(file.Name));
        foreach ((string name, byte[] content) in files)
        {
            byte[] encoded = Encoding.Unicode.GetBytes(name);
            writer.Write((ushort)(20 + encoded.Length));
            writer.Write((ushort)encoded.Length);
            writer.Write(encoded);
            writer.Write(0);
            writer.Write(offset);
            writer.Write(content.Length);
            offset += content.Length;
        }
        foreach ((string _, byte[] content) in files)
        {
            writer.Write(content);
        }
        return path;
    }

    private string Manifest(string version = "1.0", string extra = "") => $"<ModInfo><ID>{id}</ID><Name>Fixture</Name><Version>{version}</Version>{extra}</ModInfo>";

    private string Zip(params (string Path, string Content)[] files)
    {
        Directory.CreateDirectory(root);
        string path = Path.Combine(root, Guid.NewGuid().ToString("N") + ".download");
        using ZipArchive zip = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach ((string name, string content) in files)
        {
            using StreamWriter writer = new(zip.CreateEntry(name).Open(), Encoding.UTF8);
            writer.Write(content);
        }
        return path;
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }
    }
}
