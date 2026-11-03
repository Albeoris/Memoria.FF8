using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class BattleDrawLayoutTests
{
    [Theory]
    [InlineData(1, 1, 4)]
    [InlineData(4, 1, 4)]
    [InlineData(5, 2, 4)]
    [InlineData(8, 2, 4)]
    [InlineData(9, 2, 5)]
    [InlineData(10, 2, 5)]
    [InlineData(16, 2, 8)]
    [InlineData(17, 3, 8)]
    [InlineData(24, 3, 8)]
    [InlineData(25, 3, 9)]
    [InlineData(36, 3, 12)]
    [InlineData(37, 4, 12)]
    [InlineData(63, 4, 16)]
    public void AlternatingGrowthBalancesColumnsAndMapsEveryChoice(int count, int columns, int rows)
    {
        BattleDrawLayout layout = BattleDrawLayout.Calculate(count);
        Assert.Equal(columns, layout.Columns);
        Assert.Equal(rows, layout.Rows);
        Assert.Equal(count, Enumerable.Range(0, columns).Sum(layout.ColumnRows));
        for (int index = 0; index < count; index++)
        {
            (int column, int row) = layout.GetCell(index);
            Assert.Equal(index, layout.GetIndex(column, row));
        }
        Assert.Equal(-1, layout.GetIndex(columns - 1, layout.ColumnRows(columns - 1)));
    }

    [Theory]
    [InlineData(4)]
    [InlineData(9)]
    [InlineData(16)]
    [InlineData(17)]
    [InlineData(25)]
    [InlineData(37)]
    public void RenderAndPointerUseEveryColumnWithFixedBottom(int count)
    {
        List<BattleInput> submitted = [];
        BattleHudController hud = Create(count, submitted);
        hud.Choose(0);
        UiView view = hud.BuildView();
        view.Resize(960, 672);
        BattleUiTests.TestCanvas canvas = new();
        view.Draw(canvas, new UiTheme());
        var labels = canvas.TextDraws.Where(draw => draw.Text.StartsWith("Spell", StringComparison.Ordinal)).ToArray();
        Assert.Equal(count, labels.Length);
        Assert.True(canvas.Texts.FindIndex(text => text.StartsWith("Spell", StringComparison.Ordinal)) > canvas.Texts.IndexOf("Caster"));
        Assert.Equal(BattleDrawLayout.Calculate(count).Columns, labels.Select(draw => draw.Bounds.X).Distinct().Count());
        UiRect panel = canvas.Clips.First(rect => rect.X == 30 && rect.Bottom == 648);
        Assert.Equal(648, panel.Bottom);
        var last = labels[^1];
        view.Pointer(last.Bounds.X + 2, last.Bounds.Y + 2, true);
        Assert.Equal(count, Assert.Single(submitted).ActionId);
    }

    [Fact]
    public void CastCaptionIsYellowAndArrowIsOutsideWindow()
    {
        BattleHudController hud = Create(1, []);
        hud.Choose(0);
        UiView view = hud.BuildView();
        view.Resize(960, 672);
        BattleUiTests.TestCanvas stock = new();
        view.Draw(stock, new UiTheme());
        var arrow = Assert.Single(stock.Images, image => image.Source.Kind == UiImageKind.Icon && image.Source.Id == 6);
        UiRect panel = stock.Clips.First(rect => rect.X == 30 && rect.Bottom == 648);
        Assert.True(arrow.Bounds.X >= panel.Right);
        Assert.False(arrow.Source.FlipHorizontal);
        view.Pointer(arrow.Bounds.X + 2, arrow.Bounds.Y + 2, true);
        BattleUiTests.TestCanvas cast = new();
        view.Draw(cast, new UiTheme());
        Assert.True(hud.DrawCast);
        Assert.Contains(cast.TextDraws, draw => draw.Text == "Cast" && draw.Color == new UiColor(255, 255, 0));
        var backArrow = Assert.Single(cast.Images, image => image.Source.Kind == UiImageKind.Icon && image.Source.Id == 6);
        UiRect castPanel = cast.Clips.First(rect => rect.X == 30 && rect.Bottom == 648);
        Assert.True(backArrow.Source.FlipHorizontal);
        Assert.True(backArrow.Bounds.Right <= castPanel.X);
        view.Pointer(backArrow.Bounds.X + 2, backArrow.Bounds.Y + 2, false);
        view.Pointer(backArrow.Bounds.X + 2, backArrow.Bounds.Y + 2, true);
        Assert.False(hud.DrawCast);
    }

    [Fact]
    public void GuardianUnknownTextUsesLearnedAbilityColorsAndHighlightsItsModel()
    {
        BattleDefinition definition = Create(1, []).Session.Capture().Definition;
        definition.Combatants[1] = definition.Combatants[1] with { NativeDrawSlots = [new(65, 0), new(0, 0), new(0, 0), new(0, 0)] };
        definition = definition with { GuardianForces = [new(1, 100, 1000, false)] };
        BattleHudController hud = new(new(definition, BattleAtbMode.TurnBased), (_, _) => { }, () => { }, () => { });
        hud.TargetModelBounds = _ => new UiRect(400, 200, 100, 100);
        hud.Choose(0);
        UiView view = hud.BuildView();
        view.Resize(960, 672);
        BattleUiTests.TestCanvas first = new();
        view.Draw(first, new UiTheme());
        Assert.DoesNotContain(first.Images, image => image.Source.Kind == UiImageKind.Icon && image.Source.Id == 6);
        var text = Assert.Single(first.TextDraws, draw => draw.Text == "????");
        Assert.Equal(LearnedAbilityHighlight.Color(hud.BattlePresentationSeconds), text.Color);
        Assert.Contains(first.Pointers, pointer => pointer.X < 400 && pointer.Y > 200 && pointer.Y < 300);
        hud.AdvancePresentation(TimeSpan.FromSeconds(0.5));
        BattleUiTests.TestCanvas second = new();
        view.Draw(second, new UiTheme());
        Assert.NotEqual(text.Color, Assert.Single(second.TextDraws, draw => draw.Text == "????").Color);
    }

    [Fact]
    public void CastOnlyPageHasNoReturnArrow()
    {
        BattleDefinition definition = Create(1, []).Session.Capture().Definition;
        definition.Combatants[0] = definition.Combatants[0] with { MagicStocks = [new(1, 100)] };
        BattleHudController hud = new(new(definition, BattleAtbMode.TurnBased), (_, _) => { }, () => { }, () => { });
        hud.Choose(0);
        Assert.True(hud.DrawCast);
        UiView view = hud.BuildView();
        view.Resize(960, 672);
        BattleUiTests.TestCanvas canvas = new();
        view.Draw(canvas, new UiTheme());
        Assert.DoesNotContain(canvas.Images, image => image.Source.Kind == UiImageKind.Icon && image.Source.Id == 6);
    }

    private static BattleHudController Create(int count, List<BattleInput> submitted)
    {
        BattleSession session = new(new BattleDefinition
        {
            Combatants =
            [
                new() { Id = "party", Name = "Caster", IsPlayer = true, InitialAtb = BattleSession.AtbMaximum, Magic = 100, Abilities = [BattleAbility.Draw] },
                .. Enumerable.Range(0, (count + 3) / 4).Select(enemy => new BattleCombatantDefinition { Id = $"enemy{enemy}", Name = "Enemy", NativeDrawSlots = Enumerable.Range(0, 4).Select(slot => new NativeBattleDrawSlot((byte)(enemy * 4 + slot < count ? enemy * 4 + slot + 1 : 0), 0)).ToArray() })
            ],
            ActionDefinitions = Enumerable.Range(1, count).Select(id => new BattleActionDefinition(BattleAbility.Magic, id, $"Spell{id}", BattleActionEffect.Damage, 10, false)).ToArray()
        }, BattleAtbMode.TurnBased);
        return new(session, (_, _) => { }, () => { }, () => { }, execute: submitted.Add);
    }
}
