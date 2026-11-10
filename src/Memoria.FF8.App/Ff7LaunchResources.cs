using System.Security;
using Albeoris.Games.SteamLibrary;
using Memoria.FF7.Resources;
using Memoria.FF8.Platform;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal static class Ff7LaunchResources
{
    private const uint SupportedSteamAppId = 3837340;

    internal static LaunchOptions Resolve(LaunchOptions options)
    {
        string settingsPath = options.SettingsDirectory is string directory
            ? Path.Combine(directory, Path.GetFileName(LauncherSettings.DefaultPath))
            : LauncherSettings.DefaultPath;
        return Resolve(options, settingsPath, FindSteamInstallation);
    }

    internal static LaunchOptions Resolve(LaunchOptions options, string settingsPath, Func<string?> findSteamInstallation)
    {
        if (!string.IsNullOrWhiteSpace(options.Ff7GameDirectory))
        {
            return options;
        }
        string? selected = LauncherSettings.Load(settingsPath).Ff7GameDirectory;
        string? directory = string.IsNullOrWhiteSpace(selected) ? findSteamInstallation() : selected;
        return options with { Ff7GameDirectory = directory };
    }

    private static string? FindSteamInstallation()
    {
        if (OperatingSystem.IsWindows())
        {
            try
            {
                return FindSupportedInstallation(SteamLibraryAccessor.FindInstalled());
            }
            catch (Exception exception) when (IsDiscoveryFailure(exception))
            {
                return null;
            }
        }
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string[] roots = OperatingSystem.IsMacOS()
            ? [Path.Combine(home, "Library", "Application Support", "Steam")]
            : [Path.Combine(home, ".steam", "steam"), Path.Combine(home, ".local", "share", "Steam"), Path.Combine(home, ".var", "app", "com.valvesoftware.Steam", ".local", "share", "Steam")];
        foreach (string root in roots.Where(Directory.Exists))
        {
            try
            {
                if (FindSupportedInstallation(new SteamLibraryAccessor(root)) is string directory)
                {
                    return directory;
                }
            }
            catch (Exception exception) when (IsDiscoveryFailure(exception))
            {
                // Other Steam roots may still expose the supported installation.
            }
        }
        return null;
    }

    private static string? FindSupportedInstallation(SteamLibraryAccessor? steam)
    {
        string? directory = steam?.FindApplicationById(SupportedSteamAppId)?.InstallationDirectoryPath;
        if (directory is null)
        {
            return null;
        }
        Ff7Installation installation = Ff7Installation.Discover(directory);
        Ff7PcExecutableData.Load(installation);
        return installation.RootDirectory;
    }

    private static bool IsDiscoveryFailure(Exception exception) => exception is IOException or InvalidDataException or SecurityException or UnauthorizedAccessException or FormatException or InvalidOperationException or ArgumentException;
}
