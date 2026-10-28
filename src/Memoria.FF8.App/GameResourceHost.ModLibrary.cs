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
    private FileStream? modLibraryRuntimeLock;
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
        string rootKey = OperatingSystem.IsWindows() ? root.ToUpperInvariant() : root;
        string rootIdentity = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rootKey)))[..16].ToLowerInvariant();
        string identity = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(root + "\0" + Path.GetFullPath(gameDirectory) + "\0" + source.Descriptor.Edition + "\0" + language)))[..16].ToLowerInvariant();
        modLibraryTarget = $"{source.Descriptor.Edition}/{language}/{identity}";
        modLibrarySupported = ModCompatibility.IsCompatible(source.Descriptor.Edition, ModFormat.Junction) && language.Equals("en", StringComparison.OrdinalIgnoreCase);
        modLibraryUnavailableReason = modLibrarySupported ? null : "Catalog installation currently requires Classic English game data. Installed local mods remain available.";
        modLibraryCache = Path.Combine(ApplicationPaths.Current.Cache, "mod-library", identity);
        if (Path.GetDirectoryName(root) is not { } parent)
        {
            modLibrarySupported = false;
            modLibraryUnavailableReason = "Choose a mod directory below the volume root to use managed installation.";
            modInstaller = new ModPackageInstaller(Path.Combine(modLibraryCache, "disabled-state"), Path.Combine(modLibraryCache, "disabled-mods"), modLibraryTarget, false);
            return;
        }
        string state = Path.Combine(parent, ".memoria-mod-library-" + rootIdentity);
        modInstaller = new ModPackageInstaller(state, root, modLibraryTarget, modLibrarySupported);
        try
        {
            Directory.CreateDirectory(state);
            modLibraryRuntimeLock = new FileStream(Path.Combine(state, "runtime.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
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
        if (!modLibraryRecovered)
        {
            ReportModLibraryStartupDiagnostics();
            return;
        }
        HashSet<string> accepted = Mods.View.Packages.Where(package => !package.Package.RequestedEnabled || package.CommittedStatus == ModEffectiveStatus.Active && !package.NeedsAttention).Select(package => package.Package.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
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
        ReportModLibraryStartupDiagnostics();
    }

    private void ReportModLibraryStartupDiagnostics()
    {
        foreach (ModDiagnostic diagnostic in modLibraryStartupDiagnostics)
        {
            Mods.ReportFailure(diagnostic);
        }
    }
}
