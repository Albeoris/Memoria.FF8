using Memoria.FF7.Formats;
using Memoria.FF7.Runtime;
using Memoria.FF8.GameUi;
using Memoria.FF8.Ui;
using Microsoft.Xna.Framework;
using Vector3 = System.Numerics.Vector3;

namespace Memoria.FF8.App.Tests;

public sealed class Ff7BattleHudTests
{
    [Theory]
    [InlineData(-3, "Full Cure")]
    [InlineData(-2, "Death")]
    public void NativeSpecialResultsDisplayTheirMeaning(int marker, string expected)
    {
        BattleUiTests.TestCanvas canvas = new();
        SceneCameraPlacement camera = new(Vector3.Zero, new(0, 0, 1000)) { FocalLength = 480 };
        Ff7Scene.DrawBattleDamagePopup(canvas, new(255, 255, 255), 1, new(0, 0, 960, 540), camera, new(0, 0, 1000), new(false, false, 0), 0, marker);
        Assert.NotEmpty(canvas.TextDraws);
        Assert.All(canvas.TextDraws, draw => Assert.Equal(expected, draw.Text));
    }

    [Theory]
    [InlineData(false, 5u)]
    [InlineData(true, 5u)]
    [InlineData(true, 0u)]
    [InlineData(true, 1u)]
    public void VictoryShowsOriginalApAndAnAdditionalFf8RowOnlyForGuestParties(bool hasFf8Characters, uint abilityPoints)
    {
        BattleHudLayout layout = new(new(0, 0, 960, 672));
        BattleUiTests.TestCanvas canvas = new();
        Ff7BattleReward reward = new(30, 100, abilityPoints, []);

        Ff7Scene.DrawBattleRewards(canvas, new UiTheme(), layout, 1, reward, hasFf8Characters);

        Assert.Single(canvas.TextDraws, draw => draw.Text == "100 EXP   30 Gil");
        var ap = Assert.Single(canvas.TextDraws, draw => draw.Text == $"{abilityPoints} AP");
        Assert.Equal(hasFf8Characters ? 3 : 2, canvas.TextDraws.Count);
        if (hasFf8Characters)
        {
            var ff8Ap = Assert.Single(canvas.TextDraws, draw => draw.Text == $"{Math.Max(1u, abilityPoints / 2)} AP (FF8)");
            Assert.True(ff8Ap.Bounds.Y >= ap.Bounds.Bottom);
        }
        else
        {
            Assert.DoesNotContain(canvas.TextDraws, draw => draw.Text.Contains("AP (FF8)", StringComparison.Ordinal));
        }
    }

    [Theory]
    [InlineData(320, 240)]
    [InlineData(960, 540)]
    public void DamageDisplaysOnlyTheValueAndRisesFromTheProjectedTarget(int width, int height)
    {
        BattleUiTests.TestCanvas canvas = new();
        UiColor white = new(255, 255, 255);
        Rectangle viewport = new(31, 23, width, height);
        SceneCameraPlacement camera = new(Vector3.Zero, new(0, 0, 1000)) { FocalLength = 480 };
        Vector3 anchor = new(125, 0, 1000);
        Ff7BattleDamageResult damage = new(false, false, 4);

        Ff7Scene.DrawBattleDamagePopup(canvas, white, 1, viewport, camera, anchor, damage, 0);
        var first = Assert.Single(canvas.TextDraws, draw => draw.Color == white);
        Assert.All(canvas.TextDraws, draw => Assert.Equal("4", draw.Text));
        Assert.Equal(viewport.Center.X + height / 4f, first.Bounds.X + first.Bounds.Width / 2, 3);
        Assert.Equal(viewport.Center.Y, first.Bounds.Bottom);

        canvas.TextDraws.Clear();
        Ff7Scene.DrawBattleDamagePopup(canvas, white, 1, viewport, camera, anchor, damage, 30);
        var later = Assert.Single(canvas.TextDraws, draw => draw.Color == white);
        Assert.Equal(first.Bounds.X, later.Bounds.X);
        Assert.True(later.Bounds.Y < first.Bounds.Y);
    }

    [Theory]
    [InlineData(Ff7BattleCommand.Magic, false, 8, "8 MP")]
    [InlineData(Ff7BattleCommand.Magic, false, 0, "")]
    [InlineData(Ff7BattleCommand.Magic, true, 8, "8")]
    [InlineData(Ff7BattleCommand.Item, false, 8, "8")]
    [InlineData(Ff7BattleCommand.Item, true, 8, "8")]
    [InlineData(Ff7BattleCommand.Attack, false, 0, "")]
    public void MagicCostAndStockUseSeparateValueText(Ff7BattleCommand command, bool guest, int cost, string expected)
    {
        Ff7BattleCommandOption option = new(command, 1, 0x10, true, true, cost);
        Assert.Equal(expected, Ff7Scene.BattleCommandValue(option, guest));
    }

    [Fact]
    public void MpAppearsBelowHpOnlyWhenSupplied()
    {
        BattleHudLayout layout = new(new(0, 0, 960, 672));
        BattleUiTests.TestCanvas canvas = new();
        UiColor white = new(255, 255, 255);
        layout.DrawVitals(canvas, "Cloud", 123, 300, 528, 1, white, white, 0, 48);
        layout.DrawVitals(canvas, "Squall", 234, 400, 566, 1, white, white);

        var hp = Assert.Single(canvas.TextDraws, draw => draw.Text == "123");
        var mp = Assert.Single(canvas.TextDraws, draw => draw.Text.StartsWith("MP ", StringComparison.Ordinal));
        Assert.Equal("MP 0/48", mp.Text);
        Assert.Equal(new UiColor(120, 227, 255), mp.Color);
        Assert.True(mp.Bounds.Y > hp.Bounds.Y);
        Assert.True(mp.Bounds.Height < hp.Bounds.Height);
        Assert.Equal(566f, mp.Bounds.Bottom);
    }
}
