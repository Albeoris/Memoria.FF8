namespace Albeoris.Games.SteamLibrary.Abstractions.Models;

/// <summary>
/// Represents the parsed contents of a Steam application manifest.
/// </summary>
public sealed class SteamAppManifest
{
    /// <summary>
    /// Initializes a parsed application manifest.
    /// </summary>
    /// <param name="main">The application identity and state.</param>
    /// <param name="update">The application update metadata.</param>
    /// <param name="storage">The application storage metadata.</param>
    /// <param name="configuration">The application configuration sections.</param>
    public SteamAppManifest(
        SteamAppMainInfo main,
        SteamAppUpdateInfo update,
        SteamAppStorageInfo storage,
        SteamAppConfigurationInfo configuration)
    {
        ArgumentNullException.ThrowIfNull(main);
        ArgumentNullException.ThrowIfNull(update);
        ArgumentNullException.ThrowIfNull(storage);
        ArgumentNullException.ThrowIfNull(configuration);

        Main = main;
        Update = update;
        Storage = storage;
        Configuration = configuration;
    }

    /// <summary>Gets the application identity and state.</summary>
    public SteamAppMainInfo Main { get; }

    /// <summary>Gets the application update metadata.</summary>
    public SteamAppUpdateInfo Update { get; }

    /// <summary>Gets the application storage metadata.</summary>
    public SteamAppStorageInfo Storage { get; }

    /// <summary>Gets the application configuration sections.</summary>
    public SteamAppConfigurationInfo Configuration { get; }
}
