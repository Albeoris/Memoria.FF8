namespace Memoria.FF8.Mods;

public sealed partial class ModRuntime
{
    private bool hardcore;

    public static string HardcoreStatBonusesLockReason => DifficultyModPolicy.LockReason(true, "Memoria/Level-Based Stat Bonuses")!;

    public bool SetHardcoreMode(bool enabled)
    {
        lock (gate)
        {
            ThrowIfDisposed();
            if (hardcore == enabled)
            {
                return false;
            }
            hardcore = enabled;
            ReconcilePreferences();
            RequestRefreshCore(DateTime.MinValue);
            return true;
        }
    }

    public string? GetPackageLockReason(string packageId)
    {
        lock (gate)
        {
            return RequiredPackageReason(packageId);
        }
    }

    public string? GetBehaviorSettingLockReason(string packageId, string behavior, string parameter)
    {
        lock (gate)
        {
            ModPackage? package;
            if (IsComponentGroupId(packageId))
            {
                package = installedPackages.FirstOrDefault(package => IsMemoriaComponent(package)
                    && ComponentGroupId(ComponentGroup(package)).Equals(packageId, StringComparison.OrdinalIgnoreCase)
                    && (behavior == ComponentBehavior
                        ? MemoriaComponentSettingsKey(package.Id, package.Official, package.Descriptor) == parameter
                        : behavior == StatusMods.SettingsBehavior && package.Descriptor is { } descriptor && parameter.StartsWith(descriptor.Id + "/", StringComparison.Ordinal)));
            }
            else
            {
                package = installedPackages.FirstOrDefault(package => package.Id.Equals(packageId, StringComparison.OrdinalIgnoreCase));
            }

            if (package is null)
            {
                return null;
            }
            if (behavior == StatusMods.SettingsBehavior && !DifficultyModPolicy.LocksDefaults(hardcore, package.Id))
            {
                return null;
            }
            return RequiredPackageReason(package.Id);
        }
    }
}
