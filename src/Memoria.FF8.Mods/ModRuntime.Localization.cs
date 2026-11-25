using System.Text.Json;

namespace Memoria.FF8.Mods;

public sealed partial class ModRuntime
{
    private sealed record LocalizationPreferences(string[]? Order, string? PrimaryTarget, string? LanguagePackageId = null, string? LanguagePrimaryTarget = null);
    private string[]? localizationOrder;
    private string? authoredPrimaryTarget;
    private string? languagePackageId;
    private string? languagePrimaryTarget;
    public string? LocalizationPrimaryTarget => languagePrimaryTarget ?? authoredPrimaryTarget;
    public IReadOnlyList<string>? LocalizationOrder
    {
        get
        {
            lock (gate)
            {
                if (languagePackageId is null || localizationOrder is null)
                {
                    return localizationOrder?.ToArray();
                }
                return localizationOrder.Contains(languagePackageId, StringComparer.OrdinalIgnoreCase) ? localizationOrder.ToArray() : localizationOrder.Append(languagePackageId).ToArray();
            }
        }
    }

    private string LocalizationPreferencesPath => Path.Combine(preferenceRoot, "localization-stack.json");

    private void ReadLocalizationPreferences()
    {
        if (!File.Exists(LocalizationPreferencesPath))
        {
            localizationOrder = null;
            authoredPrimaryTarget = null;
            languagePackageId = null;
            languagePrimaryTarget = null;
            return;
        }
        byte[] bytes = File.ReadAllBytes(LocalizationPreferencesPath);
        if (bytes.Length > 64 * 1024)
        {
            throw new InvalidDataException("Localization stack preferences exceed their size limit.");
        }
        using JsonDocument document = JsonDocument.Parse(bytes);
        if (document.RootElement.ValueKind == JsonValueKind.Array)
        {
            localizationOrder = document.RootElement.Deserialize<string[]>();
            authoredPrimaryTarget = null;
            languagePackageId = null;
            languagePrimaryTarget = null;
        }
        else
        {
            LocalizationPreferences preferences = document.RootElement.Deserialize<LocalizationPreferences>() ?? throw new InvalidDataException("Invalid localization stack preferences.");
            localizationOrder = preferences.Order;
            authoredPrimaryTarget = preferences.PrimaryTarget;
            languagePackageId = preferences.LanguagePackageId;
            languagePrimaryTarget = preferences.LanguagePrimaryTarget;
        }
        if (localizationOrder is not null && (localizationOrder.Any(string.IsNullOrWhiteSpace) || localizationOrder.Distinct(StringComparer.OrdinalIgnoreCase).Count() != localizationOrder.Length))
        {
            throw new InvalidDataException("Localization stack preferences contain duplicate or empty package identities.");
        }
    }

    /// <summary>Accepts the displayed low-to-high stack and enablement together. Other mod priorities are untouched.</summary>
    public ModOperationResult SetLocalizationSelection(IReadOnlyList<string> lowToHigh, string? primaryTarget = null)
    {
        lock (gate)
        {
            ThrowIfDisposed();
            if (lowToHigh.Distinct(StringComparer.OrdinalIgnoreCase).Count() != lowToHigh.Count || lowToHigh.Any(id => !installedPackages.Any(package => package.Id.Equals(id, StringComparison.OrdinalIgnoreCase) && package.HasLocalization)))
            {
                return new(false, "Localization selection must identify distinct installed localization packages.");
            }
            if (primaryTarget is not null && (string.IsNullOrWhiteSpace(primaryTarget) || primaryTarget.Any(char.IsControl)))
            {
                return new(false, "Primary language must be a nonempty identifier without control characters.");
            }
            string[]? previousOrder = localizationOrder;
            string? previousPrimary = authoredPrimaryTarget;
            IReadOnlyList<ModPackage> previousPackages = installedPackages;
            List<(string Path, byte[]? Bytes)> originals = [];
            FileStream? settingsLock = null;
            FileStream? stackLock = null;
            try
            {
                stackLock = ModConfiguration.LockMemoriaSettings(LocalizationPreferencesPath);
                MemoriaSettings? componentSettings = null;
                foreach (ModPackage package in installedPackages.Where(package => package.HasLocalization))
                {
                    ValidatePreferenceTarget(package);
                    bool enabled = lowToHigh.Contains(package.Id, StringComparer.OrdinalIgnoreCase);
                    if (package.Descriptor is { } descriptor)
                    {
                        settingsLock ??= ModConfiguration.LockMemoriaSettings(MemoriaSettingsPath);
                        componentSettings ??= ReadMemoriaSettings();
                        componentSettings.Components[descriptor.Id] = enabled;
                        continue;
                    }
                    string path = ConfigurationPath(package.RootDirectory, package.Format);
                    originals.Add((path, File.Exists(path) ? File.ReadAllBytes(path) : null));
                    ModConfiguration.WriteBoolean(path, "enabled", enabled);
                }
                if (componentSettings is not null)
                {
                    originals.Add((MemoriaSettingsPath, File.Exists(MemoriaSettingsPath) ? File.ReadAllBytes(MemoriaSettingsPath) : null));
                    WriteMemoriaSettings(componentSettings);
                }
                originals.Add((LocalizationPreferencesPath, File.Exists(LocalizationPreferencesPath) ? File.ReadAllBytes(LocalizationPreferencesPath) : null));
                Directory.CreateDirectory(preferenceRoot);
                ModConfiguration.WriteAtomic(LocalizationPreferencesPath, JsonSerializer.SerializeToUtf8Bytes(new LocalizationPreferences(lowToHigh.ToArray(), primaryTarget, languagePackageId, languagePrimaryTarget)));
                localizationOrder = lowToHigh.ToArray();
                authoredPrimaryTarget = primaryTarget;
                ReconcilePreferences();
                RequestRefreshCore(DateTime.MinValue);
                return ModOperationResult.Completed;
            }
            catch (Exception exception) when (IsFileError(exception))
            {
                localizationOrder = previousOrder;
                authoredPrimaryTarget = previousPrimary;
                installedPackages = previousPackages;
                List<string> restoreErrors = [];
                foreach ((string path, byte[]? bytes) in originals.AsEnumerable().Reverse())
                {
                    try
                    {
                        if (bytes is null)
                        {
                            File.Delete(path);
                        }
                        else
                        {
                            File.WriteAllBytes(path, bytes);
                        }
                    }
                    catch (Exception restore) when (IsFileError(restore))
                    {
                        restoreErrors.Add(restore.Message);
                    }
                }
                return OperationFailed(restoreErrors.Count == 0 ? exception : new IOException(exception.Message + " Restoring preferences also failed: " + string.Join("; ", restoreErrors), exception));
            }
            finally
            {
                settingsLock?.Dispose();
                stackLock?.Dispose();
            }
        }
    }
}
