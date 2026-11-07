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
}
