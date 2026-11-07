using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Assets;
using Memoria.FF8.BattleEffects;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.BattleEffects.Tests;

public sealed class ScenarioDiscoveryGuardianRecipeTests
{
    [Theory]
    [InlineData(1, 140, 31, null)]
    [InlineData(15, 338, 32, null)]
    [InlineData(2, 97, 30, 0)]
    [InlineData(3, 98, 30, 1)]
    [InlineData(4, 99, 30, 2)]
    [InlineData(5, 100, 30, 3)]
    public void Item_route_preserves_original_child_dispatch_and_dynamic_selector(int id, int effect, int itemId, int? variant)
    {
        BattleNativeAction child = Native(244, (ushort)id, 9, (ushort)effect);
        BattleNativeAction item = Native(4, (ushort)itemId, 7, variant is null ? (ushort)id : (ushort)2) with { AttackType = 14 };
        IndependentGuardianItemRoute route = Assert.IsType<IndependentGuardianItemRoute>(ScenarioDiscovery.ResolveIndependentGuardianItemRoute(child, [child, item], out string? blocker));
        Assert.Null(blocker);
        Assert.Same(item, route.Item);
        Assert.Equal(variant, route.PocketStationAttackVariant is byte selected ? (int?)selected : null);
        Assert.Equal((byte)244, child.Command);
        Assert.Equal((ushort)effect, child.MagicId);
    }

    [Fact]
    public void Exact_child_and_item_mapping_cannot_be_replaced_by_support_alias_or_ambiguous_rows()
    {
        BattleNativeAction child = Native(244, 1, 9, 140);
        BattleNativeAction item = Native(4, 31, 7, 1) with { AttackType = 14 };
        foreach (BattleNativeAction wrong in new[] { child with { Command = 240 }, child with { Command = 245 }, child with { MagicId = 141 }, child with { KernelSection = 2 } })
        {
            Assert.Null(ScenarioDiscovery.ResolveIndependentGuardianItemRoute(wrong, [child, item], out _));
        }
        Assert.Null(ScenarioDiscovery.ResolveIndependentGuardianItemRoute(child, [child, child, item], out _));
        Assert.Null(ScenarioDiscovery.ResolveIndependentGuardianItemRoute(child, [child, item, item with { ActionId = 33 }], out _));
        Assert.Null(ScenarioDiscovery.ResolveIndependentGuardianItemRoute(child, [child, item with { TargetFlags = null }], out _));
        BattleNativeAction automatic = Native(244, 0, 9, 187);
        Assert.Null(ScenarioDiscovery.ResolveIndependentGuardianItemRoute(automatic, [automatic, item], out string? blocker));
        Assert.NotNull(blocker);
        Assert.Contains("original learned or automatic trigger", blocker);
    }

    [Theory]
    [InlineData(1, 140, 31)]
    [InlineData(15, 338, 32)]
    [InlineData(2, 97, 30)]
    [InlineData(3, 98, 30)]
    [InlineData(4, 99, 30)]
    [InlineData(5, 100, 30)]
    public void Original_resources_prepare_semantic_items_with_two_stocks_and_exact_native_children(int id, int effect, int item)
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null && Directory.Exists(directory), "Select installed resources for independent guardian recipe validation.");
        using IResourceSource source = GameDataSourceFactory.Open(directory);
        ResourceService resources = new(source);
        NativeCharacterVitals initial = NativeCharacterInitialization.Read(resources.Read(new ResourceKey("main/init.out")), resources.Read(new ResourceKey("main/kernel.bin")));
        using BattleAssets assets = new(directory, resources);
        BattleNativeAction child = Assert.Single(assets.NativeActions, action => action.Command == 244 && action.KernelSection == 9 && action.ActionId == id);
        Assert.Equal((ushort)effect, child.MagicId);
        string before = JsonSerializer.Serialize(initial);
        string path = Path.Combine(Path.GetTempPath(), "independent-guardian-recipe-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            const string template = """{"encounter":94,"target":{"actor_id":"enemy-2","physical_index":5},"fixture":{"characters":[{"row":0,"character_id":0,"stocks":[],"guardian_forces":[]}],"hold_enemy_commands":true},"driver":{"input":{}}}""";
            File.WriteAllText(path, template);
            Assert.True(ScenarioDiscovery.TryPrepareIndependentGuardianRecipe(assets, initial, child, path, out JsonObject? recipe, out string? blocker), blocker);
            recipe = JsonNode.Parse(recipe!.ToJsonString())!.AsObject();
            Assert.Equal(nameof(BattleAbility.Item), recipe!["driver"]!["input"]!["ability"]!.GetValue<string>());
            Assert.Equal(item, recipe["driver"]!["input"]!["action_id"]!.GetValue<int>());
            Assert.Equal(244, recipe["driver"]!["expected_native_command"]!.GetValue<int>());
            Assert.Equal(id, recipe["driver"]!["expected_native_action"]!.GetValue<int>());
            Assert.Equal(effect, recipe["driver"]!["expected_effect"]!.GetValue<int>());
            Assert.Equal(2, Assert.Single(recipe["fixture"]!["items"]!.AsArray())!["count"]!.GetValue<int>());
            Assert.Equal("enemy-2", recipe["target"]!["actor_id"]!.GetValue<string>());
            Assert.Null(recipe["driver"]!["controlled_shell_effect"]);
            if (id is >= 2 and <= 5)
            {
                Assert.Equal(id - 2, recipe["fixture"]!["pocket_station"]!["attack_variant"]!.GetValue<int>());
                Assert.Equal(1, recipe["fixture"]!["pocket_station"]!["level"]!.GetValue<int>());
                Assert.True(recipe["fixture"]!["pocket_station"]!["available"]!.GetValue<bool>());
                Assert.False(recipe["fixture"]!["pocket_station"]!["away"]!.GetValue<bool>());
                Assert.Null(recipe["driver"]!["repairs"]);
            }
            Assert.Equal(before, JsonSerializer.Serialize(initial));
            Assert.Equal(template, File.ReadAllText(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static BattleNativeAction Native(byte command, ushort id, int section, ushort effect) => new(command, id, section, id)
    {
        MagicId = effect,
        AttackType = 11,
        Power = 0,
        HitCount = 1,
        TargetFlags = 0x40,
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
}
