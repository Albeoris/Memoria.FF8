namespace Albeoris.Games.SteamLibrary.Abstractions.Models;

/// <summary>
/// Combines a Steam application manifest with its resolved installation paths.
/// </summary>
public sealed class SteamApplication
{
    /// <summary>
    /// Initializes an installed Steam application.
    /// </summary>
    /// <param name="manifest">The parsed application manifest.</param>
    /// <param name="steamExecutablePath">The absolute path of the Steam executable.</param>
    /// <param name="steamAppsDirectoryPath">The absolute path of the containing <c>steamapps</c> directory.</param>
    public SteamApplication(
        SteamAppManifest manifest,
        String steamExecutablePath,
        String steamAppsDirectoryPath)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentException.ThrowIfNullOrWhiteSpace(steamExecutablePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(steamAppsDirectoryPath);

        Manifest = manifest;
        SteamExecutablePath = steamExecutablePath;
        SteamAppsDirectoryPath = steamAppsDirectoryPath;
    }

    /// <summary>Gets the parsed application manifest.</summary>
    public SteamAppManifest Manifest { get; }

    /// <summary>Gets the Steam application identifier.</summary>
    public UInt32 AppId => Manifest.Main.AppId;

    /// <summary>Gets the application display name.</summary>
    public String Name => Manifest.Main.Name;

    /// <summary>Gets the absolute path of the Steam executable.</summary>
    public String SteamExecutablePath { get; }

    /// <summary>Gets the absolute path of the containing <c>steamapps</c> directory.</summary>
    public String SteamAppsDirectoryPath { get; }

    /// <summary>Gets the resolved absolute application installation path.</summary>
    public String InstallationDirectoryPath => System.IO.Path.Combine(
        SteamAppsDirectoryPath,
        "common",
        Manifest.Main.InstallationDirectoryName);

    /// <summary>Gets the Steam uninstall URI for the application.</summary>
    public Uri UninstallationUri => new($"steam://uninstall/{AppId}");

    /// <summary>Gets a command line that asks Steam to uninstall the application.</summary>
    public String UninstallationCommandLine => $"\"{SteamExecutablePath}\" {UninstallationUri}";
}
