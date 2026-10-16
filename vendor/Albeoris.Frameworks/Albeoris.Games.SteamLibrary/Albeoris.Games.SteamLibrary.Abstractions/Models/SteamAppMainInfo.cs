namespace Albeoris.Games.SteamLibrary.Abstractions.Models;

/// <summary>
/// Contains the identity and installation metadata from a Steam application manifest.
/// </summary>
public sealed class SteamAppMainInfo
{
    /// <summary>
    /// Initializes application identity information.
    /// </summary>
    /// <param name="appId">The Steam application identifier.</param>
    /// <param name="universe">The Steam universe identifier.</param>
    /// <param name="state">The application state flags.</param>
    /// <param name="name">The display name.</param>
    /// <param name="launcherPath">The launcher path stored in the manifest, if any.</param>
    /// <param name="installationDirectoryName">The directory name below <c>steamapps/common</c>.</param>
    public SteamAppMainInfo(
        UInt32 appId,
        UInt64 universe,
        SteamAppState state,
        String name,
        String? launcherPath,
        String installationDirectoryName)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(installationDirectoryName);

        AppId = appId;
        Universe = universe;
        State = state;
        Name = name;
        LauncherPath = launcherPath;
        InstallationDirectoryName = installationDirectoryName;
    }

    /// <summary>Gets the Steam application identifier.</summary>
    public UInt32 AppId { get; }

    /// <summary>Gets the Steam universe identifier.</summary>
    public UInt64 Universe { get; }

    /// <summary>Gets the application state flags.</summary>
    public SteamAppState State { get; }

    /// <summary>Gets the application display name.</summary>
    public String Name { get; }

    /// <summary>Gets the launcher path stored in the manifest, if present.</summary>
    public String? LauncherPath { get; }

    /// <summary>Gets the installation directory name below <c>steamapps/common</c>.</summary>
    public String InstallationDirectoryName { get; }
}
