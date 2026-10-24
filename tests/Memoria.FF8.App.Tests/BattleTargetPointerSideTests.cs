using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class BattleTargetPointerSideTests
{
    [Fact]
    public void TargetCaptionSwitchesSidesByPointerWithoutSubmittingAnAttack()
    {
        BattleSession battle = new(new BattleDefinition
        {
            Combatants =
            [
                new() { Id = "party", Name = "Party", IsPlayer = true, InitialAtb = BattleSession.AtbMaximum, Abilities = [BattleAbility.Attack] },
                new() { Id = "enemy", Name = "Enemy" }
            ]
        }, BattleAtbMode.TurnBased);
        int attacks = 0;
        BattleHudController hud = new(battle, (_, _) => attacks++, () => { }, () => { });
        hud.Choose(0);
        UiView view = hud.BuildView();
        view.Resize(960, 672);
        BattleUiTests.TestCanvas canvas = new();
        view.Draw(canvas, new UiTheme());
        var caption = canvas.TextDraws.Last(draw => draw.Text == "TARGET  < ENEMIES >");
        view.Pointer(caption.Bounds.X + 12, caption.Bounds.Y + caption.Bounds.Height / 2, false);
        view.Pointer(caption.Bounds.X + 12, caption.Bounds.Y + caption.Bounds.Height / 2, true);
        Assert.Equal("party", hud.SelectedTarget!.Id);
        Assert.Equal(0, attacks);
        canvas = new();
        view.Draw(canvas, new UiTheme());
        caption = canvas.TextDraws.Last(draw => draw.Text == "TARGET  < ALLIES >");
        view.Pointer(caption.Bounds.X + 12, caption.Bounds.Y + caption.Bounds.Height / 2, false);
        view.Pointer(caption.Bounds.X + 12, caption.Bounds.Y + caption.Bounds.Height / 2, true);
        Assert.Equal("enemy", hud.SelectedTarget!.Id);
        Assert.Equal(0, attacks);
    }
}
