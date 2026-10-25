using System.Security;
using Albeoris.Games.SteamLibrary;
using Memoria.FF8.Resources;
using Microsoft.Win32;

namespace Memoria.Launcher;

internal static class GameInstallationDiscovery
{
    // Same release IDs and classic registry locations as the FF8 Toolset installation sources.
    private static readonly (RegistryHive Hive, string Key)[] ClassicLocations =
    [
        (RegistryHive.LocalMachine, @"SOFTWARE\Square Soft, Inc\FINAL FANTASY VIII\1.00"),
        (RegistryHive.LocalMachine, @"SOFTWARE\WOW6432Node\Square Soft, Inc\FINAL FANTASY VIII\1.00"),
        (RegistryHive.CurrentUser, @"VirtualStore\Machine\SOFTWARE\Square Soft, Inc\FINAL FANTASY VIII\1.00"),
        (RegistryHive.CurrentUser, @"VirtualStore\Machine\SOFTWARE\Wow6432Node\Square Soft, Inc\FINAL FANTASY VIII\1.00"),
        (RegistryHive.ClassesRoot, @"VirtualStore\MACHINE\SOFTWARE\Wow6432Node\Square Soft, Inc\FINAL FANTASY VIII\1.00"),
        (RegistryHive.CurrentUser, @"Software\Classes\VirtualStore\MACHINE\SOFTWARE\Wow6432Node\Square Soft, Inc\FINAL FANTASY VIII\1.00")
    ];

    public static IReadOnlyList<GameInstallation> Find(Action<string> report)
    {
        List<GameInstallation> result = [];
        foreach ((RegistryHive hive, string path) in ClassicLocations)
        {
            try
            {
                using RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
                using RegistryKey? key = baseKey.OpenSubKey(path);
                if (key?.GetValue("AppPath") is string directory)
                {
                    result.Add(new GameInstallation(directory, "PC (2000)"));
                }
            }
            catch (Exception exception) when (exception is IOException or SecurityException or UnauthorizedAccessException)
            {
                report($"Could not inspect a classic installation: {exception.Message}");
            }
        }
        try
        {
            SteamLibraryAccessor? steam = SteamLibraryAccessor.FindInstalled();
            if (steam is not null)
            {
                foreach ((uint appId, string release) in new[] { (39150u, "Steam (2013)"), (1026680u, "Steam Remastered (2019)") })
                {
                    try
                    {
                        var game = steam.FindApplicationById(appId);
                        if (game is not null)
                        {
                            result.Add(new GameInstallation(game.InstallationDirectoryPath, release));
                        }
                    }
                    catch (Exception exception) when (exception is IOException or SecurityException or UnauthorizedAccessException or FormatException or InvalidOperationException)
                    {
                        report($"Could not inspect {release}: {exception.Message}");
                    }
                }
            }
        }
        catch (Exception exception) when (exception is IOException or SecurityException or UnauthorizedAccessException or FormatException or InvalidOperationException)
        {
            report($"Could not inspect Steam: {exception.Message}");
        }
        try
        {
            using RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);
            using RegistryKey? key = baseKey.OpenSubKey(@"SOFTWARE\GOG.com\Games\1086370078");
            if (key?.GetValue("gameID") is string productId && productId == "1086370078" && key.GetValue("path") is string directory && GameInstallationSelection.Normalize(directory) is string normalized)
            {
                GamePlatformContext context = GamePlatformContextResolver.Resolve(normalized);
                if (context.Provider == GameDistributionProvider.Gog && context.ProductId == productId)
                {
                    result.Add(new GameInstallation(normalized, "GOG Remastered (2019)"));
                }
                else
                {
                    report("The GOG installation's product metadata could not be validated. Browse to select it for local play.");
                }
            }
        }
        catch (Exception exception) when (exception is IOException or SecurityException or UnauthorizedAccessException)
        {
            report($"Could not inspect GOG: {exception.Message}");
        }
        return result;
    }
}
