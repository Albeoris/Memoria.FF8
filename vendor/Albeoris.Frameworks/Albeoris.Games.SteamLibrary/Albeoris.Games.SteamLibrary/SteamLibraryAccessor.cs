using System.Runtime.Versioning;
using Albeoris.Games.SteamLibrary.Abstractions;
using Albeoris.Games.SteamLibrary.Abstractions.Models;
using Albeoris.Games.SteamLibrary.Internal;

namespace Albeoris.Games.SteamLibrary;

/// <summary>
/// Provides read-only access to applications registered in a Steam installation.
/// </summary>
public sealed class SteamLibraryAccessor : ISteamLibraryAccessor
{
    /// <summary>
    /// Initializes an accessor for an existing Steam installation directory.
    /// </summary>
    /// <param name="steamDirectoryPath">The Steam installation directory.</param>
    /// <exception cref="DirectoryNotFoundException">
    /// The specified Steam installation directory does not exist.
    /// </exception>
    public SteamLibraryAccessor(String steamDirectoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(steamDirectoryPath);

        String fullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(steamDirectoryPath));
        if (!Directory.Exists(fullPath))
            throw new DirectoryNotFoundException($"Steam directory '{fullPath}' does not exist.");

        SteamDirectoryPath = fullPath;
        SteamExecutablePath = Path.Combine(fullPath, "steam.exe");
    }

    /// <summary>
    /// Gets the absolute path of the Steam installation directory.
    /// </summary>
    public String SteamDirectoryPath { get; }

    /// <summary>
    /// Gets the absolute path of the Steam executable.
    /// </summary>
    public String SteamExecutablePath { get; }

    /// <summary>
    /// Locates the current Windows Steam installation using registry and conventional paths.
    /// </summary>
    /// <returns>An accessor for the installation, or <c>null</c> when Steam cannot be located.</returns>
    [SupportedOSPlatform("windows")]
    public static SteamLibraryAccessor? FindInstalled()
    {
        String? path = SteamInstallationLocator.Find();
        return path is null ? null : new SteamLibraryAccessor(path);
    }

    /// <inheritdoc />
    public IReadOnlyList<SteamLibraryFolder> ReadLibraryFolders()
    {
        return SteamLibraryFoldersParser.Parse(GetLibraryDescriptorPath());
    }

    /// <inheritdoc />
    public IEnumerable<SteamApplication> EnumerateInstalledApplications()
    {
        HashSet<UInt32> visitedAppIds = [];

        foreach (SteamLibraryFolder folder in ReadLibraryFolders())
        {
            String steamAppsDirectoryPath = Path.Combine(folder.Path, "steamapps");
            if (!Directory.Exists(steamAppsDirectoryPath))
                continue;

            foreach (UInt32 appId in folder.Applications.Keys.Order())
            {
                if (visitedAppIds.Contains(appId))
                    continue;

                String manifestPath = GetManifestPath(steamAppsDirectoryPath, appId);
                if (!File.Exists(manifestPath))
                    continue;

                visitedAppIds.Add(appId);
                yield return CreateApplication(manifestPath, steamAppsDirectoryPath);
            }
        }
    }

    /// <inheritdoc />
    public SteamApplication? FindApplicationById(UInt32 appId)
    {
        foreach (SteamLibraryFolder folder in ReadLibraryFolders())
        {
            if (!folder.Applications.ContainsKey(appId))
                continue;

            String steamAppsDirectoryPath = Path.Combine(folder.Path, "steamapps");
            String manifestPath = GetManifestPath(steamAppsDirectoryPath, appId);
            if (File.Exists(manifestPath))
                return CreateApplication(manifestPath, steamAppsDirectoryPath);
        }

        return null;
    }

    private String GetLibraryDescriptorPath()
    {
        return Path.Combine(SteamDirectoryPath, "steamapps", "libraryfolders.vdf");
    }

    private static String GetManifestPath(String steamAppsDirectoryPath, UInt32 appId)
    {
        return Path.Combine(steamAppsDirectoryPath, $"appmanifest_{appId}.acf");
    }

    private SteamApplication CreateApplication(String manifestPath, String steamAppsDirectoryPath)
    {
        SteamAppManifest manifest = SteamAppManifestParser.Parse(manifestPath);
        return new SteamApplication(manifest, SteamExecutablePath, steamAppsDirectoryPath);
    }
}
