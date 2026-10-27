using System.Diagnostics;
using Memoria.FF8;
using Memoria.FF8.Resources;

namespace Memoria.Launcher;

internal sealed record GameInstallation(string Directory, string Release)
{
    public GamePlatformContext ResolvePlatformContext(bool localOnly = false) => GamePlatformContextResolver.Resolve(Directory, localOnly);

    public override string ToString() => $"{Release} � {Directory}";
}

internal static class GameInstallationSelection
{
    public static IReadOnlyList<GameInstallation> Order(string? lastGameDirectory, IEnumerable<GameInstallation> discovered)
    {
        List<GameInstallation> result = [];
        HashSet<string> seen = new(LauncherCapabilities.PathComparer);
        if (Normalize(lastGameDirectory) is string previous)
        {
            result.Add(new GameInstallation(previous, "Last used"));
            seen.Add(previous);
        }
        foreach (GameInstallation installation in discovered)
        {
            if (Normalize(installation.Directory) is string path && seen.Add(path))
            {
                result.Add(installation with { Directory = path });
            }
        }
        return result;
    }

    public static string? Normalize(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }
        try
        {
            string fullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Environment.ExpandEnvironmentVariables(path.Trim().Trim('"'))));
            return System.IO.Directory.Exists(fullPath) ? fullPath : null;
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or NotSupportedException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return null;
        }
    }

    public static string? ValidateForEngine(string path)
    {
        if (Normalize(path) is null)
        {
            return "Choose an installed FINAL FANTASY VIII folder.";
        }
        if (File.Exists(Path.Combine(path, "main.zzz")) || File.Exists(Path.Combine(path, "other.zzz")) || File.Exists(Path.Combine(path, "FFVIII_EN.exe.dat")))
        {
            foreach (string relative in new[] { "main.zzz", "other.zzz", "FFVIII_EN.exe.dat" })
            {
                if (!File.Exists(Path.Combine(path, relative)))
                {
                    return $"The selected Remastered installation is missing {relative}. Select the complete English Remastered game folder.";
                }
            }
            return null;
        }
        if (!File.Exists(Path.Combine(path, "FF8_EN.exe")))
        {
            return "Select English Steam (2013) game files including FF8_EN.exe and Data/lang-en, or English Remastered game files including main.zzz, other.zzz and FFVIII_EN.exe.dat. Remastered support is partial; unimplemented engine features stop with an explicit error.";
        }
        foreach (string archive in new[] { "main", "menu", "field", "battle", "world", "magic" })
        {
            foreach (string extension in new[] { ".fs", ".fi", ".fl" })
            {
                string relative = Path.Combine("Data", "lang-en", archive + extension);
                if (!File.Exists(Path.Combine(path, relative)))
                {
                    return $"The selected installation is missing {relative}. Select the complete English Steam (2013) game folder.";
                }
            }
        }
        return null;
    }

    public static ProcessStartInfo CreateStartInfo(string packageDirectory, string gameDirectory, bool achievementsLocalOnly = false, string? mediaToolsDirectory = null)
    {
        string dataDirectory = Path.Combine(packageDirectory, "Data");
        ProcessStartInfo result = new(Path.Combine(dataDirectory, "Memoria.FF8.exe"))
        {
            WorkingDirectory = dataDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8
        };
        result.ArgumentList.Add("--game-dir");
        result.ArgumentList.Add(Path.GetFullPath(gameDirectory));
        result.ArgumentList.Add("--mods-dir");
        result.ArgumentList.Add(Path.GetFullPath(Path.Combine(packageDirectory, "Mods")));
        result.ArgumentList.Add("--media-tools");
        result.ArgumentList.Add(MediaToolsInstallation.ResolveDirectory(mediaToolsDirectory));
        if (achievementsLocalOnly)
        {
            result.ArgumentList.Add("--achievements-local-only");
        }
        return result;
    }
}
