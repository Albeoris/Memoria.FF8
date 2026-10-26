namespace Memoria.FF8.Mods;

public enum ModUpdateOutcome
{
    Idle,
    Updating,
    Failed,
    ReviewRequired,
    Blocked,
    PublishedWithWarnings
}

/// <summary>A saved preference and its separately committed runtime state.</summary>
public enum ModPackagePresentationKind
{
    Unknown,
    Basic,
    Incompatible,
    NoUsableEntries,
    LoadFailed,
    SettingNotSaved,
    ReviewRequired,
    ChangesBlocked,
    DisableFailed,
    UpdateFailed,
    EnableFailed,
    ActiveUpdating,
    ActiveDisabling,
    Enabling
}

public enum ModPackageProblemKind
{
    Diagnostic,
    Incompatible,
    NoUsableEntries,
    LoadFailed,
    ReviewRequired,
    ChangesBlocked,
    ApplyFailed,
    Pending
}

public sealed record ModPackageView(ModPackage Package, ModEffectiveStatus? CommittedStatus, string StatusText, string? Problem, bool NeedsAttention, bool CanToggle)
{
    public ModPackagePresentationKind PresentationKind { get; init; }
    public ModPackageProblemKind ProblemKind { get; init; }
}

/// <summary>An immutable presentation snapshot. Reading it never discovers files or changes activation.</summary>
public sealed record ModServiceView(IReadOnlyList<ModPackageView> Packages, bool AutoEnableNewMods, bool IsReloadPending, ModUpdateOutcome Outcome, IReadOnlyList<ModDiagnostic> Diagnostics)
{
    public static ModServiceView Create(ModCatalogSnapshot catalog, IReadOnlyList<ModPackage> installed, bool autoEnable, bool pending, IReadOnlyList<ModDiagnostic> diagnostics, ModUpdateOutcome? outcome = null, IReadOnlySet<string>? changedPackageIds = null)
    {
        ModDiagnostic[] notices = diagnostics.Distinct().ToArray();
        ModUpdateOutcome current = outcome ?? (notices.Any(diagnostic => diagnostic.Code is "reload-failed" or "configuration-write-failed" or "configuration-rollback-failed") ? ModUpdateOutcome.Failed : pending ? ModUpdateOutcome.Updating : ModUpdateOutcome.Idle);
        if (current is ModUpdateOutcome.Failed or ModUpdateOutcome.Idle or ModUpdateOutcome.Updating)
        {
            if (notices.Any(diagnostic => diagnostic.Code == "Memoria.GuardianForceReview"))
            {
                current = ModUpdateOutcome.ReviewRequired;
            }
            else if (notices.Any(diagnostic => diagnostic.Code == "Memoria.GuardianForceBoundary"))
            {
                current = ModUpdateOutcome.Blocked;
            }
        }
        if (current == ModUpdateOutcome.Idle && notices.Any(diagnostic => diagnostic.Code is "subscriber-failed" or "resource-rejected"))
        {
            current = ModUpdateOutcome.PublishedWithWarnings;
        }
        Dictionary<string, ModPackage> committed = catalog.Packages.ToDictionary(package => package.Id, StringComparer.OrdinalIgnoreCase);
        HashSet<string> discovered = new(installed.Select(package => package.Id), StringComparer.OrdinalIgnoreCase);
        List<ModPackageView> packages = [];
        foreach (ModPackage package in installed.Concat(catalog.Packages.Where(package => !discovered.Contains(package.Id))))
        {
            committed.TryGetValue(package.Id, out ModPackage? actual);
            bool active = actual?.Status == ModEffectiveStatus.Active;
            bool incompatible = package.Status == ModEffectiveStatus.IncompatibleEdition;
            bool unusable = package.Status == ModEffectiveStatus.NoUsableEntries;
            bool loadFailure = actual?.Status == ModEffectiveStatus.ReloadFailure || package.Status == ModEffectiveStatus.ReloadFailure && current is ModUpdateOutcome.Idle or ModUpdateOutcome.PublishedWithWarnings;
            ModDiagnostic? issue = notices.FirstOrDefault(diagnostic => string.Equals(diagnostic.PackageId, package.Id, StringComparison.OrdinalIgnoreCase));
            bool changed = package.RequestedEnabled != active || changedPackageIds?.Contains(package.Id) == true || !discovered.Contains(package.Id);
            bool failed = current is ModUpdateOutcome.Failed or ModUpdateOutcome.Blocked or ModUpdateOutcome.ReviewRequired;
            bool affected = failed && changed;
            string status = active ? "Active" : package.RequestedEnabled ? "Not active" : "Disabled";
            string? problem = issue?.Message;
            ModPackagePresentationKind presentation = ModPackagePresentationKind.Basic;
            ModPackageProblemKind problemKind = ModPackageProblemKind.Diagnostic;
            void FallbackProblem(string text, ModPackageProblemKind kind)
            {
                if (problem is null)
                {
                    problem = text;
                    problemKind = kind;
                }
            }
            if (incompatible)
            {
                status = "Incompatible edition";
                presentation = ModPackagePresentationKind.Incompatible;
                FallbackProblem("This format is incompatible with the selected game-data edition.", ModPackageProblemKind.Incompatible);
            }
            else if (unusable)
            {
                status = active ? "Active · no usable update" : "No usable entries";
                presentation = ModPackagePresentationKind.NoUsableEntries;
                FallbackProblem("This package has no usable entries for the current configuration.", ModPackageProblemKind.NoUsableEntries);
            }
            else if (loadFailure && !failed)
            {
                status = active ? "Active · load failed" : "Not active · load failed";
                presentation = ModPackagePresentationKind.LoadFailed;
                FallbackProblem("This package could not be loaded.", ModPackageProblemKind.LoadFailed);
            }
            else if (issue?.Code == "configuration-write-failed")
            {
                status = $"{(active ? "Active" : "Disabled")} · setting not saved";
                presentation = ModPackagePresentationKind.SettingNotSaved;
            }
            else if (affected)
            {
                string qualifier = current == ModUpdateOutcome.ReviewRequired ? "review required" : current == ModUpdateOutcome.Blocked ? "changes blocked" : !package.RequestedEnabled && active ? "disable failed" : active ? "update failed" : "enable failed";
                status = $"{(active ? "Active" : "Not active")} · {qualifier}";
                presentation = current == ModUpdateOutcome.ReviewRequired ? ModPackagePresentationKind.ReviewRequired : current == ModUpdateOutcome.Blocked ? ModPackagePresentationKind.ChangesBlocked : !package.RequestedEnabled && active ? ModPackagePresentationKind.DisableFailed : active ? ModPackagePresentationKind.UpdateFailed : ModPackagePresentationKind.EnableFailed;
                FallbackProblem(current == ModUpdateOutcome.ReviewRequired ? "Saved changes require GF review before activation." : current == ModUpdateOutcome.Blocked ? "Saved changes cannot be committed at the current gameplay boundary." : "Saved changes could not be applied. The previous runtime configuration remains in use.", current == ModUpdateOutcome.ReviewRequired ? ModPackageProblemKind.ReviewRequired : current == ModUpdateOutcome.Blocked ? ModPackageProblemKind.ChangesBlocked : ModPackageProblemKind.ApplyFailed);
            }
            else if (pending && changed)
            {
                status = active ? package.RequestedEnabled && discovered.Contains(package.Id) ? "Active · updating…" : "Active · disabling…" : package.RequestedEnabled ? "Enabling…" : "Disabled";
                presentation = active ? package.RequestedEnabled && discovered.Contains(package.Id) ? ModPackagePresentationKind.ActiveUpdating : ModPackagePresentationKind.ActiveDisabling : package.RequestedEnabled ? ModPackagePresentationKind.Enabling : ModPackagePresentationKind.Basic;
                FallbackProblem("The saved request is waiting for runtime publication.", ModPackageProblemKind.Pending);
            }
            packages.Add(new(package, actual?.Status, status, problem, incompatible || unusable || loadFailure || affected || issue is not null, !incompatible && discovered.Contains(package.Id)) { PresentationKind = presentation, ProblemKind = problemKind });
        }
        return new(Array.AsReadOnly(packages.ToArray()), autoEnable, pending, current, Array.AsReadOnly(notices));
    }
}
