using System.Text.Json;

namespace Memoria.FF8.Mods;

public sealed partial class ModRuntime
{
    private sealed record LocalizationPreferences(string[] Order, string? PrimaryTarget);
    private string[]? localizationOrder;
    public string? LocalizationPrimaryTarget { get; private set; }
    public IReadOnlyList<string>? LocalizationOrder
    {
        get
        {
            lock (gate)
            {
                return localizationOrder?.ToArray();
            }
        }
    }

    private string LocalizationPreferencesPath => Path.Combine(modsRoot, "localization-stack.json");

    private void ReadLocalizationPreferences()
    {
        if (!File.Exists(LocalizationPreferencesPath))
        {
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
        }
        else
        {
            LocalizationPreferences preferences = document.RootElement.Deserialize<LocalizationPreferences>() ?? throw new InvalidDataException("Invalid localization stack preferences.");
            localizationOrder = preferences.Order;
            LocalizationPrimaryTarget = preferences.PrimaryTarget;
        }
        if (localizationOrder is null)
        {
            throw new InvalidDataException("Invalid localization stack preferences.");
        }
        if (localizationOrder.Any(string.IsNullOrWhiteSpace) || localizationOrder.Distinct(StringComparer.OrdinalIgnoreCase).Count() != localizationOrder.Length)
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
            string? previousPrimary = LocalizationPrimaryTarget;
            IReadOnlyList<ModPackage> previousPackages = installedPackages;
            List<(string Path, byte[]? Bytes)> originals = [];
            try
            {
                foreach (ModPackage package in installedPackages.Where(package => package.HasLocalization))
                {
                    ValidatePreferenceTarget(package);
                    string path = ConfigurationPath(package.RootDirectory, package.Format);
                    originals.Add((path, File.Exists(path) ? File.ReadAllBytes(path) : null));
                    ModConfiguration.WriteBoolean(path, "enabled", lowToHigh.Contains(package.Id, StringComparer.OrdinalIgnoreCase));
                }
                originals.Add((LocalizationPreferencesPath, File.Exists(LocalizationPreferencesPath) ? File.ReadAllBytes(LocalizationPreferencesPath) : null));
                Directory.CreateDirectory(modsRoot);
                string temporary = LocalizationPreferencesPath + ".tmp";
                File.WriteAllText(temporary, JsonSerializer.Serialize(new LocalizationPreferences(lowToHigh.ToArray(), primaryTarget)));
                File.Move(temporary, LocalizationPreferencesPath, true);
                localizationOrder = lowToHigh.ToArray();
                LocalizationPrimaryTarget = primaryTarget;
                ReconcilePreferences();
                RequestRefreshCore(DateTime.MinValue);
                return ModOperationResult.Completed;
            }
            catch (Exception exception) when (IsFileError(exception))
            {
                localizationOrder = previousOrder;
                LocalizationPrimaryTarget = previousPrimary;
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
        }
    }
}
