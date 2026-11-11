namespace Memoria.FF8.Mods;

public sealed partial class ModRuntime
{
    public const string HardcoreMeltdownLockReason = "Hardcore requires Meltdown: Vitality reduction 50%, Spirit reduction 25%. The mod cannot be disabled and these settings cannot be changed.";
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
            return IsRequiredHardcorePackage(packageId) ? HardcoreMeltdownLockReason : null;
        }
    }

    public string? GetBehaviorSettingLockReason(string packageId, string behavior, string parameter)
    {
        return GetPackageLockReason(packageId);
    }

    private bool IsRequiredHardcorePackage(string packageId)
    {
        return hardcore && packageId.Equals("Memoria/Meltdown", StringComparison.OrdinalIgnoreCase);
    }

    private IReadOnlyList<BehaviorModSetting> ApplyBehaviorSettingPolicy(ModPackage package, IReadOnlyList<BehaviorModSetting> settings)
    {
        if (!IsRequiredHardcorePackage(package.Id))
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
