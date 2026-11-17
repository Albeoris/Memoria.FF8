using System.Text.Json;

namespace Memoria.FF8.Mods.Tests;

public sealed class NativeModDescriptorTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "native-mod-descriptor-" + Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData("""{"id":"Invalid","name":"Name","package":"Memoria","description":{"en":"English"}}""")]
    [InlineData("""{"id":"one","id":"two","name":"Name","package":"Memoria","description":{"en":"English"}}""")]
    [InlineData("""{"id":"one","name":"Name","package":"Memoria","description":{"en":"English"},"extra":true}""")]
    [InlineData("""{"id":"one","name":"Name","package":"Memoria","description":{"en":"English","en":"Another English"}}""")]
    [InlineData("""{"id":"one","name":"Name","package":"Memoria","description":{"en":"English","ja":"Japanese","jp":"Also Japanese"}}""")]
    [InlineData("""{"id":"one","name":"Name","package":"Memoria","description":{"fr":"French"}}""")]
    [InlineData("""{"id":"one","name":"Name","package":"Memoria","description":{"en":17}}""")]
    [InlineData("""{"id":"one","name":"Name","package":"Memoria","description":{"en":"\uD800"}}""")]
    public void RejectsMalformedDescriptor(string json)
    {
        string descriptor = Write("mod.json", json);
        Assert.Throws<InvalidDataException>(() => NativeModDescriptor.Read(descriptor));
    }

    [Fact]
    public void StableIdentityPreservesSettingsAndOrderAcrossFolderRenameAndGroupMove()
    {
        Write("Mods/mods.ini", "auto_enable_new_mods = 0");
        Write("BundledMods/Memoria/Level-Based Stat Bonuses/README.md", "bundled");
        Write("Mods/Memoria/First folder/mod.json", """{"id":"z-fixture","package":"Collections","name":"Original name","description":{"en":"English","fr":"French","ja":"Japanese","ru":"Russian"}}""");
        Write("Mods/Memoria/First folder/content.txt", "first");
        Write("Mods/Memoria/Last folder/mod.json", """{"id":"a-fixture","package":"collections","name":"Another component","description":{"en":"English"}}""");
        Write("Mods/Memoria/Last folder/content.txt", "second");

        using ModRuntime runtime = new(root, watch: false);
        Publish(runtime);
        var group = Assert.Single(runtime.View.Packages, row => row.Package.Name == "Collections");
        Assert.False(group.CanToggle);
        Assert.Equal(2, runtime.View.Packages.Count);
        BehaviorModSetting toggle = Assert.Single(runtime.ReadBehaviorSettings(group.Package.Id), setting => setting.Parameter == "z-fixture");
        Assert.Equal(("Original name", "toggle", 0), (toggle.Label, toggle.Control, toggle.Value));
        Assert.Equal("French", toggle.Descriptor!.DescriptionForLanguage("fr"));
        Assert.Equal("Japanese", toggle.Descriptor.DescriptionForLanguage("jp"));
        Assert.Equal("Japanese", toggle.Descriptor.DescriptionForLanguage("ja"));
        Assert.Equal("Russian", toggle.Descriptor.DescriptionForLanguage("ru"));
        Assert.Equal("English", toggle.Descriptor.DescriptionForLanguage("it"));
        Assert.True(runtime.SetBehaviorSettings(group.Package.Id, runtime.ReadBehaviorSettings(group.Package.Id).Select(setting => setting with { Value = 1 }).ToArray()).Success);
        Publish(runtime);
        string[] active = ["Memoria/a-fixture", "Memoria/z-fixture"];
        Assert.Equal(active, runtime.Catalog.Files.Where(file => file.RelativePath == "content.txt").Select(file => file.PackageId));

        Directory.Move(Path.Combine(root, "Mods", "Memoria", "First folder"), Path.Combine(root, "Mods", "Memoria", "Renamed folder"));
        Write("Mods/Memoria/Renamed folder/mod.json", """{"description":{"ru":"New Russian","en":"New English"},"name":"Renamed component","id":"z-fixture","package":"Memoria"}""");
        runtime.RequestRefresh();
        Publish(runtime);
        Assert.Equal(active, runtime.Catalog.Files.Where(file => file.RelativePath == "content.txt").Select(file => file.PackageId));
        Assert.DoesNotContain(runtime.Catalog.Files, file => file.RelativePath == NativeModDescriptor.FileName);
        Assert.DoesNotContain(runtime.ReadBehaviorSettings(group.Package.Id), setting => setting.Parameter == "z-fixture");
        var memoriaSettings = runtime.ReadBehaviorSettings(ModRuntime.MemoriaPackageId);
        Assert.Equal("Battle", Assert.Single(memoriaSettings, setting => setting.Parameter == "Level-Based Stat Bonuses").Category);
        toggle = Assert.Single(memoriaSettings, setting => setting.Parameter == "z-fixture");
        Assert.Equal(("Renamed component", 1, "New Russian"), (toggle.Label, toggle.Value, toggle.Descriptor!.DescriptionForLanguage("ru")));
    }

    [Fact]
    public void CanonicalSettingsUseThePreviousPreferenceUntilThePlayerChangesIt()
    {
        Write("Mods/Memoria/memoria-settings.json", """{"components":{"Girl Next Door":false},"meltdown":{}}""");
        Write("Mods/Memoria/Other folder/mod.json", """{"id":"girl-next-door","name":"Renamed","package":"Memoria","description":{"en":"English"}}""");
        Write("Mods/Memoria/Other folder/content.txt", "content");
        using ModRuntime runtime = new(root, watch: false);
        Publish(runtime);
        ModPackage package = Assert.Single(runtime.InstalledPackages);
        Assert.Equal("Memoria/girl-next-door", package.Id);
        Assert.False(package.RequestedEnabled);
        Assert.True(runtime.SetBehaviorSetting(ModRuntime.MemoriaPackageId, "component", "girl-next-door", 1).Success);
        Publish(runtime);
        Assert.True(Assert.Single(runtime.InstalledPackages).RequestedEnabled);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("""{"id":"keep-active","package":"Memoria","name":false,"description":{"en":"English"}}""")]
    public void InvalidReplacementRetainsPublishedResourcesAndAllowsDisabling(string? replacement)
    {
        const string descriptor = "Mods/Memoria/Practical folder/mod.json";
        string path = Write(descriptor, """{"id":"keep-active","package":"Memoria","name":"Name","description":{"en":"English"}}""");
        Write("Mods/Memoria/Practical folder/content.txt", "original");
        using ModRuntime runtime = new(root, watch: false);
        Publish(runtime);
        long generation = runtime.Catalog.Generation;

        if (replacement is null)
        {
            File.Delete(path);
        }
        else
        {
            File.WriteAllText(path, replacement);
        }
        runtime.RequestRefresh();
        Assert.False(runtime.PreparePending(force: true));
        Assert.Equal(generation, runtime.Catalog.Generation);
        Assert.Equal("Memoria/keep-active", Assert.Single(runtime.Catalog.Files).PackageId);
        Assert.Contains(runtime.Diagnostics, diagnostic => diagnostic.Code == "native-descriptor" && diagnostic.PackageId == "Memoria/keep-active");

        Assert.True(runtime.SetBehaviorSetting(ModRuntime.MemoriaPackageId, "component", "keep-active", 0).Success);
        Publish(runtime);
        Assert.Empty(runtime.Catalog.Files);
        Assert.False(Assert.Single(runtime.InstalledPackages).RequestedEnabled);
    }

    [Fact]
    public void OfficialCatalogAcceptsPublishedLegacyAndCanonicalIdentityOnly()
    {
        OfficialModComponent component = OfficialModCatalog.GirlNextDoor;
        OfficialModArtifact artifact = new("https://example.com/fixture.zip", 10, 20, new string('0', 64));
        OfficialModRelease release = new("1.0.2", "[0.8.10,)", "girl-next-door-v1.0.2", "", artifact);
        OfficialModComponent legacy = component with
        {
            Id = "girl-next-door",
            RuntimePackageId = OfficialModCatalog.LegacyGirlNextDoorRuntimePackageId,
            SettingsKey = OfficialModCatalog.LegacyGirlNextDoorSettingsKey,
            Latest = release
        };
        Assert.Equal(legacy.RuntimePackageId, Assert.Single(OfficialModCatalog.Parse(Catalog(legacy)).Components).RuntimePackageId);

        release = release with { Version = "1.0.3", ReleaseTag = "girl-next-door-v1.0.3" };
        Assert.Throws<InvalidDataException>(() => OfficialModCatalog.Parse(Catalog(legacy with { Latest = release })));
        OfficialModComponent previous = legacy with { RuntimePackageId = "Memoria/girl-next-door", SettingsKey = "girl-next-door", Latest = release };
        Assert.Equal(previous.RuntimePackageId, Assert.Single(OfficialModCatalog.Parse(Catalog(previous)).Components).RuntimePackageId);

        release = release with { Version = "1.0.4", ReleaseTag = component.Id + "-v1.0.4" };
        Assert.Equal(component.RuntimePackageId, Assert.Single(OfficialModCatalog.Parse(Catalog(component with { Latest = release })).Components).RuntimePackageId);
        Assert.Throws<InvalidDataException>(() => OfficialModCatalog.Parse(Catalog(component with { SettingsKey = "wrong", Latest = release })));
    }

    private static byte[] Catalog(OfficialModComponent component)
    {
        return JsonSerializer.SerializeToUtf8Bytes(new { schemaVersion = 1, sourceId = OfficialModCatalog.SourceId, revision = 1, publishedAt = DateTimeOffset.UtcNow, components = new[] { component } }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }

    private static void Publish(ModRuntime runtime)
    {
        Assert.True(runtime.PreparePending(force: true), string.Join(Environment.NewLine, runtime.Diagnostics));
        Assert.True(runtime.CommitPending());
    }

    private string Write(string relativePath, string content)
    {
        string path = Path.Combine(root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
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
