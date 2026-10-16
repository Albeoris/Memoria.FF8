using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class PlayerSaveLayoutTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EveryVisibleSaveBlockOccupiesADistinctRowIncludingPinnedQuickSave(bool loading)
    {
        PlayerSaveEntry[] entries = Enumerable.Range(loading ? 0 : 1, loading ? 6 : 5).Select(slot => new PlayerSaveEntry(slot, PlayerSaveStore.GetLabel(slot), null, "Empty", false)).ToArray();
        UiGrid blocks = RecreateGame.BuildPlayerSaveBlocks(entries, loading);
        UiView view = new(blocks);
        view.Resize(960, 480);
        UiButton[] rows = blocks.Children.OfType<UiButton>().ToArray();
        Assert.Equal(entries.Length, rows.Length);
        for (int index = 0; index < rows.Length; index++)
        {
            Assert.True(rows[index].Bounds.Height > 50);
            if (index > 0)
            {
                Assert.True(rows[index].Bounds.Y >= rows[index - 1].Bounds.Bottom);
            }
            Assert.True(view.Focus(rows[index]));
            Assert.Equal("save-slot-" + entries[index].Slot, view.FocusedElement?.Id);
            Assert.Same(rows[index], view.Root.HitTest(rows[index].Bounds.X + 1, rows[index].Bounds.Y + 1));
        }
    }
}
