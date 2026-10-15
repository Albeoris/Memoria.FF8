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
        string wrapper = HashFile(Path.Combine(gameDirectory, "AF3DN.P"));
        if (wrapper != NativeSteamDiscMount.VerifiedWrapperSha256)
        {
            throw new NotSupportedException("Steam DISCJUMP requires the original verified AF3DN disc callback wrapper.");
        }
        //1001F2B0 rejects the other three CD: names before opening a physical file.
        //52D400 has no marker signature check; phase6 succeeds on its nonzero length.
        string markerPath = Path.Combine(gameDirectory, "Data", "disk", $"DISK{selectedDisc}");
        long markerLength;
        string markerHash;
        using (FileStream marker = File.OpenRead(markerPath))
        {
            markerLength = marker.Length;
            markerHash = Convert.ToHexString(SHA256.HashData(marker));
        }
        using IFlArchive main = FlArchive.OpenForRead(Path.Combine(gameDirectory, "Data", "lang-en", "main.fs"), FlArchiveRepresentation.Files);
        IFlArchiveEntry entry = main.Entries.Single(item => item.RelativePath.Replace('\\', '/').Split('/')[^1].Equals("wm2field.tbl", StringComparison.OrdinalIgnoreCase));
        string worldHash;
        using (Stream table = entry.OpenForRead())
        {
            worldHash = Convert.ToHexString(SHA256.HashData(table));
        }
        NativeSteamDiscMount result = new(1, selectedDisc, wrapper, markerLength, markerHash, worldHash);
        result.Validate(selectedDisc);
        return result;
    }

    private static string HashFile(string path)
    {
        using FileStream source = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(source));
    }
}
