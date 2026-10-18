using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class PlayerSaveLayoutTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OccupiedRowsKeepSlotTimeLocationAndThreePortraitsSeparate(bool loading)
    {
        PlayerSaveEntry[] entries = Enumerable.Range(loading ? 0 : 1, loading ? 6 : 5).Select(slot => new PlayerSaveEntry(slot, PlayerSaveStore.GetLabel(slot), DateTime.Now, "Balamb Garden- Infirmary", true)
        {
            PlayTime = TimeSpan.FromSeconds(90061),
            PartyPortraits = [4, 0, 1]
        }).ToArray();
        UiGrid blocks = RecreateGame.BuildPlayerSaveBlocks(entries, loading);
        UiView view = new(blocks);
        view.Resize(960, 480);
        UiButton[] rows = blocks.Children.OfType<UiButton>().ToArray();
        Assert.Equal(entries.Length, rows.Length);
        for (int index = 0; index < rows.Length; index++)
        {
            UiElement[] contents = Descendants(rows[index]).ToArray();
            UiLabel slot = Assert.Single(contents.OfType<UiLabel>(), label => label.Text == entries[index].Label);
            UiLabel time = Assert.Single(contents.OfType<UiLabel>(), label => label.Text == "25:01:01");
            UiLabel location = Assert.Single(contents.OfType<UiLabel>(), label => label.Text == entries[index].Details);
            UiImage[] portraits = contents.OfType<UiImage>().ToArray();
            Assert.Equal(new[] { 4, 0, 1 }, portraits.Select(image => image.Source!.Value.Id));
            Assert.All(portraits, image => Assert.Equal(UiImageKind.CharacterPortrait, image.Source!.Value.Kind));
            Assert.True(slot.Bounds.Right <= time.Bounds.X);
            Assert.True(location.Bounds.Y >= slot.Bounds.Bottom);
            Assert.True(location.Bounds.Y >= time.Bounds.Bottom);
            Assert.True(time.Bounds.Right <= portraits[0].Bounds.X);
            Assert.True(location.Bounds.Right <= portraits[0].Bounds.X);
            for (int member = 0; member < portraits.Length; member++)
            {
                UiRect bounds = portraits[member].Bounds;
                Assert.True(bounds.Width > 0 && bounds.Height > 0);
                Assert.True(bounds.Y >= rows[index].Bounds.Y && bounds.Bottom <= rows[index].Bounds.Bottom);
                Assert.True(bounds.Right <= rows[index].Bounds.Right);
                if (member > 0)
                {
                    Assert.True(bounds.X >= portraits[member - 1].Bounds.Right);
                }
            }
            if (index > 0)
            {
                Assert.True(rows[index].Bounds.Y >= rows[index - 1].Bounds.Bottom);
            }
        }
    }

    private static IEnumerable<UiElement> Descendants(UiElement parent)
    {
        foreach (UiElement child in parent.Children)
        {
            yield return child;
            foreach (UiElement descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }

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
