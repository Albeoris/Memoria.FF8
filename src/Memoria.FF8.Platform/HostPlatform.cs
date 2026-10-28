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
        HostPlatform.MacOS => RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "osx-arm64" : "osx-x64",
        _ => throw new ArgumentOutOfRangeException(nameof(platform))
    };

    public static bool IsMacRuntime(string runtimeIdentifier) => runtimeIdentifier is "osx-x64" or "osx-arm64";

    /// <summary>Apple Silicon is supported only as a native macOS process; other hosts require x64.</summary>
    public static bool IsSupportedArchitecture => RuntimeInformation.ProcessArchitecture == Architecture.X64 || (OperatingSystem.IsMacOS() && RuntimeInformation.ProcessArchitecture == Architecture.Arm64);

    public static string ExecutableName(this HostPlatform platform, string name) => platform == HostPlatform.Windows ? name + ".exe" : name;

    public static void RequireSupportedArchitecture()
    {
        if (!IsSupportedArchitecture)
        {
            throw new PlatformNotSupportedException("This desktop application requires an x64 process or a native Apple Silicon process on macOS.");
        }
    }
}
