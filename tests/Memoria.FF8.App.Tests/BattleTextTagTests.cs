using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class BattleTextTagTests
{
    [Theory]
    [InlineData("{Char Angelo1}", "Comet")]
    [InlineData("{Char Angelo2}", "Comet")]
    [InlineData("{Name Angelo}", "Comet")]
    [InlineData("{Char Rinoa}", "Current Rinoa")]
    [InlineData("{Name Shiva}", "Current Shiva")]
    [InlineData("{Color Red}{Char Angelo2}{Color White} Rush", "Comet Rush")]
    public void ActionListsAndTargetCaptionsResolveNativeTags(string encoded, string expected)
    {
        BattleActionDefinition action = new(BattleAbility.Magic, 2, encoded, BattleActionEffect.Damage, 10, false);
        BattleSession battle = new(new BattleDefinition
        {
            Combatants =
            [
                new() { Id = "caster", Name = "Current Rinoa", ModelId = 4, IsPlayer = true, InitialAtb = BattleSession.AtbMaximum, Abilities = [BattleAbility.Magic], MagicStocks = [new(2, 10)] },
                new() { Id = "enemy", Name = "Enemy" }
            ],
            ActionDefinitions = [action]
        }, BattleAtbMode.TurnBased);
        BattleHudController hud = new(battle, (_, _) => { }, () => { }, () => { }, guardianForceName: id => id == 1 ? "Current Shiva" : "GF", battleNames: new Dictionary<int, string> { [2] = "Comet" });
        hud.Choose(0);
        Assert.Equal(expected, Assert.Single(hud.ChoiceLabels));
        hud.Choose(0);
        Assert.Equal(expected, hud.TargetActionCaption);
        Assert.Equal(encoded, action.Name);
    }
}

