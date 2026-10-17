using Memoria.FF8.Resources;

namespace Memoria.FF8.Mods.Tests;

public sealed class ResourcePrefixTests
{
    [Fact]
    public void PrefixReadsTheRequestedHeaderAndPreservesItsFullResourceLength()
    {
        using InMemoryResourceSource source = new(new("memory", GameDataEdition.Classic, []));
        ResourceKey key = new("field/sample.jsm");
        source.Set(key, [1, 2, 3, 4, 5, 6], "jsm");
        ResourceService resources = new(source);
        ResourcePrefix prefix = resources.ReadPrefix(key, 2);
        Assert.Equal(new byte[] { 1, 2 }, prefix.Bytes);
        Assert.Equal(6, prefix.Length);
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6 }, resources.Read(key));
        Assert.Empty(resources.ReadPrefix(key, 0).Bytes);
        Assert.Throws<ArgumentOutOfRangeException>(() => resources.ReadPrefix(key, -1));
    }

    [Fact]
    public void OptimizedPrefixDoesNotOpenOrMaterializeTheWholeResource()
    {
        using PrefixSource source = new(new ResourcePrefix([1, 2], 1000000));
        ResourceService resources = new(source);
        ResourcePrefix prefix = resources.ReadPrefix(new("field/sample.jsm"), 2);
        Assert.Equal(new byte[] { 1, 2 }, prefix.Bytes);
        Assert.Equal(1000000, prefix.Length);
        Assert.Equal(0, source.FullReads);
    }

    [Fact]
    public void InvalidOptimizedBoundsAreRejectedBeforeTheirBytesArePublished()
    {
        foreach (ResourcePrefix prefix in new[] { new ResourcePrefix([1, 2, 3], 3), new ResourcePrefix([1, 2], 1), new ResourcePrefix([], -1) })
        {
            using PrefixSource source = new(prefix);
            ResourceService resources = new(source);
            Assert.Throws<InvalidDataException>(() => resources.ReadPrefix(new("field/sample.jsm"), 2));
            Assert.Equal(0, source.FullReads);
        }
    }

    private sealed class PrefixSource(ResourcePrefix prefix) : IResourceSource, IResourcePrefixSource
    {
        public ResourceSourceDescriptor Descriptor { get; } = new("prefix", GameDataEdition.Classic, []);
        public int FullReads { get; private set; }

        public ResourcePrefix? ReadPrefix(ResourceKey key, int maximumBytes)
        {
            return prefix;
        }

        public ResourceContent? Find(ResourceKey key)
        {
            FullReads++;
            throw new InvalidOperationException("A header request must not materialize this resource.");
        }

        public void Dispose()
        {
        }
    }
}
