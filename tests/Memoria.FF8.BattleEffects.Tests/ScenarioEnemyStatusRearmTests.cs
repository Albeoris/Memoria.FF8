using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Assets;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.BattleEffects.Tests;

public sealed class ScenarioEnemyStatusRearmTests
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    [Theory]
    [InlineData(0, 0u)]
    [InlineData(0, 0x4000u)]
    [InlineData(0, 0x10u)]
    [InlineData(0, 0x20u)]
    [InlineData(0, 0x3FFFu)]
    [InlineData(0, 0x02000000u)]
    [InlineData(2, 0u)]
    [InlineData(32, 0u)]
    [InlineData(0x7F, 0x7FFEFFFFu)]
    [InlineData(0x5D, 0x80000000u)]
    [InlineData(0, 0x04008000u)]
    public void EveryOrdinaryPartyActionDeclaresFullStatusCleanupWithoutATerminalHold(ushort saved, uint battle)
    {
        JsonObject recipe = Recipe();
        ScenarioDiscovery.DeclareEnemyPartyStatusRearm(recipe, Action() with { Statuses0 = saved, Statuses1 = battle }, 201, new byte[] { 0, 255, 255 }, _ => 2241);

        BattleDiagnosticCombatRepair repair = Assert.Single(Repairs(recipe));
        repair.Validate();
        Assert.Equal("party-0", repair.ActorId);
        Assert.Equal(2241, repair.HitPoints);
        Assert.False(repair.Revive);
        Assert.Equal((ushort)0xFFFE, repair.RemoveStatuses0);
        Assert.Equal(0x7FFEFFFFu, repair.RemoveStatuses1);
        Assert.Null(recipe["driver"]!["terminal_hold"]);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(201, 3)]
    [InlineData(204, 3)]
    public void PossibleRecipientsUsePhysicalPartyOrderAndTheirOwnSavedRowHp(byte selector, int count)
    {
        JsonObject recipe = Recipe();
        ScenarioDiscovery.DeclareEnemyPartyStatusRearm(recipe, Action(), selector, new byte[] { 4, 0, 5 }, row => 1000 + row * 100);

        BattleDiagnosticCombatRepair[] repairs = Repairs(recipe);
        Assert.Equal(new[] { "party-4", "party-0", "party-5" }.Take(count), repairs.Select(repair => repair.ActorId));
        Assert.Equal(new[] { 1400, 1000, 1500 }.Take(count), repairs.Select(repair => repair.HitPoints));
        Assert.All(repairs, repair =>
        {
            Assert.Equal((ushort)0xFFFE, repair.RemoveStatuses0);
            Assert.Equal(0x7FFEFFFFu, repair.RemoveStatuses1);
        });
    }

    [Theory]
    [InlineData(0)]
    [InlineData(201)]
    [InlineData(204)]
    public void AbsentPhysicalSlotsNeverCreateSavedRow255Repairs(byte selector)
    {
        JsonObject recipe = Recipe();
        ScenarioDiscovery.DeclareEnemyPartyStatusRearm(recipe, Action(), selector, new byte[] { 255, 4, 255 }, row => row == 4 ? 1400 : throw new InvalidOperationException("Only a present owner has HP."));

        Assert.Equal("party-4", Assert.Single(Repairs(recipe)).ActorId);
    }

    [Fact]
    public void FullCleanupPreservesExistingRepairHpRevivalAndTerminalHold()
    {
        JsonObject recipe = Recipe();
        recipe["driver"]!["repairs"] = new JsonArray(new JsonObject { ["actor_id"] = "party-4", ["hit_points"] = 777, ["revive"] = true, ["remove_statuses0"] = 4, ["remove_statuses1"] = 256 });
        recipe["driver"]!["terminal_hold"] = new JsonObject { ["reason"] = "Existing prospective hold" };
        string hold = recipe["driver"]!["terminal_hold"]!.ToJsonString();
        ScenarioDiscovery.DeclareEnemyPartyStatusRearm(recipe, Action() with { Statuses0 = 0x22 }, 201, new byte[] { 4, 0, 255 }, row => 1000 + row * 100);

        BattleDiagnosticCombatRepair[] repairs = Repairs(recipe);
        Assert.Equal(2, repairs.Length);
        BattleDiagnosticCombatRepair existing = repairs.Single(repair => repair.ActorId == "party-4");
        existing.Validate();
        Assert.Equal(777, existing.HitPoints);
        Assert.True(existing.Revive);
        Assert.Equal((ushort)0xFFFE, existing.RemoveStatuses0);
        Assert.Equal(0x7FFEFFFFu, existing.RemoveStatuses1);
        Assert.Equal(1000, repairs.Single(repair => repair.ActorId == "party-0").HitPoints);
        Assert.Equal(hold, recipe["driver"]!["terminal_hold"]!.ToJsonString());
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(21)]
    [InlineData(25)]
    [InlineData(32)]
    public void CurativeActionsAlsoDeclareFullStatusCleanup(byte type)
    {
        JsonObject recipe = Recipe();
        ScenarioDiscovery.DeclareEnemyPartyStatusRearm(recipe, Action() with { AttackType = type }, 201, new byte[] { 0, 255, 255 }, _ => 2241);

        BattleDiagnosticCombatRepair repair = Assert.Single(Repairs(recipe));
        repair.Validate();
        Assert.Equal((ushort)0xFFFE, repair.RemoveStatuses0);
        Assert.Equal(0x7FFEFFFFu, repair.RemoveStatuses1);
        Assert.False(repair.Revive);
    }

    [Theory]
    [InlineData(200)]
    [InlineData(202)]
    [InlineData(205)]
    public void NonPartySelectorsDoNotAcquirePartyRepairs(byte selector)
    {
        AssertUnchanged(Action(), selector);
    }

    [Theory]
    [InlineData(0x10000u)]
    [InlineData(0x14000u)]
    public void EjectionProfilesRemainOutsideOrdinaryPartyRearm(uint battle)
    {
        AssertUnchanged(Action() with { Statuses1 = battle }, 201);
    }

    [Fact]
    public void InstalledImpConfuseRecipeDeclaresFullOrdinaryStatusCleanup()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null && Directory.Exists(directory), "Select installed original resources for the native Confuse recipe.");
        using IResourceSource source = GameDataSourceFactory.Open(directory);
        ResourceService resources = new(source);
        byte[] kernel = resources.Read(new ResourceKey("main/kernel.bin"));
        NativeCharacterVitals initial = NativeCharacterInitialization.Read(resources.Read(new ResourceKey("main/init.out")), kernel);
        using BattleAssets assets = new(directory, resources);
        BattleEncounter formation = assets.LoadEncounter(246);
        EnemyScenarioModel model = new(59, assets.ReadEnemyName(59), assets.ReadEnemyAi(59, 0));
        EnemyScenarioDiscoveryResult discovery = ScenarioEnemyDiscovery.Discover([formation], [model], assets.NativeActions);
        EnemyScenarioActionOwner owner = discovery.Owners.Single(candidate => candidate.ModelId == 59 && candidate.LevelBand == 2 && candidate.AbilitySlot == 3);
        Assert.Equal((byte)2, owner.Command);
        Assert.Equal((ushort)39, owner.ActionId);
        Assert.Equal((ushort)36, owner.NativeAction!.MagicId);
        Assert.Equal(0x4000u, owner.NativeAction.Statuses1 & 0x4000u);
        string template = Path.Combine(Path.GetTempPath(), "enemy-status-rearm-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            File.WriteAllText(template, """{"fixture":{"characters":[{"row":0,"character_id":0,"stocks":[],"guardian_forces":[0]}]},"driver":{}}""");
            Assert.True(ScenarioDiscovery.TryPrepareEnemyRecipe(assets, initial, NativeBattleExperienceDefinitionReader.Read(kernel), owner, discovery.ScriptedActions, template, out JsonObject? recipe, out EnemyScenarioFormation? chosen, out _, out string? blocker, discovery.Startups), blocker);
            Assert.Equal(246, chosen!.FormationId);
            Assert.Equal((byte)201, recipe!["driver"]!["enemy_ability"]!["target_selector"]!.Deserialize<byte>());
            BattleDiagnosticCombatRepair repair = Assert.Single(Repairs(recipe));
            repair.Validate();
            Assert.Equal("party-0", repair.ActorId);
            Assert.Equal((ushort)0xFFFE, repair.RemoveStatuses0);
            Assert.Equal(0x7FFEFFFFu, repair.RemoveStatuses1);
            Assert.False(repair.Revive);
            Assert.Null(recipe["driver"]!["terminal_hold"]);
        }
        finally
        {
            File.Delete(template);
        }
    }

    private static void AssertUnchanged(BattleNativeAction action, byte selector)
    {
        JsonObject recipe = Recipe();
        string before = recipe.ToJsonString();
        ScenarioDiscovery.DeclareEnemyPartyStatusRearm(recipe, action, selector, new byte[] { 0, 255, 255 }, _ => throw new InvalidOperationException("An excluded profile has no new HP preparation."));
        Assert.Equal(before, recipe.ToJsonString());
    }

    private static JsonObject Recipe() => new() { ["driver"] = new JsonObject() };

    private static BattleDiagnosticCombatRepair[] Repairs(JsonObject recipe) => recipe["driver"]!["repairs"]!.Deserialize<BattleDiagnosticCombatRepair[]>(Json)!;

    private static BattleNativeAction Action() => new(2, 39, 3, 39)
    {
        MagicId = 36,
        AttackType = 2,
        Power = 0,
        HitCount = 1,
        TargetFlags = null,
        AttackFlags = 0,
        DamagePresentation = 0,
        Element = 0,
        ElementPercent = null,
        StatusAttack = 100,
        Statuses0 = 0,
        Statuses1 = 0x4000,
        Hit = null,
        Critical = null,
        PowerModifier = null,
        LevelModifier = null,
        RawRecord = [],
        SupplementalRecord = []
    };
}
