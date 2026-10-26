using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Assets;
using Memoria.FF8.BattleEffects;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.BattleEffects.Tests;

public sealed class ScenarioBlueMagicOwnerRecipeTests
{
    [Fact]
    public void Mighty_guard_rearm_removes_regen_before_selecting_the_next_crisis_at_declared_hp()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null && Directory.Exists(directory), "Select installed resources for original Blue Magic recipe validation.");
        using IResourceSource source = GameDataSourceFactory.Open(directory);
        ResourceService resources = new(source);
        NativeCharacterVitals initial = NativeCharacterInitialization.Read(resources.Read(new ResourceKey("main/init.out")), resources.Read(new ResourceKey("main/kernel.bin")));
        using BattleAssets assets = new(directory, resources);
        BattleNativeAction native = Assert.Single(assets.NativeActions, action => action.Command == 15 && action.ActionId == 13 && action.CrisisLevel == 2);
        Assert.Equal((ushort)73, native.MagicId);
        Assert.NotEqual(0u, native.Statuses1 & 16);
        string path = Path.Combine(Path.GetTempPath(), "blue-magic-rearm-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            File.WriteAllText(path, """{"fixture":{"characters":[{"row":0,"character_id":0,"stocks":[],"guardian_forces":[]}]},"driver":{}}""");
            Assert.True(ScenarioDiscovery.TryPrepareBlueMagicRecipe(assets, initial, native, path, out JsonObject? recipe, out string? blocker), blocker);
            JsonNode repair = Assert.Single(recipe!["driver"]!["repairs"]!.AsArray())!;
            Assert.NotEqual(0u, repair["remove_statuses1"]!.GetValue<uint>() & 16);
            Assert.Equal(recipe["fixture"]!["characters"]![0]!["hit_points"]!.GetValue<int>(), repair["hit_points"]!.GetValue<int>());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData((byte)1)]
    [InlineData((byte)2)]
    [InlineData((byte)3)]
    [InlineData((byte)4)]
    public void Homing_laser_recipe_uses_its_original_owner_and_that_records_actual_crisis_inputs(byte crisis)
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null && Directory.Exists(directory), "Select installed resources for original Blue Magic recipe validation.");
        using IResourceSource source = GameDataSourceFactory.Open(directory);
        ResourceService resources = new(source);
        NativeCharacterVitals initial = NativeCharacterInitialization.Read(resources.Read(new ResourceKey("main/init.out")), resources.Read(new ResourceKey("main/kernel.bin")));
        using BattleAssets assets = new(directory, resources);
        int ownerRow = Array.FindIndex(initial.Records, record => record.CharacterId == 3);
        Assert.InRange(ownerRow, 0, 7);
        JsonElement before = JsonSerializer.SerializeToElement(initial);
        BattleNativeAction native = new(15, 12, 19, 12)
        {
            CharacterId = 3,
            CrisisLevel = crisis,
            MagicId = 229,
            TargetFlags = 0x40,
            AttackType = 1,
            Power = 0,
            HitCount = 1,
            AttackFlags = 0,
            DamagePresentation = 0,
            Element = 0,
            ElementPercent = null,
            StatusAttack = 0,
            Statuses0 = 0,
            Statuses1 = 0,
            Hit = null,
            Critical = null,
            PowerModifier = null,
            LevelModifier = null,
            RawRecord = [],
            SupplementalRecord = []
        };
        string path = Path.Combine(Path.GetTempPath(), "blue-magic-recipe-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            File.WriteAllText(path, """{"target":{"actor_id":"enemy-2","physical_index":5},"fixture":{"characters":[{"row":0,"character_id":0,"stocks":[],"guardian_forces":[]}]},"driver":{}}""");
            Assert.True(ScenarioDiscovery.TryPrepareBlueMagicRecipe(assets, initial, native, path, out JsonObject? recipe, out string? blocker), blocker);
            Assert.NotNull(recipe);
            JsonNode fixture = recipe["fixture"]!;
            Assert.Equal(ownerRow, fixture["characters"]![0]!["row"]!.GetValue<int>());
            Assert.Equal(3, fixture["characters"]![0]!["character_id"]!.GetValue<int>());
            Assert.Equal(new[] { ownerRow, 255, 255 }, fixture["combat_party"]!.AsArray().Select(value => value!.GetValue<int>()));
            Assert.Null(fixture["limits"]!["borrow_blue_magic_for_squall"]);
            Assert.Equal(4096, fixture["limits"]!["learn_blue_magic"]!.GetValue<int>());
            Assert.Equal($"party-{ownerRow}", recipe["driver"]!["input"]!["actor_id"]!.GetValue<string>());
            Assert.Equal("enemy-2", recipe["driver"]!["input"]!["target_id"]!.GetValue<string>());
            int expectedHp = ScenarioDiscovery.ResolveLimitPreparationHitPoints(assets.ReadNativeLimitDefinition(3), initial.GetMagicMaximumHitPoints(ownerRow), initial.Records[ownerRow].Status, crisis);
            Assert.Equal(expectedHp, fixture["characters"]![0]!["hit_points"]!.GetValue<int>());
            Assert.True(JsonElement.DeepEquals(before, JsonSerializer.SerializeToElement(initial)));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
