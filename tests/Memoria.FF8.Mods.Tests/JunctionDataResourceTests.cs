using Memoria.FF8.Resources;

namespace Memoria.FF8.Mods.Tests;

public sealed class JunctionDataResourceTests
{
    [Theory]
    [InlineData("kernel.bin", "main/kernel.bin", NativeDataResourceKind.Kernel)]
    [InlineData("DIRECT\\MAIN\\KERNEL.BIN", "main/kernel.bin", NativeDataResourceKind.Kernel)]
    [InlineData("namedic.bin", "main/namedic.bin", NativeDataResourceKind.NameDictionary)]
    [InlineData("menu/mngrp.bin", "menu/mngrp.bin", NativeDataResourceKind.MenuGroupData)]
    [InlineData("menu/mngrphd.bin", "menu/mngrphd.bin", NativeDataResourceKind.MenuGroupIndex)]
    [InlineData("world/dat/wmsetus.obj", "world/wmsetus.obj", NativeDataResourceKind.WorldSettings)]
    [InlineData("direct/world/wmsetfr.obj", "world/wmsetfr.obj", NativeDataResourceKind.WorldSettings)]
    [InlineData("battle/c0m126.dat", "battle/c0m126.dat", NativeDataResourceKind.Enemy)]
    [InlineData("menu/mag12.tex", "menu/mag12.tex", NativeDataResourceKind.Texture)]
    [InlineData("menu/hires/sysfld00.tex", "menu/hires/sysfld00.tex", NativeDataResourceKind.Texture)]
    [InlineData("discerr.lzs", "main/discerr.lzs", NativeDataResourceKind.CompressedImage)]
    public void CanonicalFamiliesRetainNativeIdentity(string path, string expected, NativeDataResourceKind kind)
    {
        ResourceKey address = Assert.IsType<ResourceKey>(JunctionEncodedResources.Address(path));
        Assert.Equal(expected, address.Path);
        Assert.Equal(kind, NativeDataResourceFiles.Classify(address));
    }

    [Theory]
    [InlineData("../kernel.bin")]
    [InlineData("main/../kernel.bin")]
    [InlineData("kernel.bin/extra")]
    [InlineData("menu/temp/mngrp.bin")]
    [InlineData("battle/c0m12.dat")]
    [InlineData("battle/c0mabc.dat")]
    [InlineData("world/dat/wmsetxxx.obj")]
    [InlineData("menu/hires/sysfldx0.tex")]
    [InlineData("")]
    [InlineData("direct")]
    [InlineData("direct/")]
    public void UnregisteredPathsDoNotBecomeNativeAliases(string path)
    {
        Assert.Null(JunctionEncodedResources.Address(path));
    }

    [Fact]
    public void OverlappingCompoundContributionLosesAsAWhole()
    {
        ModFile header = File("first", "menu/mngrphd.bin", 1);
        ModFile lowerHeader = new("second", ModFormat.Junction, "menu/mngrphd.bin", 2, () => throw new IOException("A conflicting contribution must not be opened."));
        ModFile lowerData = new("second", ModFormat.Junction, "menu/mngrp.bin", 2, () => throw new IOException("A conflicting group member must not be opened."));
        Assert.True(JunctionEncodedResources.TryPrepare(new(1, [], [header, lowerHeader, lowerData]), out var resources, out var diagnostics));
        Assert.Equal("menu/mngrphd.bin", Assert.Single(resources).Key.Path);
        Assert.Equal("second", Assert.Single(diagnostics).PackageId);
        Assert.Equal("Junction.ResourceGroupConflict", diagnostics[0].Code);
    }

    [Fact]
    public void DisjointCompoundContributionsCanCombineBeforeValidation()
    {
        ModFile header = File("first", "menu/mngrphd.bin", 1);
        ModFile data = File("second", "direct/menu/mngrp.bin", 2);
        Assert.True(JunctionEncodedResources.TryPrepare(new(1, [], [header, data]), out var resources, out var diagnostics));
        Assert.Equal(2, resources.Count);
        Assert.Empty(diagnostics);
        Assert.Equal(["first:menu/mngrphd.bin", "second:direct/menu/mngrp.bin"], resources.Select(resource => resource.Content.Provenance));
    }

    [Fact]
    public void InvalidWinningCompoundMemberDoesNotRetryLowerGroup()
    {
        ModFile invalid = new("first", ModFormat.Junction, "menu/mngrp.bin", 1, () => []);
        ModFile shadowed = new("second", ModFormat.Junction, "menu/mngrp.bin", 2, () => throw new IOException("A lower winner must not be read."));
        Assert.False(JunctionEncodedResources.TryPrepare(new(1, [], [invalid, shadowed]), out _, out var diagnostics));
        Assert.Contains(diagnostics, diagnostic => diagnostic.Code == "Junction.InvalidEncodedResource" && diagnostic.PackageId == "first");
    }

    private static ModFile File(string owner, string path, byte value) => new(owner, ModFormat.Junction, path, value, () => Enumerable.Repeat(value, 16).ToArray());
}
