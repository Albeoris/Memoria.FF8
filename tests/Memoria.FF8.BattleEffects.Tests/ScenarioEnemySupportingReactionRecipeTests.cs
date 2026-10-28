using System.Collections.Immutable;
using System.Numerics;
using System.Text.Json.Nodes;
using Memoria.FF8.Assets;
using Memoria.FF8.BattleEffects;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.BattleEffects.Tests;

public sealed class ScenarioEnemySupportingReactionRecipeTests
{
    private static readonly string Hash = new('A', 64);

    [Fact]
    public void Declarations_preserve_exact_companion_reaction_identity_and_deduplicate_level_bands()
    {
        JsonObject recipe = new() { ["driver"] = new JsonObject() };
        BattleEncounter formation = Formation();
        EnemyScenarioScriptedAction[] rows = [Row(82, 179), Row(82, 179) with { LevelBand = 1 }, Row(83, 179), Row(62, 180), Row(98, 154), Row(82, 179) with { Callback = 1 }, Row(82, 179) with { ModelId = 17 }, Row(82, 179) with { Formations = [Context() with { SceneSlot = 7 }] }];

        ScenarioDiscovery.DeclareEnemySupportingReactions(recipe, formation, "enemy-0", 154, rows);

        JsonArray actual = recipe["driver"]!["supporting_invocations"]!.AsArray();
        Assert.Equal(new[] { 62, 82, 83 }, actual.Select(node => (int)node!["action_id"]!.GetValue<ushort>()));
        Assert.All(actual, node =>
        {
            Assert.Equal("enemy-1", node!["actor_id"]!.GetValue<string>());
            Assert.Equal(8, (int)node["native_command"]!.GetValue<byte>());
            Assert.Equal(2, (int)node["callback_stage"]!.GetValue<byte>());
            Assert.Equal(Hash, node["script_sha256"]!.GetValue<string>());
        });
        Assert.Null(recipe["driver"]!["supporting_effects"]);
    }

    [Fact]
    public void Invalid_source_hash_and_excessive_declarations_are_not_silently_accepted()
    {
        JsonObject recipe = new() { ["driver"] = new JsonObject() };
        EnemyScenarioScriptedAction invalid = Row(82, 179) with { MappingEvidence = ["battle/c0m043.dat:ai-callback:2:sha256:bad"] };
        Assert.Throws<InvalidDataException>(() => ScenarioDiscovery.DeclareEnemySupportingReactions(recipe, Formation(), "enemy-0", 154, [invalid]));
        Assert.Throws<InvalidDataException>(() => ScenarioDiscovery.DeclareEnemySupportingReactions(recipe, Formation(), "enemy-0", 154, Enumerable.Range(1, 33).Select(id => Row(checked((ushort)id), 179))));
        Assert.Null(recipe["driver"]!["supporting_invocations"]);
    }

    [Fact]
    public void Installed_formation36_declares_both_observed_growth_reactions_and_the_original_conditional_self_destruct()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null && Directory.Exists(directory), "Select installed resources for original companion reaction declarations.");
        using IResourceSource source = GameDataSourceFactory.Open(directory);
        using BattleAssets assets = new(directory, new ResourceService(source));
        BattleEncounter formation = assets.LoadEncounter(36);
        EnemyScenarioModel[] models = new[] { 17, 43 }.Select(model => new EnemyScenarioModel(model, assets.ReadEnemyName(model), assets.ReadEnemyAi(model, 0))).ToArray();
        EnemyScenarioDiscoveryResult discovery = ScenarioEnemyDiscovery.Discover([formation], models, assets.NativeActions);
        JsonObject recipe = new() { ["driver"] = new JsonObject() };

        ScenarioDiscovery.DeclareEnemySupportingReactions(recipe, formation, "enemy-0", 154, discovery.ScriptedActions);

        JsonArray declarations = recipe["driver"]!["supporting_invocations"]!.AsArray();
        Assert.Equal(new[] { 62, 82, 83 }, declarations.Select(node => (int)node!["action_id"]!.GetValue<ushort>()));
        Assert.All(declarations, node =>
        {
            Assert.Equal("enemy-1", node!["actor_id"]!.GetValue<string>());
            Assert.Equal(2, (int)node["callback_stage"]!.GetValue<byte>());
            Assert.Equal("3AE4C1F057643B9B21B4AE6E63BB89FB2C2BD936D120BEA8F9FC6E34B55EE625", node["script_sha256"]!.GetValue<string>());
        });
        Assert.Equal(new[] { 180, 179, 179 }, declarations.Select(node => (int)node!["effect_id"]!.GetValue<ushort>()));
    }

    private static BattleEncounter Formation() => new(36, 7, 0, 0, 0, [new(0, 17, Vector3.Zero, 20, false, false), new(1, 43, Vector3.Zero, 20, false, false)]);
    private static EnemyScenarioFormation Context() => new(36, 7, 1, true, false, false, false, 20);
    private static EnemyScenarioScriptedAction Row(ushort action, ushort effect) => new(43, "Synthetic companion", 2, 21, 0, 8, action, effect, 14, Action(action, effect), [Context()], [$"battle/c0m043.dat:ai-callback:2:sha256:{Hash}"], []);
    private static BattleNativeAction Action(ushort action, ushort effect) => new(8, action, 3, action)
    {
        MagicId = effect,
        AttackType = 0,
        Power = 0,
        HitCount = 1,
        TargetFlags = null,
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
        RawRecord = ImmutableArray.Create((byte)0),
        SupplementalRecord = []
    };
}
