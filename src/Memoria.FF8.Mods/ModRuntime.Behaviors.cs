namespace Memoria.FF8.Mods;

public sealed partial class ModRuntime
{
    public IReadOnlyList<BehaviorModSetting> ReadBehaviorSettings(string packageId)
    {
        lock (gate)
        {
            ThrowIfDisposed();
            try
            {
                if (IsComponentGroupId(packageId))
                {
                    if (!memoriaSettingsViews.TryGetValue(packageId, out IReadOnlyList<BehaviorModSetting>? settings))
                    {
                        settings = ReadMemoriaComponentSettings(packageId, ReadMemoriaSettings(), applyLocks: true);
                        memoriaSettingsViews.Add(packageId, settings);
                    }
                    return settings;
                }

                ModPackage? package = installedPackages.FirstOrDefault(item => item.Id.Equals(packageId, StringComparison.OrdinalIgnoreCase));
                return package?.Descriptor is null ? [] : ReadStatusSettings(package, ReadMemoriaSettings(), applyLocks: true);
            }
            catch (Exception exception) when (StatusMods.IsDocumentError(exception))
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
            if (IsComponentGroupId(packageId))
            {
                return SaveMemoriaSettings(packageId, changes);
            }

            ModPackage? package = installedPackages.FirstOrDefault(item => item.Id.Equals(packageId, StringComparison.OrdinalIgnoreCase) && item.Descriptor is not null);
            if (package is null || changes.Any(change => change.Behavior != StatusMods.SettingsBehavior || !change.Parameter.StartsWith(package.Descriptor!.Id + "/", StringComparison.Ordinal)))
            {
                return new(false, $"Invalid status settings for {packageId}.");
            }
            return SaveMemoriaSettings(ComponentGroupId(ComponentGroup(package)), changes);
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
            return $"The package does not declare setting {behavior}.{parameter}.";
        }
        return value < setting.Min || value > setting.Max || (value - setting.Min) % setting.Step != 0
            ? $"{behavior}.{parameter} must be between {setting.Min} and {setting.Max} in steps of {setting.Step}."
            : null;
    }

    private IReadOnlyList<BehaviorModSetting> ReadStatusSettings(ModPackage package, MemoriaSettings settings, bool applyLocks)
    {
        IEnumerable<ModFile> files = fileEntries.Values.Select(entry => entry.File).Where(file => file.PackageId.Equals(package.Id, StringComparison.OrdinalIgnoreCase)).OrderBy(file => file.RelativePath, PathOrder);
        bool lockDefaults = applyLocks && DifficultyModPolicy.LocksDefaults(hardcore, package.Id);
        try
        {
            return StatusMods.ReadSettings(package, files, settings.StatusSettings, lockDefaults);
        }
        catch (Exception exception) when (applyLocks && StatusMods.IsDocumentError(exception))
        {
            ModPackage? committed = catalog.Packages.FirstOrDefault(item => item.Id.Equals(package.Id, StringComparison.OrdinalIgnoreCase));
            if (committed is null)
            {
                throw;
            }
            return StatusMods.ReadSettings(committed, catalog.Files.Where(file => file.PackageId.Equals(committed.Id, StringComparison.OrdinalIgnoreCase)), catalog.StatusPreferences, lockDefaults);
        }
    }
}
