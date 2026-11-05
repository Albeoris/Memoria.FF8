using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class BattleMagicSectionRenderingTests
{
    [Theory]
    [InlineData(5, 1f)]
    [InlineData(12, 1f)]
    [InlineData(25, 2f)]
    public void SectionUsesDrawLayoutAndPointerMapsEveryColumn(int count, float textScale)
    {
        BattleHudController hud = Create(count);
        hud.TextScale = textScale;
        hud.Choose(0);
        UiView view = hud.BuildView();
        view.Resize(960, 672);
        BattleUiTests.TestCanvas canvas = new();
        view.Draw(canvas, new UiTheme());
        var labels = canvas.TextDraws.Where(draw => draw.Text.StartsWith("Spell", StringComparison.Ordinal)).ToArray();
        Assert.Equal(count, labels.Length);
        Assert.Equal(BattleDrawLayout.Calculate(count).Columns, labels.Select(draw => draw.Bounds.X).Distinct().Count());
        UiRect panel = canvas.Clips.First(rect => rect.X == 30 && rect.Bottom == 648);
        var caption = Assert.Single(canvas.TextDraws, draw => draw.Text == hud.MagicSectionCaption && draw.Color == new UiTheme().Foreground);
        Assert.True(caption.Bounds.Y < labels.Min(draw => draw.Bounds.Y));
        Assert.True(caption.Bounds.Right <= panel.Right);
        Assert.True(canvas.Texts.FindIndex(text => text.StartsWith("Spell", StringComparison.Ordinal)) > canvas.Texts.IndexOf("Caster"));
        var last = labels[^1];
        view.Pointer(last.Bounds.X + 2, last.Bounds.Y + 2, false);
        Assert.Equal(count - 1, hud.ChoiceIndex);
        view.Pointer(last.Bounds.X + 2, last.Bounds.Y + 2, true);
        Assert.True(hud.SelectingTarget);
        Assert.Equal(count, hud.SelectedAction!.Id);
    }

    [Fact]
    public void SectionArrowClicksRespectBothBoundariesAndReplaceCaption()
    {
        BattleHudController hud = Create(2, true);
        hud.Choose(0);
        UiView view = hud.BuildView();
        view.Resize(960, 672);
        BattleUiTests.TestCanvas first = new();
        view.Draw(first, new UiTheme());
        string firstCaption = hud.MagicSectionCaption;
        var next = Assert.Single(first.Images, image => image.Source.Kind == UiImageKind.Icon && image.Source.Id == 6);
        Assert.False(next.Source.FlipHorizontal);
        UiRect firstPanel = first.Clips.First(rect => rect.X == 30 && rect.Bottom == 648);
        Assert.True(next.Bounds.X >= firstPanel.Right);
        view.Pointer(next.Bounds.X + 2, next.Bounds.Y + 2, true);
        Assert.NotEqual(firstCaption, hud.MagicSectionCaption);
        BattleUiTests.TestCanvas second = new();
        view.Draw(second, new UiTheme());
        Assert.Contains(hud.MagicSectionCaption, second.Texts);
        Assert.DoesNotContain(firstCaption, second.Texts);
        var previous = Assert.Single(second.Images, image => image.Source.Kind == UiImageKind.Icon && image.Source.Id == 6);
        Assert.True(previous.Source.FlipHorizontal);
        Assert.True(previous.Bounds.Right <= 30);
        view.Pointer(previous.Bounds.X + 2, previous.Bounds.Y + 2, false);
        view.Pointer(previous.Bounds.X + 2, previous.Bounds.Y + 2, true);
        Assert.Equal(firstCaption, hud.MagicSectionCaption);
    }

    private static BattleHudController Create(int count, bool recovery = false)
    {
        BattleActionDefinition[] actions =
        [
            .. Enumerable.Range(1, count).Select(id => new BattleActionDefinition(BattleAbility.Magic, id, $"Spell{id}", BattleActionEffect.Damage, 10, false)),
            .. (recovery ? new[] { new BattleActionDefinition(BattleAbility.Magic, count + 1, "Cure", BattleActionEffect.Heal, 10, true) } : [])
        ];
        BattleSession session = new(new BattleDefinition
        {
            Combatants =
            [
                new() { Id = "caster", Name = "Caster", IsPlayer = true, InitialAtb = BattleSession.AtbMaximum, Abilities = [BattleAbility.Magic], MagicStocks = actions.Select(action => new BattleStock(action.Id, 10)).ToArray() },
                new() { Id = "enemy", Name = "Enemy" }
            ],
            ActionDefinitions = actions
        }, BattleAtbMode.TurnBased);
        return new(session, (_, _) => { }, () => { }, () => { });
    }
}
