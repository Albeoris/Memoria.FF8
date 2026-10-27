namespace Memoria.FF8.Platform;

/// <summary>Shared package data lives outside isolated graphics-host payloads.</summary>
public static class ApplicationInstallation
{
    public const string DirectoryVariable = "MEMORIA_FF8_APPLICATION_DIRECTORY";

    public static string DataDirectory => Resolve(AppContext.BaseDirectory, Environment.GetEnvironmentVariable(DirectoryVariable));

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
