using Memoria.FF8.Resources;
using System.Security.Cryptography;
using Albeoris.Games.FF8.FlArchives;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeSteamDiscMountReader
{
    public static NativeSteamDiscMount Read(string gameDirectory, byte selectedDisc)
    {
        if (selectedDisc is < 1 or > 4)
        {
            throw new NotSupportedException("Steam DISCJUMP requires a selected disc from1 through4.");
        }
        using IResourceSource provider = GameDataSourceFactory.Open(gameDirectory);
        ResourceService resources = new(provider);
        using Stream wrapperStream = resources.Get(new ResourceKey("runtime/disc-wrapper")).OpenRead();
        string wrapper = Convert.ToHexString(SHA256.HashData(wrapperStream));
        if (wrapper != NativeSteamDiscMount.VerifiedWrapperSha256)
        {
            throw new NotSupportedException("Steam DISCJUMP requires the original verified disc callback wrapper.");
        }
        using Stream marker = resources.Get(new ResourceKey($"files/disk/disk{selectedDisc}")).OpenRead();
        long markerLength = marker.Length;
        string markerHash = Convert.ToHexString(SHA256.HashData(marker));
        using Stream table = resources.Get(new ResourceKey("main/wm2field.tbl")).OpenRead();
        string worldHash = Convert.ToHexString(SHA256.HashData(table));
        NativeSteamDiscMount result = new(1, selectedDisc, wrapper, markerLength, markerHash, worldHash);
        result.Validate(selectedDisc);
        return result;
    }

}
