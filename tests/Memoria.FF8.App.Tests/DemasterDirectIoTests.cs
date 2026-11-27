using System.Text;
using Memoria.FF8.Assets;
using Memoria.FF8.Assets.Remastered;
using Memoria.FF8.Mods;
using Memoria.FF8.Resources;

namespace Memoria.FF8.App.Tests;

public sealed class DemasterDirectIoTests : IDisposable
{
    private const string ScanPath = "data/lang-en/desc_scan_en.dat";
    private static readonly ResourceKey Scan = new("text/enemy-scan");
    private readonly string root = Path.Combine(Path.GetTempPath(), "Memoria-DemasterTest-" + Guid.NewGuid().ToString("N"));

    public DemasterDirectIoTests()
    {
        Directory.CreateDirectory(root);
        WriteZzz(Path.Combine(root, "main.zzz"), new Dictionary<string, byte[]>
        {
            [ScanPath] = [1, 2, 3],
            ["data/lang-fr/desc_scan_fr.dat"] = [7],
            ["data/lang-en/sample.fl"] = Listing("a.dat", "b.dat"),
            ["data/lang-en/sample.fi"] = Index(2),
            ["data/lang-en/sample.fs"] = [30, 31],
            ["data/sample.fl"] = Listing("a.dat"),
            ["data/sample.fi"] = Index(1),
            ["data/sample.fs"] = [80],
            ["data/music/dmusic/ogg/068s-missile.ogg"] = [50],
        });
        WriteZzz(Path.Combine(root, "other.zzz"), new Dictionary<string, byte[]>());
    }

    [Theory]
    [InlineData("DEMASTER_EXP", ModRuntime.InstalledDemasterPackageId)]
    [InlineData("Mods/Demaster/Scan", "Demaster/Scan")]
    [InlineData("Mods/Demastered/Scan", "Demaster/Scan")]
    public void MountsEachLocationAndAppliesDisableOnRestart(string directory, string id)
    {
        Write(directory + "/" + ScanPath, [4, 5, 6]);
        Write(directory + "/shaders/unknown.frag", [100]);
        using (GameResourceHost host = Open())
        {
            Assert.Equal([4, 5, 6], host.Resources.Read(Scan));
            Assert.Equal(id + "/" + ScanPath, host.Resources.Get(Scan).Provenance);
            Assert.Equal(ModEffectiveStatus.Active, Assert.Single(host.Mods.Catalog.Packages, package => package.Id == id).Status);
            Assert.True(host.Mods.SetEnabled(id, false).Success);
            host.Pump(force: true);
            Assert.Equal([4, 5, 6], host.Resources.Read(Scan));
            Assert.Contains(host.Mods.Diagnostics, diagnostic => diagnostic.Code == "Demaster.ExternalFileRestartRequired" && diagnostic.PackageId == id);
        }
        using GameResourceHost restarted = Open();
        Assert.Equal([1, 2, 3], restarted.Resources.Read(Scan));
    }

    [Fact]
    public void PreservesPriorityAndReportsBothAliasConflictPaths()
    {
        Write("DEMASTER_EXP/" + ScanPath, [9]);
        Write("Mods/Demaster/Twin/" + ScanPath, [10]);
        Write("Mods/Demastered/Twin/" + ScanPath, [11]);
        Write("Mods/Demastered/Higher/" + ScanPath, [12]);
        Write("Mods/Demastered/Higher/mod.ini", Encoding.UTF8.GetBytes("priority=3\n"));
        using (GameResourceHost host = Open())
        {
            Assert.Equal([12], host.Resources.Read(Scan));
            ModDiagnostic duplicate = Assert.Single(host.Mods.Diagnostics, diagnostic => diagnostic.Code == "duplicate-package");
            Assert.Contains(Path.Combine("Demaster", "Twin"), duplicate.Message);
            Assert.Contains(Path.Combine("Demastered", "Twin"), duplicate.Message);
            Assert.True(host.Mods.SetEnabled("Demaster/Higher", false).Success);
        }
        using (GameResourceHost restarted = Open())
        {
            Assert.Equal([10], restarted.Resources.Read(Scan));
            Assert.True(restarted.Mods.SetEnabled("Demaster/Twin", false).Success);
        }
        using GameResourceHost installed = Open();
        Assert.Equal([9], installed.Resources.Read(Scan));
    }

    [Fact]
    public void SelectsEachTripletFileAndKeepsItsActualMembership()
    {
        string package = "Mods/Demaster/Archive/data/lang-en/";
        Write(package + "sample.fs", [90, 91]);
        using (GameResourceHost host = Open())
        {
            Assert.Equal([90], host.Resources.Read(new("sample/a.dat")));
            Assert.Equal([91], host.Resources.Read(new("sample/b.dat")));
        }
        Write(package + "sample.fl", Listing("a.dat"));
        Write(package + "sample.fi", Index(1));
        Write(package + "sample.fs", [92]);
        using (GameResourceHost host = Open())
        {
            Assert.Equal([92], host.Resources.Read(new("sample/a.dat")));
            Assert.Null(host.Resources.GetSource<RemasteredGameDataSource>()!.Find(new("sample/b.dat")));
        }
        using GameResourceHost french = Open("fr");
        Assert.Equal([7], french.Resources.Read(Scan));
        Assert.Equal([80], french.Resources.Read(new("sample/a.dat")));
    }

    [Fact]
    public void RejectsBrokenTripletBeforeOpeningGameResourcesAndIdentifiesAllInputs()
    {
        Write("Mods/Demaster/Archive/data/lang-en/sample.fs", [99]);
        using GameResourceHost host = Open();
        Assert.Equal([30], host.Resources.Read(new("sample/a.dat")));
        ModDiagnostic failure = Assert.Single(host.Mods.Diagnostics, diagnostic => diagnostic.Code == "Demaster.InvalidExternalData");
        Assert.Contains("main.zzz:data/lang-en/sample.fl", failure.Message);
        Assert.Contains("main.zzz:data/lang-en/sample.fi", failure.Message);
        Assert.Contains("Demaster/Archive/data/lang-en/sample.fs", failure.Message);
    }

    [Fact]
    public void OpensIndependentSeekableStreamsAndUsesExactMediaAndMarkerPaths()
    {
        Write("DEMASTER_EXP/" + ScanPath, [4, 5, 6]);
        Write("DEMASTER_EXP/data/music/dmusic/ogg/068s-missle.ogg", [51]);
        Write("DEMASTER_EXP/data/music/dmusic/ogg/new.ogg", [52]);
        Write("DEMASTER_EXP/data/disk/wrong_disk", []);
        using GameResourceHost host = Open();
        using Stream first = host.Resources.Get(Scan).OpenRead();
        using Stream second = host.Resources.Get(Scan).OpenRead();
        Assert.Equal(3, first.Length);
        Assert.Equal(2, first.Seek(-1, SeekOrigin.End));
        Assert.Equal(6, first.ReadByte());
        Assert.Equal(4, second.ReadByte());
        Assert.Equal([50], File.ReadAllBytes(host.ResolveGameFile("music/dmusic/068s-missile.sgt")));
        Assert.Equal([52], File.ReadAllBytes(host.ResolveGameFile("music/dmusic/new.sgt")));
        var source = host.Resources.GetSource<RemasteredGameDataSource>()!;
        Assert.True(source.ContainsExternalData("data/disk/wrong_disk"));
        RemasteredExternalFileInfo selected = Assert.IsType<RemasteredExternalFileInfo>(source.DescribeExternalData(ScanPath));
        Assert.Equal(3, selected.Length);
        Assert.Equal(ModRuntime.InstalledDemasterPackageId + "/" + ScanPath, selected.Origin);
        Assert.False(source.ContainsExternalData("data/disk/missing"));
        using Stream marker = host.Resources.Get(new("files/disk/wrong_disk")).OpenRead();
        Assert.Equal(0, marker.Length);
    }

    [Fact]
    public void OptionalExportChangesScanAndCardNameDataAndRemovesBattleMember()
    {
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF8_REMASTERED_DIR");
        string? export = Environment.GetEnvironmentVariable("MEMORIA_FF8_DEMASTER_DATA_DIR");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(game) && Directory.Exists(game) && !string.IsNullOrWhiteSpace(export) && Directory.Exists(Path.Combine(export, "data")),
            "Set the optional Remastered installation and Demaster data fixture directories.");
        using RemasteredGameDataSource stock = new(game!);
        using RemasteredGameDataSource source = new(game!);
        source.PrepareLooseData(Directory.EnumerateFiles(Path.Combine(export!, "data"), "*", SearchOption.AllDirectories).Select(path =>
            new RemasteredLooseDataFile(Path.GetRelativePath(export!, path).Replace('\\', '/'), "Demaster fixture: " + Path.GetRelativePath(export!, path), 2, () => File.OpenRead(path))));
        source.SealLooseData();
        ResourceService resources = new(source);
        using GameAssets assets = new(game!, resources: resources);
        byte[] scan = File.ReadAllBytes(Path.Combine(export!, ScanPath));
        Assert.Equal(scan, resources.Read(Scan));
        Assert.False(scan.SequenceEqual(new ResourceService(stock).Read(Scan)));
        Assert.Equal(scan[320..], assets.ReadNativeScanTextResources().DescriptionBytes.ToArray());
        ResourceKey cardNames = new("text/card-names-alternate");
        byte[] alternate = File.ReadAllBytes(Path.Combine(export!, "data/lang-en/off_cards_names2_en.dat"));
        Assert.Equal(alternate, resources.Read(cardNames));
        Assert.False(alternate.SequenceEqual(new ResourceService(stock).Read(cardNames)));
        Assert.NotNull(stock.Find(new("battle/d0w007.dat")));
        Assert.Null(source.Find(new("battle/d0w007.dat")));
        NotSupportedException marker = Assert.Throws<NotSupportedException>(() => NativeSteamDiscMountReader.Read(game!, 1, resources));
        Assert.Contains("rejected-marker", marker.Message);
    }

    private GameResourceHost Open(string language = "en")
    {
        return new(root, applicationBaseDirectory: root, watch: false, modsDirectory: Path.Combine(root, "Mods"), resourceLanguage: language);
    }

    private void Write(string path, byte[] bytes)
    {
        string target = Path.Combine(root, path);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.WriteAllBytes(target, bytes);
    }

    private static byte[] Listing(params string[] names)
    {
        return Encoding.ASCII.GetBytes(string.Concat(names.Select(name => @"C:\FF8\Data\" + name + "\r\n")));
    }

    private static byte[] Index(int count)
    {
        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream);
        for (int i = 0; i < count; i++)
        {
            writer.Write(1);
            writer.Write(i);
            writer.Write(0);
        }
        return stream.ToArray();
    }

    private static void WriteZzz(string path, IReadOnlyDictionary<string, byte[]> entries)
    {
        using BinaryWriter writer = new(File.Create(path));
        writer.Write(entries.Count);
        long offset = sizeof(int) + entries.Keys.Sum(name => Encoding.UTF8.GetByteCount(name) + sizeof(int) + sizeof(long) + sizeof(int));
        foreach ((string name, byte[] payload) in entries)
        {
            byte[] address = Encoding.UTF8.GetBytes(name);
            writer.Write(address.Length);
            writer.Write(address);
            writer.Write(offset);
            writer.Write(payload.Length);
            offset += payload.Length;
        }
        foreach (byte[] payload in entries.Values)
        {
            writer.Write(payload);
        }
    }

    public void Dispose()
    {
        Directory.Delete(root, true);
    }
}
