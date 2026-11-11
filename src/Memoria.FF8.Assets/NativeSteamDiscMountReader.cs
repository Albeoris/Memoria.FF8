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
        NativeFieldRuntimeProfile profile = provider is IGameRuntimeSource runtime ? runtime.RequireNativeRuntimeProfile() : throw new NotSupportedException("Steam DISCJUMP requires an authenticated edition runtime provider.");
        ResourceService resources = new(provider);
        using Stream wrapperStream = resources.Get(new ResourceKey("runtime/disc-wrapper")).OpenRead();
        string wrapper = Convert.ToHexString(SHA256.HashData(wrapperStream));
        int version = profile.ExecutableHash == NativeFieldRuntimeProfile.RemasteredEnImageHash ? 2 : 1;
        if (version == 2 && (provider.Find(new ResourceKey("files/disk/wrong_disk")) is not null || File.Exists(Path.Combine(gameDirectory, "data", "disk", "WRONG_DISK"))))
        {
            throw new NotSupportedException("Remastered DISCJUMP requires the rejected-marker path to remain unavailable.");
        }
        string expectedWrapper = version == 2 ? NativeSteamDiscMount.VerifiedRemasteredWrapperSha256 : NativeSteamDiscMount.VerifiedWrapperSha256;
        if (wrapper != expectedWrapper)
        {
            throw new NotSupportedException("Steam DISCJUMP requires the original verified disc callback wrapper.");
        }
        using Stream marker = resources.Get(new ResourceKey($"files/disk/disk{selectedDisc}")).OpenRead();
        long markerLength = marker.Length;
        string markerHash = Convert.ToHexString(SHA256.HashData(marker));
        using Stream table = resources.Get(new ResourceKey("main/wm2field.tbl")).OpenRead();
        string worldHash = Convert.ToHexString(SHA256.HashData(table));
        NativeSteamDiscMount result = new(version, selectedDisc, wrapper, markerLength, markerHash, worldHash);
        result.Validate(selectedDisc, profile.ExecutableHash);
        return result;
    }

}
