using System.Diagnostics;
using Memoria.FF8.Platform;
using Memoria.FF8;
using Memoria.FF8.Resources;

namespace Memoria.Launcher;

internal sealed record GameInstallation(string Directory, string Release)
{
    public GamePlatformContext ResolvePlatformContext(bool localOnly = false) => GamePlatformContextResolver.Resolve(Directory, localOnly);

    public override string ToString() => $"{Release} · {Directory}";
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
        if (ResolveEdition(path) == GameDataEdition.Remastered)
        {
            foreach (string relative in new[] { "main.zzz", "other.zzz", "FFVIII_EFIGS.dll" })
            {
                if (!File.Exists(Path.Combine(path, relative)))
                {
                    return $"The selected Remastered installation is missing {relative}. Select the complete English Steam Remastered game folder or verify its game files in Steam.";
                }
            }
            return null;
        }
        if (!File.Exists(Path.Combine(path, "FF8_EN.exe")))
        {
            return "Select English Steam (2013) game files including FF8_EN.exe and Data/lang-en, or English Steam Remastered game files including main.zzz, other.zzz and FFVIII_EFIGS.dll. Remastered support is partial.";
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

    internal static GameDataEdition ResolveEdition(string path)
    {
        return File.Exists(Path.Combine(path, "main.zzz")) || File.Exists(Path.Combine(path, "other.zzz")) || File.Exists(Path.Combine(path, "FFVIII_EN.exe.dat")) ? GameDataEdition.Remastered : GameDataEdition.Classic;
    }

    public static ProcessStartInfo CreateStartInfo(string packageDirectory, string gameDirectory, bool achievementsLocalOnly = false, string? mediaToolsDirectory = null, Memoria.FF8.Platform.GraphicsBackend? graphicsBackend = null, Memoria.FF8.Platform.HostPlatform? hostPlatform = null, string? configurationDirectory = null, string? resourceLanguage = null, string? modelSourceDirectory = null, bool redirectOutput = true, string? ff7GameDirectory = null, bool ff7Debug = false, bool openModManager = false)
    {
        if (ff7Debug && string.IsNullOrWhiteSpace(ff7GameDirectory))
        {
            throw new ArgumentException("Select a FINAL FANTASY VII installation before starting its debug title.", nameof(ff7GameDirectory));
        }
        Memoria.FF8.Platform.HostPlatform platform = hostPlatform ?? Memoria.FF8.Platform.HostPlatforms.Current;
        string dataDirectory = Path.Combine(packageDirectory, "Data");
        ProcessStartInfo result = new(Path.Combine(dataDirectory, platform.ExecutableName("Memoria.FF8")))
        {
            WorkingDirectory = dataDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = redirectOutput,
            RedirectStandardError = redirectOutput,
            StandardOutputEncoding = redirectOutput ? System.Text.Encoding.UTF8 : null,
            StandardErrorEncoding = redirectOutput ? System.Text.Encoding.UTF8 : null
        };
        result.ArgumentList.Add("--game-dir");
        result.ArgumentList.Add(Path.GetFullPath(gameDirectory));
        result.Environment.Remove("MEMORIA_FF7_GAME_DIR");
        if (!string.IsNullOrWhiteSpace(ff7GameDirectory))
        {
            result.ArgumentList.Add("--ff7-game-dir");
            result.ArgumentList.Add(Path.GetFullPath(ff7GameDirectory));
        }
        if (ff7Debug)
        {
            result.ArgumentList.Add("--ff7-debug");
        }
        if (openModManager)
        {
            result.ArgumentList.Add("--open-mod-manager");
        }
        result.Environment.Remove("MEMORIA_FF8_MODEL_SOURCE_DIR");
        if (!string.IsNullOrWhiteSpace(modelSourceDirectory))
        {
            result.ArgumentList.Add("--model-source-dir");
            result.ArgumentList.Add(Path.GetFullPath(modelSourceDirectory));
        }
        result.Environment[ApplicationInstallation.ModPackageDirectoryVariable] = Path.GetFullPath(packageDirectory);
        if (configurationDirectory is not null)
        {
            result.ArgumentList.Add("--settings-dir");
            result.ArgumentList.Add(Path.GetFullPath(configurationDirectory));
        }
        result.ArgumentList.Add("--media-tools");
        result.ArgumentList.Add(MediaToolsInstallation.ResolveDirectory(mediaToolsDirectory));
        if (graphicsBackend is Memoria.FF8.Platform.GraphicsBackend backend)
        {
            result.ArgumentList.Add("--graphics-backend");
            result.ArgumentList.Add(backend.ToString());
        }
        if (GameResourceLanguages.Normalize(resourceLanguage) is string language)
        {
            result.ArgumentList.Add("--resource-language");
            result.ArgumentList.Add(language);
        }
        if (achievementsLocalOnly)
        {
            result.ArgumentList.Add("--achievements-local-only");
        }
        return result;
    }
}
