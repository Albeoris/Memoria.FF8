using System.Numerics;
using System.Text.Json.Nodes;
using Memoria.FF8.Assets;
using Memoria.FF8.BattleEffects;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.BattleEffects.Tests;

public sealed class ScenarioEnemyEntryRecipeTests
{
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    public void Fixed_startup_target_requires_an_allocated_original_actor_unless_its_producer_is_suspended(bool targetEnabled, bool producerSuspended, bool expected)
    {
        EnemyScenarioDiscoveryResult discovery = Discover([48, 4, 140, 12, 1, 0]);
        BattleEnemyPlacement producer = new(0, 123, Vector3.Zero, 20, true, true) { InitiallySuspended = producerSuspended };
        BattleEnemyPlacement target = new(1, 124, Vector3.Zero, 20, true, true) { Enabled = targetEnabled };
        BattleEncounter formation = new(42, 9, 0, 0, 0, [producer]) { Slots = [producer, target] };

        Assert.Equal(expected, ScenarioDiscovery.TryValidateFreshEnemyEntry(formation, discovery.ScriptedActions, out string? blocker, discovery.Startups));
        if (!expected)
        {
            Assert.Contains("selector 140", blocker);
        }
        Assert.True(discovery.ScriptedActions.All(action => action.UnconditionalStartupPrefix));
    }

    [Theory]
    [InlineData(new byte[] { 2, 0, 0, 0, 0, 0, 0, 0, 4, 140, 12, 1, 0 })]
    [InlineData(new byte[] { 31, 3, 4, 140, 12, 1, 0 })]
    [InlineData(new byte[] { 53, 4, 4, 140, 12, 1, 0 })]
    [InlineData(new byte[] { 35, 0, 0, 4, 140, 12, 1, 0 })]
    public void Conditional_or_stateful_prefix_is_not_reclassified_as_an_unconditional_cold_dependency(byte[] code)
    {
        EnemyScenarioDiscoveryResult discovery = Discover(code);
        BattleEncounter formation = new(42, 9, 0, 0, 0, [new(0, 123, Vector3.Zero, 20, false, false)]);

        Assert.NotEmpty(discovery.ScriptedActions);
        Assert.All(discovery.ScriptedActions, action => Assert.False(action.UnconditionalStartupPrefix));
        Assert.True(ScenarioDiscovery.TryValidateFreshEnemyEntry(formation, discovery.ScriptedActions, out string? blocker, discovery.Startups));
        Assert.Null(blocker);
    }

    [Theory]
    [InlineData(new byte[] { 31, 3, 0 }, false)]
    [InlineData(new byte[] { 2, 0, 0, 0, 0, 0, 0, 0, 31, 3, 0 }, false)]
    [InlineData(new byte[] { 255, 0 }, false)]
    [InlineData(new byte[] { 2 }, false)]
    [InlineData(new byte[] { 35, 255, 127 }, false)]
    [InlineData(new byte[] { 0 }, true)]
    public void Another_initializing_actor_with_possible_allocation_or_unknown_flow_prevents_cold_rejection(byte[] otherStartup, bool allocationAbsenceProved)
    {
        NativeBattleAiAbility[] abilities = Enumerable.Repeat(new NativeBattleAiAbility(8, 13, 352), 48).ToArray();
        EnemyScenarioModel producer = new(123, "Synthetic producer", new([new byte[] { 48, 4, 140, 12, 1, 0 }, [], [], [], []], abilities, 0, []));
        EnemyScenarioModel other = new(11, "Other initializer", new([otherStartup, [], [], [], []], abilities, 0, []));
        EnemyScenarioDiscoveryResult discovery = ScenarioEnemyDiscovery.Discover([], [producer, other], []);
        BattleEncounter formation = new(42, 9, 0, 0, 0, [new(0, 11, Vector3.Zero, 20, false, false), new(1, 123, Vector3.Zero, 20, true, true)]);

        Assert.Equal(allocationAbsenceProved, discovery.Startups.Single(startup => startup.ModelId == 11).AllocationAbsenceProved);
        Assert.Equal(!allocationAbsenceProved, ScenarioDiscovery.TryValidateFreshEnemyEntry(formation, discovery.ScriptedActions, out _, discovery.Startups));
        Assert.True(ScenarioDiscovery.TryValidateFreshEnemyEntry(formation, discovery.ScriptedActions, out _));
    }

    [Fact]
    public void Installed_phase_records_require_the_missing_startup_target_and_model121_selects_the_original_fresh_entry()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null && Directory.Exists(directory), "Select installed resources for the original phase entry recipe.");
        using IResourceSource source = GameDataSourceFactory.Open(directory);
        ResourceService resources = new(source);
        byte[] kernel = resources.Read(new ResourceKey("main/kernel.bin"));
        NativeCharacterVitals initial = NativeCharacterInitialization.Read(resources.Read(new ResourceKey("main/init.out")), kernel);
        NativeBattleExperienceDefinition experience = NativeBattleExperienceDefinitionReader.Read(kernel);
        using BattleAssets assets = new(directory, resources);
        BattleEncounter[] formations = [assets.LoadEncounter(508), assets.LoadEncounter(509), assets.LoadEncounter(511)];
        EnemyScenarioModel[] models = new[] { 121, 122, 123 }.Select(model => new EnemyScenarioModel(model, assets.ReadEnemyName(model), assets.ReadEnemyAi(model, 0))).ToArray();
        EnemyScenarioDiscoveryResult discovery = ScenarioEnemyDiscovery.Discover(formations, models, assets.NativeActions);
        foreach (BattleEncounter phase in formations.Take(2))
        {
            Assert.False(ScenarioDiscovery.TryValidateFreshEnemyEntry(phase, discovery.ScriptedActions, out string? blocker, discovery.Startups));
            Assert.Contains("selector 140", blocker);
        }
        Assert.True(ScenarioDiscovery.TryValidateFreshEnemyEntry(formations[2], discovery.ScriptedActions, out _, discovery.Startups));
        string template = Path.Combine(Path.GetTempPath(), "enemy-entry-recipe-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            File.WriteAllText(template, """{"fixture":{"characters":[{"row":0,"character_id":0,"stocks":[],"guardian_forces":[]}]},"driver":{}}""");
            int prepared = 0;
            foreach (EnemyScenarioActionOwner owner in discovery.Owners.Where(owner => owner.ModelId == 121 && owner.NativeAction?.MagicId == 207))
            {
                if (ScenarioDiscovery.TryPrepareEnemyRecipe(assets, initial, experience, owner, discovery.ScriptedActions, template, out JsonObject? recipe, out EnemyScenarioFormation? chosen, out _, out _, discovery.Startups))
                {
                    Assert.Equal(511, chosen!.FormationId);
                    Assert.Equal(511, recipe!["encounter"]!.GetValue<int>());
                    prepared++;
                }
            }
            Assert.True(prepared > 0);
        }
        finally
        {
            File.Delete(template);
        }
    }

    private static EnemyScenarioDiscoveryResult Discover(byte[] startup)
    {
        NativeBattleAiAbility[] abilities = Enumerable.Repeat(new NativeBattleAiAbility(8, 13, 352), 48).ToArray();
        EnemyScenarioModel model = new(123, "Synthetic startup producer", new([startup, [], [], [], []], abilities, 0, []));
        return ScenarioEnemyDiscovery.Discover([], [model], []);
    }
}
