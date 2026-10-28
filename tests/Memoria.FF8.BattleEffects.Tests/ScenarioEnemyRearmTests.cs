using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.BattleEffects.Tests;

public sealed class ScenarioEnemyRearmTests
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    [Theory]
    [InlineData(1, 30, 0)]
    [InlineData(2, 40, 0)]
    [InlineData(22, 80, 0)]
    [InlineData(26, 0, 1)]
    [InlineData(27, 100, 0)]
    [InlineData(2, 0, 1)]
    public void NativeDamageAndKoProfilesDeclareProspectiveDefeatHoldAndExplicitRevival(byte type, byte power, ushort statuses)
    {
        JsonObject recipe = Recipe();
        ScenarioDiscovery.DeclareEnemyPartyKnockoutRearm(recipe, Action(type, power, statuses), 201, new byte[] { 0, 255, 255 }, _ => 486);

        BattleDiagnosticTerminalHoldDeclaration hold = recipe["driver"]!["terminal_hold"]!.Deserialize<BattleDiagnosticTerminalHoldDeclaration>(Json)!;
        BattleDiagnosticCombatRepair repair = recipe["driver"]!["repairs"]![0]!.Deserialize<BattleDiagnosticCombatRepair>(Json)!;
        hold.Validate();
        repair.Validate();
        Assert.Equal(new[] { "party-0" }, hold.PartyActorIds);
        Assert.InRange(hold.MaximumCallbacks, 1, 18000);
        Assert.Equal("party-0", repair.ActorId);
        Assert.Equal(486, repair.HitPoints);
        Assert.True(repair.Revive);
        Assert.Equal((ushort)0, repair.RemoveStatuses0);
        Assert.Equal(0u, repair.RemoveStatuses1);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(201, 3)]
    [InlineData(204, 3)]
    public void PowerZeroPetrifyDeclaresOnlyActualRecipientsWithTheirOwnMaximumHp(byte selector, int count)
    {
        JsonObject recipe = Recipe();
        ScenarioDiscovery.DeclareEnemyPartyKnockoutRearm(recipe, Action(2, 0, 4), selector, new byte[] { 4, 0, 5 }, row => 1000 + row * 100);

        BattleDiagnosticTerminalHoldDeclaration hold = recipe["driver"]!["terminal_hold"]!.Deserialize<BattleDiagnosticTerminalHoldDeclaration>(Json)!;
        BattleDiagnosticCombatRepair[] repairs = recipe["driver"]!["repairs"]!.Deserialize<BattleDiagnosticCombatRepair[]>(Json)!;
        hold.Validate();
        Assert.Contains("incapacitation", hold.Reason);
        Assert.Equal(count, repairs.Length);
        Assert.Equal(new[] { "party-4", "party-0", "party-5" }.Take(count), repairs.Select(repair => repair.ActorId));
        Assert.Equal(new[] { 1400, 1000, 1500 }.Take(count), repairs.Select(repair => repair.HitPoints));
        Assert.Equal(repairs.Select(repair => repair.ActorId), hold.PartyActorIds);
        Assert.All(repairs, repair =>
        {
            repair.Validate();
            Assert.True(repair.Revive);
            Assert.Equal((ushort)4, repair.RemoveStatuses0);
            Assert.Equal(0u, repair.RemoveStatuses1);
        });
    }

    [Theory]
    [InlineData(4, 0, 4)]
    [InlineData(36, 0, 4)]
    [InlineData(4, 32, 36)]
    [InlineData(1, 32, 32)]
    public void PetrifyAddsOnlyItsProvedStatusBitAndPreservesExistingStatusDeclarations(ushort applied, ushort existing, ushort expected)
    {
        JsonObject recipe = Recipe();
        recipe["driver"]!["repairs"] = new JsonArray(new JsonObject { ["actor_id"] = "party-0", ["hit_points"] = 486, ["revive"] = false, ["remove_statuses0"] = (int)existing, ["remove_statuses1"] = 256 });
        ScenarioDiscovery.DeclareEnemyPartyKnockoutRearm(recipe, Action(2, 0, applied), 201, new byte[] { 0, 255, 255 }, _ => 486);

        BattleDiagnosticCombatRepair repair = recipe["driver"]!["repairs"]![0]!.Deserialize<BattleDiagnosticCombatRepair>(Json)!;
        repair.Validate();
        Assert.Equal(expected, repair.RemoveStatuses0);
        Assert.Equal(256u, repair.RemoveStatuses1);
        Assert.Equal(486, repair.HitPoints);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(32)]
    public void OrdinaryPowerZeroNonPetrifyStatusesDoNotAddAHoldOrRepair(ushort statuses)
    {
        JsonObject recipe = Recipe();
        string before = recipe.ToJsonString();
        ScenarioDiscovery.DeclareEnemyPartyKnockoutRearm(recipe, Action(2, 0, statuses), 201, new byte[] { 0, 255, 255 }, _ => throw new InvalidOperationException("No HP owner should be consulted."));
        Assert.Equal(before, recipe.ToJsonString());
    }

    [Theory]
    [InlineData(200)]
    [InlineData(205)]
    public void PetrifyOnANonPartySelectorDoesNotDeclarePartyRepairs(byte selector)
    {
        JsonObject recipe = Recipe();
        string before = recipe.ToJsonString();
        ScenarioDiscovery.DeclareEnemyPartyKnockoutRearm(recipe, Action(2, 0, 4), selector, new byte[] { 0, 255, 255 }, _ => throw new InvalidOperationException("No party HP owner should be consulted."));
        Assert.Equal(before, recipe.ToJsonString());
    }

    [Fact]
    public void PetrifyDoesNotOverrideTheOriginalEjectExclusion()
    {
        JsonObject recipe = Recipe();
        string before = recipe.ToJsonString();
        ScenarioDiscovery.DeclareEnemyPartyKnockoutRearm(recipe, Action(2, 0, 4) with { Statuses1 = 0x10000 }, 201, new byte[] { 0, 255, 255 }, _ => throw new InvalidOperationException("An ejected owner cannot be prepared for repair."));
        Assert.Equal(before, recipe.ToJsonString());
    }

    [Fact]
    public void GroupRecipientDeclarationsUseSavedPartyRowsAndTheirOwnMaximumHpWithoutClearingStatuses()
    {
        JsonObject recipe = Recipe();
        recipe["driver"]!["repairs"] = new JsonArray(new JsonObject { ["actor_id"] = "party-0", ["hit_points"] = 486, ["revive"] = false, ["remove_statuses0"] = 32, ["remove_statuses1"] = 256 });
        ScenarioDiscovery.DeclareEnemyPartyKnockoutRearm(recipe, Action(2, 50, 0), 204, new byte[] { 0, 4, 5 }, row => 1000 + row * 100);

        BattleDiagnosticCombatRepair[] repairs = recipe["driver"]!["repairs"]!.Deserialize<BattleDiagnosticCombatRepair[]>(Json)!;
        Assert.Equal(new[] { "party-0", "party-4", "party-5" }, repairs.Select(repair => repair.ActorId));
        Assert.Equal(new[] { 1000, 1400, 1500 }, repairs.Select(repair => repair.HitPoints));
        Assert.All(repairs, repair => Assert.True(repair.Revive));
        Assert.Equal((ushort)32, repairs[0].RemoveStatuses0);
        Assert.Equal(256u, repairs[0].RemoveStatuses1);
        Assert.All(repairs.Skip(1), repair => Assert.Equal(0u, repair.RemoveStatuses1));
        Assert.Equal(new[] { "party-0", "party-4", "party-5" }, recipe["driver"]!["terminal_hold"]!["party_actor_ids"]!.Deserialize<string[]>(Json));
    }

    [Fact]
    public void PhysicalPartyZeroSelectorDoesNotDeclareOtherRecipients()
    {
        JsonObject recipe = Recipe();
        ScenarioDiscovery.DeclareEnemyPartyKnockoutRearm(recipe, Action(27, 100, 0), 0, new byte[] { 4, 0, 5 }, _ => 1500);
        Assert.Equal("party-4", recipe["driver"]!["repairs"]![0]!["actor_id"]!.GetValue<string>());
        Assert.Single(recipe["driver"]!["repairs"]!.AsArray());
    }

    [Theory]
    [InlineData(2, 40, 200)]
    [InlineData(2, 40, 205)]
    [InlineData(3, 40, 201)]
    [InlineData(2, 0, 204)]
    public void NonHostileAndNondamagingRowsKeepTheirExistingRecipe(byte type, byte power, byte selector)
    {
        JsonObject recipe = Recipe();
        string before = recipe.ToJsonString();
        ScenarioDiscovery.DeclareEnemyPartyKnockoutRearm(recipe, Action(type, power, 0), selector, new byte[] { 0, 255, 255 }, _ => throw new InvalidOperationException("No HP owner should be consulted."));
        Assert.Equal(before, recipe.ToJsonString());
    }

    private static JsonObject Recipe() => new() { ["driver"] = new JsonObject() };

    private static BattleNativeAction Action(byte type, byte power, ushort statuses) => new(8, 37, 3, 37)
    {
        MagicId = 157,
        AttackType = type,
        Power = power,
        HitCount = 1,
        TargetFlags = null,
        AttackFlags = 0,
        DamagePresentation = 0,
        Element = 0,
        ElementPercent = null,
        StatusAttack = 100,
        Statuses0 = statuses,
        Statuses1 = 0,
        Hit = null,
        Critical = null,
        PowerModifier = null,
        LevelModifier = null,
        RawRecord = [],
        SupplementalRecord = []
    };
}
