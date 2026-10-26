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
public sealed record ModPackageView(ModPackage Package, ModEffectiveStatus? CommittedStatus, string StatusText, string? Problem, bool NeedsAttention, bool CanToggle);

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
            if (incompatible)
            {
                status = "Incompatible edition";
                problem ??= "This format is incompatible with the selected game-data edition.";
            }
            else if (unusable)
            {
                status = active ? "Active · no usable update" : "No usable entries";
                problem ??= "This package has no usable entries for the current configuration.";
            }
            else if (loadFailure && !failed)
            {
                status = active ? "Active · load failed" : "Not active · load failed";
                problem ??= "This package could not be loaded.";
            }
            else if (issue?.Code == "configuration-write-failed")
            {
                status = $"{(active ? "Active" : "Disabled")} · setting not saved";
            }
            else if (affected)
            {
                string qualifier = current == ModUpdateOutcome.ReviewRequired ? "review required" : current == ModUpdateOutcome.Blocked ? "changes blocked" : !package.RequestedEnabled && active ? "disable failed" : active ? "update failed" : "enable failed";
                status = $"{(active ? "Active" : "Not active")} · {qualifier}";
                problem ??= current == ModUpdateOutcome.ReviewRequired ? "Saved changes require GF review before activation." : current == ModUpdateOutcome.Blocked ? "Saved changes cannot be committed at the current gameplay boundary." : "Saved changes could not be applied. The previous runtime configuration remains in use.";
            }
            else if (pending && changed)
            {
                status = active ? package.RequestedEnabled && discovered.Contains(package.Id) ? "Active · updating…" : "Active · disabling…" : package.RequestedEnabled ? "Enabling…" : "Disabled";
                problem ??= "The saved request is waiting for runtime publication.";
            }
            packages.Add(new(package, actual?.Status, status, problem, incompatible || unusable || loadFailure || affected || issue is not null, !incompatible && discovered.Contains(package.Id)));
        }
        return new(Array.AsReadOnly(packages.ToArray()), autoEnable, pending, current, Array.AsReadOnly(notices));
    }
}
