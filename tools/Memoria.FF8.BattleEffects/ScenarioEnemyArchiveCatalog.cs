using Memoria.FF8.Assets;

namespace Memoria.FF8.BattleEffects;

public sealed record EnemyScenarioArchiveExclusion(int ModelId, string Disposition, string Reason, string[] MappingEvidence);

/// <summary>Unused empty archive entries require absence from the complete original scene slot catalog.</summary>
public sealed class ScenarioEnemyArchiveCatalog
{
    private readonly HashSet<int> referencedModels;
    private readonly bool complete;

    public ScenarioEnemyArchiveCatalog(IReadOnlyList<BattleEncounter> formations)
    {
        complete = formations.Count == 1024 && formations.Select(formation => formation.Id).Order().SequenceEqual(Enumerable.Range(0, 1024));
        referencedModels = formations.SelectMany(formation => formation.Slots).Where(slot => slot.IsEnemyModel).Select(slot => slot.ModelId).ToHashSet();
    }

    public bool CanExclude(int modelId, bool emptyAiSection) => complete && emptyAiSection && !referencedModels.Contains(modelId);

    public static EnemyScenarioArchiveExclusion Exclusion(int modelId) => new(modelId, "excluded_with_reason", "The original archive DAT has an empty AI section and no reference in any slot of the complete 1024-formation scene catalog, including disabled slots.", [$"battle/c0m{modelId:000}.dat:empty-native-ai-section", "battle/scene.out:all-1024-formations:all-slots:no-model-reference"]);

    public static DiscoveredScenario ExcludedScenario(EnemyScenarioArchiveExclusion exclusion) => new($"archive-enemy-m{exclusion.ModelId:D3}-excluded", null, $"enemy-model:{exclusion.ModelId}", "unused_installed_archive_entry", "EXCLUDED", exclusion.Disposition, false, null, null, null, $"Enemy archive model {exclusion.ModelId}", null, null, null, null, null, "No scene owner or AI bank exists for this unused archive entry.", [], [], 0, [], exclusion.MappingEvidence, [], [exclusion.Reason], []);
}
