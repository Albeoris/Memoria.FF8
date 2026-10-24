using System.Text.Json;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class BattleHudStatusPresentationTests
{
    [Theory]
    [InlineData(0u, false, 254, 149, 14, 179, 40, 1)]
    [InlineData(0u, true, 255, 255, 0, 255, 255, 0)]
    [InlineData(2u, false, 240, 5, 58, 150, 0, 110)]
    [InlineData(2u, true, 240, 5, 58, 150, 0, 110)]
    [InlineData(4u, false, 94, 46, 251, 70, 0, 140)]
    [InlineData(4u, true, 94, 46, 251, 70, 0, 140)]
    [InlineData(8u, false, 154, 154, 154, 154, 154, 154)]
    [InlineData(14u, false, 240, 5, 58, 150, 0, 110)]
    [InlineData(12u, false, 94, 46, 251, 70, 0, 140)]
    public void GaugeUsesNativeStatusPrecedenceAndHorizontalColorEndpoints(uint statuses, bool ready, byte leftR, byte leftG, byte leftB, byte rightR, byte rightG, byte rightB)
    {
        BattleSession battle = CreateBattle(statuses, ready);
        BattleUiTests.TestCanvas canvas = Draw(new(battle, (_, _) => { }, () => { }, () => { }));

        var fills = canvas.Fills.Where(fill => fill.Bounds.Y == 541 && fill.Bounds.Height == 8).ToArray();
        Assert.Equal(16, fills.Length);
        Assert.Equal(new UiColor(leftR, leftG, leftB), fills[0].Color);
        Assert.Equal(new UiColor(rightR, rightG, rightB), fills[^1].Color);
        Assert.Equal(832, fills[0].Bounds.X);
        Assert.Equal(ready ? 932 : 882, fills[^1].Bounds.Right);
    }

    [Fact]
    public void ExistingMightyGuardStatusesAreIdentifiedAndDoNotNeedScaffoldEffects()
    {
        BattleSession battle = CreateBattle(0x2062, false);
        Assert.Empty(battle.Combatants[0].StatusEffects);
        string before = JsonSerializer.Serialize(battle.Capture());
        BattleUiTests.TestCanvas canvas = Draw(new(battle, (_, _) => { }, () => { }, () => { }));

        foreach (string name in new[] { "Haste", "Protect", "Shell", "Float" })
        {
            Assert.Contains(canvas.TextDraws, draw => draw.Text == name && draw.Bounds.Y == 553);
        }
        Assert.Equal(before, JsonSerializer.Serialize(battle.Capture()));
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(before)!);
        Assert.Equal(new BattleStatusPresentation(0, 0x2062), restored.ReadStatusPresentation("party"));
        BattleUiTests.TestCanvas restoredCanvas = Draw(new(restored, (_, _) => { }, () => { }, () => { }));
        Assert.Equal(canvas.Texts, restoredCanvas.Texts);
    }

    [Theory]
    [InlineData(2, 0u, "Poison", 273)]
    [InlineData(64, 0u, "Zombie", 278)]
    [InlineData(0, 1u, "Sleep", 279)]
    [InlineData(0, 4u, "Slow", 280)]
    [InlineData(0, 8u, "Stop", 281)]
    [InlineData(0, 512u, "Curse", 282)]
    [InlineData(0, 16384u, "Confuse", 283)]
    [InlineData(0, 32768u, "Drain", 284)]
    public void TargetRowsShowStatusNameAndOriginalIcon(ushort saved, uint transient, string name, int icon)
    {
        BattleSession battle = CreateBattle(0, true, saved, transient);
        BattleHudController hud = new(battle, (_, _) => { }, () => { }, () => { });
        hud.Choose(0);
        Assert.True(hud.SelectingTarget);

        BattleUiTests.TestCanvas canvas = Draw(hud);

        var label = Assert.Single(canvas.TextDraws, draw => draw.Text == name);
        Assert.True(label.Bounds.X < 496);
        Assert.Contains(canvas.Images, image => image.Source == new UiImageSource(UiImageKind.Icon, icon) && image.Bounds.Y == label.Bounds.Y);
    }

    [Fact]
    public void ExplicitSyntheticEffectsUseTheSameStatusProjection()
    {
        BattleSession battle = new(new BattleDefinition
        {
            Combatants =
            [
                new() { Id = "party", Name = "Party", IsPlayer = true, InitialAtb = BattleSession.AtbMaximum / 2, StatusEffects = [new() { Kind = BattleStatusKind.Haste }, new() { Kind = BattleStatusKind.Protect }] },
                new() { Id = "enemy", Name = "Enemy" }
            ]
        }, BattleAtbMode.Normal);

        Assert.Equal(new BattleStatusPresentation(0, 34), battle.ReadStatusPresentation("party"));
        BattleUiTests.TestCanvas canvas = Draw(new(battle, (_, _) => { }, () => { }, () => { }));
        Assert.Contains("Haste", canvas.Texts);
        Assert.Contains("Protect", canvas.Texts);
        Assert.Contains(canvas.Fills, fill => fill.Bounds.Y == 541 && fill.Color == new UiColor(240, 5, 58));
    }

    private static BattleSession CreateBattle(uint partyStatus, bool ready, ushort enemySaved = 0, uint enemyStatus = 0)
    {
        return new(new BattleDefinition
        {
            Combatants =
            [
                new() { Id = "party", Name = "Party", IsPlayer = true, InitialAtb = ready ? BattleSession.AtbMaximum : BattleSession.AtbMaximum / 2, NativeStatus1 = partyStatus, Abilities = [BattleAbility.Attack] },
                new() { Id = "enemy", Name = "Enemy", NativeStatus0 = enemySaved, NativeStatus1 = enemyStatus }
            ]
        }, BattleAtbMode.Normal);
    }

    private static BattleUiTests.TestCanvas Draw(BattleHudController hud)
    {
        BattleUiTests.TestCanvas canvas = new();
        UiView view = hud.BuildView();
        view.Resize(960, 672);
        view.Draw(canvas, new UiTheme());
        return canvas;
    }
}
