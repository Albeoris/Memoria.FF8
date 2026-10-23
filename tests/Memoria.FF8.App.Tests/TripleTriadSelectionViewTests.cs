using System.Drawing;
using System.Drawing.Imaging;
using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class TripleTriadSelectionViewTests
{
    [Fact]
    public void SelectionListsOwnedCardsInDescendingOrderAndWrapsWithinNineRowPage()
    {
        TripleTriadController controller = Create();
        Assert.Equal(Enumerable.Range(0, 23).Reverse(), controller.Available);
        Assert.Equal(9, TripleTriadController.CardsPerPage);
        controller.Navigate(UiKey.Up);
        Assert.Equal(8, controller.Selection);
        Assert.Equal(0, controller.SelectionPage);
        controller.Navigate(UiKey.Down);
        Assert.Equal(0, controller.Selection);
        Assert.Equal(0, controller.SelectionPage);
    }

    [Theory]
    [InlineData(UiKey.Right, UiKey.Left)]
    [InlineData(UiKey.PageDown, UiKey.PageUp)]
    public void PagingRetainsRowClampsShortPageAndCycles(UiKey forward, UiKey backward)
    {
        TripleTriadController controller = Create();
        controller.Navigate(UiKey.Up);
        controller.Navigate(forward);
        Assert.Equal(17, controller.Selection);
        Assert.Equal(1, controller.SelectionPage);
        controller.Navigate(forward);
        Assert.Equal(22, controller.Selection);
        Assert.Equal(2, controller.SelectionPage);
        controller.Navigate(UiKey.Down);
        Assert.Equal(18, controller.Selection);
        Assert.Equal(2, controller.SelectionPage);
        controller.Navigate(UiKey.Up);
        Assert.Equal(22, controller.Selection);
        controller.Navigate(forward);
        Assert.Equal(4, controller.Selection);
        Assert.Equal(0, controller.SelectionPage);
        controller.Navigate(backward);
        Assert.Equal(22, controller.Selection);
        Assert.Equal(2, controller.SelectionPage);
    }

    [Fact]
    public void ExhaustedCardRemainsInItsRowAndCannotBeSelectedUntilReturned()
    {
        TripleTriadController controller = Create();
        controller.ChooseCard(22);
        Assert.Equal(1, controller.Remaining(22));
        controller.ChooseCard(22);
        Assert.Equal(0, controller.Remaining(22));
        controller.Navigate(UiKey.Confirm);
        Assert.Equal(new[] { 22, 22 }, controller.Deck);
        Assert.Equal(22, controller.Available[controller.Selection]);
        controller.Navigate(UiKey.Cancel);
        Assert.Equal(1, controller.Remaining(22));
        controller.Navigate(UiKey.Confirm);
        Assert.Equal(new[] { 22, 22 }, controller.Deck);
    }

    [Fact]
    public void SelectionDrawsNativeBackgroundPagedRowsAndHandsOnTheirPlayingSides()
    {
        TripleTriadController controller = Create();
        controller.ChooseCard(22);
        UiView view = controller.BuildView();
        RecordingCanvas canvas = Draw(view);
        Assert.Single(canvas.Images, image => image.Source.Kind == UiImageKind.TripleTriadBoardBackground);
        var backs = canvas.Images.Where(image => image.Source.Kind == UiImageKind.CardBack).ToArray();
        Assert.Equal(5, backs.Length);
        Assert.All(backs, image => Assert.True(image.Bounds.Right < 480));
        var chosen = Assert.Single(canvas.Images, image => image.Source.Kind == UiImageKind.CardFace && image.Bounds.X >= 960 - backs[0].Bounds.Right);
        Assert.Equal(22, chosen.Source.Id);
        Assert.Equal(0, chosen.Source.Palette);
        Assert.Equal(9, canvas.Images.Count(image => image.Source.Kind == UiImageKind.Icon && image.Source.Id == 215));
        Assert.Equal(Enumerable.Range(14, 9).Reverse().Select(id => $"Card {id}"), canvas.Texts.Where(line => line.Text.StartsWith("Card ", StringComparison.Ordinal)).Select(line => line.Text));

        UiRect next = Assert.Single(canvas.Texts, line => line.Text == "▶").Bounds;
        Click(view, next);
        Assert.Equal(1, controller.SelectionPage);
        canvas = Draw(view);
        Assert.Equal(Enumerable.Range(5, 9).Reverse().Select(id => $"Card {id}"), canvas.Texts.Where(line => line.Text.StartsWith("Card ", StringComparison.Ordinal)).Select(line => line.Text));
        UiRect row = Assert.Single(canvas.Texts, line => line.Text == "Card 11").Bounds;
        Click(view, row);
        Assert.Equal(new[] { 22, 11 }, controller.Deck);
        canvas = Draw(view);
        Assert.Contains(canvas.Images, image => image.Source.Kind == UiImageKind.CardFace && image.Source.Id == 11 && image.Source.Palette == 0 && image.Bounds.X > 480);
        UiRect previous = Assert.Single(canvas.Texts, line => line.Text == "◀").Bounds;
        Click(view, previous);
        Click(view, previous);
        Assert.Equal(2, controller.SelectionPage);
        canvas = Draw(view);
        Assert.Equal(5, canvas.Images.Count(image => image.Source.Kind == UiImageKind.Icon && image.Source.Id == 215));
        Assert.Equal(Enumerable.Range(0, 5).Reverse().Select(id => $"Card {id}"), canvas.Texts.Where(line => line.Text.StartsWith("Card ", StringComparison.Ordinal)).Select(line => line.Text));
    }

    [Fact]
    public void ClickingRenderedPlayerHandThenBoardPlacesThatCardAtTheDisplayedCell()
    {
        TripleTriadController controller = Create();
        foreach (int id in Enumerable.Range(0, 5))
        {
            controller.ChooseCard(id);
        }
        UiView view = controller.BuildView();
        RecordingCanvas canvas = Draw(view);
        UiRect hand = Assert.Single(canvas.Images, image => image.Source.Kind == UiImageKind.CardFace && image.Source.Id == 2 && image.Source.Palette == 0).Bounds;
        view.Pointer(hand.X + hand.Width / 2, hand.Y + 8, true);
        view.Pointer(hand.X + hand.Width / 2, hand.Y + 8, false);
        Assert.Equal(2, controller.SelectedHand);
        Assert.True(controller.ChoosingCell);
        view.Pointer(480, 336, true);
        Assert.Equal(2, controller.Match!.Board[4]!.CardId);
        canvas = Draw(view);
        var placed = Assert.Single(canvas.Images, image => image.Source.Kind == UiImageKind.CardFace && image.Source.Id == 2 && image.Source.Palette == 0);
        Assert.True(placed.Bounds.Contains(480, 336));
    }

    [Fact]
    public void OccupiedCellKeepsTheNativePointerVisibleAboveCardsAndRejectsPlacement()
    {
        TripleTriadController controller = Create();
        foreach (int id in Enumerable.Range(0, 5))
        {
            controller.ChooseCard(id);
        }
        controller.Match!.Play(0, 4);
        controller.Match.Play(0, 8);
        controller.RestoreMatch(controller.Match);
        controller.SelectHand(1);
        Assert.Equal(4, controller.SelectedCell);
        RecordingCanvas canvas = Draw(controller.BuildView());
        UiRect occupied = Assert.Single(canvas.Images, image => image.Source.Kind == UiImageKind.CardFace && image.Source.Id == 0 && image.Source.Palette == 0).Bounds;
        UiRect pointer = Assert.Single(canvas.Pointers);
        Assert.True(occupied.Contains(pointer.X, pointer.Y));
        Assert.True(occupied.Contains(pointer.Right - 1, pointer.Bottom - 1));
        Assert.True(canvas.Operations.LastIndexOf("pointer") > canvas.Operations.LastIndexOf("image"));
        TripleTriadPlacedCard before = controller.Match.Board[4]!;
        controller.Navigate(UiKey.Confirm);
        Assert.Equal(before, controller.Match.Board[4]);
        Assert.Equal(1, controller.Match.Hands[0][1]);
        Assert.True(controller.ChoosingCell);
    }

    [Fact]
    public void OriginalArtworkRendersSelectionPagesChosenCardsAndMatch()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Original resources required.");
        TripleTriadCatalog catalog = TripleTriadCatalog.Load(directory);
        TripleTriadController controller = new(catalog.Cards, Enumerable.Range(0, 110).ToDictionary(id => id, _ => 1), [0, 1, 2, 3, 4], catalog.Name);
        string output = Environment.GetEnvironmentVariable("MEMORIA_FF8_TRIAD_UI_OUTPUT_DIRECTORY") ?? Path.Combine(Path.GetTempPath(), "ff8-triad-selection-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(output);
        Render("selection-first-page");
        controller.Navigate(UiKey.Right);
        Render("selection-second-page");
        controller.ChooseCard(109);
        controller.ChooseCard(108);
        Render("selection-chosen-hand");
        controller.Navigate(UiKey.Left);
        controller.Navigate(UiKey.Left);
        Render("selection-last-page");
        controller.ChooseCard(107);
        controller.ChooseCard(106);
        controller.ChooseCard(105);
        Render("selection-match");
        controller.Match!.Play(0, 0);
        controller.Match.Play(0, 8);
        controller.RestoreMatch(controller.Match);
        controller.SelectHand(1);
        Render("match-occupied-cursor");
        controller.BeginTradeSelection([0, 1, 2, 3, 4], 1, _ => throw new InvalidOperationException("Rendering must not settle cards."));
        Render("trade-selection");
        controller = new TripleTriadController(catalog.Cards, Enumerable.Range(0, 23).ToDictionary(id => id, id => id == 22 ? 2 : 1), [0, 1, 2, 3, 4], catalog.Name, recentDecks: [[22, 22, 22, 21, 109], [20, 19, 18, 17, 16], [15, 14, 13, 12, 11]]);
        Render("selection-recent-decks");
        controller.ChooseDeck(0);
        Render("selection-recent-incomplete");
        Console.WriteLine($"Triple Triad selection original-resource renders: {output}");

        void Render(string name)
        {
            using Bitmap bitmap = new(960, 672, PixelFormat.Format32bppArgb);
            using BattlePreviewCanvas canvas = new(bitmap, directory);
            UiView view = controller.BuildView();
            view.Resize(960, 672);
            view.Draw(canvas, new UiTheme());
            bitmap.Save(Path.Combine(output, name + ".png"), ImageFormat.Png);
        }
    }

    [Fact]
    public void RecentDeckKeyboardRowsAndPagesKeepEveryOwnedCardReachable()
    {
        TripleTriadController controller = Create();
        controller.SetRecentDecks([[22, 21, 20, 19, 18], [17, 16, 15, 14, 13], [12, 11, 10, 9, 8]]);
        Assert.Equal(0, controller.RecentDeckSelection);
        controller.Navigate(UiKey.Down);
        Assert.Equal(1, controller.RecentDeckSelection);
        controller.Navigate(UiKey.Down);
        Assert.Equal(2, controller.RecentDeckSelection);
        controller.Navigate(UiKey.Down);
        Assert.Equal(-1, controller.RecentDeckSelection);
        Assert.Equal(0, controller.Selection);
        controller.Navigate(UiKey.Up);
        Assert.Equal(2, controller.RecentDeckSelection);
        List<int> cards = [];
        for (int page = 0; page < controller.SelectionPageCount; page++)
        {
            cards.AddRange(controller.Available.Skip(controller.FirstVisibleCard).Take(controller.VisibleCardCount));
            controller.Navigate(UiKey.PageDown);
        }
        Assert.Equal(controller.Available, cards);
        Assert.Equal(0, controller.SelectionPage);
        controller.Navigate(UiKey.Up);
        Assert.Equal(2, controller.RecentDeckSelection);
        controller.Navigate(UiKey.Confirm);
        Assert.Equal([12, 11, 10, 9, 8], controller.Deck);
        Assert.NotNull(controller.Match);
    }

    [Fact]
    public void RecentDeckPointerUsesRenderedRowsAndGraysOnlyMissingCopiesOnFirstPage()
    {
        TripleTriadController controller = Create();
        controller.SetRecentDecks([[22, 22, 22, 21, 90], [20, 19, 18, 17, 16], [15, 14, 13, 12, 11]]);
        UiView view = controller.BuildView();
        RecordingCanvas canvas = Draw(view);
        Assert.Equal(["Recent deck 1", "Recent deck 2", "Recent deck 3"], canvas.Texts.Where(line => line.Text.StartsWith("Recent deck", StringComparison.Ordinal)).Select(line => line.Text));
        var missing = canvas.Images.Where(image => image.Source.Kind == UiImageKind.CardFaceGrayscale).ToArray();
        Assert.Equal([22, 90], missing.Select(image => image.Source.Id));
        Assert.Equal(6, canvas.Images.Count(image => image.Source.Kind == UiImageKind.Icon && image.Source.Id == 215));
        UiRect recent = Assert.Single(canvas.Texts, line => line.Text == "Recent deck 1").Bounds;
        Click(view, recent);
        Assert.Equal([22, 22, 21], controller.Deck);
        Assert.Null(controller.Match);
        canvas = Draw(view);
        Assert.Contains(canvas.Texts, line => line.Text.Contains("2 more", StringComparison.Ordinal));
        controller.Navigate(UiKey.Right);
        canvas = Draw(view);
        Assert.DoesNotContain(canvas.Texts, line => line.Text.StartsWith("Recent deck", StringComparison.Ordinal));
        Assert.DoesNotContain(canvas.Images, image => image.Source.Kind == UiImageKind.CardFaceGrayscale);
        Assert.Equal(9, canvas.Images.Count(image => image.Source.Kind == UiImageKind.Icon && image.Source.Id == 215));
        controller.Navigate(UiKey.Left);
        canvas = Draw(view);
        Click(view, Assert.Single(canvas.Texts, line => line.Text == "Recent deck 2").Bounds);
        Assert.Equal([20, 19, 18, 17, 16], controller.Deck);
        Assert.NotNull(controller.Match);
    }

    private static TripleTriadController Create()
    {
        TripleTriadCard[] cards = Enumerable.Range(0, 110).Select(id => new TripleTriadCard(id, 1, 1, 1, 1, TripleTriadElement.None)).ToArray();
        Dictionary<int, int> collection = Enumerable.Range(0, 23).ToDictionary(id => id, _ => 1);
        collection[22] = 2;
        return new TripleTriadController(cards, collection, [0, 1, 2, 3, 4], id => $"Card {id}");
    }

    private static RecordingCanvas Draw(UiView view)
    {
        view.Resize(960, 672);
        RecordingCanvas canvas = new();
        view.Draw(canvas, new UiTheme());
        return canvas;
    }

    private static void Click(UiView view, UiRect bounds)
    {
        float x = bounds.X + bounds.Width / 2;
        float y = bounds.Y + bounds.Height / 2;
        view.Pointer(x, y, true);
        view.Pointer(x, y, false);
    }

    private sealed class RecordingCanvas : IUiCanvas
    {
        public List<(UiImageSource Source, UiRect Bounds)> Images { get; } = [];
        public List<(string Text, UiRect Bounds)> Texts { get; } = [];
        public List<UiRect> Pointers { get; } = [];
        public List<string> Operations { get; } = [];

        public void Fill(UiRect bounds, UiColor color)
        {
        }

        public void Text(string text, float fontSize, UiRect bounds, UiColor color)
        {
            Texts.Add((text, bounds));
        }

        public void Text(string text, float fontSize, UiRect bounds, UiColor color, UiAlignment verticalAlignment)
        {
            Text(text, fontSize, bounds, color);
        }

        public void OutlinedText(string text, float fontSize, UiRect bounds, UiColor color, UiColor outline, float thickness)
        {
            Text(text, fontSize, bounds, color);
        }

        public void Image(UiImageSource source, UiRect bounds)
        {
            Images.Add((source, bounds));
            Operations.Add("image");
        }

        public void MenuPointer(UiRect bounds)
        {
            Pointers.Add(bounds);
            Operations.Add("pointer");
        }

        public void Line(float x1, float y1, float x2, float y2, UiColor color, float thickness)
        {
        }

        public void PushClip(UiRect bounds)
        {
        }

        public void PopClip()
        {
        }
    }
}
