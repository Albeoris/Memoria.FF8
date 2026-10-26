using System.Runtime.InteropServices;

namespace Memoria.FF8.Platform;

public enum HostPlatform
{
    Windows,
    Linux,
    MacOS
}

public static class HostPlatforms
{
    public static HostPlatform Current => OperatingSystem.IsWindows() ? HostPlatform.Windows : OperatingSystem.IsLinux() ? HostPlatform.Linux : OperatingSystem.IsMacOS() ? HostPlatform.MacOS : throw new PlatformNotSupportedException("Supported desktop hosts are Windows, Linux and macOS.");

    public static string RuntimeIdentifier(this HostPlatform platform) => platform switch
    {
        HostPlatform.Windows => "win-x64",
        HostPlatform.Linux => "linux-x64",
        HostPlatform.MacOS => "osx-x64",
        _ => throw new ArgumentOutOfRangeException(nameof(platform))
    };

    public static string ExecutableName(this HostPlatform platform, string name) => platform == HostPlatform.Windows ? name + ".exe" : name;

    public static void RequireX64()
    {
        if (RuntimeInformation.ProcessArchitecture != Architecture.X64)
        {
            throw new PlatformNotSupportedException("This desktop application requires an x64 process.");
        }
    }
}
