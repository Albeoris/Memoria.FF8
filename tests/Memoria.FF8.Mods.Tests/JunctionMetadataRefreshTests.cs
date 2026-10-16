namespace Memoria.FF8.Mods.Tests;

public sealed class JunctionMetadataRefreshTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "ff8-junction-metadata-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void NameOnlyRefreshPublishesMetadataWithoutReplacingUnchangedPayloads()
    {
        string package = CreatePackage(includePayload: true, disabled: false);
        using ModRuntime runtime = new(root, watch: false, debounce: TimeSpan.Zero);
        Publish(runtime);
        ModCatalogSnapshot original = runtime.Catalog;
        ModFile payload = Assert.Single(original.Files);
        Assert.Equal("Before", Assert.Single(original.Packages).Name);
        RenameManifest(package);
        runtime.RequestRefresh();
        Publish(runtime);
        Assert.True(runtime.Catalog.Generation > original.Generation);
        ModPackage current = Assert.Single(runtime.Catalog.Packages);
        Assert.Equal("After!", current.Name);
        Assert.Equal(ModEffectiveStatus.Active, current.Status);
        Assert.Same(payload, Assert.Single(runtime.Catalog.Files));
        Assert.Equal(payload.Revision, runtime.Catalog.Files[0].Revision);
    }

    [Theory]
    [InlineData(true, ModEffectiveStatus.Disabled)]
    [InlineData(false, ModEffectiveStatus.NoUsableEntries)]
    public void NameOnlyRefreshPublishesMetadataWithoutActiveFiles(bool disabled, ModEffectiveStatus expectedStatus)
    {
        string package = CreatePackage(includePayload: disabled, disabled);
        using ModRuntime runtime = new(root, watch: false, debounce: TimeSpan.Zero);
        Publish(runtime);
        long originalGeneration = runtime.Catalog.Generation;
        ModPackage original = Assert.Single(runtime.Catalog.Packages);
        Assert.Equal("Before", original.Name);
        Assert.Equal(expectedStatus, original.Status);
        Assert.Empty(runtime.Catalog.Files);
        RenameManifest(package);
        runtime.RequestRefresh();
        Publish(runtime);
        Assert.True(runtime.Catalog.Generation > originalGeneration);
        ModPackage current = Assert.Single(runtime.Catalog.Packages);
        Assert.Equal("After!", current.Name);
        Assert.Equal(expectedStatus, current.Status);
        Assert.Equal(original.RequestedEnabled, current.RequestedEnabled);
        Assert.Empty(runtime.Catalog.Files);
    }

    private string CreatePackage(bool includePayload, bool disabled)
    {
        string package = Path.Combine(root, "Mods", "Junction", "Example");
        Directory.CreateDirectory(package);
        File.WriteAllText(Path.Combine(package, "mod.xml"), Manifest("Before"));
        if (includePayload)
        {
            string movies = Path.Combine(package, "Assets", "movies");
            Directory.CreateDirectory(movies);
            File.WriteAllBytes(Path.Combine(movies, "opening.cam"), [1, 2, 3]);
        }
        if (disabled)
        {
            File.WriteAllText(Path.Combine(package, "mod.ini"), "enabled = 0\n");
        }
        return package;
    }

    private static void RenameManifest(string package)
    {
        string path = Path.Combine(package, "mod.xml");
        DateTime timestamp = File.GetLastWriteTimeUtc(path);
        long length = new FileInfo(path).Length;
        File.WriteAllText(path, Manifest("After!"));
        File.SetLastWriteTimeUtc(path, timestamp);
        // Same-size, same-timestamp edits still change the parsed metadata identity.
        Assert.Equal(length, new FileInfo(path).Length);
        Assert.Equal(timestamp, File.GetLastWriteTimeUtc(path));
    }

    private static string Manifest(string name)
    {
        return $"<ModInfo><Name>{name}</Name><ModFolder Folder='Assets' /></ModInfo>";
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
