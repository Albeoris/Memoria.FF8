using System.Text;
using Memoria.FF8.Assets.Remastered;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Assets.Tests;

public sealed class ClassicGameDataSourceConcurrencyTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CachedArchiveContentsProvideIndependentOverlappingStreams(bool remastered)
    {
        using Corpus corpus = new();
        corpus.Write("main", ("sysfnt.tdw", [1, 2, 3]), ("namedic.bin", [4, 5, 6]));
        using IResourceSource source = corpus.Open(remastered);
        ResourceContent font = Assert.IsType<ResourceContent>(source.Find(new("main/sysfnt.tdw")));
        ResourceContent dictionary = Assert.IsType<ResourceContent>(source.Find(new("main/namedic.bin")));
        using Stream first = font.OpenRead();
        Assert.Equal(1, first.ReadByte());
        using Stream second = dictionary.OpenRead();
        using Stream repeated = font.OpenRead();
        Assert.Equal(new byte[] { 4, 5, 6 }, ReadRemaining(second));
        Assert.Equal(new byte[] { 1, 2, 3 }, ReadRemaining(repeated));
        Assert.Equal(new byte[] { 2, 3 }, ReadRemaining(first));
    }

    [Fact]
    public void NestedPrefixReadCanOverlapAnOuterArchiveContentStream()
    {
        using Corpus corpus = new();
        ArchiveData nested = Archive(("room.inf", [7, 8, 9, 10]));
        corpus.Write("field", ("other.bin", [1, 2, 3]), ("room.fl", nested.Fl), ("room.fi", nested.Fi), ("room.fs", nested.Fs));
        using ClassicGameDataSource source = new(corpus.Root);
        using Stream other = Assert.IsType<ResourceContent>(source.Find(new("field/other.bin"))).OpenRead();
        Assert.Equal(1, other.ReadByte());
        ResourcePrefix prefix = Assert.IsType<ResourcePrefix>(source.ReadPrefix(new("field/room/room.inf"), 2));
        Assert.Equal(new byte[] { 7, 8 }, prefix.Bytes);
        Assert.Equal(4, prefix.Length);
        Assert.Equal(new byte[] { 2, 3 }, ReadRemaining(other));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OpenContentSnapshotSurvivesSourceDisposalButNewOpensAreRejected(bool remastered)
    {
        using Corpus corpus = new();
        corpus.Write("main", ("namedic.bin", [4, 5, 6]));
        using IResourceSource source = corpus.Open(remastered);
        ResourceContent content = Assert.IsType<ResourceContent>(source.Find(new("main/namedic.bin")));
        using Stream stream = content.OpenRead();
        Assert.Equal(4, stream.ReadByte());
        source.Dispose();
        Assert.Equal(new byte[] { 5, 6 }, ReadRemaining(stream));
        Assert.Throws<ObjectDisposedException>(() => content.OpenRead());
        string archivePath = remastered ? Path.Combine(corpus.Root, "main.zzz") : Path.Combine(corpus.Root, "Data", "lang-en", "main.fs");
        using FileStream exclusive = new(archivePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.True(exclusive.Length > 0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ParallelReadersResolveAndReadTheSameArchiveWithoutCrossingPayloads(bool remastered)
    {
        using Corpus corpus = new();
        byte[] font = Enumerable.Repeat((byte)17, 65536).ToArray();
        byte[] dictionary = Enumerable.Repeat((byte)39, 32768).ToArray();
        corpus.Write("main", ("sysfnt.tdw", font), ("namedic.bin", dictionary));
        using IResourceSource source = corpus.Open(remastered);
        using Barrier start = new(8);
        Task[] readers = Enumerable.Range(0, 8).Select(index => Task.Run(() =>
        {
            Assert.True(start.SignalAndWait(TimeSpan.FromSeconds(20)));
            ResourceService resources = new(source);
            for (int iteration = 0; iteration < 20; iteration++)
            {
                bool readFont = (index + iteration) % 2 == 0;
                Assert.Equal(readFont ? font : dictionary, resources.Read(new(readFont ? "main/sysfnt.tdw" : "main/namedic.bin")));
            }
        })).ToArray();
        await Task.WhenAll(readers);
    }

    private static byte[] ReadRemaining(Stream stream)
    {
        using MemoryStream bytes = new();
        stream.CopyTo(bytes);
        return bytes.ToArray();
    }

    private static ArchiveData Archive(params (string Name, byte[] Bytes)[] entries)
    {
        using MemoryStream listing = new();
        using MemoryStream index = new();
        using MemoryStream content = new();
        using BinaryWriter writer = new(index, Encoding.ASCII, true);
        foreach ((string name, byte[] bytes) in entries)
        {
            listing.Write(Encoding.ASCII.GetBytes("c:\\ff8\\data\\" + name.Replace('/', '\\') + "\r\n"));
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
        private readonly List<(string Name, byte[] Bytes)> mounted = [];
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "ff8-stream-tests-" + Guid.NewGuid().ToString("N"));

        public Corpus()
        {
            Directory.CreateDirectory(Path.Combine(Root, "Data", "lang-en"));
        }

        public void Write(string family, params (string Name, byte[] Bytes)[] entries)
        {
            ArchiveData archive = Archive(entries);
            string stem = Path.Combine(Root, "Data", "lang-en", family);
            File.WriteAllBytes(stem + ".fl", archive.Fl);
            File.WriteAllBytes(stem + ".fi", archive.Fi);
            File.WriteAllBytes(stem + ".fs", archive.Fs);
            mounted.Add(($"data/lang-en/{family}.fl", archive.Fl));
            mounted.Add(($"data/lang-en/{family}.fi", archive.Fi));
            mounted.Add(($"data/lang-en/{family}.fs", archive.Fs));
        }

        public IResourceSource Open(bool remastered)
        {
            if (!remastered)
            {
                return new ClassicGameDataSource(Root);
            }
            WriteMounted("main.zzz", mounted);
            WriteMounted("other.zzz", []);
            return new RemasteredGameDataSource(Root);
        }

        private void WriteMounted(string name, IReadOnlyList<(string Name, byte[] Bytes)> entries)
        {
            using FileStream file = File.Create(Path.Combine(Root, name));
            using BinaryWriter writer = new(file, Encoding.UTF8, true);
            writer.Write((uint)entries.Count);
            ulong offset = (ulong)(4 + entries.Sum(entry => 16 + Encoding.UTF8.GetByteCount(entry.Name)));
            foreach ((string path, byte[] bytes) in entries)
            {
                byte[] encoded = Encoding.UTF8.GetBytes(path);
                writer.Write((uint)encoded.Length);
                writer.Write(encoded);
                writer.Write(offset);
                writer.Write((uint)bytes.Length);
                offset += (uint)bytes.Length;
            }
            foreach ((string _, byte[] bytes) in entries)
            {
                writer.Write(bytes);
            }
        }

        public void Dispose()
        {
            Directory.Delete(Root, true);
        }
    }
}
