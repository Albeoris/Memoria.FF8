using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class PlayerSaveTextScaleTests
{
    [Theory]
    [InlineData(0.25f, 5)]
    [InlineData(1f, 5)]
    [InlineData(1.5f, 3)]
    [InlineData(3f, 1)]
    public void ScalingPreservesSelectedSlotAndUsesTheSameCapacityForPageNavigation(float scale, int expectedCapacity)
    {
        string directory = Path.Combine(Path.GetTempPath(), "save-layout-" + Guid.NewGuid().ToString("N"));
        PlayerSaveStore store = new(Path.Combine(directory, "checkpoint.json"), Path.Combine(directory, "saves"));
        PlayerSaveMenu menu = new(store);
        menu.Open(PlayerSaveMenuMode.Save);
        menu.SelectSlot(17);
        PlayerSaveView.ConfigureTextScale(menu, scale);
        Assert.Equal(expectedCapacity, menu.PageCapacity);
        Assert.Equal(17, menu.SelectedSlot);
        Assert.Contains(menu.VisibleEntries, entry => entry.Slot == menu.SelectedSlot);
        UiView view = PlayerSaveView.Build(menu, "Transfer", "Bank / Page", textScale: scale);
        UiButton selected = Assert.IsType<UiButton>(view.FocusedElement);
        Assert.Equal("save-slot-17", selected.Id);
        Assert.True(selected.Bounds.Height >= 60 * scale);
        Assert.Same(selected, view.Root.HitTest(selected.Bounds.X + 1, selected.Bounds.Y + selected.Bounds.Height / 2));
        int before = menu.Selection;
        menu.RequestPage(1);
        Assert.Equal(before + expectedCapacity, menu.Selection);
        Assert.Contains(menu.VisibleEntries, entry => entry.Slot == menu.SelectedSlot);
        int selectedSlot = menu.SelectedSlot;
        PlayerSaveView.ConfigureTextScale(menu, 1);
        Assert.Equal(selectedSlot, menu.SelectedSlot);
        Assert.Contains(menu.VisibleEntries, entry => entry.Slot == selectedSlot);
        UiView next = PlayerSaveView.Build(menu, "Transfer", "Bank / Page", textScale: scale);
        Assert.Contains(PlayerSaveView.FindButtons(next.Root), button => button.Id == "save-slot-" + selectedSlot);
    }

    [Theory]
    [InlineData(0.25f)]
    [InlineData(1f)]
    [InlineData(1.5f)]
    [InlineData(3f)]
    public void LongSaveNamesAndDatesRemainInSeparateScaledMetadataViewports(float scale)
    {
        PlayerSaveEntry entry = new(1, "A deliberately very long saved character and location name", new DateTime(2026, 10, 21, 23, 59, 59), "Long location metadata with additional saved character details", true)
        {
            PlayTime = TimeSpan.FromHours(1234),
            PartyPortraits = [0, 1, 2]
        };
        UiGrid blocks = PlayerSaveView.BuildBlocks([entry], false, scale, 1);
        UiView view = new(blocks);
        view.Resize(920, Math.Max(90, 60 * scale + 12));
        UiLabel[] labels = Descendants(blocks).OfType<UiLabel>().ToArray();
        UiLabel name = Assert.Single(labels, label => label.Text == entry.Label);
        UiLabel detail = Assert.Single(labels, label => label.Text == entry.Details);
        UiLabel date = Assert.Single(labels, label => label.Text == entry.SavedAtText);
        UiLabel play = Assert.Single(labels, label => label.Text == entry.PlayTimeText);
        Assert.Equal(20 * scale, name.FontSize);
        Assert.Equal(16 * scale, date.FontSize);
        Assert.True(name.Bounds.Bottom <= detail.Bounds.Y);
        Assert.True(play.Bounds.X - name.Bounds.Right >= 6 * scale - 0.01f);
        Assert.True(date.Bounds.X - detail.Bounds.Right >= 6 * scale - 0.01f);
        Assert.All(labels, label =>
        {
            Assert.Equal(UiTextFlow.SingleLine, label.TextFlow);
            Assert.True(label.Bounds.Width > 0);
            Assert.True(label.Bounds.Height >= label.FontSize * 1.4f);
        });
        Assert.Equal(3, Descendants(blocks).OfType<UiImage>().Count());
    }

    private static IEnumerable<UiElement> Descendants(UiElement element)
    {
        yield return element;
        foreach (UiElement child in element.Children)
        {
            foreach (UiElement descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }
}
