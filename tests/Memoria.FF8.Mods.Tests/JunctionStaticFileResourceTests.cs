using Memoria.FF8.Resources;

namespace Memoria.FF8.Mods.Tests;

public sealed class JunctionStaticFileResourceTests
{
    [Theory]
    [InlineData("sysfnt.tdw", "main/sysfnt.tdw")]
    [InlineData("DIRECT\\SYSFNT.TDW", "main/sysfnt.tdw")]
    [InlineData("main/sysfnt.tdw", "main/sysfnt.tdw")]
    [InlineData("direct/menu/sysfnt.tdw", "menu/sysfnt.tdw")]
    [InlineData("other_table.tdw", "main/other_table.tdw")]
    [InlineData("runtime/native-executable", null)]
    [InlineData("files/sysfnt.tdw", null)]
    [InlineData("other/sysfnt.tdw", null)]
    [InlineData("menu/../sysfnt.tdw", null)]
    [InlineData("direct//sysfnt.tdw", null)]
    [InlineData("sysfnt.bin", null)]
    [InlineData(".tdw", null)]
    public void RegisteredStaticFormatUsesOrdinaryArchivePaths(string path, string? expected)
    {
        ResourceKey? key = JunctionEncodedResources.Address(path);

        Assert.Equal(expected, key?.Path);
        if (key is ResourceKey addressed)
        {
            Assert.True(JunctionEncodedResources.IsStaticFileResource(addressed));
        }
    }

    [Fact]
    public void RootAndMainAliasesFreezeFirstWinnerWhileMenuRemainsIndependent()
    {
        byte[] winner = [1, 2, 3];
        ModFile root = new("first", ModFormat.Junction, "sysfnt.tdw", 5, () => winner);
        ModFile shadowed = new("second", ModFormat.Junction, "direct/main/sysfnt.tdw", 6, () => throw new IOException("Shadowed files must not be read."));
        ModFile menu = new("second", ModFormat.Junction, "menu/sysfnt.tdw", 7, () => [4, 5, 6]);
        Assert.True(JunctionEncodedResources.TryPrepare(new(1, [], [root, shadowed, menu]), out var selected, out var diagnostics));
        Assert.Empty(diagnostics);
        Array.Fill(winner, (byte)255);
        using InMemoryResourceSource source = new(new("native", GameDataEdition.Classic, []));
        source.Set(new("main/sysfnt.tdw"), [0], "tdw");
        source.Set(new("menu/sysfnt.tdw"), [9], "tdw");
        ResourceService resources = new(source);
        resources.Prepare(null, encodedResources: selected).Commit();

        Assert.Equal(new byte[] { 1, 2, 3 }, resources.Read(new("main/sysfnt.tdw")));
        Assert.Equal(new byte[] { 4, 5, 6 }, resources.Read(new("menu/sysfnt.tdw")));
        Assert.Equal("first:sysfnt.tdw", resources.Get(new("main/sysfnt.tdw")).Provenance);
        Assert.Same(source, resources.GetSource<InMemoryResourceSource>());
        Assert.Throws<ResourceReloadRequiredException>(() => resources.Prepare(null, encodedResources: []));
    }

    [Fact]
    public void EmptyFirstWinnerFailsWithoutTryingLowerCandidate()
    {
        ModFile first = new("first", ModFormat.Junction, "table.tdw", 1, () => []);
        ModFile second = new("second", ModFormat.Junction, "main/table.tdw", 2, () => throw new IOException("Lower priority file must not be read."));

        Assert.False(JunctionEncodedResources.TryPrepare(new(1, [], [first, second]), out var selected, out var diagnostics));

        Assert.Empty(selected);
        ModDiagnostic diagnostic = Assert.Single(diagnostics);
        Assert.Equal("first", diagnostic.PackageId);
        Assert.Equal("table.tdw", diagnostic.RelativePath);
    }
}
