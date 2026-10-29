namespace Memoria.FF8.Platform;

public static class MediaToolPaths
{
    public static string ResolveExecutable(string directory, string toolName) => ResolveExecutable(directory, toolName, HostPlatforms.Current);

    public static string ResolveExecutable(string directory, string toolName, HostPlatform platform)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        if (toolName is not ("ffmpeg" or "ffprobe"))
        {
            throw new ArgumentException("Unknown media tool.", nameof(toolName));
        }
        if (platform is not (HostPlatform.Windows or HostPlatform.Linux or HostPlatform.MacOS))
        {
            throw new ArgumentOutOfRangeException(nameof(platform));
        }
        string path = Path.Combine(directory, toolName + (platform == HostPlatform.Windows ? ".exe" : ""));
        if (platform == HostPlatform.MacOS && !File.Exists(path))
        {
            // Shared-library macOS packages keep tools in bin/ because they load their libraries from @executable_path/../lib.
            string packaged = Path.Combine(directory, "bin", toolName);
            if (File.Exists(packaged))
            {
                return packaged;
            }
        }
        return path;
    }
}
