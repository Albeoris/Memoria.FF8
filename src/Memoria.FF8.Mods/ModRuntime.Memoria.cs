using System.Text.Json;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Mods;

public sealed partial class ModRuntime
{
    public const string MemoriaPackageId = "Memoria";
    public const string Ff7EnemyIntegrationPackageId = "Memoria/FF7EnemyIntegration";
    internal const string MemoriaSettingsFile = "memoria-settings.json";
    private const string Ff7BridgeLockReason = "The transition from FFVIII enables FFVII Enemy Integration in Automatic mode.";
    private const string ComponentBehavior = "component";
    private const string SettingsGroupPrefix = "Memoria:";
    private static IReadOnlyList<OfficialModComponent> MemoriaComponents { get; } = OfficialModCatalog.SeedComponents.Select(component => component.Id == OfficialModCatalog.GirlNextDoor.Id ? OfficialModCatalog.LegacyGirlNextDoor : component).ToArray();

    private bool forceFf7EnemyIntegration;
    private readonly Dictionary<string, IReadOnlyList<BehaviorModSetting>> memoriaSettingsViews = new(StringComparer.OrdinalIgnoreCase);
    private string MemoriaSettingsPath => Path.Combine(preferenceRoot, "Memoria", MemoriaSettingsFile);

    private sealed class MemoriaSettings
    {
        [JsonPropertyName("components")]
        public Dictionary<string, bool> Components { get; init; } = new(StringComparer.Ordinal);

        [JsonPropertyName("statusSettings")]
        public Dictionary<string, int> StatusSettings { get; init; } = new(StringComparer.Ordinal);
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
        if (DifficultyModPolicy.LockReason(hardcore, packageId) is { } difficultyReason)
        {
            return difficultyReason;
        }
        return forceFf7EnemyIntegration && packageId.Equals(Ff7EnemyIntegrationPackageId, StringComparison.OrdinalIgnoreCase) ? Ff7BridgeLockReason : null;
    }

    private static bool IsMemoriaComponent(ModPackage package)
    {
        return package.Descriptor is not null || package.Official is not null || package.IsBundled && MemoriaComponents.Any(component => package.Id.Equals(component.RuntimePackageId, StringComparison.OrdinalIgnoreCase));
    }

    public static string? SettingsGroupId(ModPackage package)
    {
        return IsMemoriaComponent(package) ? ComponentGroupId(ComponentGroup(package)) : null;
    }

    private static string ComponentGroup(ModPackage package)
    {
        string? group = package.Descriptor?.Group;
        return group is null || group.Equals(MemoriaPackageId, StringComparison.OrdinalIgnoreCase) ? MemoriaPackageId : group;
    }

    private static string ComponentGroupId(string group) => group == MemoriaPackageId ? MemoriaPackageId : SettingsGroupPrefix + group;

    private static bool IsComponentGroupId(string id) => id.Equals(MemoriaPackageId, StringComparison.OrdinalIgnoreCase) || id.StartsWith(SettingsGroupPrefix, StringComparison.OrdinalIgnoreCase);

    internal static bool IsBundledMemoriaIdentity(string id, string key)
    {
        return MemoriaComponents.Any(component => component.Delivery == "engine" && (component.SettingsKey.Equals(key, StringComparison.OrdinalIgnoreCase) || component.RuntimePackageId.Equals(id, StringComparison.OrdinalIgnoreCase)));
    }

    private static NativeModDescriptor? ReadPackageDescriptor(string root, OfficialModComponent? official, bool previouslyDescribed)
    {
        string[] paths = Directory.EnumerateFileSystemEntries(root).Where(path => Path.GetFileName(path).Equals(NativeModDescriptor.FileName, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (paths.Length == 0)
        {
            if (previouslyDescribed || official is not null && OfficialModCatalog.HasDescriptorIdentity(official))
            {
                throw new InvalidDataException("The installed native package requires a root mod.json descriptor.");
            }
            return null;
        }
        if (paths.Length != 1 || Path.GetFileName(paths[0]) != NativeModDescriptor.FileName)
        {
            throw new InvalidDataException($"Use exactly one root descriptor named '{NativeModDescriptor.FileName}'.");
        }
        NativeModDescriptor descriptor = NativeModDescriptor.Read(paths[0]);
        if (IsBundledMemoriaIdentity(descriptor.RuntimePackageId, descriptor.Id))
        {
            throw new InvalidDataException("A native mod descriptor conflicts with a bundled component.");
        }
        if (official is not null && (!OfficialModCatalog.HasDescriptorIdentity(official) || official.Id != descriptor.Id))
        {
            throw new InvalidDataException("The native mod descriptor does not match its installation receipt.");
        }
        return descriptor;
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
            settings = JsonSerializer.Deserialize<MemoriaSettings>(content, new JsonSerializerOptions { AllowDuplicateProperties = false });
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Invalid Memoria settings document.", exception);
        }
        if (settings?.Components is null || settings.StatusSettings is null)
        {
            throw new InvalidDataException("Invalid Memoria settings document.");
        }
        return settings;
    }

    private static string? MemoriaComponentSettingsKey(string packageId, OfficialModComponent? official, NativeModDescriptor? descriptor)
    {
        return descriptor?.Id ?? official?.SettingsKey ?? MemoriaComponents.FirstOrDefault(component => packageId.Equals(component.RuntimePackageId, StringComparison.OrdinalIgnoreCase))?.SettingsKey;
    }

    private static bool? MemoriaComponentPreference(MemoriaSettings settings, string packageId, OfficialModComponent? official, NativeModDescriptor? descriptor)
    {
        string? key = MemoriaComponentSettingsKey(packageId, official, descriptor);
        if (key is not null)
        {
            foreach (string candidate in OfficialModCatalog.PreferenceKeys(key))
            {
                if (settings.Components.TryGetValue(candidate, out bool enabled))
                {
                    return enabled;
                }
            }
        }
        return null;
    }

    private IReadOnlyList<BehaviorModSetting> ReadMemoriaComponentSettings(string groupId, MemoriaSettings settings, bool applyLocks)
    {
        List<BehaviorModSetting> result = [];
        if (groupId.Equals(MemoriaPackageId, StringComparison.OrdinalIgnoreCase))
        {
            groupId = MemoriaPackageId;
            foreach (var component in MemoriaComponents)
            {
                ModPackage? package = installedPackages.FirstOrDefault(package => package is { IsBundled: true, Descriptor: null } && package.Id.Equals(component.RuntimePackageId, StringComparison.OrdinalIgnoreCase));
                if (package is null)
                {
                    continue;
                }
                bool enabledByDefault = ReadPackageEnabled(package) ?? autoEnable;
                bool enabled = (applyLocks && RequiredPackageReason(package.Id) is not null) || (MemoriaComponentPreference(settings, package.Id, package.Official, package.Descriptor) ?? enabledByDefault);
                result.Add(new(ComponentBehavior, component.SettingsKey, component.Name, 0, 1, enabledByDefault ? 1 : 0, enabled ? 1 : 0)
                {
                    Category = component.Category,
                    Control = "toggle",
                    Description = component.Description,
                    Unit = string.Empty
                });
            }
        }
        foreach (ModPackage package in installedPackages.Where(package => (package.Descriptor is not null || package.Official is not null && !package.IsBundled)
            && ComponentGroupId(ComponentGroup(package)).Equals(groupId, StringComparison.OrdinalIgnoreCase)).OrderBy(package => package.Id, StringComparer.Ordinal))
        {
            NativeModDescriptor? descriptor = package.Descriptor;
            OfficialModComponent? component = package.Official;
            bool enabledByDefault = ReadPackageEnabled(package) ?? autoEnable;
            bool enabled = (applyLocks && RequiredPackageReason(package.Id) is not null) || (MemoriaComponentPreference(settings, package.Id, component, descriptor) ?? enabledByDefault);
            result.Add(new(ComponentBehavior, descriptor?.Id ?? component!.SettingsKey, package.Name, 0, 1, enabledByDefault ? 1 : 0, enabled ? 1 : 0)
            {
                Category = descriptor is null ? component!.Category : string.Empty,
                Control = "toggle",
                Description = descriptor?.DescriptionForLanguage("en") ?? component!.Description,
                Descriptor = descriptor,
                Unit = string.Empty
            });
            if (descriptor is not null)
            {
                try
                {
                    result.AddRange(ReadStatusSettings(package, settings, applyLocks).Select(setting => setting with { ParentComponent = descriptor.Id }));
                }
                catch (Exception exception) when (StatusMods.IsDocumentError(exception))
                {
                    // A rejected package can still be disabled from its settings group.
                }
            }
        }
        return result;
    }

    private ModOperationResult SaveMemoriaSettings(string groupId, IReadOnlyList<(string Behavior, string Parameter, int Value)> changes)
    {
        try
        {
            MemoriaSettings settings = ReadMemoriaSettings();
            IReadOnlyList<BehaviorModSetting> current = ReadMemoriaComponentSettings(groupId, settings, applyLocks: false);
            foreach (var change in changes)
            {
                string? error = ValidateSettingChange(groupId, current, change.Behavior, change.Parameter, change.Value);
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
                    settings.StatusSettings[change.Parameter] = change.Value;
                }
            }
            WriteMemoriaSettings(settings);
            ReconcilePreferences();
            RequestRefreshCore(DateTime.MinValue);
            return ModOperationResult.Completed;
        }
        catch (Exception exception) when (StatusMods.IsDocumentError(exception))
        {
            return OperationFailed(exception, groupId);
        }
    }

    private void WriteMemoriaSettings(MemoriaSettings settings)
    {
        ModConfiguration.WriteAtomic(MemoriaSettingsPath, JsonSerializer.SerializeToUtf8Bytes(settings, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static ModServiceView GroupMemoriaComponents(ModServiceView view)
    {
        var groups = view.Packages.Where(item => IsMemoriaComponent(item.Package)).GroupBy(item => ComponentGroup(item.Package), StringComparer.OrdinalIgnoreCase).OrderBy(group => group.Key != MemoriaPackageId).ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase).ToArray();
        if (groups.Length == 0)
        {
            return view;
        }
        return view with { Packages = Array.AsReadOnly(groups.Select(group => CreateMemoriaGroup(group.Key, group.ToArray())).Concat(view.Packages.Where(item => !IsMemoriaComponent(item.Package))).ToArray()) };
    }

    private static ModPackageView CreateMemoriaGroup(string name, ModPackageView[] components)
    {
        ModPackageView representative = components.FirstOrDefault(item => item.NeedsAttention)
            ?? components.FirstOrDefault(item => item.ProblemKind == ModPackageProblemKind.Pending)
            ?? components.FirstOrDefault(item => item.CommittedStatus == ModEffectiveStatus.Active)
            ?? components.FirstOrDefault(item => item.Package.RequestedEnabled)
            ?? components[0];
        ModPackage package = representative.Package with
        {
            Id = ComponentGroupId(name),
            Name = name,
            Descriptor = null,
            IsSettingsGroup = true,
            RequestedEnabled = components.Any(item => item.Package.RequestedEnabled),
            RootDirectory = Path.GetDirectoryName(representative.Package.RootDirectory) ?? representative.Package.RootDirectory,
            HasLocalization = false,
            LocalizationOnly = false
        };
        ModEffectiveStatus? committedStatus = components.Any(item => item.CommittedStatus == ModEffectiveStatus.Active) ? ModEffectiveStatus.Active : representative.CommittedStatus;
        return representative with { Package = package, CommittedStatus = committedStatus, CanToggle = false };
    }
}
