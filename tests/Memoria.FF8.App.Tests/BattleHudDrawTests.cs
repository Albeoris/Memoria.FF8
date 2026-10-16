using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class BattleHudDrawTests
{
    [Theory]
    [InlineData(false, "ally")]
    [InlineData(true, "fallen")]
    public void CastSelectsSourceThenSeparateRecipient(bool revive, string recipient)
    {
        BattleSession battle = Create(revive);
        List<BattleInput> inputs = [];
        BattleHudController hud = new(battle, (_, _) => { }, () => { }, () => { }, execute: inputs.Add);
        hud.Choose(0);
        hud.Navigate(MenuInput.Right);
        hud.Navigate(MenuInput.Confirm);
        Assert.Equal("source", Assert.Single(hud.Targets).Id);
        hud.Navigate(MenuInput.Confirm);
        Assert.True(hud.SelectingDrawCastRecipient);
        Assert.Empty(inputs);
        hud.SelectTarget(recipient, true);
        BattleInput input = Assert.Single(inputs);
        Assert.Equal("source", input.TargetId);
        Assert.Equal(recipient, input.DrawCastTargetId);
        Assert.True(input.DrawCast);
        Assert.False(hud.SelectingTarget);
        input.Validate();
    }

    [Fact]
    public void CancelReturnsFromCastRecipientToSourceWithoutSubmitting()
    {
        BattleSession battle = Create(false);
        List<BattleInput> inputs = [];
        BattleHudController hud = new(battle, (_, _) => { }, () => { }, () => { }, execute: inputs.Add);
        hud.Choose(0);
        hud.Navigate(MenuInput.Right);
        hud.Navigate(MenuInput.Confirm);
        hud.Navigate(MenuInput.Confirm);
        hud.Navigate(MenuInput.Cancel);
        Assert.False(hud.SelectingDrawCastRecipient);
        Assert.True(hud.SelectingTarget);
        Assert.Equal("source", Assert.Single(hud.Targets).Id);
        Assert.Empty(inputs);
    }

    [Fact]
    public void GuardianForceDrawUsesSavedNameAndStockWithoutCastRecipient()
    {
        BattleSession battle = Create(false, true);
        List<BattleInput> inputs = [];
        BattleHudController hud = new(battle, (_, _) => { }, () => { }, () => { }, execute: inputs.Add, guardianForceName: id => id == 1 ? "Custom Shiva" : $"GF{id}");
        hud.Choose(0);
        Assert.Contains("Custom Shiva · Stock", hud.ChoiceLabels);
        hud.Navigate(MenuInput.Right);
        hud.Navigate(MenuInput.Confirm);
        hud.Navigate(MenuInput.Confirm);
        BattleInput input = Assert.Single(inputs);
        Assert.Equal(65, input.ActionId);
        Assert.Equal("source", input.TargetId);
        Assert.False(input.DrawCast);
        Assert.Null(input.DrawCastTargetId);
    }

    [Fact]
    public void UnseenSpellUsesTheSavedKnowledgeBankWithoutHidingItsDrawSelection()
    {
        BattleDefinition definition = Create(false).Capture().Definition;
        NativeKnownMagic empty = new(NativeInputConfiguration.OriginalInitHash, System.Collections.Immutable.ImmutableArray.Create<uint>(0, 0));
        BattleSession battle = new(definition with { NativeKnownMagic = empty }, BattleAtbMode.TurnBased);
        BattleHudController hud = new(battle, (_, _) => { }, () => { }, () => { });
        hud.Choose(0);
        Assert.Equal("???? · Stock", Assert.Single(hud.ChoiceLabels));
        Assert.Equal(2, hud.SelectedAction!.Id);
    }

    private static BattleSession Create(bool revive, bool gf = false)
    {
        return new(new BattleDefinition
        {
            Combatants =
            [
                new() { Id = "party", Name = "Caster", IsPlayer = true, MaximumHp = 100, CurrentHp = 100, InitialAtb = BattleSession.AtbMaximum, Speed = 1, Abilities = [BattleAbility.Draw] },
                new() { Id = "ally", Name = "Ally", IsPlayer = true, MaximumHp = 100, CurrentHp = 10, Speed = 1 },
                new() { Id = "fallen", Name = "Fallen", IsPlayer = true, MaximumHp = 100, CurrentHp = 0, NativeStatus0 = 1, Speed = 1 },
                new() { Id = "source", Name = "Source", MaximumHp = 100, CurrentHp = 100, Speed = 1, NativeDrawSlots = [new(gf ? (byte)65 : (byte)2, 0), new(0, 0), new(0, 0), new(0, 0)] },
                new() { Id = "other", Name = "Other", MaximumHp = 100, CurrentHp = 100, Speed = 1 }
            ],
            GuardianForces = [new(1, 1000, 100, false)],
            ActionDefinitions = [new(BattleAbility.Magic, 2, revive ? "Life" : "Cure", revive ? BattleActionEffect.Revive : BattleActionEffect.Heal, 0, true)]
        }, BattleAtbMode.TurnBased);
    }
}
