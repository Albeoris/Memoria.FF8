namespace Memoria.FF8.Mods;

public interface IModService
{
    ModCatalogSnapshot Catalog { get; }
    /// <summary>Installed package preferences and activation status, including packages whose publication failed.</summary>
    IReadOnlyList<ModPackage> InstalledPackages => Catalog.Packages;
    ModServiceView View => ModServiceView.Create(Catalog, InstalledPackages, AutoEnableNewMods, IsReloadPending, Diagnostics);
    bool AutoEnableNewMods { get; }
    bool IsReloadPending { get; }
    IReadOnlyList<ModDiagnostic> Diagnostics { get; }
    ModOperationResult SetEnabled(string packageId, bool enabled);
    ModOperationResult SetAutoEnableNewMods(bool enabled);
    IReadOnlyList<BehaviorModSetting> ReadBehaviorSettings(string packageId) => [];
    ModOperationResult SetBehaviorSetting(string packageId, string behavior, string parameter, int value) => new(false, "Behavior settings are unavailable.");
    ModOperationResult SetBehaviorSettings(string packageId, IReadOnlyList<BehaviorModSetting> settings)
    {
        foreach (BehaviorModSetting setting in settings)
        {
            ModOperationResult result = SetBehaviorSetting(packageId, setting.Behavior, setting.Parameter, setting.Value);
            if (!result.Success)
            {
                return result;
            }
        }
        return ModOperationResult.Completed;
    }
    string? GetPackageLockReason(string packageId) => null;
    string? GetBehaviorSettingLockReason(string packageId, string behavior, string parameter) => null;
    void Refresh();
    IDisposable Subscribe(Action<ModCatalogSnapshot> changed);
}

public sealed record ModOperationResult(bool Success, string? Error = null)
{
    public static ModOperationResult Completed { get; } = new(true);
}

/// <summary>Commit publishes only prevalidated values and must not perform fallible preparation or partially mutate published state.</summary>
public sealed record ModPreparationResult(bool Success, IReadOnlyCollection<string>? UsablePackageIds = null, IReadOnlyList<ModDiagnostic>? Diagnostics = null, Action? Commit = null, Action? AfterCommit = null)
{
    public bool Deferred { get; init; }
    public Action? Discard { get; init; }
    internal IReadOnlyList<string> RejectedPackageIds { get; init; } = [];
    public static ModPreparationResult Accepted { get; } = new(true);

    /// <summary>Rejects independently invalid package documents, with a diagnostic for each package.</summary>
    public static ModPreparationResult RejectPackagePayloads(IReadOnlyList<ModDiagnostic> diagnostics)
    {
        return new(false, Diagnostics: diagnostics)
        {
            RejectedPackageIds = diagnostics.All(diagnostic => diagnostic.PackageId is not null)
                ? diagnostics.Select(diagnostic => diagnostic.PackageId!).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
                : []
        };
    }
}
