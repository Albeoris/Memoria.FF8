using System.Text.Json;
using Memoria.FF8.BattleEffects;

namespace Memoria.FF8.BattleEffects.Tests;

public sealed class EffectCoverageReconciliationTests
{
    [Fact]
    public void Completed_runnable_subset_cannot_certify_the_full_native_catalog()
    {
        int[] catalog = Enumerable.Range(1, 345).Except([224, 225]).ToArray();
        int[] selected = catalog.Take(206).ToArray();
        EffectCoverageReport report = EffectCoverageReconciliation.Create(catalog, selected, selected);
        Assert.Equal(343, report.Catalog.Count);
        Assert.Equal(206, report.Runnable.Count);
        Assert.Equal(206, report.Completed.Count);
        Assert.Equal(137, report.Blocked.Count);
        Assert.Equal(catalog.Skip(206), report.Missing.EffectIds);
        Assert.Equal(report.Missing.EffectIds, report.Blocked.EffectIds);
        Assert.Empty(report.PendingRunnable.EffectIds);
        Assert.False(report.FullPreparationComplete);
        Assert.False(report.FullExecutionComplete);
        InvalidDataException error = Assert.Throws<InvalidDataException>(() => EffectCoverageReconciliation.RequireFullExecution(report));
        Assert.Contains("missing=137", error.Message);
    }

    [Fact]
    public void Null_bindings_are_excluded_but_nonempty_aliases_require_their_own_effect_identity()
    {
        DiscoveredBinding[] bindings = [Binding(1, 0x1000, 0x2000), Binding(2, 0x1000, 0x2000), Binding(3, 0, 0), Binding(4, 0, 0x2000), Binding(5, 0x1000, 0)];
        EffectCoverageReport report = EffectCoverageReconciliation.Create(bindings, [1, 2, 4, 5], [1, 4, 5]);
        Assert.Equal(new[] { 1, 2, 4, 5 }, report.Catalog.EffectIds);
        Assert.Equal(new[] { 2 }, report.Missing.EffectIds);
        Assert.True(report.FullPreparationComplete);
        Assert.False(report.FullExecutionComplete);
        Assert.DoesNotContain(3, report.Catalog.EffectIds);
    }

    [Fact]
    public void Blocked_and_pending_runnable_effects_remain_distinct_in_preparation_report()
    {
        EffectCoverageReport report = EffectCoverageReconciliation.Create([4, 3, 2, 1], [3, 1], [1]);
        Assert.Equal(new[] { 2, 4 }, report.Blocked.EffectIds);
        Assert.Equal(new[] { 3 }, report.PendingRunnable.EffectIds);
        Assert.Equal(new[] { 2, 3, 4 }, report.Missing.EffectIds);
        using JsonDocument json = JsonDocument.Parse(JsonSerializer.Serialize(report));
        Assert.False(json.RootElement.GetProperty(nameof(report.FullExecutionComplete)).GetBoolean());
        Assert.Equal(3, json.RootElement.GetProperty(nameof(report.Missing)).GetProperty(nameof(EffectCoverageSet.Count)).GetInt32());
    }

    [Fact]
    public void Duplicate_cases_are_normalized_without_inflating_completed_effect_count()
    {
        EffectCoverageReport report = EffectCoverageReconciliation.Create([2, 1, 2], [2, 1, 1, 2], [1, 2, 2, 1]);
        Assert.Equal(new[] { 1, 2 }, report.Catalog.EffectIds);
        Assert.Equal(2, report.Runnable.Count);
        Assert.Equal(2, report.Completed.Count);
        Assert.True(report.FullPreparationComplete);
        Assert.True(report.FullExecutionComplete);
        EffectCoverageReconciliation.RequireFullExecution(report);
    }

    [Fact]
    public void Foreign_ids_cannot_replace_missing_native_effects_or_certify_full_execution()
    {
        EffectCoverageReport report = EffectCoverageReconciliation.Create([1, 2], [1, 99], [1, 99]);
        Assert.Equal(new[] { 1 }, report.Runnable.EffectIds);
        Assert.Equal(new[] { 1 }, report.Completed.EffectIds);
        Assert.Equal(new[] { 99 }, report.NonCatalogRunnable.EffectIds);
        Assert.Equal(new[] { 99 }, report.NonCatalogCompleted.EffectIds);
        Assert.Equal(new[] { 2 }, report.Missing.EffectIds);
        Assert.False(report.FullExecutionComplete);
        Assert.Throws<InvalidDataException>(() => EffectCoverageReconciliation.RequireFullExecution(report));
        EffectCoverageReport completeWithForeignEvidence = EffectCoverageReconciliation.Create([1, 2], [1, 2], [1, 2, 99]);
        Assert.Empty(completeWithForeignEvidence.Missing.EffectIds);
        Assert.False(completeWithForeignEvidence.FullExecutionComplete);
    }

    [Fact]
    public void Empty_catalog_never_produces_a_vacuous_full_pass()
    {
        EffectCoverageReport report = EffectCoverageReconciliation.Create(Array.Empty<int>(), [], []);
        Assert.Equal(0, report.Catalog.Count);
        Assert.False(report.FullPreparationComplete);
        Assert.False(report.FullExecutionComplete);
        Assert.Throws<InvalidDataException>(() => EffectCoverageReconciliation.RequireFullExecution(report));
    }

    [Fact]
    public void Nonpositive_catalog_identity_is_rejected_instead_of_treated_as_an_effect()
    {
        Assert.Throws<ArgumentException>(() => EffectCoverageReconciliation.Create([0, 1], [1], [1]));
    }

    [Fact]
    public void Coverage_does_not_require_a_hardcoded_catalog_size_or_numeric_ceiling()
    {
        EffectCoverageReport report = EffectCoverageReconciliation.Create([1001, 401], [401, 1001], [1001, 401]);
        Assert.Equal(new[] { 401, 1001 }, report.Catalog.EffectIds);
        Assert.True(report.FullExecutionComplete);
    }

    private static DiscoveredBinding Binding(int effect, uint entry, uint initializer) => new(effect, entry, initializer, [], "unresolved", "test catalog binding", []);
}
