namespace Memoria.Launcher;

internal static class LauncherCapabilities
{
    private static bool SupportedDesktop => OperatingSystem.IsWindows() || OperatingSystem.IsLinux() || OperatingSystem.IsMacOS();
    public static bool CanLaunchGame => SupportedDesktop && System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture == System.Runtime.InteropServices.Architecture.X64;
    public static bool CanUpdate => CanLaunchGame;
    public static StringComparer PathComparer => OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
    public const string GameStatus = "The game requires an x64 Windows, Linux or Intel macOS host and its matching native distribution.";
    public const string UpdateStatus = "Updates require a matching x64 Windows, Linux or Intel macOS distribution. Packages for another operating system cannot be installed here.";
}
