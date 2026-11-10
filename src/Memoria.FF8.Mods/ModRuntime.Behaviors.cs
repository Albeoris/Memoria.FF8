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
            ModPackage? package = installedPackages.FirstOrDefault(item => item.Id.Equals(packageId, StringComparison.OrdinalIgnoreCase));
            if (package is null || package.Format != ModFormat.Memoria)
            {
                return [];
            }
            try
            {
                return ReadBehaviorSettingsCore(package);
            }
            catch (Exception exception) when (BehaviorMods.IsDocumentError(exception))
            {
                return [];
            }
        }
    }

    public ModOperationResult SetBehaviorSetting(string packageId, string behavior, string parameter, int value)
    {
        lock (gate)
        {
            ThrowIfDisposed();
            ModPackage? package = installedPackages.FirstOrDefault(item => item.Id.Equals(packageId, StringComparison.OrdinalIgnoreCase));
            if (package is null || package.Format != ModFormat.Memoria)
            {
                return new(false, $"Unknown behavior mod package: {packageId}.");
            }
            try
            {
                ValidatePreferenceTarget(package);
                IReadOnlyList<BehaviorModSetting> settings = ReadBehaviorSettingsCore(package, true);
                BehaviorModSetting? setting = settings.FirstOrDefault(item => item.Behavior == behavior && item.Parameter == parameter);
                if (setting is null)
                {
                    return new(false, $"The package does not declare behavior parameter {behavior}.{parameter}.");
                }
                if (value < setting.Min || value > setting.Max)
                {
                    return new(false, $"{behavior}.{parameter} must be between {setting.Min} and {setting.Max}.");
                }
                string path = BehaviorConfigurationPath(package);
                ValidatePreferencePath(path);
                Dictionary<string, Dictionary<string, int>> overrides = new(StringComparer.Ordinal);
                foreach (BehaviorModSetting item in settings)
                {
                    int selectedValue = item == setting ? value : item.Value;
                    if (selectedValue == item.DefaultValue)
                    {
                        continue;
                    }
                    if (!overrides.TryGetValue(item.Behavior, out Dictionary<string, int>? parameters))
                    {
                        parameters = new(StringComparer.Ordinal);
                        overrides.Add(item.Behavior, parameters);
                    }
                    parameters.Add(item.Parameter, selectedValue);
                }
                ModConfiguration.WriteAtomic(path, JsonSerializer.SerializeToUtf8Bytes(overrides, new JsonSerializerOptions { WriteIndented = true }));
                if (behaviorSettingsCache.TryGetValue(package.Id, out var cached))
                {
                    behaviorSettingsCache[package.Id] = (cached.Payload, cached.Configuration, settings.Select(item => item == setting ? item with { Value = value } : item).ToArray());
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

    private string BehaviorConfigurationPath(ModPackage package)
    {
        return Path.Combine(Path.GetDirectoryName(PreferencePath(package))!, BehaviorMods.ConfigurationPath);
    }

    private IReadOnlyList<BehaviorModSetting> ReadBehaviorSettingsCore(ModPackage package, bool freshConfiguration = false)
    {
        ModFile[] files = fileEntries.Values.Select(entry => entry.File).Where(file => file.PackageId.Equals(package.Id, StringComparison.OrdinalIgnoreCase)).ToArray();
        ModFile? payload = files.SingleOrDefault(file => BehaviorMods.IsPayloadPath(file.RelativePath));
        if (payload is null)
        {
            return [];
        }
        ModFile? configurationFile = files.SingleOrDefault(file => file.RelativePath.Equals(BehaviorMods.ConfigurationPath, StringComparison.OrdinalIgnoreCase));
        if (!freshConfiguration && behaviorSettingsCache.TryGetValue(package.Id, out var cached) && ReferenceEquals(cached.Payload, payload) && ReferenceEquals(cached.Configuration, configurationFile))
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
        IReadOnlyList<BehaviorModSetting> settings = BehaviorMods.ReadSettings(payload.ReadBytes(), configuration);
        behaviorSettingsCache[package.Id] = (payload, configurationFile, settings);
        return settings;
    }
}
