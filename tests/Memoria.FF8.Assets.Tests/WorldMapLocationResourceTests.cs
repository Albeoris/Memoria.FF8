using System.Buffers.Binary;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Assets.Tests;

public sealed class WorldMapLocationResourceTests
{
    [Fact]
    public void ActiveResourceSelectionSuppliesPlaceMapWithoutReopeningInstallation()
    {
        using LocationSource source = new();
        ResourceService resources = new(source);
        ResourceKey key = new("world/wmsetus.obj");
        resources.Prepare(null, encodedResources: [new EncodedResource(key, "obj", 1, "fixture", Map(254))]).Commit();
        WorldMapLocations locations = WorldMapLocations.Read(resources);
        Assert.Equal(-2, locations.GetPlaceId(0, 0));
        Assert.Equal(0, source.Reads);
        ResourceReloadRequiredException failure = Assert.Throws<ResourceReloadRequiredException>(() => resources.Prepare(null, encodedResources: []));
        Assert.Contains(key, failure.Resources);
        Assert.Equal(-2, locations.GetPlaceId(0, 0));
    }

    private static byte[] Map(byte place)
    {
        const int start = 196;
        int end = start + WorldAssets.Columns * WorldAssets.Rows + 4;
        byte[] bytes = new byte[end + 1];
        for (int section = 0; section < 48; section++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(section * 4), section <= 18 ? start : end);
        }
        bytes.AsSpan(start, WorldAssets.Columns * WorldAssets.Rows).Fill(place);
        return bytes;
    }

    private sealed class LocationSource : IResourceSource
    {
        public ResourceSourceDescriptor Descriptor { get; } = new("memory", GameDataEdition.Classic, []);
        public int Reads { get; private set; }

        public ResourceContent? Find(ResourceKey key)
        {
            Reads++;
            throw new InvalidOperationException("Selected map must come from the active resource publication.");
        }

        public void Dispose()
        {
        }
    }
}
