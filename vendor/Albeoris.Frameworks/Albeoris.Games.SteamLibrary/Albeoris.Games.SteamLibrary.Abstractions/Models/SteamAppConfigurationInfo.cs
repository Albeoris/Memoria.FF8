namespace Albeoris.Games.SteamLibrary.Abstractions.Models;

/// <summary>
/// Contains the user and mounted configuration sections of a Steam application manifest.
/// </summary>
public sealed class SteamAppConfigurationInfo
{
    /// <summary>
    /// Initializes application configuration information.
    /// </summary>
    /// <param name="user">The user configuration section.</param>
    /// <param name="mounted">The mounted configuration section.</param>
    public SteamAppConfigurationInfo(SteamConfigurationValues user, SteamConfigurationValues mounted)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(mounted);

        User = user;
        Mounted = mounted;
    }

    /// <summary>Gets the user configuration section.</summary>
    public SteamConfigurationValues User { get; }

    /// <summary>Gets the mounted configuration section.</summary>
    public SteamConfigurationValues Mounted { get; }
}
