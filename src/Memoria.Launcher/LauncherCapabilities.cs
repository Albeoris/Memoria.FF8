namespace Memoria.Launcher;

internal static class LauncherCapabilities
{
    public static bool CanLaunchGame => OperatingSystem.IsWindows();
    public static bool CanUpdate => OperatingSystem.IsWindows();
    public static StringComparer PathComparer => OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
    public const string GameStatus = "Game launch is unavailable on this platform until the portable game host is delivered. You can browse and validate game files and save launcher settings.";
    public const string UpdateStatus = "Updates are unavailable on this platform until portable distribution updates are delivered. Windows update packages cannot be installed here.";
}
