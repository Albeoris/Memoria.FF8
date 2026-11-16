using System.Text.Json;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Mods;

public sealed partial class ModRuntime
{
    public const string MemoriaPackageId = "Memoria";
    public const string Ff7EnemyIntegrationPackageId = "Memoria/FF7EnemyIntegration";
    internal const string MeltdownPackageId = "Memoria/Meltdown";
    private const string Ff7BridgeLockReason = "The transition from FFVIII enables FFVII Enemy Integration in Automatic mode.";
    private const string ComponentBehavior = "component";
    private static IReadOnlyList<OfficialModComponent> MemoriaComponents => OfficialModCatalog.SeedComponents;

    private bool forceFf7EnemyIntegration;
    private IReadOnlyList<BehaviorModSetting>? memoriaSettingsView;
    private string MemoriaSettingsPath => Path.Combine(modsRoot, "Memoria", BehaviorMods.MemoriaSettingsFile);

    private sealed class MemoriaSettings
    {
        [JsonPropertyName("components")]
        public Dictionary<string, bool> Components { get; init; } = new(StringComparer.Ordinal);

        [JsonPropertyName("meltdown")]
        public Dictionary<string, int> Meltdown { get; init; } = new(StringComparer.Ordinal);
    }

    public bool SetFf7BridgeEnemyIntegration(bool enabled)
    {
        lock (gate)
        {
            ThrowIfDisposed();
            if (forceFf7EnemyIntegration == enabled)
            {
                return false;
            }
            forceFf7EnemyIntegration = enabled;
            ReconcilePreferences();
            RequestRefreshCore(DateTime.MinValue);
            return true;
        }
    }

    private string? RequiredPackageReason(string packageId)
    {
        if (IsRequiredHardcorePackage(packageId))
        {
            return packageId.Equals(MeltdownPackageId, StringComparison.OrdinalIgnoreCase) ? HardcoreMeltdownLockReason : HardcoreStatBonusesLockReason;
        }
        return forceFf7EnemyIntegration && packageId.Equals(Ff7EnemyIntegrationPackageId, StringComparison.OrdinalIgnoreCase) ? Ff7BridgeLockReason : null;
    }

    private static bool IsMemoriaComponent(ModPackage package)
    {
        return package.Official is not null || package.IsBundled && MemoriaComponents.Any(component => package.Id.Equals(component.RuntimePackageId, StringComparison.OrdinalIgnoreCase));
    }

    internal static bool IsBundledMemoriaIdentity(string id, string key)
    {
        return MemoriaComponents.Any(component => component.Delivery == "engine" && (component.SettingsKey.Equals(key, StringComparison.OrdinalIgnoreCase) || component.RuntimePackageId.Equals(id, StringComparison.OrdinalIgnoreCase)));
    }

    private MemoriaSettings ReadMemoriaSettings()
    {
        ValidatePreferencePath(MemoriaSettingsPath);
        return File.Exists(MemoriaSettingsPath) ? ParseMemoriaSettings(File.ReadAllBytes(MemoriaSettingsPath)) : new();
    }

    private static MemoriaSettings ParseMemoriaSettings(byte[] content)
    {
        MemoriaSettings? settings;
        try
        {
            settings = JsonSerializer.Deserialize<MemoriaSettings>(content);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Invalid Memoria settings document.", exception);
        }
        if (settings?.Components is null || settings.Meltdown is null)
        {
            throw new InvalidDataException("Invalid Memoria settings document.");
        }
        return settings;
    }

    internal static IReadOnlyDictionary<string, int>? ReadMemoriaMeltdownSettings(ModFile? file)
    {
        return file is null ? null : ParseMemoriaSettings(file.ReadBytes()).Meltdown;
    }

    private static bool? MemoriaComponentPreference(MemoriaSettings settings, string packageId, OfficialModComponent? official = null)
    {
        if (official is not null)
        {
            return settings.Components.TryGetValue(official.SettingsKey, out bool stored) ? stored : null;
        }
        foreach (var component in MemoriaComponents)
        {
            if (packageId.Equals(component.RuntimePackageId, StringComparison.OrdinalIgnoreCase) && settings.Components.TryGetValue(component.SettingsKey, out bool enabled))
            {
                return enabled;
            }
        }
        return null;
    }

    private IReadOnlyList<BehaviorModSetting> ReadMemoriaComponentSettings(MemoriaSettings settings, bool applyLocks)
    {
        List<BehaviorModSetting> result = [];
        foreach (var component in MemoriaComponents)
        {
            ModPackage? package = installedPackages.FirstOrDefault(package => IsMemoriaComponent(package) && package.Id.Equals(component.RuntimePackageId, StringComparison.OrdinalIgnoreCase));
            if (package is null)
            {
                continue;
            }
            bool enabledByDefault = ReadPackageEnabled(package) ?? autoEnable;
            bool enabled = (applyLocks && RequiredPackageReason(package.Id) is not null) || (MemoriaComponentPreference(settings, package.Id, package.Official) ?? enabledByDefault);
            result.Add(new(ComponentBehavior, component.SettingsKey, component.Name, 0, 1, enabledByDefault ? 1 : 0, enabled ? 1 : 0)
            {
                Category = component.Category,
                Control = "toggle",
                Description = component.Description,
                Unit = string.Empty
            });
            if (component.RuntimePackageId == MeltdownPackageId)
            {
                try
                {
                    IReadOnlyList<BehaviorModSetting> behavior = ReadBehaviorSettingsCore(package, freshConfiguration: true, overrides: settings.Meltdown);
                    if (applyLocks)
                    {
                        behavior = ApplyBehaviorSettingPolicy(package, behavior);
                    }
                    result.AddRange(behavior.Select(setting => setting with { Category = component.Category, ParentComponent = component.SettingsKey }));
                }
                catch (Exception exception) when (BehaviorMods.IsDocumentError(exception))
                {
                    // The catalog reports the invalid document. Keep the component toggle available.
                }
            }
        }
        foreach (ModPackage package in installedPackages.Where(package => package.Official is not null && !MemoriaComponents.Any(component => component.RuntimePackageId.Equals(package.Id, StringComparison.OrdinalIgnoreCase))))
        {
            OfficialModComponent component = package.Official!;
            bool enabledByDefault = ReadPackageEnabled(package) ?? component.DefaultEnabled;
            bool enabled = MemoriaComponentPreference(settings, package.Id, component) ?? enabledByDefault;
            result.Add(new(ComponentBehavior, component.SettingsKey, component.Name, 0, 1, enabledByDefault ? 1 : 0, enabled ? 1 : 0)
            {
                Category = component.Category,
                Control = "toggle",
                Description = component.Description,
                Unit = string.Empty
            });
        }
        return result;
    }

    private ModOperationResult SaveMemoriaSettings(IReadOnlyList<(string Behavior, string Parameter, int Value)> changes)
    {
        try
        {
            MemoriaSettings settings = ReadMemoriaSettings();
            IReadOnlyList<BehaviorModSetting> current = ReadMemoriaComponentSettings(settings, applyLocks: false);
            foreach (var change in changes)
            {
                string? error = ValidateSettingChange(MemoriaPackageId, current, change.Behavior, change.Parameter, change.Value);
                if (error is not null)
                {
                    return new(false, error);
                }
                if (change.Behavior == ComponentBehavior)
                {
                    settings.Components[change.Parameter] = change.Value == 1;
                }
                else
                {
                    settings.Meltdown[change.Parameter] = change.Value;
                }
            }
            ModConfiguration.WriteAtomic(MemoriaSettingsPath, JsonSerializer.SerializeToUtf8Bytes(settings, new JsonSerializerOptions { WriteIndented = true }));
            ReconcilePreferences();
            RequestRefreshCore(DateTime.MinValue);
            return ModOperationResult.Completed;
        }
        catch (Exception exception) when (BehaviorMods.IsDocumentError(exception))
        {
            return OperationFailed(exception, MemoriaPackageId);
        }
    }

    private static ModServiceView GroupMemoriaComponents(ModServiceView view)
    {
        ModPackageView[] components = view.Packages.Where(item => IsMemoriaComponent(item.Package)).ToArray();
        if (components.Length == 0)
        {
            return view;
        }
        ModPackageView representative = components.FirstOrDefault(item => item.NeedsAttention)
            ?? components.FirstOrDefault(item => item.ProblemKind == ModPackageProblemKind.Pending)
            ?? components.FirstOrDefault(item => item.CommittedStatus == ModEffectiveStatus.Active)
            ?? components.FirstOrDefault(item => item.Package.RequestedEnabled)
            ?? components[0];
        ModPackage package = representative.Package with
        {
            Id = MemoriaPackageId,
            Name = MemoriaPackageId,
            RequestedEnabled = components.Any(item => item.Package.RequestedEnabled),
            RootDirectory = Path.GetDirectoryName(representative.Package.RootDirectory) ?? representative.Package.RootDirectory,
            HasLocalization = false,
            LocalizationOnly = false
        };
        ModEffectiveStatus? committedStatus = components.Any(item => item.CommittedStatus == ModEffectiveStatus.Active) ? ModEffectiveStatus.Active : representative.CommittedStatus;
        ModPackageView group = representative with { Package = package, CommittedStatus = committedStatus, CanToggle = false };
        return view with { Packages = Array.AsReadOnly(view.Packages.Where(item => !IsMemoriaComponent(item.Package)).Prepend(group).ToArray()) };
    }
}
