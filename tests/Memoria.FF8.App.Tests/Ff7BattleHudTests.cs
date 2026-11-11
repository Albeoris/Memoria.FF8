using Memoria.FF7.Runtime;
using Memoria.FF8.GameUi;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class Ff7BattleHudTests
{
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
