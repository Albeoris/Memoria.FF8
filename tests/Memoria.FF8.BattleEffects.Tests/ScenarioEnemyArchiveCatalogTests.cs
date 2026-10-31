using System.Buffers.Binary;
using System.Numerics;
using System.Text.Json;
using Memoria.FF8.Assets;

namespace Memoria.FF8.BattleEffects.Tests;

public sealed class ScenarioEnemyArchiveCatalogTests
{
    [Theory]
    [InlineData(2)]
    [InlineData(11)]
    public void Empty_original_AI_section_is_a_catalog_exclusion_only_without_any_scene_reference(int sections)
    {
        byte[] bytes = Directory(sections, true);
        Assert.True(BattleAssets.HasEmptyNativeEnemyAiSection(bytes));
        ScenarioEnemyArchiveCatalog catalog = new(Formations());
        Assert.True(catalog.CanExclude(37, BattleAssets.HasEmptyNativeEnemyAiSection(bytes)));
        Assert.False(catalog.CanExclude(37, BattleAssets.HasEmptyNativeEnemyAiSection(Directory(sections, false))));
        EnemyScenarioArchiveExclusion exclusion = ScenarioEnemyArchiveCatalog.Exclusion(37);
        Assert.Equal("excluded_with_reason", exclusion.Disposition);
        Assert.Contains(exclusion.MappingEvidence, evidence => evidence.Contains("all-slots:no-model-reference", StringComparison.Ordinal));
        DiscoveredScenario scenario = ScenarioEnemyArchiveCatalog.ExcludedScenario(exclusion);
        Assert.Equal("EXCLUDED", scenario.Status);
        Assert.Empty(scenario.ExecutionBlockers);
        Assert.Null(scenario.EffectId);
        Assert.Equal(0, scenario.Executions);
        EnemyScenarioDiscoveryResult result = new([], []) { ExcludedArchiveModels = [exclusion] };
        EnemyScenarioDiscoveryResult restored = JsonSerializer.Deserialize<EnemyScenarioDiscoveryResult>(JsonSerializer.Serialize(result))!;
        Assert.Empty(restored.Issues);
        Assert.Equal(37, Assert.Single(restored.ExcludedArchiveModels).ModelId);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Active_and_disabled_scene_slots_both_prevent_exclusion(bool enabled)
    {
        BattleEncounter[] formations = Formations();
        BattleEnemyPlacement slot = new(7, 37, Vector3.Zero, 1, true, true) { Enabled = enabled, InitiallySuspended = true };
        formations[81] = formations[81] with { Slots = [slot] };
        ScenarioEnemyArchiveCatalog catalog = new(formations);
        Assert.False(catalog.CanExclude(37, true));
        Assert.True(catalog.CanExclude(38, true));
    }

    [Fact]
    public void Missing_or_duplicate_formations_cannot_prove_archive_absence()
    {
        BattleEncounter[] formations = Formations();
        Assert.False(new ScenarioEnemyArchiveCatalog(formations[..1023]).CanExclude(37, true));
        formations[1023] = formations[0];
        Assert.False(new ScenarioEnemyArchiveCatalog(formations).CanExclude(37, true));
    }

    [Fact]
    public void Truncated_out_of_bounds_or_unordered_directories_stay_unresolved()
    {
        Assert.False(BattleAssets.HasEmptyNativeEnemyAiSection(new byte[12]));
        Assert.False(BattleAssets.HasEmptyNativeEnemyAiSection(Directory(11, true)[..48]));
        byte[] bytes = Directory(11, true);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(36), bytes.Length + 1);
        Assert.False(BattleAssets.HasEmptyNativeEnemyAiSection(bytes));
        bytes = Directory(11, true);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(24), 0);
        Assert.False(BattleAssets.HasEmptyNativeEnemyAiSection(bytes));
    }

    [Fact]
    public void Discovery_schema_accepts_zero_only_for_excluded_unused_archive_records()
    {
        DiscoveredScenario excluded = ScenarioEnemyArchiveCatalog.ExcludedScenario(ScenarioEnemyArchiveCatalog.Exclusion(37));
        SchemaValidation.ValidateRecord(new[] { excluded }, "scenario-plans");
        Assert.Throws<InvalidDataException>(() => SchemaValidation.ValidateRecord(new[] { excluded with { Executions = 2 } }, "scenario-plans"));
        Assert.Throws<InvalidDataException>(() => SchemaValidation.ValidateRecord(new[] { excluded with { EffectId = 17 } }, "scenario-plans"));
        Assert.Throws<InvalidDataException>(() => SchemaValidation.ValidateRecord(new[] { excluded with { ExecutionBlockers = ["An unresolved owner remains."] } }, "scenario-plans"));
        foreach (string status in new[] { "RUNNABLE", "PLANNED_BLOCKED", "UNRESOLVED", "COVERED_BY_CASE" })
        {
            DiscoveredScenario planned = excluded with { Status = status };
            Assert.Throws<InvalidDataException>(() => SchemaValidation.ValidateRecord(new[] { planned }, "scenario-plans"));
            Assert.Throws<InvalidDataException>(() => SchemaValidation.ValidateRecord(new[] { planned with { Executions = 1 } }, "scenario-plans"));
            SchemaValidation.ValidateRecord(new[] { planned with { Executions = 2 } }, "scenario-plans");
        }
        DiscoveredScenario otherExclusion = excluded with { Family = "enemy_owned_ability" };
        Assert.Throws<InvalidDataException>(() => SchemaValidation.ValidateRecord(new[] { otherExclusion }, "scenario-plans"));
        SchemaValidation.ValidateRecord(new[] { otherExclusion with { Executions = 2 } }, "scenario-plans");
    }

    private static BattleEncounter[] Formations() => Enumerable.Range(0, 1024).Select(id => new BattleEncounter(id, 0, 0, 0, 0, [])).ToArray();

    private static byte[] Directory(int sections, bool empty)
    {
        byte[] bytes = new byte[256];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, sections);
        int aiIndex = sections == 11 ? 7 : 1;
        for (int index = 0; index <= sections; index++)
        {
            int offset = index < aiIndex ? (sections + 2) * 4 : index == aiIndex && !empty ? bytes.Length - 16 : bytes.Length;
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4 + 4 * index), offset);
        }
        return bytes;
    }
}
