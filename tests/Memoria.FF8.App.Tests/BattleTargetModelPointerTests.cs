using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class BattleTargetModelPointerTests
{
    [Theory]
    [InlineData(960, 672)]
    [InlineData(1920, 1080)]
    public void Duplicate_names_follow_the_selected_identity_and_cancel_removes_the_model_pointer(int width, int height)
    {
        BattleHudController hud = CreateHud();
        Dictionary<string, UiRect> models = new()
        {
            ["first"] = new(width * 0.3f, height * 0.15f, width * 0.1f, height * 0.2f),
            ["second"] = new(width * 0.6f, height * 0.2f, width * 0.1f, height * 0.2f),
            ["party"] = new(width * 0.8f, height * 0.15f, width * 0.1f, height * 0.2f)
        };
        hud.TargetModelBounds = id => models.GetValueOrDefault(id);
        Assert.Empty(ModelPointers(hud, width, height));
        hud.Navigate(MenuInput.Confirm);
        Assert.Equal("first", hud.SelectedTarget?.Id);
        UiRect first = Assert.Single(ModelPointers(hud, width, height));
        Assert.True(first.Right < models["first"].X);
        hud.Navigate(MenuInput.Down);
        Assert.Equal("second", hud.SelectedTarget?.Id);
        UiRect second = Assert.Single(ModelPointers(hud, width, height));
        Assert.True(second.X > first.X);
        Assert.True(second.Right < models["second"].X);
        hud.ChangeTargetSide();
        Assert.Equal("party", hud.SelectedTarget?.Id);
        UiRect ally = Assert.Single(ModelPointers(hud, width, height));
        Assert.True(ally.Right < models["party"].X);
        hud.Navigate(MenuInput.Cancel);
        Assert.Empty(ModelPointers(hud, width, height));
    }

    [Fact]
    public void Group_selection_marks_each_visible_recipient_and_skips_absent_or_offscreen_models()
    {
        BattleHudController hud = CreateHud(group: true);
        hud.TargetModelBounds = id => id switch
        {
            "first" => new(300, 100, 100, 100),
            "second" => new(550, 150, 100, 100),
            _ => null
        };
        hud.Navigate(MenuInput.Confirm);
        hud.Navigate(MenuInput.Confirm);
        Assert.True(hud.SelectingTargetGroup);
        Assert.Equal(2, ModelPointers(hud, 960, 672).Count);
        hud.TargetModelBounds = id => id == "first" ? new UiRect(-200, 100, 100, 100) : null;
        Assert.Empty(ModelPointers(hud, 960, 672));
        hud.TargetModelBounds = id => id == "first" ? new UiRect(5, 5, 100, 100) : null;
        Assert.Equal(0, Assert.Single(ModelPointers(hud, 960, 672)).X);
    }

    private static BattleHudController CreateHud(bool group = false)
    {
        BattleSession battle = new(new BattleDefinition
        {
            Combatants =
            [
                new() { Id = "party", Name = "Party", IsPlayer = true, InitialAtb = BattleSession.AtbMaximum, Abilities = group ? [BattleAbility.Magic] : [BattleAbility.Attack], MagicStocks = group ? [new(1, 10)] : [] },
                new() { Id = "first", Name = "Same monster" },
                new() { Id = "second", Name = "Same monster" }
            ],
            ActionDefinitions = group ? [new(BattleAbility.Magic, 1, "Group", BattleActionEffect.Damage, 10, false, AllTargets: true)] : []
        }, BattleAtbMode.TurnBased);
        return new(battle, (_, _) => { }, () => { }, () => { });
    }

    private static List<UiRect> ModelPointers(BattleHudController hud, int width, int height)
    {
        BattleUiTests.TestCanvas canvas = new();
        UiView view = hud.BuildView();
        view.Resize(width, height);
        view.Draw(canvas, new UiTheme());
        return canvas.Pointers.Where(pointer => pointer.Y < height * 0.45f).ToList();
    }
}
