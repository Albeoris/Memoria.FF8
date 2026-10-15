using System.Text;
using Memoria.FF8.Assets;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Mods.Tests;

public sealed class ClassicGameDataSourceTests
{
    [Fact]
    public void ExactNamedRootArchiveWinsOverEarlierUnrelatedArchive()
    {
        using Corpus corpus = new();
        corpus.Write("aaa", ("wmsetus.obj", [99]));
        corpus.Write("world", ("world/dat/WMSETUS.OBJ", [1, 2, 3]), ("world/dat/texl.obj", [4, 5]));
        using ClassicGameDataSource source = new(corpus.Root);
        ResourceService resources = new(source);
        Assert.Equal(new byte[] { 1, 2, 3 }, resources.Read(new("WORLD/WMSETUS.OBJ")));
        Assert.Equal(new byte[] { 4, 5 }, resources.Read(new("world/texl.obj")));
        Assert.Null(source.Find(new("unknown/wmsetus.obj")));
        Assert.Equal(GameDataEdition.Classic, source.Descriptor.Edition);
    }

    [Fact]
    public void NestedArchiveIsReadThroughItsOwnCompanionFiles()
    {
        using Corpus corpus = new();
        ArchiveData nested = Archive(("field/mapdata/test/test.mim", [11, 12, 13]), ("field/mapdata/test/chara.one", [14, 15]));
        ArchiveData other = Archive(("test.mim", [99]));
        corpus.Write("field", ("field/mapdata/aa/test.fl", nested.Fl), ("field/mapdata/aa/test.fi", nested.Fi), ("field/mapdata/aa/test.fs", nested.Fs), ("field/mapdata/aa/other.fl", other.Fl), ("field/mapdata/aa/other.fi", other.Fi), ("field/mapdata/aa/other.fs", other.Fs));
        using ClassicGameDataSource source = new(corpus.Root);
        ResourceService resources = new(source);
        ResourceKey key = new("field/test/test.mim");
        Assert.Equal(new byte[] { 11, 12, 13 }, resources.Read(key));
        Assert.Equal(new byte[] { 14, 15 }, resources.Read(new("field/test/chara.one")));
        Assert.Equal(new byte[] { 99 }, resources.Read(new("field/other/test.mim")));
        ResourceContent content = Assert.IsType<ResourceContent>(source.Find(key));
        Assert.Equal("mim", content.Format);
        Assert.Equal(key, content.Key);
        Assert.Equal("classic:field/test/test.mim", content.Provenance);
        Assert.Same(content, source.Find(key));
        Assert.Null(source.Find(new("field/test/missing.mim")));
    }

    [Fact]
    public void MissingNestedArchiveIsAnAbsentResource()
    {
        using Corpus corpus = new();
        corpus.Write("field", ("field/other.bin", [1]));
        using ClassicGameDataSource source = new(corpus.Root);
        Assert.Null(source.Find(new("field/missing/map.mim")));
    }

    [Fact]
    public void AmbiguousNativeBasenamesFailInsteadOfChoosingEnumerationOrder()
    {
        using Corpus corpus = new();
        corpus.Write("world", ("world/first/texl.obj", [1]), ("world/second/texl.obj", [2]));
        using ClassicGameDataSource source = new(corpus.Root);
        Assert.Throws<InvalidDataException>(() => source.Find(new("world/texl.obj")));
    }

    [Fact]
    public void DisposeClosesOpenedRootAndNestedArchives()
    {
        using Corpus corpus = new();
        ArchiveData nested = Archive(("test.mim", [1, 2]));
        corpus.Write("field", ("test.fl", nested.Fl), ("test.fi", nested.Fi), ("test.fs", nested.Fs));
        ClassicGameDataSource source = new(corpus.Root);
        Assert.NotNull(source.Find(new("field/test/test.mim")));
        source.Dispose();
        source.Dispose();
        Assert.Throws<ObjectDisposedException>(() => source.Find(new("field/test/test.mim")));
        using FileStream exclusive = new(Path.Combine(corpus.Root, "Data", "lang-en", "field.fs"), FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.True(exclusive.Length > 0);
    }

    private static ArchiveData Archive(params (string Name, byte[] Bytes)[] entries)
    {
        using MemoryStream listing = new();
        using MemoryStream index = new();
        using MemoryStream content = new();
        using BinaryWriter writer = new(index, Encoding.ASCII, true);
        foreach ((string name, byte[] bytes) in entries)
        {
            byte[] line = Encoding.ASCII.GetBytes("c:\\ff8\\data\\" + name.Replace('/', '\\') + "\r\n");
            listing.Write(line);
            writer.Write((uint)bytes.Length);
            writer.Write((uint)content.Position);
            writer.Write(0);
            content.Write(bytes);
        }
        return new(listing.ToArray(), index.ToArray(), content.ToArray());
    }

    private sealed record ArchiveData(byte[] Fl, byte[] Fi, byte[] Fs);

    private sealed class Corpus : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "ff8-source-tests-" + Guid.NewGuid().ToString("N"));

        public Corpus()
        {
            Directory.CreateDirectory(Path.Combine(Root, "Data", "lang-en"));
        }

        public void Write(string name, params (string Name, byte[] Bytes)[] entries)
        {
            ArchiveData archive = Archive(entries);
            string stem = Path.Combine(Root, "Data", "lang-en", name);
            File.WriteAllBytes(stem + ".fl", archive.Fl);
            File.WriteAllBytes(stem + ".fi", archive.Fi);
            File.WriteAllBytes(stem + ".fs", archive.Fs);
        }

        public void Dispose()
        {
            Directory.Delete(Root, true);
        }
    }
}
