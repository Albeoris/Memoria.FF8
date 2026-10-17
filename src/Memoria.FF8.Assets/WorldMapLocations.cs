using Memoria.FF8.Resources;
using Albeoris.Games.FF8.FlArchives;
using Albeoris.Games.FF8.FlArchives.Abstractions;

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
        using IFlArchive archive = (provider as IGameArchiveSource ?? throw new NotSupportedException("The resource source does not support archive enumeration.")).OpenArchive("world");
        IFlArchiveEntry entry = archive.Entries.Single(entry => Path.GetFileName(entry.RelativePath).Equals("wmsetus.obj", StringComparison.OrdinalIgnoreCase));
        using Stream source = entry.OpenForRead();
        using MemoryStream buffer = new();
        source.CopyTo(buffer);
        return new WorldMapLocations(buffer.ToArray());
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
