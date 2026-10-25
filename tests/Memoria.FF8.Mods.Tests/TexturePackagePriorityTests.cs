using System.Text;

namespace Memoria.FF8.Mods.Tests;

public sealed class TexturePackagePriorityTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "texture-priority-" + Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData(ModFormat.Demaster)]
    [InlineData(ModFormat.Tonberry)]
    public void HigherPriorityWinsAndDisablingItExposesBase(ModFormat format)
    {
        Write(root, format, "Alpha", "base");
        Write(root, format, "Zeta", "addon", "priority = +1\n");
        using ModRuntime runtime = Runtime();
        Publish(runtime);
        Assert.Equal("addon", Winner(runtime, format));
        Assert.Equal(1, runtime.Catalog.Packages.Single(package => package.Name == "Zeta").Priority);
        Assert.True(runtime.SetEnabled($"{format}/Zeta", false).Success);
        Publish(runtime);
        Assert.Equal("base", Winner(runtime, format));
    }

    [Theory]
    [InlineData(ModFormat.Demaster)]
    [InlineData(ModFormat.Tonberry)]
    public void DefaultAndEqualPrioritiesRetainAlphabeticalOrder(ModFormat format)
    {
        Write(root, format, "Zeta", "second");
        Write(root, format, "Alpha", "first");
        using ModRuntime runtime = Runtime();
        Publish(runtime);
        Assert.Equal(new[] { $"{format}/Alpha", $"{format}/Zeta" }, runtime.Catalog.FindAll(format, "textures/test.png").Select(file => file.PackageId));
        Assert.All(runtime.Catalog.Packages, package => Assert.Equal(0, package.Priority));
        WriteConfiguration(root, format, "Alpha", "priority = -2\n");
        WriteConfiguration(root, format, "Zeta", "priority = -2\n");
        runtime.RequestRefresh();
        Publish(runtime);
        Assert.Equal("first", Winner(runtime, format));
    }

    [Fact]
    public void ChangingOnlyPriorityPublishesNewOrdering()
    {
        Write(root, ModFormat.Demaster, "Alpha", "base");
        Write(root, ModFormat.Demaster, "Zeta", "addon", "priority = -1\n");
        using ModRuntime runtime = Runtime();
        Publish(runtime);
        long generation = runtime.Catalog.Generation;
        Assert.Equal("base", Winner(runtime, ModFormat.Demaster));
        WriteConfiguration(root, ModFormat.Demaster, "Zeta", "priority = 1\n");
        runtime.RequestRefresh();
        Publish(runtime);
        Assert.Equal(generation + 1, runtime.Catalog.Generation);
        Assert.Equal("addon", Winner(runtime, ModFormat.Demaster));
    }

    [Fact]
    public void WatcherPublishesPriorityOnlyChanges()
    {
        Write(root, ModFormat.Demaster, "Alpha", "base");
        Write(root, ModFormat.Demaster, "Zeta", "addon", "priority = -1\n");
        using ModRuntime runtime = new(root, watch: true, debounce: TimeSpan.Zero);
        Publish(runtime);
        WriteConfiguration(root, ModFormat.Demaster, "Zeta", "priority = 1\n");
        DateTime deadline = DateTime.UtcNow.AddSeconds(5);
        while (Winner(runtime, ModFormat.Demaster) != "addon" && DateTime.UtcNow < deadline)
        {
            runtime.PreparePending(force: true);
            runtime.CommitPending();
            Thread.Sleep(20);
        }
        Assert.Equal("addon", Winner(runtime, ModFormat.Demaster));
    }

    [Fact]
    public void EarlierRootOwnsEntireDuplicateBeforePriorityIsRead()
    {
        string game = Path.Combine(root, "game");
        Write(game, ModFormat.Demaster, "shared", "game", "priority = -10\n");
        Write(root, ModFormat.Demaster, "SHARED", "application", "priority = 100\n");
        string extra = Path.Combine(root, "Mods", "Demaster", "SHARED", "textures", "application-only.png");
        File.WriteAllText(extra, "application only");
        using ModRuntime runtime = new(root, watch: false, debounce: TimeSpan.Zero, additionalBaseDirectories: [game]);
        Publish(runtime);
        Assert.Equal("game", Winner(runtime, ModFormat.Demaster));
        Assert.Empty(runtime.Catalog.FindAll(ModFormat.Demaster, "textures/application-only.png"));
        Assert.Equal(-10, Assert.Single(runtime.Catalog.Packages).Priority);
        Assert.Contains(runtime.Diagnostics, diagnostic => diagnostic.Code == "duplicate-package");
        WriteConfiguration(root, ModFormat.Demaster, "SHARED", "priority = malformed\n");
        runtime.RequestRefresh();
        Assert.False(runtime.PreparePending());
        Assert.DoesNotContain(runtime.Diagnostics, diagnostic => diagnostic.Code == "invalid-package-priority");
    }

    [Theory]
    [InlineData("not-an-integer")]
    [InlineData("2147483648")]
    [InlineData("")]
    public void InvalidPriorityHasOwnedDiagnosticAndRetainsLastPublication(string priority)
    {
        Write(root, ModFormat.Demaster, "example", "original");
        using ModRuntime runtime = Runtime();
        Publish(runtime);
        ModCatalogSnapshot previous = runtime.Catalog;
        WriteConfiguration(root, ModFormat.Demaster, "example", $"priority = {priority}\n");
        runtime.RequestRefresh();
        Assert.False(runtime.PreparePending());
        Assert.False(runtime.CommitPending());
        Assert.Same(previous, runtime.Catalog);
        Assert.Contains(runtime.Diagnostics, diagnostic => diagnostic.Code == "invalid-package-priority" && diagnostic.PackageId == "Demaster/example" && diagnostic.RelativePath == "mod.ini");
    }

    [Fact]
    public void InvalidPriorityNeverActivatesOnInitialDiscovery()
    {
        Write(root, ModFormat.Demaster, "example", "unpublished", "priority = invalid\n");
        using ModRuntime runtime = Runtime();
        Assert.False(runtime.PreparePending());
        Assert.False(runtime.CommitPending());
        Assert.Empty(runtime.Catalog.Files);
        Assert.Contains(runtime.Diagnostics, diagnostic => diagnostic.Code == "invalid-package-priority" && diagnostic.PackageId == "Demaster/example");
    }

    [Fact]
    public void EnabledWritesPreservePriorityAndComments()
    {
        const string configuration = "# package settings\npriority = +7 ; addon order\nenabled = 1 # selection\n";
        Write(root, ModFormat.Tonberry, "example", "texture", configuration);
        using ModRuntime runtime = Runtime();
        Publish(runtime);
        Assert.True(runtime.SetEnabled("Tonberry/example", false).Success);
        string updated = File.ReadAllText(ConfigurationPath(root, ModFormat.Tonberry, "example"));
        Assert.Contains("# package settings", updated);
        Assert.Contains("priority = +7 ; addon order", updated);
        Assert.Contains("enabled = 0 # selection", updated);
        Publish(runtime);
        Assert.Equal(7, Assert.Single(runtime.Catalog.Packages).Priority);
        Assert.Empty(runtime.Catalog.Files);
    }

    private ModRuntime Runtime()
    {
        return new(root, watch: false, debounce: TimeSpan.Zero);
    }

    private static string Winner(ModRuntime runtime, ModFormat format)
    {
        return Encoding.UTF8.GetString(runtime.Catalog.FindAll(format, "textures/test.png")[0].ReadBytes());
    }

    private static void Publish(ModRuntime runtime)
    {
        Assert.True(runtime.PreparePending());
        Assert.True(runtime.CommitPending());
    }

    private static void Write(string baseDirectory, ModFormat format, string name, string contents, string? configuration = null)
    {
        string path = Path.Combine(baseDirectory, "Mods", format.ToString(), name, "textures", "test.png");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, contents);
        if (configuration is not null)
        {
            WriteConfiguration(baseDirectory, format, name, configuration);
        }
    }

    private static string ConfigurationPath(string baseDirectory, ModFormat format, string name)
    {
        return Path.Combine(baseDirectory, "Mods", format.ToString(), name, "mod.ini");
    }

    private static void WriteConfiguration(string baseDirectory, ModFormat format, string name, string configuration)
    {
        File.WriteAllText(ConfigurationPath(baseDirectory, format, name), configuration);
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }
    }
}
