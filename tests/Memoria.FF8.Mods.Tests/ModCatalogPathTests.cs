namespace Memoria.FF8.Mods.Tests;

public sealed class ModCatalogPathTests
{
    [Theory]
    [InlineData("mods/textures/magic/mag012.tim-0_00.dds")]
    [InlineData("texture.png")]
    [InlineData("a/.hidden/..other/file.png")]
    public void Canonical_addresses_retain_the_original_string_without_allocating_segments(string path)
    {
        for (int warmup = 0; warmup < 100; warmup++)
        {
            Assert.Same(path, ModCatalogSnapshot.NormalizePath(path));
        }
        long before = GC.GetAllocatedBytesForCurrentThread();
        int total = 0;
        for (int index = 0; index < 1000; index++)
        {
            total += ModCatalogSnapshot.NormalizePath(path).Length;
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(path.Length * 1000, total);
        Assert.Equal(0, allocated);
    }

    [Theory]
    [InlineData("a\\b/./c/../texture.png", "a/b/texture.png")]
    [InlineData("a//b///texture.png/", "a/b/texture.png")]
    [InlineData("./texture.png", "texture.png")]
    [InlineData("a/../texture.png", "texture.png")]
    public void Noncanonical_addresses_keep_the_existing_relative_path_rules(string path, string expected)
    {
        Assert.Equal(expected, ModCatalogSnapshot.NormalizePath(path));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("a/..")]
    [InlineData("../texture.png")]
    [InlineData("a/../../texture.png")]
    [InlineData("/texture.png")]
    [InlineData("\\texture.png")]
    [InlineData("C:relative.png")]
    [InlineData("//server/texture.png")]
    public void Fast_path_does_not_admit_empty_rooted_or_escaping_addresses(string path)
    {
        Assert.Throws<ArgumentException>(() => ModCatalogSnapshot.NormalizePath(path));
    }

    [Fact]
    public void Null_addresses_keep_their_explicit_argument_guard()
    {
        Assert.Throws<ArgumentNullException>(() => ModCatalogSnapshot.NormalizePath(null!));
    }
}
