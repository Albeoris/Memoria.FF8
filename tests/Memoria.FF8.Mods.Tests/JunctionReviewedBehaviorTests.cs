using System.Text.Json;
using System.Xml.Linq;

namespace Memoria.FF8.Mods.Tests;

public sealed class JunctionReviewedBehaviorTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "ff8-reviewed-behavior-" + Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData("Selection/hext/ff8/en_nv/example.hext", 1)]
    [InlineData("Selection/hext/ff8/en_nv/example.hext", 0)]
    [InlineData("Selection/hext/ff8/fr_nv/example.hext", 0)]
    [InlineData("Selection/example.hext", 1)]
    [InlineData("Selection/hext/ff8/en_nv/example.dll", 0)]
    public void UnreviewedPayloadsFailEvenWhenUnselected(string path, int selected)
    {
        Write("mod.xml", Manifest(1));
        Write(path, "unreviewed payload"u8.ToArray());
        NotSupportedException exception = Assert.Throws<NotSupportedException>(() => JunctionModImporter.Open(root, new Dictionary<string, int> { ["option0"] = selected }));
        Assert.Contains("review", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExecutablesRemainRejectedInInactiveFolders()
    {
        Write("mod.xml", Manifest(1));
        Write("Selection/helper.exe", [1, 2, 3]);
        Assert.Throws<NotSupportedException>(() => JunctionModImporter.Open(root, new Dictionary<string, int> { ["option0"] = 0 }));
    }

    [Fact]
    public void RecognitionReadHasAPerFileBound()
    {
        Write("mod.xml", Manifest(1));
        Write("Selection/hext/ff8/en_nv/large.hext", new byte[64 * 1024 + 1]);
        NotSupportedException exception = Assert.Throws<NotSupportedException>(() => JunctionModImporter.Open(root));
        Assert.Contains("limit", exception.Message);
    }

    [Fact]
    public void OptionalReviewedArchiveSupportsIndependentSelectionAndIntrinsicManualSkip()
    {
        JunctionArchive archive = OptionalArchive();
        JunctionModPackage original = JunctionModImporter.Open(archive.SourcePath);
        Assert.Equal(Enum.GetValues<JunctionBehavior>(), original.Manifest.Behaviors);
        Assert.Equal(4, original.Manifest.Options.Count);
        Assert.Empty(original.Files);
        JunctionArchiveEntry[] payloads = archive.Entries.Where(entry => entry.RelativePath.EndsWith(".hext", StringComparison.OrdinalIgnoreCase)).ToArray();
        Assert.Equal(5, payloads.Length);
        List<(byte[] Bytes, JunctionBehavior Behavior)> recognized = [];
        foreach (JunctionArchiveEntry entry in payloads)
        {
            using Stream input = entry.OpenRead();
            using MemoryStream bytes = new();
            input.CopyTo(bytes);
            Write("mod.xml", Manifest(1));
            Write("Selection/hext/ff8/en_nv/neutral.hext", bytes.ToArray());
            JunctionModPackage package = JunctionModImporter.Open(root);
            Assert.Empty(package.Files);
            JunctionBehavior behavior = Assert.Single(package.Manifest.Behaviors);
            recognized.Add((bytes.ToArray(), behavior));
            JunctionModPackage inactive = JunctionModImporter.Open(root, new Dictionary<string, int> { ["option0"] = 0 });
            if (behavior == JunctionBehavior.InstantManualSkip)
            {
                Assert.Equal(new[] { JunctionBehavior.InstantManualSkip }, inactive.Manifest.Behaviors);
                Assert.Empty(package.Manifest.Options);
                Assert.Empty(inactive.Manifest.Options);
            }
            else
            {
                Assert.Empty(inactive.Manifest.Behaviors);
                Assert.Equal(0, inactive.Manifest.Options["option0"]);
            }
            byte[] mutation = bytes.ToArray();
            mutation[0] ^= 1;
            Write("Selection/hext/ff8/en_nv/neutral.hext", mutation);
            Assert.Throws<NotSupportedException>(() => JunctionModImporter.Open(root, new Dictionary<string, int> { ["option0"] = 0 }));
            File.Delete(Path.Combine(root, "Selection/hext/ff8/en_nv/neutral.hext"));
            Write("Selection/hext/ff8/fr_nv/neutral.hext", bytes.ToArray());
            Assert.Throws<NotSupportedException>(() => JunctionModImporter.Open(root));
            Directory.Delete(Path.Combine(root, "Selection"), recursive: true);
        }
        Assert.Equal(Enum.GetValues<JunctionBehavior>().Order(), recognized.Select(pair => pair.Behavior).Order());
        Write("mod.xml", Manifest(5));
        for (int index = 0; index < recognized.Count; index++)
        {
            Write($"Selection{index}/hext/ff8/en_nv/neutral.hext", recognized[index].Bytes);
        }
        for (int mask = 0; mask < 32; mask++)
        {
            Dictionary<string, int> options = Enumerable.Range(0, 5).ToDictionary(index => "option" + index, index => (mask >> index) & 1);
            JunctionModPackage package = JunctionModImporter.Open(root, options);
            JunctionBehavior[] expected = recognized.Where((pair, index) => pair.Behavior == JunctionBehavior.InstantManualSkip || options["option" + index] == 1).Select(pair => pair.Behavior).Order().ToArray();
            Assert.Equal(expected, package.Manifest.Behaviors);
            Assert.Empty(package.Files);
            int intrinsic = recognized.FindIndex(pair => pair.Behavior == JunctionBehavior.InstantManualSkip);
            Assert.DoesNotContain("option" + intrinsic, package.Manifest.Options.Keys);
        }
        AssertMetadataRefresh(recognized.First(pair => pair.Behavior != JunctionBehavior.InstantManualSkip).Bytes);
    }

    private void AssertMetadataRefresh(byte[] payload)
    {
        string package = "Mods/Junction/Neutral/";
        Write(package + "mod.xml", Manifest(1));
        Write(package + "Selection/hext/ff8/en_nv/neutral.hext", payload);
        using ModRuntime runtime = new(root, watch: false, debounce: TimeSpan.Zero);
        Publish(runtime);
        ModPackage before = Assert.Single(runtime.Catalog.Packages);
        Assert.Equal(ModEffectiveStatus.Active, before.Status);
        long generation = runtime.Catalog.Generation;
        string payloadPath = Path.Combine(root, package + "Selection/hext/ff8/en_nv/neutral.hext");
        DateTime payloadTimestamp = File.GetLastWriteTimeUtc(payloadPath);
        byte[] mutation = payload.ToArray();
        mutation[0] ^= 1;
        File.WriteAllBytes(payloadPath, mutation);
        File.SetLastWriteTimeUtc(payloadPath, payloadTimestamp);
        runtime.RequestRefresh();
        Publish(runtime);
        Assert.True(runtime.Catalog.Generation > generation);
        Assert.Equal(ModEffectiveStatus.ReloadFailure, Assert.Single(runtime.Catalog.Packages).Status);
        Assert.Empty(runtime.Catalog.Files);
        File.WriteAllBytes(payloadPath, payload);
        File.SetLastWriteTimeUtc(payloadPath, payloadTimestamp);
        runtime.RequestRefresh();
        Publish(runtime);
        Assert.Equal(ModEffectiveStatus.Active, Assert.Single(runtime.Catalog.Packages).Status);
        generation = runtime.Catalog.Generation;
        string manifest = Path.Combine(root, package + "mod.xml");
        DateTime timestamp = File.GetLastWriteTimeUtc(manifest);
        long length = new FileInfo(manifest).Length;
        File.WriteAllText(manifest, Manifest(1).Replace("<Default>1</Default>", "<Default>0</Default>", StringComparison.Ordinal));
        File.SetLastWriteTimeUtc(manifest, timestamp);
        Assert.Equal(length, new FileInfo(manifest).Length);
        runtime.RequestRefresh();
        Publish(runtime);
        Assert.True(runtime.Catalog.Generation > generation);
        Assert.Empty(Assert.Single(runtime.Catalog.Packages).JunctionMetadata!.Behaviors);
        Assert.Equal(ModEffectiveStatus.NoUsableEntries, runtime.Catalog.Packages[0].Status);
    }

    private static void Publish(ModRuntime runtime)
    {
        Assert.True(runtime.PreparePending(force: true));
        Assert.True(runtime.CommitPending());
    }

    private static JunctionArchive OptionalArchive()
    {
        string? catalogPath = Environment.GetEnvironmentVariable("MEMORIA_FF8_TEST_FIXTURE_CATALOG");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(catalogPath) && File.Exists(catalogPath), "Configure an optional external fixture catalog.");
        Dictionary<string, string> catalog = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(catalogPath))!;
        Assert.SkipUnless(catalog.TryGetValue("junction-startup", out string? path), "External fixture catalog has no startup behavior archive.");
        string basePath = Environment.GetEnvironmentVariable("MEMORIA_FF8_TEST_FIXTURES") ?? Path.GetDirectoryName(Path.GetFullPath(catalogPath))!;
        string archive = Path.GetFullPath(path!, basePath);
        Assert.SkipUnless(File.Exists(archive), "Optional startup behavior archive is unavailable.");
        return new JunctionArchive(archive);
    }

    private static string Manifest(int count)
    {
        XElement root = new("ModInfo", new XElement("Name", "Neutral behavior package"));
        for (int index = 0; index < count; index++)
        {
            root.Add(new XElement("ConfigOption", new XElement("Type", "Bool"), new XElement("ID", "option" + index), new XElement("Default", 1)));
            root.Add(new XElement("ModFolder", new XAttribute("Folder", count == 1 ? "Selection" : "Selection" + index), new XAttribute("ActiveWhen", $"option{index} = 1")));
        }
        return root.ToString(SaveOptions.DisableFormatting);
    }

    private void Write(string path, string text)
    {
        Write(path, System.Text.Encoding.UTF8.GetBytes(text));
    }

    private void Write(string path, byte[] bytes)
    {
        string destination = Path.Combine(root, path);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.WriteAllBytes(destination, bytes);
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
