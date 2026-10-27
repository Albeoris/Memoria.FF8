namespace Memoria.FF8.Platform;

/// <summary>Shared package data lives outside isolated graphics-host payloads.</summary>
public static class ApplicationInstallation
{
    public const string DirectoryVariable = "MEMORIA_FF8_APPLICATION_DIRECTORY";

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
