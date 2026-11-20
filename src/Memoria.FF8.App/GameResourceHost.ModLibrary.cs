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
    private bool officialModLibrarySupported;
    private readonly List<ModPackageInstaller> previousModInstallers = [];
    private readonly List<(string Root, ModPackageInstaller Installer, IReadOnlyList<ModPackageInstallResult> Results)> modLibraryRecovery = [];
    private readonly Dictionary<string, ModDirectoryResourceLease> modLibraryLeases = new(ApplicationInstallation.ModPathComparer);
    private readonly HashSet<string> unleasedModRoots = new(ApplicationInstallation.ModPathComparer);
    private string? modLibraryUnavailableReason;
    private readonly List<ModDiagnostic> modLibraryStartupDiagnostics = [];
    internal ModPackageInstaller PackageInstaller => modInstaller!;
    internal IModLibraryService? ExistingModLibrary => modLibrary;

    // Creation starts only local worker initialization. Network access begins with a catalog command.
    public IModLibraryService ModLibrary => modLibrary ??= new ModLibraryService(modLibraryCache!, modInstaller!, Mods, modLibraryTarget!, modLibrarySupported, unavailableReason: modLibraryUnavailableReason, openDownloadPage: OpenModDownloadPage, officialCacheDirectory: OfficialModCatalog.DefaultCacheDirectory, officialSupported: officialModLibrarySupported, previousInstallers: previousModInstallers, officialDownloadsDirectory: ModLibraryProfile.OfficialDownloadsDirectory(Mods.LegacyModsDirectory, ApplicationPaths.Current));

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

    private void RecoverModLibrary(string applicationDataDirectory)
    {
        string root = Mods.PrimaryModsDirectory;
        string preferences = Mods.PreferencesDirectory;
        string legacyRoot = Mods.LegacyModsDirectory;
        string edition = source.Descriptor.Edition.ToString();
        string language = resourceLanguage ?? source.Descriptor.Language;
        string identity = ModLibraryProfile.Identity(legacyRoot, gameDirectory, edition, language);
        modLibraryTarget = $"{edition}/{language}/{identity}";
        modLibrarySupported = ModCompatibility.IsCompatible(source.Descriptor.Edition, ModFormat.Junction) && language.Equals("en", StringComparison.OrdinalIgnoreCase);
        modLibraryUnavailableReason = null;
        modLibraryCache = Path.Combine(ApplicationPaths.Current.Cache, "mod-library", identity);
        bool junctionSupported = modLibrarySupported;
        if (Path.GetDirectoryName(root) is null)
        {
            modLibrarySupported = false;
            modLibraryUnavailableReason = "Choose a mod directory below the volume root to use managed installation.";
            modInstaller = new ModPackageInstaller(Path.Combine(modLibraryCache, "disabled-state"), Path.Combine(modLibraryCache, "disabled-mods"), modLibraryTarget, false);
            return;
        }
        modInstaller = ModLibraryProfile.CreateInstaller(root, gameDirectory, edition, language, junctionSupported, preferences);
        string bundledRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.Combine(applicationDataDirectory, "BundledMods")));
        foreach (string directory in Mods.ModsDirectories.OrderBy(directory => directory, StringComparer.OrdinalIgnoreCase).ThenBy(directory => directory, StringComparer.Ordinal))
        {
            bool primary = ApplicationInstallation.ModPathComparer.Equals(directory, root);
            bool legacy = ApplicationInstallation.ModPathComparer.Equals(directory, legacyRoot);
            if (!primary && (ApplicationInstallation.ModPathComparer.Equals(directory, bundledRoot) || Path.GetDirectoryName(directory) is null
                || !legacy && !Directory.Exists(ModPackageInstaller.StateDirectoryFor(directory))))
            {
                continue;
            }
            ModPackageInstaller installer = primary ? modInstaller : ModLibraryProfile.CreateInstaller(directory, gameDirectory, edition, language, junctionSupported, preferences);
            if (!primary)
            {
                previousModInstallers.Add(installer);
            }
            try
            {
                modLibraryLeases.Add(directory, ModDirectoryResourceLease.Acquire(directory, true));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException)
            {
                unleasedModRoots.Add(directory);
                modLibraryStartupDiagnostics.Add(new("library-resource-lease", "Managed packages in this directory cannot be read safely: " + exception.Message));
            }
        }
        if (!modLibraryLeases.TryGetValue(root, out ModDirectoryResourceLease? primaryLease) || !primaryLease.IsWriter)
        {
            modLibrarySupported = false;
            modLibraryUnavailableReason = "Other game instances or directory permissions prevent installation. Pending changes wait until all readers exit; local mods remain available.";
            modLibraryStartupDiagnostics.Add(new("library-recovery", modLibraryUnavailableReason));
        }
        try
        {
            if (modLibraryLeases.TryGetValue(legacyRoot, out ModDirectoryResourceLease? lease) && lease.IsWriter)
            {
                GirlNextDoorMigration.FromGame(applicationDataDirectory, legacyRoot);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or System.Text.Json.JsonException)
        {
            modLibraryStartupDiagnostics.Add(new("library-migration", "Girl Next Door migration will retry on the next start: " + exception.Message, "Memoria/Girl Next Door"));
        }
        RecoverModRelocations();
        foreach (ModPackageInstaller installer in previousModInstallers.Prepend(modInstaller))
        {
            if (!modLibraryLeases.TryGetValue(installer.ModsRoot, out ModDirectoryResourceLease? lease) || !lease.IsWriter)
            {
                continue;
            }
            try
            {
                IReadOnlyList<ModPackageInstallResult> results = installer.RecoverAndApply();
                modLibraryRecovery.Add((installer.ModsRoot, installer, results));
                foreach (ModPackageInstallResult result in results)
                {
                    if (result.Error is not null)
                    {
                        modLibraryStartupDiagnostics.Add(new("library-recovery", result.Error, result.PackageId));
                    }
                }
                officialModLibrarySupported |= ReferenceEquals(installer, modInstaller);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or System.Text.Json.JsonException)
            {
                if (ReferenceEquals(installer, modInstaller))
                {
                    modLibrarySupported = false;
                    modLibraryUnavailableReason = "Cannot manage this mod directory. Close other game instances and check its access permissions; installed mods remain available.";
                }
                modLibraryStartupDiagnostics.Add(new("library-recovery", "Mod library recovery is unavailable: " + exception.Message));
            }
        }
    }

    private void ValidateModLibraryStartup(CancellationToken cancellationToken)
    {
        try
        {
            bool rolledBack = ValidateModLibraryApplications();
            bool relocated = RecoverModRelocations();
            if (rolledBack || relocated)
            {
                Mods.Refresh();
                CompleteStartupPreparation(cancellationToken);
                if (relocated && Mods.Catalog.Packages.Any(package => package.Status == ModEffectiveStatus.Active && !Directory.Exists(package.RootDirectory) && !File.Exists(package.RootDirectory)))
                {
                    throw new InvalidDataException("The relocated mod packages could not be reloaded. Restart the game to load them from their new directory.");
                }
            }
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
                    officialModLibrarySupported = false;
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

    private bool RecoverModRelocations()
    {
        ModPackageInstaller[] writable = previousModInstallers.Prepend(modInstaller!)
            .Where(installer => modLibraryLeases.TryGetValue(installer.ModsRoot, out ModDirectoryResourceLease? lease) && lease.IsWriter).ToArray();
        bool relocated = false;
        foreach (ModPackageInstaller installer in writable)
        {
            try
            {
                foreach (ModPackageInstallResult result in installer.RecoverRelocations(writable))
                {
                    relocated |= result.Relocated;
                    if (result.Error is not null)
                    {
                        modLibraryStartupDiagnostics.Add(new("library-relocation", result.Error, result.PackageId));
                    }
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
            {
                modLibraryStartupDiagnostics.Add(new("library-relocation", "Mod relocation recovery is unavailable: " + exception.Message));
            }
        }
        return relocated;
    }

    private bool ValidateModLibraryApplications()
    {
        bool rolledBack = false;
        ModServiceView view = Mods.View;
        foreach ((string root, ModPackageInstaller installer, IReadOnlyList<ModPackageInstallResult> applications) in modLibraryRecovery)
        {
            HashSet<string> accepted = view.Packages.Where(package => !package.Package.RequestedEnabled || package.CommittedStatus == ModEffectiveStatus.Active && !package.NeedsAttention).Where(package => !package.Package.Id.StartsWith("Junction/managed-", StringComparison.OrdinalIgnoreCase) || ApplicationInstallation.ModPathComparer.Equals(Path.GetFullPath(package.Package.RootDirectory), Path.GetFullPath(Path.Combine(root, package.Package.Id.Replace('/', Path.DirectorySeparatorChar))))).Select(package => package.Package.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (ModPackageInstallResult application in applications.Where(result => result.Installed && result.NativeDestinationName is not null))
            {
                string destination = Path.Combine(root, "Memoria", application.NativeDestinationName!);
                ModPackage? package = Mods.InstalledPackages.FirstOrDefault(package => package.Official is not null && package.Id == application.PackageId && ApplicationInstallation.ModPathComparer.Equals(Path.GetFullPath(package.RootDirectory), destination));
                if (package is not null && (package.Status == ModEffectiveStatus.Disabled || Mods.Catalog.Packages.Any(active => active.Id == package.Id && active.Status == ModEffectiveStatus.Active) && !view.Diagnostics.Any(error => error.PackageId == package.Id && error.Code is "native-availability" or "resource-rejected")))
                {
                    accepted.Add(package.Id);
                }
            }
            foreach (ModPackageInstallResult result in installer.CompleteStartupApplications(accepted))
            {
                if (result.Error is not null)
                {
                    rolledBack = true;
                    modLibraryStartupDiagnostics.Add(new("library-activation", result.Error, result.PackageId));
                }
            }
        }
        return rolledBack;
    }

    private string? NativeRootUnavailable(string root)
    {
        return unleasedModRoots.Contains(root) ? "This official mod cannot be read safely while its directory is in use." : null;
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
