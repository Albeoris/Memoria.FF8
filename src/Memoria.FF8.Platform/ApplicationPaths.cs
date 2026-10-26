namespace Memoria.FF8.Platform;

/// <summary>Resolves user storage without touching the filesystem or changing the process working directory.</summary>
public sealed record ApplicationPaths(string Configuration, string Saves, string Cache, string Logs, string LegacyState)
{
    public string LauncherSettings => Path.Combine(Configuration, "launcher.json");
    public string DefaultCheckpoint => Path.Combine(LegacyState, "checkpoint.json");
    public string UpdateStaging => Path.Combine(Cache, "updates");

    public static ApplicationPaths Current => Resolve(HostPlatforms.Current, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Environment.GetEnvironmentVariable);

    public static ApplicationPaths Resolve(HostPlatform platform, string home, string localApplicationData, Func<string, string?> environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentException.ThrowIfNullOrWhiteSpace(home);
        if (platform == HostPlatform.Windows)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(localApplicationData);
            string configuration = Path.Combine(localApplicationData, "Memoria", "FF8");
            return new(configuration, Path.Combine(localApplicationData, "Memoria.FF8", "player-saves"), Path.Combine(configuration, "cache"), Path.Combine(configuration, "logs"), Path.Combine(localApplicationData, "Memoria.FF8"));
        }
        if (platform == HostPlatform.MacOS)
        {
            string data = Path.Combine(home, "Library", "Application Support", "Memoria", "FF8");
            return new(data, Path.Combine(data, "saves"), Path.Combine(home, "Library", "Caches", "Memoria", "FF8"), Path.Combine(home, "Library", "Logs", "Memoria", "FF8"), data);
        }
        if (platform != HostPlatform.Linux)
        {
            throw new ArgumentOutOfRangeException(nameof(platform));
        }
        string config = Path.Combine(Xdg("XDG_CONFIG_HOME", ".config"), "Memoria", "FF8");
        string state = Path.Combine(Xdg("XDG_STATE_HOME", ".local/state"), "Memoria", "FF8");
        string dataRoot = Path.Combine(Xdg("XDG_DATA_HOME", ".local/share"), "Memoria", "FF8");
        return new(config, Path.Combine(dataRoot, "saves"), Path.Combine(Xdg("XDG_CACHE_HOME", ".cache"), "Memoria", "FF8"), Path.Combine(state, "logs"), dataRoot);

        string Xdg(string variable, string fallback)
        {
            string? value = environment(variable);
            return !string.IsNullOrWhiteSpace(value) && Path.IsPathFullyQualified(value) ? value : Path.Combine(home, fallback);
        }
    }
}

