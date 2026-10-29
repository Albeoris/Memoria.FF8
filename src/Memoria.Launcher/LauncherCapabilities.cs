namespace Memoria.Launcher;

internal static class LauncherCapabilities
{
    private static bool SupportedDesktop => OperatingSystem.IsWindows() || OperatingSystem.IsLinux() || OperatingSystem.IsMacOS();
    public static bool CanLaunchGame => SupportedDesktop && Memoria.FF8.Platform.HostPlatforms.IsSupportedArchitecture;
    // Apple Silicon builds are local-only until releases publish an osx-arm64 package.
    public static bool CanUpdate => SupportedDesktop && System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture == System.Runtime.InteropServices.Architecture.X64;
    public static StringComparer PathComparer => OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
    public const string GameStatus = "The game requires an x64 Windows, Linux or macOS host, or an Apple Silicon Mac, and its matching native distribution.";
    public const string UpdateStatus = "Updates require a matching x64 Windows, Linux or Intel macOS distribution. Apple Silicon builds are local-only; rebuild from source to update. Packages for another operating system cannot be installed here.";
}
