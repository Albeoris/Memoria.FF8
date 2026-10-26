using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Assets;
using Memoria.FF8.BattleEffects;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.BattleEffects.Tests;

public sealed class ScenarioPairedEnemyRecipeTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Mad_Cow_Special_uses_both_original_formation_actors_and_the_actual_second_actor_producer(int band)
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null && Directory.Exists(directory), "Select installed resources for the original paired enemy recipe.");
        using IResourceSource source = GameDataSourceFactory.Open(directory);
        ResourceService resources = new(source);
        byte[] kernel = resources.Read(new ResourceKey("main/kernel.bin"));
        NativeCharacterVitals initial = NativeCharacterInitialization.Read(resources.Read(new ResourceKey("main/init.out")), kernel);
        NativeBattleExperienceDefinition experience = NativeBattleExperienceDefinitionReader.Read(kernel);
        using BattleAssets assets = new(directory, resources);
        BattleEncounter single = assets.LoadEncounter(189);
        BattleEncounter paired = assets.LoadEncounter(190);
        EnemyScenarioModel model = new(63, assets.ReadEnemyName(63), assets.ReadEnemyAi(63, 0));
        EnemyScenarioDiscoveryResult discovery = ScenarioEnemyDiscovery.Discover([single, paired], [model], assets.NativeActions);
        EnemyScenarioActionOwner owner = discovery.Owners.Single(value => value.ModelId == 63 && value.LevelBand == band && value.AbilitySlot == 2);
        Assert.Equal((ushort)258, owner.NativeAction!.MagicId);
        JsonElement originalVitals = JsonSerializer.SerializeToElement(initial);
        JsonElement originalFormation = JsonSerializer.SerializeToElement(paired);
        string? selectedTemplate = Environment.GetEnvironmentVariable("MEMORIA_FF8_ENEMY_RECIPE_TEMPLATE");
        string template = selectedTemplate ?? Path.Combine(Path.GetTempPath(), "paired-enemy-recipe-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            if (selectedTemplate is null)
            {
                File.WriteAllText(template, """{"fixture":{"characters":[{"row":0,"character_id":0,"stocks":[],"guardian_forces":[]}]},"driver":{}}""");
            }
            EnemyScenarioActionOwner unsuitable = owner with { Formations = owner.Formations.Where(value => value.FormationId == 189).ToArray() };
            Assert.False(ScenarioDiscovery.TryPrepareEnemyRecipe(assets, initial, experience, unsuitable, discovery.ScriptedActions, template, out _, out _, out _, out _));
            Assert.True(ScenarioDiscovery.TryPrepareEnemyRecipe(assets, initial, experience, owner, discovery.ScriptedActions, template, out JsonObject? recipe, out EnemyScenarioFormation? context, out EnemyScenarioScriptedAction? producer, out string? blocker), blocker);
            Assert.NotNull(recipe);
            Assert.Equal(190, context!.FormationId);
            Assert.Equal(50, context.StageId);
            Assert.Equal(1, context.SceneSlot);
            Assert.Equal(1, producer!.Callback);
            Assert.Contains(producer, discovery.ScriptedActions);
            Assert.Equal((byte?)8, producer.Command);
            Assert.Equal((ushort?)213, producer.ActionId);
            Assert.Equal((byte?)14, producer.Sequence);
            Assert.Equal("enemy-1", recipe["driver"]!["input"]!["actor_id"]!.GetValue<string>());
            Assert.Equal("party-0", recipe["driver"]!["input"]!["target_id"]!.GetValue<string>());
            Assert.Equal(1, recipe["driver"]!["enemy_ability"]!["scene_slot"]!.GetValue<int>());
            Assert.Equal(band, recipe["driver"]!["enemy_ability"]!["level_band"]!.GetValue<int>());
            Assert.Equal(4, recipe["target"]!["physical_index"]!.GetValue<int>());
            Assert.Equal(63, recipe["target"]!["model"]!.GetValue<int>());
            Assert.Equal(new[] { 62, 63 }, paired.Slots.Where(value => value.Enabled).Select(value => value.ModelId));
            int preparedLevel = recipe["fixture"]!["characters"]![0]!["level"]!.GetValue<int>();
            byte[] levels = initial.CombatParty.Where(row => row != 255).Select(row => row == 0 ? checked((byte)preparedLevel) : NativeStatDefinitions.Level(initial.Records[row].Experience, initial.Definitions.Characters[initial.Records[row].CharacterId])).ToArray();
            int[] actualBands = ScenarioDiscovery.ResolvePossibleEnemyLevels(checked((byte)context.SceneLevel), levels).Select(level => assets.ReadEnemyAi(63, level).LevelBand).Distinct().ToArray();
            Assert.Equal(new[] { band }, actualBands);
            Assert.True(JsonElement.DeepEquals(originalVitals, JsonSerializer.SerializeToElement(initial)));
            Assert.True(JsonElement.DeepEquals(originalFormation, JsonSerializer.SerializeToElement(paired)));
            if (Environment.GetEnvironmentVariable("MEMORIA_FF8_ENEMY_RECIPE_OUTPUT") is string output)
            {
                Assert.True(Path.IsPathFullyQualified(output));
                Directory.CreateDirectory(output);
                using FileStream file = new(Path.Combine(output, $"band-{band}.json"), FileMode.CreateNew, FileAccess.Write, FileShare.Read);
                using Utf8JsonWriter writer = new(file, new JsonWriterOptions { Indented = true });
                recipe.WriteTo(writer);
            }
        }
        finally
        {
            if (selectedTemplate is null)
            {
                File.Delete(template);
            }
        }
    }
}
