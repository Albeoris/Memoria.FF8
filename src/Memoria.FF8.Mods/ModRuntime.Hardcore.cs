namespace Memoria.FF8.Mods;

public sealed partial class ModRuntime
{
    public const string HardcoreMeltdownLockReason = "Hardcore requires Meltdown: Vitality reduction 50%, Spirit reduction 25%. The mod cannot be disabled and these settings cannot be changed.";
    public const string HardcoreStatBonusesLockReason = "Hardcore requires Level-Based Stat Bonuses. The mod cannot be disabled.";
    private bool hardcore;

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
        if (packageId.Equals(MemoriaPackageId, StringComparison.OrdinalIgnoreCase))
        {
            string? componentId = MemoriaComponents.FirstOrDefault(component => component.SettingsKey.Equals(parameter, StringComparison.OrdinalIgnoreCase))?.RuntimePackageId;
            return behavior == ComponentBehavior ? componentId is null ? null : GetPackageLockReason(componentId) : GetPackageLockReason(MeltdownPackageId);
        }
        return GetPackageLockReason(packageId);
    }

    private bool IsRequiredHardcorePackage(string packageId)
    {
        return hardcore && (packageId.Equals("Memoria/Meltdown", StringComparison.OrdinalIgnoreCase) || packageId.Equals("Memoria/Level-Based Stat Bonuses", StringComparison.OrdinalIgnoreCase));
    }

    private IReadOnlyList<BehaviorModSetting> ApplyBehaviorSettingPolicy(ModPackage package, IReadOnlyList<BehaviorModSetting> settings)
    {
        if (!hardcore || !package.Id.Equals("Memoria/Meltdown", StringComparison.OrdinalIgnoreCase))
        {
            return settings;
        }
        return settings.Select(setting => setting with { Value = setting.Parameter switch
        {
            BehaviorMods.VitalityReductionParameter => BehaviorMods.HardcoreVitalityReductionPercent,
            BehaviorMods.SpiritReductionParameter => BehaviorMods.HardcoreSpiritReductionPercent,
            _ => setting.Value
        } }).ToArray();
    }
}
