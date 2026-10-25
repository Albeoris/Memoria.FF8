using Memoria.FF8.Resources;

namespace Memoria.FF8.Mods.Tests;

public sealed class ResourceTrafficTelemetryTests
{
    [Fact]
    public void Staging_counts_payload_and_publication_without_counting_a_cached_source_as_a_copy()
    {
        using InMemoryResourceSource source = new(new("memory", GameDataEdition.Classic, []));
        ResourceKey key = new("battle/sample.dat");
        source.Set(key, [1, 2, 3, 4], "dat");
        ResourceService resources = new(source);
        Dictionary<int, long> bytes = [];
        using (ResourceTrafficTelemetry.BeginScope((site, count) => bytes[site] = checked(bytes.GetValueOrDefault(site) + count)))
        {
            Assert.Equal(new byte[] { 1, 2, 3, 4 }, resources.Read(key));
        }
        Assert.Equal(4, bytes[1502]);
        Assert.Equal(4, bytes[1503]);
        Assert.False(bytes.ContainsKey(1501));
        _ = resources.Read(key);
        Assert.Equal(4, bytes[1503]);
    }
}
