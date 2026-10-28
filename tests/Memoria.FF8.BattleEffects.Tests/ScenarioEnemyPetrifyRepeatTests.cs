using System.Text.Json.Nodes;
using Memoria.FF8.Assets;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.BattleEffects.Tests;

public sealed class ScenarioEnemyPetrifyRepeatTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(201)]
    public void SinglePartyPetrifySelectsThreeGenuineLivingRecordsWithoutChangingSavedOwners(byte selector)
    {
        NativeCharacterVital[] records = Records();
        Assert.True(ScenarioDiscovery.TrySelectEnemyPetrifyRepeatParty(Petrify(), selector, records, new byte[] { 0, 255, 255 }, out byte[]? party, out string? blocker), blocker);
        Assert.Equal(new byte[] { 0, 1, 3 }, party);
        Assert.All(records, record => Assert.Equal(new NativeHpWord(100, 100), record.HitPoints));
        Assert.Equal(3, party!.Select(row => records[row].CharacterId).Distinct().Count());
    }

    [Fact]
    public void MissingLivingPartyOwnersRemainAnExplicitPreparationBoundary()
    {
        NativeCharacterVital[] records = Records();
        for (int row = 1; row < records.Length; row++)
        {
            records[row] = records[row] with { HitPoints = new(0, 0), Status = 1 };
        }
        Assert.False(ScenarioDiscovery.TrySelectEnemyPetrifyRepeatParty(Petrify(), 201, records, new byte[] { 0, 255, 255 }, out byte[]? party, out string? blocker));
        Assert.Null(party);
        Assert.StartsWith("PETRIFY_REPEAT_PARTY_PRECONDITION:", blocker);
    }

    [Theory]
    [InlineData(204, 4, 0u)]
    [InlineData(204, 4, 0x4000000u)]
    [InlineData(200, 4, 0u)]
    [InlineData(201, 0, 0u)]
    [InlineData(201, 4, 0x10000u)]
    public void OtherSelectorsAndEjectProfilesDoNotAcquireAPetrifyPartyOrAnUnprovedGuaranteedOutcome(byte selector, ushort saved, uint battle)
    {
        Assert.True(ScenarioDiscovery.TrySelectEnemyPetrifyRepeatParty(Petrify() with { Statuses0 = saved, Statuses1 = battle, Hit = 255, StatusAttack = 255 }, selector, Records(), new byte[] { 0, 255, 255 }, out byte[]? party, out string? blocker));
        Assert.Null(party);
        Assert.Null(blocker);
    }

    [Fact]
    public void InstalledPetrifyRecipeKeepsCapableCompanionsAtTheSameLevelAndDeclaresOnlyOriginalStatusRepair()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null && Directory.Exists(directory), "Select installed resources for genuine Petrify companions.");
        using IResourceSource source = GameDataSourceFactory.Open(directory);
        ResourceService resources = new(source);
        byte[] kernel = resources.Read(new ResourceKey("main/kernel.bin"));
        NativeCharacterVitals initial = NativeCharacterInitialization.Read(resources.Read(new ResourceKey("main/init.out")), kernel);
        byte[] originalParty = initial.CombatParty.ToArray();
        NativeBattleExperienceDefinition experience = NativeBattleExperienceDefinitionReader.Read(kernel);
        using BattleAssets assets = new(directory, resources);
        BattleEncounter formation = assets.LoadEncounter(41);
        EnemyScenarioModel model = new(6, assets.ReadEnemyName(6), assets.ReadEnemyAi(6, 0));
        EnemyScenarioDiscoveryResult discovery = ScenarioEnemyDiscovery.Discover([formation], [model], assets.NativeActions);
        EnemyScenarioActionOwner owner = discovery.Owners.Single(value => value.LevelBand == 1 && value.AbilitySlot == 1);
        string template = Path.Combine(Path.GetTempPath(), "petrify-party-recipe-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            File.WriteAllText(template, """{"fixture":{"characters":[{"row":0,"character_id":0,"stocks":[],"guardian_forces":[0]}]},"driver":{}}""");
            Assert.True(ScenarioDiscovery.TryPrepareEnemyRecipe(assets, initial, experience, owner, discovery.ScriptedActions, template, out JsonObject? recipe, out EnemyScenarioFormation? chosen, out _, out string? blocker, discovery.Startups), blocker);
            JsonObject declared = JsonNode.Parse(recipe!.ToJsonString())!.AsObject();
            Assert.Equal(41, chosen!.FormationId);
            Assert.Equal(new byte[] { 0, 1, 3 }, declared["fixture"]!["combat_party"]!.AsArray().Select(node => node!.GetValue<byte>()));
            Assert.Equal("party-0", declared["driver"]!["input"]!["target_id"]!.GetValue<string>());
            int level = declared["fixture"]!["characters"]![0]!["level"]!.GetValue<int>();
            NativeCharacterVitals prepared = initial;
            foreach (byte row in new byte[] { 0, 1, 3 })
            {
                prepared = prepared.WithDiagnosticBattleLevel(row, level, experience);
            }
            JsonArray characters = declared["fixture"]!["characters"]!.AsArray();
            JsonArray repairs = declared["driver"]!["repairs"]!.AsArray();
            Assert.Equal(3, characters.Count);
            Assert.Equal(3, repairs.Count);
            foreach (JsonNode? node in characters)
            {
                int row = node!["row"]!.GetValue<int>();
                Assert.Equal(prepared.Records[row].CharacterId, node["character_id"]!.GetValue<byte>());
                Assert.Equal(prepared.GetMagicMaximumHitPoints(row), node["hit_points"]!.GetValue<ushort>());
                Assert.Equal(level, node["level"]!.GetValue<int>());
                JsonNode repair = repairs.Single(candidate => candidate!["actor_id"]!.GetValue<string>() == $"party-{row}")!;
                Assert.Equal(4, repair["remove_statuses0"]!.GetValue<int>());
                Assert.Equal(0u, repair["remove_statuses1"]!.GetValue<uint>());
                Assert.Equal(prepared.GetMagicMaximumHitPoints(row), repair["hit_points"]!.GetValue<ushort>());
            }
            Assert.Equal(new[] { 0 }, characters[0]!["guardian_forces"]!.AsArray().Select(node => node!.GetValue<int>()));
            Assert.Equal(originalParty, initial.CombatParty);
            byte[] partyLevels = Enumerable.Repeat(checked((byte)level), 3).ToArray();
            Assert.Equal(new[] { 1 }, ScenarioDiscovery.ResolvePossibleEnemyLevels(checked((byte)chosen.SceneLevel), partyLevels).Select(enemyLevel => assets.ReadEnemyAi(6, enemyLevel).LevelBand).Distinct());
        }
        finally
        {
            File.Delete(template);
        }
    }

    [Fact]
    public void SharedSameLevelPreparationPreservesDeclaredRowsAndRepairs()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null && Directory.Exists(directory), "Select installed resources for shared party preparation.");
        using IResourceSource source = GameDataSourceFactory.Open(directory);
        ResourceService resources = new(source);
        byte[] kernel = resources.Read(new ResourceKey("main/kernel.bin"));
        NativeCharacterVitals initial = NativeCharacterInitialization.Read(resources.Read(new ResourceKey("main/init.out")), kernel);
        NativeBattleExperienceDefinition experience = NativeBattleExperienceDefinitionReader.Read(kernel);
        JsonObject recipe = JsonNode.Parse("""{"fixture":{"characters":[{"row":0,"character_id":0,"stocks":[],"guardian_forces":[0]},{"row":3,"character_id":3,"stocks":[{"action":1,"quantity":2}],"guardian_forces":[]},{"row":4,"character_id":4,"stocks":[],"guardian_forces":[]}]},"driver":{"repairs":[{"actor_id":"party-0","hit_points":486,"revive":false,"remove_statuses0":32,"remove_statuses1":256}],"terminal_hold":{"reason":"Existing declaration","party_actor_ids":["party-0"],"maximum_callbacks":100}}}""")!.AsObject();
        string repairs = recipe["driver"]!.ToJsonString();
        string unrelated = recipe["fixture"]!["characters"]![2]!.ToJsonString();
        ScenarioDiscovery.PrepareEnemyRepeatParty(recipe, initial, experience, [0, 1, 3], 24);
        JsonObject declared = JsonNode.Parse(recipe.ToJsonString())!.AsObject();
        Assert.Equal(repairs, recipe["driver"]!.ToJsonString());
        Assert.Equal(4, declared["fixture"]!["characters"]!.AsArray().Count);
        Assert.Equal(2, declared["fixture"]!["characters"]![1]!["stocks"]![0]!["quantity"]!.GetValue<int>());
        Assert.Equal(unrelated, recipe["fixture"]!["characters"]![2]!.ToJsonString());
    }

    private static NativeCharacterVital[] Records() => Enumerable.Range(0, 8).Select(row => new NativeCharacterVital((byte)row, new(100, 100), 0, null!, 0, 0, 0, [], [], 0, 0)).ToArray();
    private static BattleNativeAction Petrify() => new(8, 19, 3, 19)
    {
        MagicId = 120,
        AttackType = 2,
        Power = 0,
        HitCount = 1,
        TargetFlags = null,
        AttackFlags = 0,
        DamagePresentation = 0,
        Element = 0,
        ElementPercent = null,
        StatusAttack = 255,
        Statuses0 = 4,
        Statuses1 = 0,
        Hit = 255,
        Critical = null,
        PowerModifier = null,
        LevelModifier = null,
        RawRecord = [],
        SupplementalRecord = []
    };
}
