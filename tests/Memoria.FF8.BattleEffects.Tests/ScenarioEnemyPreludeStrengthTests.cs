using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Assets;
using Memoria.FF8.BattleEffects;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.BattleEffects.Tests;

public sealed class ScenarioEnemyPreludeStrengthTests
{
    [Fact]
    public void Installed_prelude_preparation_selects_best_native_strength_stock_and_preserves_existing_learning_and_band()
    {
        var (original, reader, _) = InstalledInputs();
        BattleNativePlayerStatRules rules = reader.ReadRules(original, 0);
        JsonObject recipe = Recipe(original);
        int reserved = Enumerable.Range(0, 16).First(id => original.AcquireGuardianForce(id).GuardianForceAbilities(id).Any(ability => !ability.Learned && ability.Ability != 2));
        NativeGuardianForceAbility learning = original.AcquireGuardianForce(reserved).GuardianForceAbilities(reserved).First(ability => !ability.Learned && ability.Ability != 2);
        JsonObject existing = new() { ["id"] = checked((byte)reserved), ["acquire"] = true, ["learning_ability"] = checked((byte)learning.Ability), ["ability_points"] = checked((ushort)Math.Max(1, learning.RequiredPoints - learning.Progress)) };
        recipe["fixture"]!["guardian_forces"]!.AsArray().Add(existing);
        string prerequisite = existing.ToJsonString();
        string before = JsonSerializer.Serialize(original);
        string driver = recipe["driver"]!.ToJsonString();
        ScenarioDiscovery.PrepareEnemyPreludeStrength(recipe, original, rules);
        Assert.Equal(prerequisite, recipe["fixture"]!["guardian_forces"]!.AsArray().Single(value => value!["id"]!.Deserialize<int>() == reserved)!.ToJsonString());
        JsonObject character = recipe["fixture"]!["characters"]![0]!.AsObject();
        int spell = character["magic_junctions"]![0]!["action"]!.Deserialize<int>();
        Assert.Equal(1, character["magic_junctions"]![0]!["slot"]!.Deserialize<int>());
        Assert.Equal(rules.MagicRecords.Skip(1).Max(record => record[24]), rules.MagicRecords[spell][24]);
        NativeCharacterVitals prepared = Apply(original, recipe);
        Assert.True(reader.Read(prepared, 0).Strength > reader.Read(original, 0).Strength);
        Assert.Equal(original.Records.Select(record => record.Experience), prepared.Records.Select(record => record.Experience));
        Assert.Equal(original.Records[0].Weapon, prepared.Records[0].Weapon);
        Assert.Equal(original.Records[0].StatBonuses, prepared.Records[0].StatBonuses);
        Assert.Equal(100, prepared.GetMagicCount(0, spell));
        Assert.Equal(driver, recipe["driver"]!.ToJsonString());
        Assert.Equal(before, JsonSerializer.Serialize(original));
        Assert.Equal(original.CombatParty, prepared.CombatParty);
        byte[] originalLevels = original.CombatParty.Where(row => row != 255).Select(row => NativeStatDefinitions.Level(original.Records[row].Experience, original.Definitions.Characters[original.Records[row].CharacterId])).ToArray();
        byte[] preparedLevels = prepared.CombatParty.Where(row => row != 255).Select(row => NativeStatDefinitions.Level(prepared.Records[row].Experience, prepared.Definitions.Characters[prepared.Records[row].CharacterId])).ToArray();
        foreach (byte sceneLevel in new byte[] { 0, 1, 10, 100, 255 })
        {
            Assert.Equal(ScenarioDiscovery.ResolvePossibleEnemyLevels(sceneLevel, originalLevels), ScenarioDiscovery.ResolvePossibleEnemyLevels(sceneLevel, preparedLevels));
        }
    }

    [Fact]
    public void Conflicting_junction_and_wrong_saved_identity_leave_recipe_and_source_unchanged()
    {
        var (original, reader, _) = InstalledInputs();
        BattleNativePlayerStatRules rules = reader.ReadRules(original, 0);
        JsonObject recipe = Recipe(original);
        recipe["fixture"]!["characters"]![0]!["magic_junctions"] = new JsonArray(new JsonObject { ["slot"] = 1, ["action"] = 1 });
        string before = recipe.ToJsonString();
        Assert.Throws<InvalidDataException>(() => ScenarioDiscovery.PrepareEnemyPreludeStrength(recipe, original, rules));
        Assert.Equal(before, recipe.ToJsonString());
        recipe = Recipe(original);
        recipe["fixture"]!["characters"]![0]!["character_id"] = (original.Records[0].CharacterId + 1) % 11;
        before = recipe.ToJsonString();
        Assert.Throws<InvalidDataException>(() => ScenarioDiscovery.PrepareEnemyPreludeStrength(recipe, original, rules));
        Assert.Equal(before, recipe.ToJsonString());
    }

    [Fact]
    public void First_party_row_selects_its_own_native_rules_and_preserves_the_other_character()
    {
        var (original, reader, _) = InstalledInputs();
        JsonObject recipe = Recipe(original);
        recipe["fixture"]!["combat_party"] = new JsonArray(1, 0, 255);
        JsonObject character = recipe["fixture"]!["characters"]![0]!.AsObject();
        character["row"] = 1;
        character["character_id"] = original.Records[1].CharacterId;
        string other = JsonSerializer.Serialize(original.Records[0]);
        ScenarioDiscovery.PrepareEnemyPreludeStrength(recipe, original, reader.ReadRules(original, 1));
        Assert.Equal(1, recipe["fixture"]!["characters"]![0]!["row"]!.Deserialize<int>());
        Assert.Equal(1, recipe["fixture"]!["characters"]![0]!["magic_junctions"]![0]!["slot"]!.Deserialize<int>());
        Assert.Equal(other, JsonSerializer.Serialize(original.Records[0]));
        JsonObject invalid = Recipe(original);
        invalid["fixture"]!["combat_party"] = new JsonArray(255, 0, 1);
        Assert.Throws<InvalidDataException>(() => ScenarioDiscovery.PrepareEnemyPreludeStrength(invalid, original, reader.ReadRules(original, 0)));
    }

    [Fact]
    public void Missing_acquisition_is_rejected_and_repeat_party_does_not_apply_declared_gf_progress()
    {
        (NativeCharacterVitals original, BattlePlayerStatReader reader, NativeBattleExperienceDefinition experience) = InstalledInputs();
        JsonObject recipe = Recipe(original);
        int id = Enumerable.Range(0, 16).First(candidate => original.AcquireGuardianForce(candidate).GuardianForceAbilities(candidate).Any(ability => !ability.Learned));
        NativeGuardianForceAbility learning = original.AcquireGuardianForce(id).GuardianForceAbilities(id).First(ability => !ability.Learned);
        JsonObject declaration = new() { ["id"] = id, ["learning_ability"] = learning.Ability, ["ability_points"] = Math.Max(1, learning.RequiredPoints - learning.Progress) };
        recipe["fixture"]!["guardian_forces"]!.AsArray().Add(declaration);
        string invalid = recipe.ToJsonString();
        Assert.Throws<InvalidDataException>(() => ScenarioDiscovery.PrepareEnemyPreludeStrength(recipe, original, reader.ReadRules(original, 0)));
        Assert.Equal(invalid, recipe.ToJsonString());
        declaration["acquire"] = true;
        string gfBefore = JsonSerializer.Serialize(original.GuardianForces);
        byte[] party = [0, 1, 6];
        int level = party.Max(row => NativeStatDefinitions.Level(original.Records[row].Experience, original.Definitions.Characters[original.Records[row].CharacterId]));
        NativeCharacterVitals prepared = ScenarioDiscovery.PrepareEnemyRepeatParty(recipe, original, experience, party, level);
        Assert.Equal(gfBefore, JsonSerializer.Serialize(prepared.GuardianForces));
        Assert.Equal(gfBefore, JsonSerializer.Serialize(original.GuardianForces));
        string requirement = declaration.ToJsonString();
        ScenarioDiscovery.PrepareEnemyPreludeStrength(recipe, prepared, reader.ReadRules(prepared, 0));
        Assert.Equal(requirement, recipe["fixture"]!["guardian_forces"]!.AsArray().Single(value => value!["id"]!.Deserialize<int>() == id)!.ToJsonString());
        Assert.NotNull(recipe["fixture"]!["characters"]![0]!["magic_junctions"]);
        Assert.Equal(gfBefore, JsonSerializer.Serialize(prepared.GuardianForces));
    }

    private static JsonObject Recipe(NativeCharacterVitals source) => new()
    {
        ["fixture"] = new JsonObject
        {
            ["guardian_forces"] = new JsonArray(),
            ["characters"] = new JsonArray(new JsonObject { ["row"] = 0, ["character_id"] = source.Records[0].CharacterId, ["stocks"] = new JsonArray(), ["guardian_forces"] = new JsonArray() }),
            ["hold_enemy_commands"] = true
        },
        ["driver"] = new JsonObject { ["enemy_ability"] = new JsonObject { ["model"] = 122, ["level_band"] = 0 } }
    };

    private static NativeCharacterVitals Apply(NativeCharacterVitals source, JsonObject recipe)
    {
        NativeCharacterVitals result = source.DeepCopy();
        foreach (JsonNode? declaration in recipe["fixture"]!["guardian_forces"]!.AsArray())
        {
            int id = declaration!["id"]!.Deserialize<int>();
            result = result.AcquireGuardianForce(id);
            if (declaration["learning_ability"] is { } learning)
            {
                result = result.SelectGuardianForceLearning(id, learning.Deserialize<int>());
                result = result.AwardGuardianForceAbilityPoints(id, checked((ushort)declaration["ability_points"]!.Deserialize<int>()));
            }
        }
        JsonNode character = recipe["fixture"]!["characters"]![0]!;
        foreach (JsonNode? id in character["guardian_forces"]!.AsArray())
        {
            if ((result.Records[0].Junction.GuardianForces & (1 << id!.Deserialize<int>())) == 0)
            {
                result = result.ToggleGuardianForceJunction(0, id.Deserialize<int>());
            }
        }
        foreach (JsonNode? stock in character["stocks"]!.AsArray())
        {
            int spell = stock!["action"]!.Deserialize<int>();
            result = result.ChangeMagicStock(0, spell, stock["count"]!.Deserialize<int>() - result.GetMagicCount(0, spell), MagicOperationReason.ScriptReward, out _);
        }
        return result.SetJunctionMagic(0, 1, character["magic_junctions"]![0]!["action"]!.Deserialize<int>());
    }

    private static (NativeCharacterVitals Vitals, BattlePlayerStatReader Reader, NativeBattleExperienceDefinition Experience) InstalledInputs()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null && Directory.Exists(directory), "Select installed native Strength junction resources.");
        using IResourceSource source = GameDataSourceFactory.Open(directory);
        ResourceService resources = new(source);
        byte[] kernel = resources.Read(new ResourceKey("main/kernel.bin"));
        return (NativeCharacterInitialization.Read(resources.Read(new ResourceKey("main/init.out")), kernel) with { CombatParty = [0, 255, 255] }, new(kernel), NativeBattleExperienceDefinitionReader.Read(kernel));
    }
}
