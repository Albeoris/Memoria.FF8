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
    public static ModPreparationResult Accepted { get; } = new(true);
}
