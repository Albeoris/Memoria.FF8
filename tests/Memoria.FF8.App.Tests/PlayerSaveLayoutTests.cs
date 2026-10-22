using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class PlayerSaveLayoutTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OccupiedRowsKeepSlotPlayTimeLocalTimestampLocationAndPortraitsSeparate(bool loading)
    {
        PlayerSaveEntry[] entries = Enumerable.Range(loading ? 0 : 1, 5).Select(slot => new PlayerSaveEntry(slot, PlayerSaveStore.GetLabel(slot), new DateTime(2025, 1, 2, 15, 23, 45, DateTimeKind.Utc), "Balamb Garden- Infirmary", true)
        {
            SavedLocalAt = new DateTimeOffset(2025, 1, 2, 17, 23, 45, TimeSpan.FromHours(2)),
            PlayTime = TimeSpan.FromSeconds(90061),
            PartyPortraits = [4, 0, 1]
        }).ToArray();
        UiGrid blocks = PlayerSaveView.BuildBlocks(entries, loading);
        UiView view = new(blocks);
        view.Resize(960, 480);
        UiButton[] rows = blocks.Children.OfType<UiButton>().ToArray();
        Assert.Equal(entries.Length, rows.Length);
        for (int index = 0; index < rows.Length; index++)
        {
            UiElement[] contents = Descendants(rows[index]).ToArray();
            UiLabel slot = Assert.Single(contents.OfType<UiLabel>(), label => label.Text == entries[index].Label);
            UiLabel time = Assert.Single(contents.OfType<UiLabel>(), label => label.Text == "25:01:01");
            UiLabel timestamp = Assert.Single(contents.OfType<UiLabel>(), label => label.Text == "2025-01-02 17:23:45");
            UiLabel location = Assert.Single(contents.OfType<UiLabel>(), label => label.Text == entries[index].Details);
            UiImage[] portraits = contents.OfType<UiImage>().ToArray();
            Assert.Equal(new[] { 4, 0, 1 }, portraits.Select(image => image.Source!.Value.Id));
            Assert.All(portraits, image => Assert.Equal(UiImageKind.CharacterPortrait, image.Source!.Value.Kind));
            Assert.Equal(slot.Bounds.Y, time.Bounds.Y);
            Assert.Equal(slot.Bounds.Height, time.Bounds.Height);
            Assert.Equal(location.Bounds.Y, timestamp.Bounds.Y);
            Assert.Equal(location.Bounds.Height, timestamp.Bounds.Height);
            Assert.True(slot.Bounds.Right <= time.Bounds.X);
            Assert.True(location.Bounds.Y >= slot.Bounds.Bottom);
            Assert.True(location.Bounds.Y >= time.Bounds.Bottom);
            Assert.True(timestamp.Bounds.Y >= time.Bounds.Bottom);
            Assert.True(location.Bounds.Right <= timestamp.Bounds.X);
            Assert.True(time.Bounds.Right <= portraits[0].Bounds.X);
            Assert.True(location.Bounds.Right <= portraits[0].Bounds.X);
            Assert.True(timestamp.Bounds.Right <= portraits[0].Bounds.X);
            float textTop = Math.Min(slot.Bounds.Y, time.Bounds.Y);
            float textBottom = Math.Max(location.Bounds.Bottom, timestamp.Bounds.Bottom);
            Assert.InRange(Math.Abs((textTop + textBottom) / 2 - (rows[index].Bounds.Y + rows[index].Bounds.Height / 2)), 0, 1);
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
    public void EveryVisibleSaveBlockOccupiesADistinctRowIncludingQuickSnapshots(bool loading)
    {
        int[] slots = loading ? [0, -1, 8, -2, 12] : [1, 2, 3, 4, 5];
        PlayerSaveEntry[] entries = slots.Select(slot => new PlayerSaveEntry(slot, PlayerSaveStore.GetLabel(slot), null, "Empty", false)).ToArray();
        UiGrid blocks = PlayerSaveView.BuildBlocks(entries, loading);
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
