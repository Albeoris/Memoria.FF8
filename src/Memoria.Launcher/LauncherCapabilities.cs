namespace Memoria.Launcher;

internal static class LauncherCapabilities
{
    private static bool SupportedDesktop => OperatingSystem.IsWindows() || OperatingSystem.IsLinux() || OperatingSystem.IsMacOS();
    public static bool CanLaunchGame => SupportedDesktop && Memoria.FF8.Platform.HostPlatforms.IsSupportedArchitecture;
    public static bool CanUpdate => SupportedDesktop && Memoria.FF8.Platform.HostPlatforms.IsSupportedArchitecture;
    public static StringComparer PathComparer => OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
    public const string GameStatus = "The game requires an x64 Windows, Linux or macOS host, or an Apple Silicon Mac, and its matching native distribution.";
    public const string UpdateStatus = "Updates require a distribution matching this launcher's operating system and architecture: Windows x64, Linux x64, Intel macOS or Apple Silicon macOS. Packages for another operating system cannot be installed here.";
}
