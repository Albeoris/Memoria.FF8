using Memoria.FF8.BattleEffects;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Tests;

public sealed class DiagnosticVictoryHoldDeclarationTests
{
    [Theory]
    [InlineData(53, 288)]
    [InlineData(56, 85)]
    public void Only_single_fresh_destructive_slots_accepts_a_victory_hold(byte magic, ushort effect)
    {
        DiagnosticBattleDriverRecipe recipe = new(new(BattleInputKind.Action, ActorId: "party-5", TargetId: "enemy-0", Ability: BattleAbility.LimitBreak, ActionId: 16 << 16 | magic), effect)
        {
            ExpectedNativeAction = magic,
            ExpectedNativeCommand = 16,
            LimitSelection = new(4, 4, 256),
            SlotsAdmission = new(magic, 256, 5) { Reselection = DiagnosticSlotsDeclaration.ReadySelfBatch2 },
            FreshEntry = new(DiagnosticFreshEntryDeclaration.SingleUse, [new("FF8_EN.exe", new string('a', 64))]),
            TerminalHold = new("Single-use native victory cleanup", ["party-5"], 1800) { Candidate = BattleOutcome.Victory }
        };
        recipe.Validate();
        Assert.Throws<InvalidDataException>(() => (recipe with { FreshEntry = null }).Validate());
        Assert.Throws<InvalidDataException>(() => (recipe with { Repairs = [new("party-5", 100)] }).Validate());
        Assert.Throws<InvalidDataException>(() => (recipe with { TerminalHold = recipe.TerminalHold with { PartyActorIds = ["party-0"] } }).Validate());
        Assert.Throws<InvalidDataException>(() => (recipe with { TerminalHold = recipe.TerminalHold with { Candidate = BattleOutcome.Defeat } }).Validate());
        Assert.Throws<InvalidDataException>(() => (recipe with { SlotsAdmission = new(51, 256, 5) }).Validate());
    }

    [Fact]
    public void Native_death_trigger_victory_hold_requires_one_hash_pinned_original_enemy()
    {
        string hash = new('a', 64);
        DiagnosticBattleDriverRecipe driver = new(new(BattleInputKind.Action, ActorId: "party-0", TargetId: "enemy-1", Ability: BattleAbility.Attack), 290)
        {
            ExpectedNativeAction = 291,
            ExpectedNativeCommand = 8,
            IndependentRosterEntry = true,
            EnemyTrigger = new("enemy-1", 109, 1, 0, 3, hash),
            TerminalHold = new("Drain the original death effect before victory", ["party-0"], 1800) { Candidate = BattleOutcome.Victory }
        };
        DiagnosticBattleRoster roster = new([], 1) { DonorCount = 0, AuthoredFormationHash = hash };
        DiagnosticBattleRequest request = new(431, 0, 0x10, 0, 0) { Driver = driver, Roster = roster };
        request.Validate();
        Assert.Throws<InvalidDataException>(() => (request with { Roster = roster with { AuthoredFormationHash = null } }).Validate());
        Assert.Throws<InvalidDataException>(() => (request with { Roster = roster with { OriginalSceneSlot = 0 } }).Validate());
        Assert.Throws<InvalidDataException>(() => (request with { Roster = roster with { DonorCount = 1, Donors = [new(356, 0, hash)] } }).Validate());
        Assert.Throws<InvalidDataException>(() => (request with { Driver = driver with { EnemyTrigger = driver.EnemyTrigger! with { Callback = 2 } } }).Validate());
    }
}
