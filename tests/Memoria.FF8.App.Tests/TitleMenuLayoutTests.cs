using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class TitleMenuLayoutTests
{
    [Theory]
    [InlineData(640, 448, 0.25f)]
    [InlineData(960, 672, 1f)]
    [InlineData(1280, 720, 1.5f)]
    [InlineData(1920, 1080, 3f)]
    public void ChoicesAndWrappedHintStayInsideViewportWithoutOverlapping(int width, int height, float scale)
    {
        UiRect bounds = new(20, 30, width, height);
        TitleMenuLayout layout = TitleMenuLayout.Create(bounds, scale);
        Assert.Equal(3, layout.Choices.Length);
        for (int index = 0; index < layout.Choices.Length; index++)
        {
            UiRect row = layout.Choices[index];
            Assert.True(row.Height > 0);
            Assert.True(row.X >= bounds.X && row.Right <= bounds.Right);
            if (index > 0)
            {
                Assert.True(row.Y >= layout.Choices[index - 1].Bottom - 0.001f);
            }
        }
        Assert.True(layout.Hint.Y > layout.Choices[^1].Bottom);
        Assert.True(layout.Hint.Bottom <= bounds.Bottom);
        Assert.True(layout.Hint.X >= bounds.X && layout.Hint.Right <= bounds.Right);
        Assert.True(layout.HintFontSize > 0);
    }
}
