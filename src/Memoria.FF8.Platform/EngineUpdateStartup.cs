using System.Diagnostics;
using Memoria.Launcher;

namespace Memoria.FF8.Platform;

public static class EngineUpdateStartup
{
    public static bool StartRecoveryIfNeeded(string applicationDataDirectory)
    {
        string applicationData = Path.TrimEndingDirectorySeparator(DistributionPaths.GetFullPath(applicationDataDirectory));
        string root = DistributionUpdateBackup.GameDistributionRoot(applicationData);
        if (!DistributionUpdateBackup.NeedsRecovery(root))
        {
            return false;
        }
        string executable = DistributionPaths.IsBundleRoot(root) ? "Contents/MacOS/Memoria.Launcher" : HostPlatforms.Current.ExecutableName("Memoria.Launcher");
        string launcher = Path.Combine(root, executable);
        PackageManifest.EnsureNoReparsePoints(launcher);
        ProcessStartInfo start = new(launcher) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(launcher)! };
        using Process process = Process.Start(start) ?? throw new IOException("Could not start the launcher to recover the interrupted engine update.");
        return true;
    }
}
