using Memoria.FF8.Platform;

namespace Memoria.FF8.Mods;

public sealed partial class ModRuntime
{
    public bool CanDeleteLocalPackage(ModPackage package) => LocalRemovalRoot(package) is not null;

    public void DeleteLocalPackage(ModPackage package)
    {
        string modsDirectory;
        lock (gate)
        {
            ThrowIfDisposed();
            if (!installedPackages.Contains(package) || LocalRemovalRoot(package) is not { } root)
            {
                throw new InvalidOperationException("The selected mod is no longer available for local deletion.");
            }
            modsDirectory = root;
        }

        ModPackageInstaller.EnsureNoLinks(modsDirectory);
        using ModDirectoryResourceLease lease = ModDirectoryResourceLease.Acquire(modsDirectory, requestWriter: true);
        if (!lease.IsWriter)
        {
            throw new InvalidOperationException("Close the game before deleting this mod.");
        }
        ModPackageInstaller installer = new(ModPackageInstaller.StateDirectoryFor(modsDirectory), modsDirectory, "");
        installer.DeleteLocalPayload(package);
    }

    private string? LocalRemovalRoot(ModPackage package)
    {
        if (package.IsBundled || package.IsSettingsGroup || package.Official is not null)
        {
            return null;
        }
        string path = Path.GetFullPath(package.RootDirectory);
        string? directory = Path.GetDirectoryName(path);
        bool junction = package.Format == ModFormat.Junction;
        bool junctionArchive = junction && path.EndsWith(".iroj", StringComparison.OrdinalIgnoreCase);
        foreach (string root in ModsDirectories)
        {
            if (ApplicationInstallation.ModPathComparer.Equals(root, bundledModsRoot))
            {
                continue;
            }
            string formatDirectory = Path.Combine(root, package.Format.ToString());
            bool direct = ApplicationInstallation.ModPathComparer.Equals(directory, formatDirectory);
            bool wrapped = junctionArchive && directory is not null && ApplicationInstallation.ModPathComparer.Equals(Path.GetDirectoryName(directory), formatDirectory);
            if (direct || wrapped)
            {
                string? name = Path.GetFileName(direct ? path : directory);
                return junction && name?.StartsWith("managed-", StringComparison.OrdinalIgnoreCase) == true ? null : root;
            }
        }
        return null;
    }
}
