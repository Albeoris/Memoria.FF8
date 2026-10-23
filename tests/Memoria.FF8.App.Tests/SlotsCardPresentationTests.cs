using System.Drawing;
using System.Drawing.Imaging;
using Memoria.FF8.GameUi;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class SlotsCardPresentationTests
{
    [Theory]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(7)]
    [InlineData(9)]
    public void SparseTraversalWrapsInBoardOrderAndStartsAtCenter(int count)
    {
        SlotsCardController cards = new(count, 0);
        cards.Advance(TimeSpan.FromSeconds(1), false, false);
        Assert.Equal(4, SlotsCardController.Cell(cards.FocusIndex));
        foreach (MenuInput direction in new[] { MenuInput.Left, MenuInput.Right, MenuInput.Up, MenuInput.Down })
        {
            for (int move = 0; move < 12; move++)
            {
                int step = direction switch { MenuInput.Left => -1, MenuInput.Right => 1, MenuInput.Up => -3, _ => 3 };
                int cell = SlotsCardController.Cell(cards.FocusIndex);
                int next;
                do
                {
                    cell = (cell + step + 9) % 9;
                    next = Enumerable.Range(0, count).FirstOrDefault(index => SlotsCardController.Cell(index) == cell, -1);
                }
                while (next < 0);
                cards.Navigate(direction);
                Assert.Equal(next, cards.FocusIndex);
            }
        }
        HashSet<int> reached = [];
        for (int move = 0; move < count; move++)
        {
            reached.Add(cards.FocusIndex);
            cards.Navigate(MenuInput.Right);
        }
        Assert.Equal(count, reached.Count);
    }

    [Theory]
    [InlineData(30, 3, 0)]
    [InlineData(30, 9, 511)]
    [InlineData(60, 9, 341)]
    [InlineData(60, 9, 0)]
    public void GroupTimelinesCarryElapsedAndEmitOneCuePerTransition(int fps, int count, int mask)
    {
        SlotsCardController cards = new(count, mask);
        List<TripleTriadSound> sounds = [];
        cards.SoundRequested += sounds.Add;
        cards.Begin();
        cards.Begin();
        int frames = 0;
        while (!cards.Ready)
        {
            cards.Advance(TimeSpan.FromSeconds(1d / fps), false, false);
            frames++;
            Assert.True(frames < fps);
        }
        Assert.True((double)frames / fps <= 0.4);
        Assert.Equal(1, sounds.Count(sound => sound == TripleTriadSound.Place));
        Assert.Equal(mask == 0 ? 0 : 1, sounds.Count(sound => sound == TripleTriadSound.Flip));
        for (int index = 0; index < count; index++)
        {
            Assert.Equal((mask & (1 << index)) != 0, cards.PresentedCard(index).FaceUp);
        }
        int chosen = Enumerable.Range(0, count).FirstOrDefault(index => (mask & (1 << index)) == 0);
        cards.Select(chosen);
        cards.Select((chosen + 1) % count);
        cards.Navigate(MenuInput.Right);
        Assert.Equal(chosen, cards.SelectedIndex);
        Assert.Equal(chosen, cards.FocusIndex);
        frames = 0;
        while (!cards.Finished)
        {
            cards.Advance(TimeSpan.FromSeconds(1d / fps), true, true);
            frames++;
        }
        Assert.True((double)frames / fps <= 0.25);
        Assert.Equal(1, sounds.Count(sound => sound == TripleTriadSound.Confirm));
    }

    [Fact]
    public void EntranceHeldInputRequiresReleaseAndStationaryMouseKeepsCenter()
    {
        SlotsCardController cards = new(9, 0);
        cards.Hover(0, 0, 8);
        cards.Advance(TimeSpan.FromMilliseconds(20), false, false);
        cards.Advance(TimeSpan.FromMilliseconds(200), true, true);
        Assert.False(cards.CanConfirm(false));
        Assert.False(cards.CanConfirm(true));
        cards.Hover(0, 0, 8);
        Assert.Equal(0, cards.FocusIndex);
        cards.Advance(TimeSpan.Zero, false, false);
        Assert.True(cards.CanConfirm(false));
        Assert.True(cards.CanConfirm(true));
        cards.Hover(1, 0, 8);
        Assert.Equal(8, cards.FocusIndex);
    }

    [Fact]
    public void InitialRevealsShareMidpointAndPauseDoesNotAdvanceAnimation()
    {
        SlotsCardController cards = new(9, 511);
        cards.Advance(TimeSpan.FromMilliseconds(220), false, false);
        var before = cards.PresentedCard(0);
        Assert.False(before.FaceUp);
        for (int index = 0; index < 9; index++)
        {
            Assert.Equal(before, cards.PresentedCard(index));
        }
        cards.Advance(TimeSpan.Zero, false, false);
        Assert.Equal(before, cards.PresentedCard(0));
        cards.Advance(TimeSpan.FromMilliseconds(80), false, false);
        Assert.True(cards.PresentedCard(0).FaceUp);
        Assert.Equal(before.Width, cards.PresentedCard(0).Width, 5);
    }

    [Fact]
    public void RestoredChoiceSettlesWithoutReplayingEntranceSounds()
    {
        SlotsCardController cards = new(9, 511, 4, restored: true);
        List<TripleTriadSound> sounds = [];
        cards.SoundRequested += sounds.Add;
        cards.Advance(TimeSpan.FromSeconds(1), false, false);
        Assert.True(cards.Finished);
        Assert.Empty(sounds);
        Assert.Equal(4, cards.SelectedIndex);
    }

    [Fact]
    public void OriginalBacksAndDedicatedFrontsRenderAcrossViewportSizes()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Original resources required.");
        string output = Environment.GetEnvironmentVariable("MEMORIA_FF8_SLOTS_UI_OUTPUT_DIRECTORY") ?? Path.Combine(Path.GetTempPath(), "ff8-slots-ui-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(output);
        foreach ((int width, int height) in new[] { (640, 448), (960, 720), (1280, 720), (1720, 720) })
        {
            foreach (int count in new[] { 3, 9 })
            {
                foreach (int mask in new[] { 0, 0x155 & ((1 << count) - 1) })
                {
                    SlotsCardController cards = new(count, mask);
                    cards.Advance(TimeSpan.FromSeconds(1), false, false);
                    using Bitmap bitmap = new(width, height, PixelFormat.Format32bppArgb);
                    using BattlePreviewCanvas canvas = new(bitmap, directory);
                    UiRect bounds = new(0, 0, width, height);
                    SlotsCardView.Draw(canvas, new UiTheme(), bounds, cards, _ => "Full-cure", _ => 3, 1.5f, "↑ ↓ ← →", "Enter");
                    bitmap.Save(Path.Combine(output, $"slots-{width}x{height}-{count}-{mask}.png"), ImageFormat.Png);
                    UiRect center = SlotsCardView.CardBounds(bounds, 0, 1);
                    Assert.Equal(width / 2f, center.X + center.Width / 2, 3);
                    Assert.Equal(height / 2f, center.Y + center.Height / 2, 3);
                    for (int index = 0; index < count; index++)
                    {
                        UiRect card = SlotsCardView.CardBounds(bounds, index, 1);
                        Assert.True(card.X >= 0 && card.Y >= 0 && card.X + card.Width <= width && card.Y + card.Height <= height);
                        Assert.Equal(5f / 6, card.Width / card.Height, 4);
                    }
                }
            }
        }
        Console.WriteLine($"Slots UI original-resource renders: {output}");
    }
}
