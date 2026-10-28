using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Memoria.FF8.Assets;
using Memoria.FF8.BattleEffects;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.BattleEffects.Tests;

public sealed class ScenarioOrdinaryAttackPreludeTests
{
    [Theory]
    [InlineData(false, new byte[] { 58, 5, 44, 0 }, true)]
    [InlineData(false, new byte[] { 2, 58, 5, 44, 0, 0, 0, 0, 0 }, false)]
    [InlineData(true, new byte[] { 29, 200, 51, 31, 2, 0 }, true)]
    [InlineData(true, new byte[] { 2, 29, 200, 51, 31, 2, 0, 0, 0 }, false)]
    public void Phase_witnesses_must_start_on_decoded_AI_instruction_boundaries(bool allocation, byte[] script, bool expected)
    {
        Assert.Equal(expected, ScenarioDiscovery.HasPreludeDeathTransition(script, allocation));
    }

    [Theory]
    [InlineData(100, 6, 120, 87, false, 5, 104)]
    [InlineData(122, 9, 511, 121, true, 3, 21)]
    public void Installed_original_carriers_generate_one_declared_ordinary_attack_prelude(int model, int slot, int carrier, int predecessor, bool allocation, int physical, int effect)
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null && Directory.Exists(directory), "Select installed original resources for the two declared native phase routes.");
        using IResourceSource source = GameDataSourceFactory.Open(directory);
        ResourceService resources = new(source);
        byte[] kernel = resources.Read(new ResourceKey("main/kernel.bin"));
        NativeCharacterVitals initial = NativeCharacterInitialization.Read(resources.Read(new ResourceKey("main/init.out")), kernel);
        using BattleAssets assets = new(directory, resources);
        BattleEncounter[] formations = [assets.LoadEncounter(120), assets.LoadEncounter(509), assets.LoadEncounter(511)];
        EnemyScenarioModel[] models = new[] { 87, 100, 121, 122, 123 }.Select(id => new EnemyScenarioModel(id, assets.ReadEnemyName(id), assets.ReadEnemyAi(id, 0))).ToArray();
        EnemyScenarioDiscoveryResult discovery = ScenarioEnemyDiscovery.Discover(formations, models, assets.NativeActions);
        EnemyScenarioActionOwner owner = discovery.Owners.Single(candidate => candidate.ModelId == model && candidate.LevelBand == 0 && candidate.AbilitySlot == slot);
        Assert.Equal(effect, owner.NativeAction!.MagicId);
        string template = Path.Combine(Path.GetTempPath(), "ordinary-phase-template-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            File.WriteAllText(template, """{"seed":0,"flags":128,"field_battle_mode":0,"elapsed_counter":0,"battle_music":5,"primary_music_channel":-1,"prepare_entry_before_presentation":true,"entry_callback_budget":1024,"fixture":{"guardian_forces":[],"characters":[{"row":0,"character_id":0,"stocks":[],"guardian_forces":[]}],"hold_enemy_commands":true},"driver":{"baseline_seconds":0.1}}""");
            Assert.True(ScenarioDiscovery.TryPrepareEnemyRecipe(assets, initial, NativeBattleExperienceDefinitionReader.Read(kernel), owner, discovery.ScriptedActions, template, out JsonObject? recipe, out EnemyScenarioFormation? chosen, out _, out string? blocker, discovery.Startups), blocker);
            Assert.Equal(carrier, chosen!.FormationId);
            Assert.Equal(carrier, recipe!["encounter"]!.GetValue<int>());
            Assert.Equal("Normal", recipe["atb_mode"]!.GetValue<string>());
            Assert.Equal(physical, recipe["target"]!["physical_index"]!.GetValue<int>());
            Assert.Equal("enemy-2", recipe["driver"]!["input"]!["actor_id"]!.GetValue<string>());
            JsonNode prelude = recipe["prelude"]!;
            Assert.Equal(predecessor, prelude["defeated_model"]!.GetValue<int>());
            Assert.Equal(allocation, prelude["require_native_allocation"]!.GetValue<bool>());
            Assert.Equal("Attack", prelude["input"]!["ability"]!.GetValue<string>());
            Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(assets.ReadEnemyAi(predecessor, 0).Scripts[3])), prelude["death_script_sha256"]!.GetValue<string>());
            SchemaValidation.ValidateBytes(System.Text.Encoding.UTF8.GetBytes(prelude.ToJsonString()), "ordinary-attack-prelude");
            string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_PRELUDE_RECIPE_OUTPUT");
            if (output is not null)
            {
                Assert.True(Path.IsPathFullyQualified(output), "Select an explicit external evidence directory for the two prepared recipes.");
                Directory.CreateDirectory(output);
                string path = Path.Combine(output, $"enemy-m{model:000}-band0-slot{slot:00}.prelude.json");
                using FileStream file = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                System.Text.Json.JsonSerializer.Serialize(file, recipe, RecordJson.Options);
            }
        }
        finally
        {
            File.Delete(template);
        }
    }
}
