namespace Memoria.FF8.BattleEffects;

public sealed record EffectCoverageSet(int[] EffectIds)
{
    public int Count => EffectIds.Length;
}

public sealed record EffectCoverageReport(int SchemaVersion, EffectCoverageSet Catalog, EffectCoverageSet Runnable, EffectCoverageSet Completed, EffectCoverageSet Blocked, EffectCoverageSet PendingRunnable, EffectCoverageSet Missing, EffectCoverageSet NonCatalogRunnable, EffectCoverageSet NonCatalogCompleted)
{
    public bool FullPreparationComplete => Catalog.Count > 0 && Blocked.Count == 0 && NonCatalogRunnable.Count == 0;
    public bool FullExecutionComplete => FullPreparationComplete && Missing.Count == 0 && NonCatalogCompleted.Count == 0;
}

/// <summary>Reconciles effect identities without treating runnable scenarios or shared native callbacks as completed coverage.</summary>
public static class EffectCoverageReconciliation
{
    public static EffectCoverageReport Create(IEnumerable<DiscoveredBinding> bindings, IEnumerable<int> runnableEffectIds, IEnumerable<int> authenticatedCompletedEffectIds)
    {
        ArgumentNullException.ThrowIfNull(bindings);
        return Create(bindings.Where(binding => binding.Entry != 0 || binding.Initializer != 0).Select(binding => binding.EffectId), runnableEffectIds, authenticatedCompletedEffectIds);
    }

    /// <summary>Completed IDs must already have authenticated evidence for the required executions; this helper does not validate individual receipts or FPS.</summary>
    public static EffectCoverageReport Create(IEnumerable<int> nonemptyCatalogEffectIds, IEnumerable<int> runnableEffectIds, IEnumerable<int> authenticatedCompletedEffectIds)
    {
        ArgumentNullException.ThrowIfNull(nonemptyCatalogEffectIds);
        ArgumentNullException.ThrowIfNull(runnableEffectIds);
        ArgumentNullException.ThrowIfNull(authenticatedCompletedEffectIds);
        int[] catalog = Normalize(nonemptyCatalogEffectIds);
        if (catalog.Any(effect => effect <= 0))
        {
            throw new ArgumentException("Nonempty catalog bindings require positive effect IDs.", nameof(nonemptyCatalogEffectIds));
        }
        int[] runnable = Normalize(runnableEffectIds);
        int[] completed = Normalize(authenticatedCompletedEffectIds);
        return new(1, new(catalog), Set(catalog.Intersect(runnable)), Set(catalog.Intersect(completed)), Set(catalog.Except(runnable)), Set(catalog.Intersect(runnable).Except(completed)), Set(catalog.Except(completed)), Set(runnable.Except(catalog)), Set(completed.Except(catalog)));
    }

    /// <summary>Call only for an explicitly required full execution. Preparation can publish the same incomplete report without invoking this gate.</summary>
    public static void RequireFullExecution(EffectCoverageReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        if (!report.FullExecutionComplete)
        {
            throw new InvalidDataException($"Full effect execution is incomplete: catalog={report.Catalog.Count}, runnable={report.Runnable.Count}, completed={report.Completed.Count}, blocked={report.Blocked.Count}, pending_runnable={report.PendingRunnable.Count}, missing={report.Missing.Count}, noncatalog_runnable={report.NonCatalogRunnable.Count}, noncatalog_completed={report.NonCatalogCompleted.Count}.");
        }
    }

    private static EffectCoverageSet Set(IEnumerable<int> effectIds) => new(Normalize(effectIds));

    private static int[] Normalize(IEnumerable<int> effectIds) => effectIds.Distinct().Order().ToArray();
}
