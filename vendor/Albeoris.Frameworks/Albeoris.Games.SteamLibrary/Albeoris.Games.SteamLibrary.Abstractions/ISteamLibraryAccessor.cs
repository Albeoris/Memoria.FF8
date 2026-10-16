using Albeoris.Games.SteamLibrary.Abstractions.Models;

namespace Albeoris.Games.SteamLibrary.Abstractions;

/// <summary>
/// Provides read-only access to the applications installed in a Steam library.
/// </summary>
public interface ISteamLibraryAccessor
{
    /// <summary>
    /// Gets the absolute path of the Steam installation directory.
    /// </summary>
    String SteamDirectoryPath { get; }

    /// <summary>
    /// Gets the absolute path of the Steam executable.
    /// </summary>
    String SteamExecutablePath { get; }

    /// <summary>
    /// Reads the library folders registered by the Steam installation.
    /// </summary>
    /// <returns>The registered library folders in descriptor order.</returns>
    IReadOnlyList<SteamLibraryFolder> ReadLibraryFolders();

    /// <summary>
    /// Enumerates installed applications whose manifest files are present.
    /// </summary>
    /// <returns>The installed Steam applications.</returns>
    IEnumerable<SteamApplication> EnumerateInstalledApplications();

    /// <summary>
    /// Finds an installed application by its Steam application identifier.
    /// </summary>
    /// <param name="appId">The Steam application identifier.</param>
    /// <returns>The application, or <c>null</c> when it is not installed.</returns>
    SteamApplication? FindApplicationById(UInt32 appId);
}
