using System.Text;

namespace Memoria.FF8.Mods.Tests;

public sealed class JunctionOptionOverrideTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "Memoria.JunctionOptions.Tests", Guid.NewGuid().ToString("N"));
    private const string Manifest = "<ModInfo><Name>Options</Name><ConfigOption><ID>STYLE</ID><Type>List</Type><Default>0</Default><Option Value=\"0\" Name=\"Original\"/><Option Value=\"2\" Name=\"Alternate\"/></ConfigOption><ConfigOption><ID>EXTRA</ID><Type>Bool</Type><Default>0</Default></ConfigOption><ModFolder Folder=\"base\" ActiveWhen=\"STYLE = 0\"/><ModFolder Folder=\"alternate\" ActiveWhen=\"STYLE = 2\"/><ModFolder Folder=\"extra\" ActiveWhen=\"EXTRA = 1\"/></ModInfo>";

    [Fact]
    public void DeclaredOverridesSelectResourcesAndDefaultsRemainUnchanged()
    {
        string folder = CreateFolder();
        Assert.Equal("base", Read(JunctionModImporter.Open(folder)));
        JunctionModPackage alternate = JunctionModImporter.Open(folder, new Dictionary<string, int> { ["style"] = 2, ["EXTRA"] = 1 });
        Assert.Equal("alternate", Read(alternate));
        Assert.Contains(alternate.Files, file => file.RelativePath == "direct/exe/extra.msd");
        Assert.Equal("base", Read(JunctionModImporter.Open(folder)));
    }

    [Theory]
    [InlineData("UNKNOWN", 0)]
    [InlineData("STYLE", 1)]
    [InlineData("EXTRA", 2)]
    public void UnknownIdsAndUndeclaredValuesFail(string id, int value)
    {
        Assert.Throws<InvalidDataException>(() => JunctionModImporter.Open(CreateFolder(), new Dictionary<string, int> { [id] = value }));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SidecarChangesReloadSelectedSourceWithoutChangingPackage(bool archive)
    {
        string folder = CreateFolder();
        string package = archive ? Path.Combine(root, "Mods", "Junction", "Options.iroj") : folder;
        if (archive)
        {
            WriteArchive(package);
            Directory.Delete(folder, recursive: true);
        }
        string config = archive ? package + ".ini" : Path.Combine(package, "mod.ini");
        using ModRuntime runtime = new(root, watch: false, debounce: TimeSpan.Zero);
        Publish(runtime);
        ModFile initial = Assert.Single(runtime.Catalog.FindAll(ModFormat.Junction, "direct/exe/labels.msd"));
        Assert.Equal("base", Encoding.UTF8.GetString(initial.ReadBytes()));
        string packageId = Assert.Single(runtime.Catalog.Packages).Id;
        System.IO.File.WriteAllText(config, "enabled = 1\noption.STYLE = 2 # selected alternate\n");
        runtime.RequestRefresh();
        Publish(runtime);
        ModFile selected = Assert.Single(runtime.Catalog.FindAll(ModFormat.Junction, "direct/exe/labels.msd"));
        Assert.Equal("alternate", Encoding.UTF8.GetString(selected.ReadBytes()));
        Assert.Equal(packageId, Assert.Single(runtime.Catalog.Packages).Id);
        Assert.True(selected.Revision > initial.Revision);
        Assert.Equal("base", Encoding.UTF8.GetString(initial.ReadBytes()));
        System.IO.File.WriteAllText(config, "option.STYLE = invalid\n");
        runtime.RequestRefresh();
        Publish(runtime);
        Assert.Equal(ModEffectiveStatus.ReloadFailure, Assert.Single(runtime.Catalog.Packages).Status);
        Assert.Contains(runtime.Diagnostics, diagnostic => diagnostic.Code == "junction-import-failed" && diagnostic.PackageId == packageId);
    }

    private string CreateFolder()
    {
        string folder = Path.Combine(root, "Mods", "Junction", "Options");
        foreach ((string path, string content) in Files())
        {
            string target = Path.Combine(folder, path);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            System.IO.File.WriteAllText(target, content);
        }
        return folder;
    }

    private static (string Name, string Content)[] Files() => [("mod.xml", Manifest), ("base/direct/exe/labels.msd", "base"), ("alternate/direct/exe/labels.msd", "alternate"), ("extra/direct/exe/extra.msd", "extra")];

    private static string Read(JunctionModPackage package)
    {
        using StreamReader reader = new(Assert.Single(package.Files, file => file.RelativePath == "direct/exe/labels.msd").OpenRead());
        return reader.ReadToEnd();
    }

    private static void WriteArchive(string path)
    {
        var files = Files();
        using FileStream stream = System.IO.File.Create(path);
        using BinaryWriter writer = new(stream, Encoding.Unicode, true);
        writer.Write(0x534f5249);
        writer.Write(0x10002);
        writer.Write(0);
        writer.Write(16);
        writer.Write(files.Length);
        long offset = 20 + files.Sum(file => 20 + Encoding.Unicode.GetByteCount(file.Name));
        foreach ((string name, string content) in files)
        {
            byte[] nameBytes = Encoding.Unicode.GetBytes(name);
            byte[] bytes = Encoding.UTF8.GetBytes(content);
            writer.Write((ushort)(20 + nameBytes.Length));
            writer.Write((ushort)nameBytes.Length);
            writer.Write(nameBytes);
            writer.Write(0);
            writer.Write(offset);
            writer.Write(bytes.Length);
            offset += bytes.Length;
        }
        foreach ((string name, string content) in files)
        {
            writer.Write(Encoding.UTF8.GetBytes(content));
        }
    }

    private static void Publish(ModRuntime runtime)
    {
        Assert.True(runtime.PreparePending(force: true));
        Assert.True(runtime.CommitPending());
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
