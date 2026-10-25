using System.Diagnostics;

namespace Memoria.FF8.BattleEffects;

public static class PrivateEvidencePath
{
    public static string Validate(string path)
    {
        if (!Path.IsPathFullyQualified(path))
        {
            throw new ArgumentException("Evidence directories require absolute paths.", nameof(path));
        }
        string fullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        for (DirectoryInfo? directory = new(fullPath); directory is not null; directory = directory.Parent)
        {
            if (directory.Exists && (directory.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new ArgumentException("Evidence directory ancestors cannot be links or junctions.", nameof(path));
            }
            if ((File.Exists(Path.Combine(directory.FullName, ".git")) || Directory.Exists(Path.Combine(directory.FullName, ".git"))) && !IsIgnored(directory.FullName, fullPath))
            {
                throw new ArgumentException("Evidence inside a repository must use a Git-ignored directory.", nameof(path));
            }
        }
        return fullPath;
    }

    private static bool IsIgnored(string repository, string path)
    {
        ProcessStartInfo start = new("git") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (string argument in new[] { "-C", repository, "check-ignore", "--quiet", "--", Path.GetRelativePath(repository, path).Replace('\\', '/') + "/" })
        {
            start.ArgumentList.Add(argument);
        }
        try
        {
            using Process process = Process.Start(start) ?? throw new InvalidOperationException("Git ignore validation did not start.");
            Task<string> output = process.StandardOutput.ReadToEndAsync();
            Task<string> error = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(5000))
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit();
                throw new ArgumentException("Git ignore validation exceeded its deadline.");
            }
            Task.WhenAll(output, error).GetAwaiter().GetResult();
            return process.ExitCode == 0;
        }
        catch (System.ComponentModel.Win32Exception exception)
        {
            throw new ArgumentException("Git is required to verify ignored evidence directories inside a repository.", exception);
        }
    }
}
