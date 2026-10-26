using Memoria.FF8.Resources;

namespace Memoria.FF8.Mods.Tests;

public sealed class JunctionFieldResourceGroupTests
{
    [Theory]
    [InlineData("field/mapdata/sa/sample/sample.jsm", "field/sample/sample.jsm")]
    [InlineData("DIRECT\\FIELD\\MAPDATA\\SA\\SAMPLE\\SAMPLE.MSD", "field/sample/sample.msd")]
    [InlineData("field/mapdata/sa/sample/chara.one", "field/sample/chara.one")]
    [InlineData("field/mapdata/xx/sample/sample.jsm", null)]
    [InlineData("field/mapdata/sa/sample/other.jsm", null)]
    [InlineData("field/mapdata/sa/sample/sample.unknown", null)]
    [InlineData("field/mapdata/sa/sample/sample.jsm/extra", null)]
    [InlineData("field/mapdata/sa/../sample.jsm", null)]
    [InlineData("field/sample/sample.jsm", null)]
    public void AddressMapsOnlyCanonicalRuntimeFieldComponents(string path, string? expected)
    {
        Assert.Equal(expected, JunctionEncodedResources.Address(path)?.Path);
    }

    [Theory]
    [InlineData("field/mapdata/sa/sample/sample.sym", true)]
    [InlineData("DIRECT\\FIELD\\MAPDATA\\SA\\SAMPLE\\SAMPLE.SYM", true)]
    [InlineData("field/mapdata/xx/sample/sample.sym", false)]
    [InlineData("field/mapdata/sa/sample/other.sym", false)]
    [InlineData("field/mapdata/sa/sample/sample.msd", false)]
    [InlineData("field/sample/sample.sym", false)]
    public void SymbolMetadataIsRecognizedSeparatelyFromRuntimePayloads(string path, bool expected)
    {
        Assert.Equal(expected, JunctionEncodedResources.IsFieldMetadata(path));
        if (expected)
        {
            Assert.Null(JunctionEncodedResources.Address(path));
            ModFile metadata = new("symbols", ModFormat.Junction, path, 1, () => throw new IOException("Symbols are not loaded as runtime payloads."));
            Assert.True(JunctionEncodedResources.TryPrepare(new(1, [], [metadata]), out var resources, out var diagnostics));
            Assert.Empty(resources);
            Assert.Empty(diagnostics);
        }
    }

    [Theory]
    [InlineData("ca")]
    [InlineData("id")]
    [InlineData("inf")]
    [InlineData("jsm")]
    [InlineData("map")]
    [InlineData("mim")]
    [InlineData("mrt")]
    [InlineData("msd")]
    [InlineData("msk")]
    [InlineData("pcb")]
    [InlineData("pmd")]
    [InlineData("rat")]
    [InlineData("sfx")]
    public void RuntimeComponentsRetainTheirEncodedFormatAndCanonicalGroup(string extension)
    {
        ModFile file = File("package", "sample", extension, 7);

        Assert.True(JunctionEncodedResources.TryPrepare(new(1, [], [file]), out var resources, out var diagnostics));

        EncodedResource selected = Assert.Single(resources);
        Assert.Empty(diagnostics);
        Assert.Equal(extension, selected.Content.Format);
        Assert.Equal("sample", JunctionEncodedResources.FieldGroup(selected.Key));
        using Stream input = selected.Content.OpenRead();
        Assert.Equal(7, input.ReadByte());
        Assert.Null(JunctionEncodedResources.FieldGroup(new("field/main_chr/d000.mch")));
        Assert.Null(JunctionEncodedResources.FieldGroup(new("battle/d0c000.dat")));
    }

    [Fact]
    public void OverlappingLowerPriorityGroupContributesNoneOfItsUniqueTail()
    {
        ModFile first = File("first", "sample", "jsm", 1);
        ModFile shadowed = Unread("second", "sample", "jsm");
        ModFile tail = Unread("second", "sample", "msd");
        ModFile independent = File("second", "other", "msd", 2);

        Assert.True(JunctionEncodedResources.TryPrepare(new(1, [], [first, shadowed, tail, independent]), out var resources, out var diagnostics));

        Assert.Equal(new[] { "field/sample/sample.jsm", "field/other/other.msd" }, resources.Select(resource => resource.Key.Path));
        ModDiagnostic conflict = Assert.Single(diagnostics);
        Assert.Equal("Junction.FieldGroupConflict", conflict.Code);
        Assert.Equal("second", conflict.PackageId);
    }

    [Fact]
    public void DisjointContributionsToTheSameFieldCombine()
    {
        ModFile first = File("first", "sample", "jsm", 1);
        ModFile second = File("second", "sample", "msd", 2);

        Assert.True(JunctionEncodedResources.TryPrepare(new(1, [], [first, second]), out var resources, out var diagnostics));

        Assert.Equal(2, resources.Count);
        Assert.Empty(diagnostics);
        Assert.Contains(resources, resource => resource.Content.Provenance.StartsWith("first:", StringComparison.Ordinal));
        Assert.Contains(resources, resource => resource.Content.Provenance.StartsWith("second:", StringComparison.Ordinal));
    }

    [Fact]
    public void RejectedMiddleGroupDoesNotReserveMembersAgainstLowerDisjointGroups()
    {
        ModFile first = File("first", "sample", "jsm", 1);
        ModFile middleScript = Unread("middle", "sample", "jsm");
        ModFile middleText = Unread("middle", "sample", "msd");
        ModFile last = File("last", "sample", "msd", 3);

        Assert.True(JunctionEncodedResources.TryPrepare(new(1, [], [first, middleScript, middleText, last]), out var resources, out var diagnostics));

        Assert.Equal(2, resources.Count);
        Assert.StartsWith("first:", resources.Single(resource => resource.Content.Format == "jsm").Content.Provenance);
        Assert.StartsWith("last:", resources.Single(resource => resource.Content.Format == "msd").Content.Provenance);
        Assert.Equal("middle", Assert.Single(diagnostics).PackageId);
    }

    [Fact]
    public void WholeGroupUsesPackagePrecedenceEvenWhenMemberEntriesAreInterleaved()
    {
        ModFile firstScript = File("first", "sample", "jsm", 1);
        ModFile secondText = Unread("second", "sample", "msd");
        ModFile firstText = File("first", "sample", "msd", 2);

        Assert.True(JunctionEncodedResources.TryPrepare(new(1, [], [firstScript, secondText, firstText]), out var resources, out var diagnostics));

        Assert.Equal(2, resources.Count);
        Assert.All(resources, resource => Assert.StartsWith("first:", resource.Content.Provenance));
        Assert.Equal("second", Assert.Single(diagnostics).PackageId);
    }

    [Fact]
    public void CanonicalAliasesWithinOneGroupRetainFirstMemberWithoutSelfConflict()
    {
        ModFile first = File("package", "sample", "jsm", 1);
        ModFile alias = new("package", ModFormat.Junction, "DIRECT/FIELD/MAPDATA/SA/SAMPLE/SAMPLE.JSM", 2, () => throw new IOException("Shadowed alias must remain unread."));

        Assert.True(JunctionEncodedResources.TryPrepare(new(1, [], [first, alias]), out var resources, out var diagnostics));

        Assert.Single(resources);
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void InvalidAcceptedGroupDoesNotFallThroughToShadowedPackage()
    {
        ModFile first = Unread("first", "sample", "jsm");
        ModFile second = Unread("second", "sample", "jsm");

        Assert.False(JunctionEncodedResources.TryPrepare(new(1, [], [first, second]), out var resources, out var diagnostics));

        Assert.Empty(resources);
        Assert.Contains(diagnostics, diagnostic => diagnostic.Code == "Junction.InvalidEncodedResource" && diagnostic.PackageId == "first");
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Code == "Junction.InvalidEncodedResource" && diagnostic.PackageId == "second");
    }

    [Fact]
    public void RuntimeCatalogRetainsFullShadowedGroupsForAtomicSelection()
    {
        string directory = Path.Combine(Path.GetTempPath(), "junction-field-groups-" + Guid.NewGuid().ToString("N"));
        try
        {
            Write("A", "jsm");
            Write("B", "jsm");
            Write("B", "msd");
            using ModRuntime runtime = new(directory, watch: false);
            Assert.True(runtime.PreparePending(force: true));
            Assert.True(runtime.CommitPending());
            Assert.Equal(3, runtime.Catalog.Files.Count);

            Assert.True(JunctionEncodedResources.TryPrepare(runtime.Catalog, out var resources, out var diagnostics));

            Assert.Equal("jsm", Assert.Single(resources).Content.Format);
            Assert.Equal("Junction/B", Assert.Single(diagnostics).PackageId);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }

        void Write(string package, string extension)
        {
            string path = Path.Combine(directory, "Mods/Junction", package);
            Directory.CreateDirectory(Path.Combine(path, "field/mapdata/sa/sample"));
            System.IO.File.WriteAllText(Path.Combine(path, "mod.xml"), "<ModInfo><ModFolder Folder='field/mapdata/sa/sample'/></ModInfo>");
            System.IO.File.WriteAllBytes(Path.Combine(path, "field/mapdata/sa/sample/sample." + extension), [1]);
        }
    }

    [Fact]
    public void UnchangedGenerationRetainsCommittedGroupNotices()
    {
        string directory = Path.Combine(Path.GetTempPath(), "junction-field-notices-" + Guid.NewGuid().ToString("N"));
        try
        {
            string package = Path.Combine(directory, "Mods", "Junction", "A");
            Directory.CreateDirectory(Path.Combine(package, "assets"));
            System.IO.File.WriteAllText(Path.Combine(package, "mod.xml"), "<ModInfo><ModFolder Folder='assets'/></ModInfo>");
            System.IO.File.WriteAllBytes(Path.Combine(package, "assets", "example.bin"), [1]);
            using ModRuntime runtime = new(directory, prepare: catalog => new(catalog.Files.Count > 0, Diagnostics: [new("Junction.FieldGroupConflict", catalog.Files.Count > 0 ? "committed" : "failed")]), watch: false);
            Assert.True(runtime.PreparePending(true));
            Assert.True(runtime.CommitPending());
            runtime.RequestRefresh();
            Assert.False(runtime.PreparePending(true));
            Assert.Contains(runtime.Diagnostics, diagnostic => diagnostic.Message == "committed");
            Assert.DoesNotContain(runtime.Diagnostics, diagnostic => diagnostic.Message == "failed");
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
    }

    private static ModFile File(string package, string field, string extension, byte value) => new(package, ModFormat.Junction, $"field/mapdata/{field[..2]}/{field}/{field}.{extension}", 1, () => [value]);

    private static ModFile Unread(string package, string field, string extension) => new(package, ModFormat.Junction, $"field/mapdata/{field[..2]}/{field}/{field}.{extension}", 1, () => throw new IOException("Rejected group must remain unread."));
}
