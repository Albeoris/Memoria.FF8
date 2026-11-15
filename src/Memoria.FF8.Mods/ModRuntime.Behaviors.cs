using System.Text.Json;

namespace Memoria.FF8.Mods;

public sealed partial class ModRuntime
{
    private readonly Dictionary<string, (ModFile Payload, ModFile? Configuration, IReadOnlyList<BehaviorModSetting> Settings)> behaviorSettingsCache = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<BehaviorModSetting> ReadBehaviorSettings(string packageId)
    {
        lock (gate)
        {
            ThrowIfDisposed();
            if (packageId.Equals(MemoriaPackageId, StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    return memoriaSettingsView ??= ReadMemoriaComponentSettings(ReadMemoriaSettings(), applyLocks: true);
                }
                catch (Exception exception) when (BehaviorMods.IsDocumentError(exception))
                {
                    return [];
                }
            }
            ModPackage? package = installedPackages.FirstOrDefault(item => item.Id.Equals(packageId, StringComparison.OrdinalIgnoreCase));
            if (package is null || package.Format != ModFormat.Memoria)
            {
                return [];
            }
            try
            {
                IReadOnlyDictionary<string, int>? overrides = IsMemoriaComponent(package) && package.Id.Equals(MeltdownPackageId, StringComparison.OrdinalIgnoreCase) ? ReadMemoriaSettings().Meltdown : null;
                return ApplyBehaviorSettingPolicy(package, ReadBehaviorSettingsCore(package, overrides: overrides is { Count: > 0 } ? overrides : null));
            }
            catch (Exception exception) when (BehaviorMods.IsDocumentError(exception))
            {
                return [];
            }
        }
    }

    public ModOperationResult SetBehaviorSetting(string packageId, string behavior, string parameter, int value)
    {
        return SetBehaviorSettingsCore(packageId, [(behavior, parameter, value)]);
    }

    public ModOperationResult SetBehaviorSettings(string packageId, IReadOnlyList<BehaviorModSetting> settings)
    {
        return SetBehaviorSettingsCore(packageId, settings.Select(setting => (setting.Behavior, setting.Parameter, setting.Value)).ToArray());
    }

    private ModOperationResult SetBehaviorSettingsCore(string packageId, IReadOnlyList<(string Behavior, string Parameter, int Value)> changes)
    {
        lock (gate)
        {
            ThrowIfDisposed();
            if (changes.Count == 0)
            {
                return ModOperationResult.Completed;
            }
            if (packageId.Equals(MemoriaPackageId, StringComparison.OrdinalIgnoreCase))
            {
                return SaveMemoriaSettings(changes);
            }
            if (GetPackageLockReason(packageId) is string lockReason)
            {
                return new(false, lockReason);
            }
            ModPackage? package = installedPackages.FirstOrDefault(item => item.Id.Equals(packageId, StringComparison.OrdinalIgnoreCase));
            if (package is null || package.Format != ModFormat.Memoria)
            {
                return new(false, $"Unknown behavior mod package: {packageId}.");
            }
            try
            {
                if (IsMemoriaComponent(package) && package.Id.Equals(MeltdownPackageId, StringComparison.OrdinalIgnoreCase) && ReadMemoriaSettings().Meltdown.Count > 0)
                {
                    return SaveMemoriaSettings(changes);
                }
                ValidatePreferenceTarget(package);
                IReadOnlyList<BehaviorModSetting> settings = ReadBehaviorSettingsCore(package, true);
                Dictionary<(string Behavior, string Parameter), int> values = new();
                foreach ((string behavior, string parameter, int value) in changes)
                {
                    if (ValidateSettingChange(packageId, settings, behavior, parameter, value) is string error)
                    {
                        return new(false, error);
                    }
                    values[(behavior, parameter)] = value;
                }
                BehaviorModSetting[] updated = settings.Select(setting => values.TryGetValue((setting.Behavior, setting.Parameter), out int value) ? setting with { Value = value } : setting).ToArray();
                string path = BehaviorConfigurationPath(package);
                ValidatePreferencePath(path);
                Dictionary<string, Dictionary<string, int>> overrides = new(StringComparer.Ordinal);
                foreach (BehaviorModSetting item in updated)
                {
                    if (item.Value == item.DefaultValue)
                    {
                        continue;
                    }
                    if (!overrides.TryGetValue(item.Behavior, out Dictionary<string, int>? parameters))
                    {
                        parameters = new(StringComparer.Ordinal);
                        overrides.Add(item.Behavior, parameters);
                    }
                    parameters.Add(item.Parameter, item.Value);
                }
                ModConfiguration.WriteAtomic(path, JsonSerializer.SerializeToUtf8Bytes(overrides, new JsonSerializerOptions { WriteIndented = true }));
                if (behaviorSettingsCache.TryGetValue(package.Id, out var cached))
                {
                    behaviorSettingsCache[package.Id] = (cached.Payload, cached.Configuration, updated);
                }
                RequestRefreshCore(DateTime.MinValue);
                return ModOperationResult.Completed;
            }
            catch (Exception exception) when (BehaviorMods.IsDocumentError(exception))
            {
                return OperationFailed(exception, packageId);
            }
        }
    }

    private string? ValidateSettingChange(string packageId, IReadOnlyList<BehaviorModSetting> settings, string behavior, string parameter, int value)
    {
        if (GetBehaviorSettingLockReason(packageId, behavior, parameter) is string lockReason)
        {
            return lockReason;
        }
        BehaviorModSetting? setting = settings.FirstOrDefault(item => item.Behavior == behavior && item.Parameter == parameter);
        if (setting is null)
        {
            return $"The package does not declare behavior parameter {behavior}.{parameter}.";
        }
        return value < setting.Min || value > setting.Max || (value - setting.Min) % setting.Step != 0
            ? $"{behavior}.{parameter} must be between {setting.Min} and {setting.Max} in steps of {setting.Step}."
            : null;
    }

    private string BehaviorConfigurationPath(ModPackage package)
    {
        return Path.Combine(Path.GetDirectoryName(PreferencePath(package))!, BehaviorMods.ConfigurationPath);
    }

    private IReadOnlyList<BehaviorModSetting> ReadBehaviorSettingsCore(ModPackage package, bool freshConfiguration = false, IReadOnlyDictionary<string, int>? overrides = null)
    {
        ModFile[] files = fileEntries.Values.Select(entry => entry.File).Where(file => file.PackageId.Equals(package.Id, StringComparison.OrdinalIgnoreCase)).ToArray();
        ModFile? payload = files.SingleOrDefault(file => BehaviorMods.IsPayloadPath(file.RelativePath));
        if (payload is null)
        {
            return [];
        }
        ModFile? configurationFile = files.SingleOrDefault(file => file.RelativePath.Equals(BehaviorMods.ConfigurationPath, StringComparison.OrdinalIgnoreCase));
        if (!freshConfiguration && overrides is null && behaviorSettingsCache.TryGetValue(package.Id, out var cached) && ReferenceEquals(cached.Payload, payload) && ReferenceEquals(cached.Configuration, configurationFile))
        {
            return cached.Settings;
        }
        byte[]? configuration;
        if (freshConfiguration)
        {
            string path = BehaviorConfigurationPath(package);
            ValidatePreferencePath(path);
            configuration = File.Exists(path) ? File.ReadAllBytes(path) : configurationFile?.ReadBytes();
        }
        else
        {
            configuration = configurationFile?.ReadBytes();
        }
        IReadOnlyList<BehaviorModSetting> settings = BehaviorMods.ReadSettings(payload.ReadBytes(), configuration, overrides);
        if (overrides is null)
        {
            behaviorSettingsCache[package.Id] = (payload, configurationFile, settings);
        }
        return settings;
    }
}
