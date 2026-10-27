namespace Memoria.Launcher;

internal static class DistributionPaths
{
    internal static string GetFullPath(string path)
    {
        string fullPath = Path.GetFullPath(path);
        if (!OperatingSystem.IsMacOS())
        {
            return fullPath;
        }

        // macOS exposes these system directories through links into /private.
        // Resolve only their exact OS targets; installation and staging links remain forbidden.
        foreach (string name in new[] { "var", "tmp", "etc" })
        {
            string alias = "/" + name;
            if (fullPath != alias && !fullPath.StartsWith(alias + "/", StringComparison.Ordinal))
            {
                continue;
            }

            string target = "/private/" + name;
            FileSystemInfo? resolved = new DirectoryInfo(alias).ResolveLinkTarget(returnFinalTarget: false);
            if (resolved is not null && Path.TrimEndingDirectorySeparator(resolved.FullName) == target)
            {
                return target + fullPath[alias.Length..];
            }
        }

        return fullPath;
    }
}
