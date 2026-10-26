using System.Security.Cryptography;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Mods.Tests;

public sealed class EncodedResourceStoreTests
{
    private static readonly ResourceKey Key = new("audio/clip.bin");

    [Fact]
    public void SnapshotCopiesAndHashesAStreamOnceWithoutRetainingTheInputBuffer()
    {
        using EncodedResourceStore store = new();
        byte[] chunk = Enumerable.Range(0, 8192).Select(index => (byte)index).ToArray();
        int copies = 0;
        using IncrementalHash expected = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        EncodedResource resource = store.Store(Key, "bin", 7, "selected", destination =>
        {
            copies++;
            for (int index = 0; index < 512; index++)
            {
                destination.Write(chunk);
                expected.AppendData(chunk);
            }
        });
        chunk[0] = 99;

        Assert.Equal(Convert.ToHexString(expected.GetHashAndReset()), resource.Fingerprint);
        Assert.Equal(7, resource.Content.Revision);
        Assert.Equal("selected", resource.Content.Provenance);
        using Stream first = resource.Content.OpenRead();
        using Stream second = resource.Content.OpenRead();
        Assert.IsType<FileStream>(first);
        Assert.Equal(8192L * 512, first.Length);
        Assert.False(first.CanWrite);
        Assert.Equal(0, first.ReadByte());
        Assert.Equal(1, first.ReadByte());
        Assert.Equal(0, second.ReadByte());
        Assert.Equal(1, copies);
    }

    [Fact]
    public void SnapshotIdentitySupportsUnchangedRefreshAndRejectsChangedAcquiredBytes()
    {
        using EncodedResourceStore store = new();
        using InMemoryResourceSource source = new(new("installed", GameDataEdition.Classic, []));
        ResourceService service = new(source);
        EncodedResource first = store.Store(Key, "bin", 1, "first", stream => stream.Write([1, 2, 3]));
        service.Prepare(null, encodedResources: [first]).Commit();
        Assert.Equal(new byte[] { 1, 2 }, service.ReadPrefix(Key, 2).Bytes);
        EncodedResource refreshed = store.Store(Key, "bin", 2, "refreshed", stream => stream.Write([1, 2, 3]));

        service.Prepare(null, encodedResources: [refreshed]).Commit();

        Assert.Equal(first.Fingerprint, refreshed.Fingerprint);
        Assert.Equal("refreshed", service.Get(Key).Provenance);
        EncodedResource changed = store.Store(Key, "bin", 3, "changed", stream => stream.Write([4, 5, 6]));
        Assert.Throws<ResourceReloadRequiredException>(() => service.Prepare(null, encodedResources: [changed]));
        Assert.Equal(new byte[] { 1, 2, 3 }, service.Read(Key));
    }

    [Fact]
    public void FailedCopyRemovesIncompleteFilesAndPreservesPriorSnapshots()
    {
        using EncodedResourceStore store = new();
        EncodedResource first = store.Store(Key, "bin", 1, "first", stream => stream.Write([1, 2, 3]));
        string directory;
        using (FileStream input = Assert.IsType<FileStream>(first.Content.OpenRead()))
        {
            directory = Path.GetDirectoryName(input.Name)!;
        }

        Assert.Throws<IOException>(() => store.Store(Key, "bin", 2, "broken", stream =>
        {
            stream.Write([4, 5]);
            throw new IOException("Selected source changed during copy.");
        }));

        Assert.Single(Directory.GetFiles(directory));
        using Stream retained = first.Content.OpenRead();
        Assert.Equal(1, retained.ReadByte());
        EncodedResource next = store.Store(new("audio/next.bin"), "bin", 3, "next", stream => stream.Write([7]));
        using Stream valid = next.Content.OpenRead();
        Assert.Equal(7, valid.ReadByte());
    }

    [Fact]
    public void EqualContentSharesOneSnapshotAcrossDifferentKeysAndRevisions()
    {
        using EncodedResourceStore store = new();
        EncodedResource first = store.Store(Key, "bin", 1, "first", stream => stream.Write([1, 2, 3]));
        EncodedResource second = store.Store(new("audio/other.bin"), "bin", 2, "second", stream => stream.Write([1, 2, 3]));
        using FileStream firstStream = Assert.IsType<FileStream>(first.Content.OpenRead());
        using FileStream secondStream = Assert.IsType<FileStream>(second.Content.OpenRead());

        Assert.Equal(firstStream.Name, secondStream.Name);
        Assert.Single(Directory.GetFiles(Path.GetDirectoryName(firstStream.Name)!));
        Assert.Equal("first", first.Content.Provenance);
        Assert.Equal("second", second.Content.Provenance);
        Assert.Equal(1, firstStream.ReadByte());
        Assert.Equal(1, secondStream.ReadByte());
    }

    [Fact]
    public void DisposalRemovesOwnedFilesWhileAlreadyOpenedReadersRemainUsable()
    {
        EncodedResourceStore store = new();
        EncodedResource resource = store.Store(Key, "bin", 1, "first", stream => stream.Write([1, 2, 3]), new("audio"));
        using FileStream input = Assert.IsType<FileStream>(resource.Content.OpenRead());
        string path = input.Name;
        Assert.Equal(new ResourceKey("audio"), resource.ExclusiveScope);

        store.Dispose();
        store.Dispose();

        Assert.False(File.Exists(path));
        Assert.False(Directory.Exists(Path.GetDirectoryName(path)));
        Assert.Equal(1, input.ReadByte());
        Assert.Throws<ObjectDisposedException>(() => resource.Content.OpenRead());
        Assert.Throws<ObjectDisposedException>(() => store.Store(Key, "bin", 2, "later", stream => stream.Write([4])));
    }
}
