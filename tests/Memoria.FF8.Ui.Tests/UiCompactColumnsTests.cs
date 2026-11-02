namespace Memoria.FF8.Ui.Tests;

public sealed class UiCompactColumnsTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Columns_use_measured_width_and_center_as_a_compact_group(int columns)
    {
        UiCompactColumns content = new() { RowHeight = 30, ColumnGap = 20 };
        for (int index = 0; index < columns * 2; index++)
        {
            content.Children.Add(new UiLabel { Text = "Rule", Width = 100 + index / 2 * 10, TextFlow = UiTextFlow.SingleLine });
        }
        content.Arrange(new UiRect(0, 0, 800, 60));
        UiRect first = content.Children[0].Bounds;
        UiRect last = content.Children[^1].Bounds;
        Assert.Equal(first.X, 800 - last.Right);
        Assert.Equal(30, first.Height);
        Assert.Equal(30, content.Children[1].Bounds.Y);
        if (columns > 1)
        {
            Assert.Equal(20, content.Children[2].Bounds.X - first.Right);
        }
    }
}
