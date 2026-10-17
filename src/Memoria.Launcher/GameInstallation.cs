using System.Diagnostics;

namespace Memoria.Launcher;

internal sealed record GameInstallation(string Directory, string Release)
{
    public override string ToString() => $"{Release} — {Directory}";
}

internal static class GameInstallationSelection
{
    public static IReadOnlyList<GameInstallation> Order(string? lastGameDirectory, IEnumerable<GameInstallation> discovered)
    {
        List<GameInstallation> result = [];
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
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
        if (!File.Exists(Path.Combine(path, "FF8_EN.exe")))
        {
            return "This engine currently requires the English Steam (2013) edition, including FF8_EN.exe and Data/lang-en. Select that installation folder. The original PC and Remastered editions cannot be launched by this engine yet.";
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

    public static ProcessStartInfo CreateStartInfo(string packageDirectory, string gameDirectory)
    {
        string dataDirectory = Path.Combine(packageDirectory, "Data");
        ProcessStartInfo result = new(Path.Combine(dataDirectory, "Memoria.FF8.exe"))
        {
            WorkingDirectory = dataDirectory,
            UseShellExecute = false
        };
        result.ArgumentList.Add("--game-dir");
        result.ArgumentList.Add(Path.GetFullPath(gameDirectory));
        result.ArgumentList.Add("--media-tools");
        result.ArgumentList.Add(Path.Combine(dataDirectory, "media-tools"));
        return result;
    }
}
