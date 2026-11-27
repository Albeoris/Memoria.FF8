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
        using IResourceSource provider = GameDataSourceFactory.Open(gameDirectory);
        return Read(gameDirectory, selectedDisc, new ResourceService(provider));
    }

    public static NativeSteamDiscMount Read(string gameDirectory, byte selectedDisc, ResourceService resources)
    {
        if (selectedDisc is < 1 or > 4)
        {
            throw new NotSupportedException("Steam DISCJUMP requires a selected disc from 1 through 4.");
        }
        NativeFieldRuntimeProfile profile = resources.GetSource<IGameRuntimeSource>() is { } runtime ? runtime.RequireNativeRuntimeProfile() : throw new NotSupportedException("Steam DISCJUMP requires an authenticated edition runtime provider.");
        ResourceKey wrapperKey = new("runtime/disc-wrapper");
        ResourceContent wrapperContent = resources.GetSource<IResourceSource>()?.Find(wrapperKey) ?? throw new FileNotFoundException("The authenticated disc wrapper is unavailable.");
        using Stream wrapperStream = wrapperContent.OpenRead();
        string wrapper = Convert.ToHexString(SHA256.HashData(wrapperStream));
        int version = profile.ExecutableHash == NativeFieldRuntimeProfile.RemasteredEnImageHash ? 2 : 1;
        if (version == 2 && (resources.GetSource<Remastered.RemasteredGameDataSource>()?.ContainsExternalData("data/disk/wrong_disk") == true || File.Exists(Path.Combine(gameDirectory, "data", "disk", "WRONG_DISK"))))
        {
            throw new NotSupportedException("Remastered DISCJUMP requires the rejected-marker path to remain unavailable.");
        }
        bool verifiedWrapper = version == 2 ? NativeSteamDiscMount.IsVerifiedRemasteredWrapper(wrapper) : wrapper == NativeSteamDiscMount.VerifiedWrapperSha256;
        if (!verifiedWrapper)
        {
            throw new NotSupportedException("Steam DISCJUMP requires a verified disc callback wrapper for the selected edition.");
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
