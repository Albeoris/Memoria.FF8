using System.Runtime.Versioning;
using System.Security;
using Microsoft.Win32;

namespace Albeoris.Games.SteamLibrary.Internal;

[SupportedOSPlatform("windows")]
internal static class SteamInstallationLocator
{
    public static String? Find()
    {
        IEnumerable<String?> candidates =
        [
            ReadRegistryValue(RegistryHive.CurrentUser, RegistryView.Default, @"SOFTWARE\Valve\Steam", "SteamPath"),
            ReadRegistryValue(RegistryHive.LocalMachine, RegistryView.Registry32, @"SOFTWARE\Valve\Steam", "InstallPath"),
            ReadRegistryValue(RegistryHive.LocalMachine, RegistryView.Registry64, @"SOFTWARE\Valve\Steam", "InstallPath"),
            CombineIfPresent(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam"),
            CombineIfPresent(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Steam"),
        ];

        return candidates
            .Where(path => !String.IsNullOrWhiteSpace(path))
            .Select(path => Path.GetFullPath(path!))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(IsSteamDirectory);
    }

    private static String? ReadRegistryValue(
        RegistryHive hive,
        RegistryView view,
        String subKeyName,
        String valueName)
    {
        try
        {
            using RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, view);
            using RegistryKey? subKey = baseKey.OpenSubKey(subKeyName);
            return subKey?.GetValue(valueName) as String;
        }
        catch (Exception exception) when (exception is IOException or SecurityException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static String? CombineIfPresent(String parent, String child)
    {
        return String.IsNullOrWhiteSpace(parent) ? null : Path.Combine(parent, child);
    }

    private static Boolean IsSteamDirectory(String path)
    {
        return Directory.Exists(path) && File.Exists(Path.Combine(path, "steamapps", "libraryfolders.vdf"));
    }
}
