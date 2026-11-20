using System.Security.Cryptography;
using System.Text;
using Memoria.FF8.Platform;

namespace Memoria.FF8.Mods;

public static class ModLibraryProfile
{
    public static string Identity(string root, string gameDirectory, string edition, string language)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(root + "\0" + Path.GetFullPath(gameDirectory) + "\0" + edition + "\0" + language)))[..16].ToLowerInvariant();
    }

    public static ModPackageInstaller CreateInstaller(string root, string gameDirectory, string edition, string language, bool junctionSupported, string preferenceRoot)
    {
        string profile = $"{edition}/{language}/{Identity(root, gameDirectory, edition, language)}";
        return new(ModPackageInstaller.StateDirectoryFor(root), root, profile, junctionSupported, preferenceRoot: preferenceRoot);
    }

    public static string? OfficialDownloadsDirectory(string legacyRoot, ApplicationPaths paths)
    {
        if (Path.GetDirectoryName(legacyRoot) is null)
        {
            return null;
        }
        string state = ModPackageInstaller.StateDirectoryFor(legacyRoot);
        string legacy = Path.Combine(state, "official-downloads");
        return Directory.Exists(legacy) ? legacy : Path.Combine(paths.Cache, "mod-library", Path.GetFileName(state), "official-downloads");
    }
}
