using System.Security.Cryptography;
using System.Text;
using Memoria.FF8.Mods;
using Memoria.FF8.Platform;

namespace Memoria.FF8;

internal sealed partial class GameResourceHost
{
    private ModLibraryService? modLibrary;
    private ModPackageInstaller? modInstaller;
    private string? modLibraryCache;
    private string? modLibraryTarget;
    private bool modLibrarySupported;
    private bool modLibraryRecovered;
    private readonly Dictionary<string, ModDirectoryResourceLease> modLibraryLeases = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> unleasedModRoots = new(StringComparer.OrdinalIgnoreCase);
    private string? modLibraryUnavailableReason;
    private readonly List<ModDiagnostic> modLibraryStartupDiagnostics = [];
    internal ModPackageInstaller PackageInstaller => modInstaller!;
    internal IModLibraryService? ExistingModLibrary => modLibrary;

    // Creation starts only local worker initialization. Network access begins with a catalog command.
    public IModLibraryService ModLibrary => modLibrary ??= new ModLibraryService(modLibraryCache!, modInstaller!, Mods, modLibraryTarget!, modLibrarySupported, unavailableReason: modLibraryUnavailableReason, openDownloadPage: OpenModDownloadPage);

    private static void OpenModDownloadPage(Uri uri)
    {
        if (uri.Scheme is not ("https" or "http") || !string.IsNullOrEmpty(uri.UserInfo))
        {
            throw new InvalidOperationException("This download page address is unsupported.");
        }
        System.Diagnostics.ProcessStartInfo start;
        if (OperatingSystem.IsWindows())
        {
            start = new(uri.AbsoluteUri) { UseShellExecute = true };
        }
        else
        {
            start = new(OperatingSystem.IsMacOS() ? "open" : "xdg-open") { UseShellExecute = false, CreateNoWindow = true };
            start.ArgumentList.Add(uri.AbsoluteUri);
        }
        System.Diagnostics.Process.Start(start)?.Dispose();
    }

    private void RecoverModLibrary()
    {
        string root = Mods.PrimaryModsDirectory;
        string language = resourceLanguage ?? source.Descriptor.Language;
        string identity = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(root + "\0" + Path.GetFullPath(gameDirectory) + "\0" + source.Descriptor.Edition + "\0" + language)))[..16].ToLowerInvariant();
        modLibraryTarget = $"{source.Descriptor.Edition}/{language}/{identity}";
        modLibrarySupported = ModCompatibility.IsCompatible(source.Descriptor.Edition, ModFormat.Junction) && language.Equals("en", StringComparison.OrdinalIgnoreCase);
        modLibraryUnavailableReason = modLibrarySupported ? null : "Catalog installation currently requires Classic English game data. Installed local mods remain available.";
        modLibraryCache = Path.Combine(ApplicationPaths.Current.Cache, "mod-library", identity);
        foreach (string directory in Mods.ModsDirectories.OrderBy(directory => directory, StringComparer.OrdinalIgnoreCase).ThenBy(directory => directory, StringComparer.Ordinal))
        {
            try
            {
                modLibraryLeases.Add(directory, ModDirectoryResourceLease.Acquire(directory, directory.Equals(root, StringComparison.OrdinalIgnoreCase)));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException)
            {
                unleasedModRoots.Add(directory);
                modLibraryStartupDiagnostics.Add(new("library-resource-lease", "Managed packages in this directory cannot be read safely: " + exception.Message));
            }
        }
        if (Path.GetDirectoryName(root) is null)
        {
            modLibrarySupported = false;
            modLibraryUnavailableReason = "Choose a mod directory below the volume root to use managed installation.";
            modInstaller = new ModPackageInstaller(Path.Combine(modLibraryCache, "disabled-state"), Path.Combine(modLibraryCache, "disabled-mods"), modLibraryTarget, false);
            return;
        }
        string state = ModDirectoryResourceLease.StateDirectory(root);
        modInstaller = new ModPackageInstaller(state, root, modLibraryTarget, modLibrarySupported);
        if (!modLibraryLeases.TryGetValue(root, out ModDirectoryResourceLease? primary) || !primary.IsWriter)
        {
            modLibrarySupported = false;
            modLibraryUnavailableReason = "Other game instances or directory permissions prevent installation. Pending changes wait until all readers exit; local mods remain available.";
            modLibraryStartupDiagnostics.Add(new("library-recovery", modLibraryUnavailableReason));
            return;
        }
        try
        {
            foreach (ModPackageInstallResult result in modInstaller.RecoverAndApply())
            {
                if (result.Error is not null)
                {
                    modLibraryStartupDiagnostics.Add(new("library-recovery", result.Error, result.PackageId));
                }
            }
            modLibraryRecovered = true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or System.Text.Json.JsonException)
        {
            modLibrarySupported = false;
            modLibraryUnavailableReason = "Cannot manage this mod directory. Close other game instances and check its access permissions; installed mods remain available.";
            modLibraryStartupDiagnostics.Add(new("library-recovery", "Mod library recovery is unavailable: " + exception.Message));
        }
    }

    private void ValidateModLibraryStartup()
    {
        try
        {
            ValidateModLibraryApplications();
        }
        finally
        {
            bool discoveryChanged = false;
            foreach ((string root, ModDirectoryResourceLease lease) in modLibraryLeases)
            {
                try
                {
                    lease.FinishStartup();
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
                {
                    unleasedModRoots.Add(root);
                    discoveryChanged = true;
                    modLibrarySupported = false;
                    modLibraryUnavailableReason = "A managed resource lease could not be retained. Restart after checking directory permissions.";
                    modLibraryStartupDiagnostics.Add(new("library-resource-lease", modLibraryUnavailableReason + " " + exception.Message));
                }
            }
            if (discoveryChanged)
            {
                Mods.Refresh();
                Pump(true);
            }
            ReportModLibraryStartupDiagnostics();
        }
    }

    private void ValidateModLibraryApplications()
    {
        if (!modLibraryRecovered)
        {
            return;
        }
        StringComparison pathComparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        HashSet<string> accepted = Mods.View.Packages.Where(package => !package.Package.RequestedEnabled || package.CommittedStatus == ModEffectiveStatus.Active && !package.NeedsAttention).Where(package => !package.Package.Id.StartsWith("Junction/managed-", StringComparison.OrdinalIgnoreCase) || Path.GetFullPath(package.Package.RootDirectory).Equals(Path.GetFullPath(Path.Combine(Mods.PrimaryModsDirectory, package.Package.Id.Replace('/', Path.DirectorySeparatorChar))), pathComparison)).Select(package => package.Package.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        bool rolledBack = false;
        foreach (ModPackageInstallResult result in modInstaller!.CompleteStartupApplications(accepted))
        {
            if (result.Error is not null)
            {
                rolledBack = true;
                modLibraryStartupDiagnostics.Add(new("library-activation", result.Error, result.PackageId));
            }
        }
        if (rolledBack)
        {
            Mods.Refresh();
            Pump(true);
        }
    }

    private bool CanReadJunctionPackage(string path)
    {
        if (!Path.GetFileName(path).StartsWith("managed-", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        StringComparison comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return !unleasedModRoots.Any(root => path.StartsWith(Path.Combine(root, "Junction") + Path.DirectorySeparatorChar, comparison));
    }

    private void ReportModLibraryStartupDiagnostics()
    {
        foreach (ModDiagnostic diagnostic in modLibraryStartupDiagnostics)
        {
            Mods.ReportFailure(diagnostic);
        }
    }
}
