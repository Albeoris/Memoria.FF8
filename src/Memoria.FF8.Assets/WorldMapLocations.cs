using Memoria.FF8.Resources;

namespace Memoria.FF8.Assets;

/// <summary>Original WMSET section18, indexed by native centered world coordinates.</summary>
public sealed class WorldMapLocations
{
    private readonly byte[] locations;

    public WorldMapLocations(byte[] wmset)
    {
        int[] sections = WorldAssetReader.ReadSections(wmset);
        if (sections[19] - sections[18] != WorldAssets.Columns * WorldAssets.Rows + 4)
        {
            throw new InvalidDataException("Invalid native world map-location table.");
        }
        locations = wmset.AsSpan(sections[18], WorldAssets.Columns * WorldAssets.Rows).ToArray();
    }

    public static WorldMapLocations ReadInstalled(string gameDirectory)
    {
        using IResourceSource provider = GameDataSourceFactory.Open(gameDirectory);
        return Read(new ResourceService(provider));
    }

    public static WorldMapLocations Read(ResourceService resources)
    {
        ArgumentNullException.ThrowIfNull(resources);
        return new WorldMapLocations(resources.Read(NativeResourceNames.WorldSettings(resources.ResourceLanguage)));
    }

    public short GetPlaceId(int x, int rawZ)
    {
        if (x is < -131072 or >= 131072 || rawZ is < -98304 or >= 98304)
        {
            throw new InvalidDataException("Invalid native world map-location coordinates.");
        }
        // Native542DA0 binds dword20400BC; native549E80 stores its signed byte in word1CFDC5C.
        int segment = 32 * ((rawZ + 294912) % 196608 / 8192) + (x + 393216) % 262144 / 8192;
        return unchecked((sbyte)locations[segment]);
    }
}
