namespace Memoria.FF8.Platform;

public enum ModInstallationLocation
{
    Default,
    Memoria,
    Game,
    UserData,
    Custom
}

public sealed record ModDirectories(string Primary, IReadOnlyList<string> Discovery, string Preferences, string LegacyDefault);

/// <summary>Shared package data lives outside isolated graphics-host payloads.</summary>
public static class ApplicationInstallation
{
    public const string DirectoryVariable = "MEMORIA_FF8_APPLICATION_DIRECTORY";
    public const string ModPackageDirectoryVariable = "MEMORIA_FF8_MOD_PACKAGE_DIRECTORY";
    public static StringComparer ModPathComparer => OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    public static string DataDirectory => Resolve(AppContext.BaseDirectory, Environment.GetEnvironmentVariable(DirectoryVariable));

    public static string DefaultModsDirectory => ResolveModsDirectory(Path.GetDirectoryName(DataDirectory) ?? DataDirectory, HostPlatforms.Current, ApplicationPaths.Current.Configuration);

    public static string ResolveModsDirectory(string packageExecutableDirectory, HostPlatform platform, string configurationDirectory)
    {
        string root = platform == HostPlatform.MacOS ? configurationDirectory : packageExecutableDirectory;
        if (!Path.IsPathFullyQualified(root))
        {
            throw new ArgumentException("The mod directory root must be absolute.", platform == HostPlatform.MacOS ? nameof(configurationDirectory) : nameof(packageExecutableDirectory));
        }
        return Path.GetFullPath(Path.Combine(root, "Mods"));
    }

    public static string ResolveModPackageDirectory(string dataDirectory)
    {
        string directory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(dataDirectory));
        string? parent = Path.GetDirectoryName(directory);
        if (parent is not null && Path.GetFileName(directory).Equals("Data", StringComparison.OrdinalIgnoreCase)
            && (File.Exists(Path.Combine(directory, "distribution.json")) || File.Exists(Path.Combine(parent, HostPlatforms.Current.ExecutableName("Memoria.Launcher")))))
        {
            return parent;
        }
        return directory;
    }

    public static string ConfiguredModPackageDirectory(string standalonePackageDirectory)
    {
        string? configured = Environment.GetEnvironmentVariable(ModPackageDirectoryVariable);
        if (configured is null)
        {
            return standalonePackageDirectory;
        }
        if (!Path.IsPathFullyQualified(configured))
        {
            throw new InvalidDataException($"{ModPackageDirectoryVariable} must be an absolute package directory.");
        }
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(configured));
    }

    public static ModDirectories ResolveModDirectories(string packageExecutableDirectory, string gameDirectory, ApplicationPaths paths, LauncherSettings settings, HostPlatform? hostPlatform = null)
    {
        HostPlatform platform = hostPlatform ?? HostPlatforms.Current;
        string package = Path.TrimEndingDirectorySeparator(Path.GetFullPath(packageExecutableDirectory));
        string? contents = Path.GetDirectoryName(package);
        string? bundle = contents is null ? null : Path.GetDirectoryName(contents);
        if (platform == HostPlatform.MacOS && Path.GetFileName(contents) == "Contents" && bundle?.EndsWith(".app", StringComparison.OrdinalIgnoreCase) == true)
        {
            package = Path.GetDirectoryName(bundle)!;
        }
        else
        {
            bundle = null;
        }
        string memoria = Path.GetFullPath(Path.Combine(package, "Mods"));
        string game = Path.GetFullPath(Path.Combine(gameDirectory, "Mods"));
        string userData = Path.GetFullPath(Path.Combine(platform == HostPlatform.Windows ? paths.Configuration : paths.LegacyState, "Mods"));
        string previousDefault = ResolveModsDirectory(packageExecutableDirectory, platform, paths.Configuration);
        string primary = settings.ModLocation switch
        {
            ModInstallationLocation.Memoria => memoria,
            ModInstallationLocation.Game => game,
            ModInstallationLocation.UserData => userData,
            ModInstallationLocation.Custom => NormalizeCustomModsDirectory(settings.CustomModsDirectory),
            _ => previousDefault
        };
        if (settings.ModLocation == ModInstallationLocation.Custom && bundle is not null
            && (primary.Equals(bundle, StringComparison.OrdinalIgnoreCase) || primary.StartsWith(bundle + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException("Choose a directory outside the signed Memoria app bundle.");
        }
        List<string> roots = [game, previousDefault, memoria, userData];
        foreach (string? custom in (settings.PreviousCustomModsDirectories ?? []).Reverse().Append(settings.CustomModsDirectory))
        {
            try
            {
                roots.Add(NormalizeCustomModsDirectory(custom));
            }
            catch (ArgumentException)
            {
                // Ignore invalid historical paths. The selected custom path is validated above.
            }
        }
        roots.Add(primary);
        return new(primary, roots.Distinct(ModPathComparer).ToArray(), Path.Combine(paths.Configuration, "Mods"), previousDefault);
    }

    public static string NormalizeCustomModsDirectory(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Path.IsPathFullyQualified(directory))
        {
            throw new ArgumentException("Choose an absolute mod directory.", nameof(directory));
        }
        string path = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
        if (Path.GetDirectoryName(path) is null)
        {
            throw new ArgumentException("Choose a mod directory below the volume root.", nameof(directory));
        }
        return path;
    }

    public static string Resolve(string hostDirectory, string? configuredDirectory)
    {
        if (configuredDirectory is null)
        {
            return Path.GetFullPath(hostDirectory);
        }
        if (!Path.IsPathFullyQualified(configuredDirectory))
        {
            throw new InvalidDataException($"{DirectoryVariable} must be an absolute application data directory.");
        }
        return Path.GetFullPath(configuredDirectory);
    }
}
