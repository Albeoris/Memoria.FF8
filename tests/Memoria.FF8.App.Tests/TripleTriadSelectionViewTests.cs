using Bitmap = SkiaSharp.SKBitmap;
using Color = SkiaSharp.SKColor;

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
    public void SelectionDrawsVectorBoardPagedRowsAndHandsOnTheirPlayingSides()
    {
        TripleTriadController controller = Create();
        controller.ChooseCard(22);
        UiView view = controller.BuildView();
        RecordingCanvas canvas = Draw(view);
        Assert.DoesNotContain(canvas.Images, image => image.Source.Kind == UiImageKind.TripleTriadBoardBackground);
        Assert.Contains(canvas.Texts, text => text.Text == "Triple Triad");
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
            using Bitmap bitmap = new(960, 672, SkiaSharp.SKColorType.Rgba8888, SkiaSharp.SKAlphaType.Unpremul);
            using BattlePreviewCanvas canvas = new(bitmap, directory);
            UiView view = controller.BuildView();
            view.Resize(960, 672);
            view.Draw(canvas, new UiTheme());
            bitmap.SavePng(Path.Combine(output, name + ".png"));
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

    [Theory]
    [InlineData(2, 2, 2)]
    [InlineData(4, 4, 8)]
    [InlineData(5, 5, 30)]
    public void ExtendedLayoutKeepsEveryHandCardVisibleAndCellsClickable(int width, int height, int handSize)
    {
        TripleTriadLayout layout = new(width, height, handSize);
        for (int cell = 0; cell < width * height; cell++)
        {
            UiRect bounds = layout.Cell(cell);
            Assert.InRange(bounds.X, 220, 740);
            Assert.InRange(bounds.Bottom, 48, 624);
        }
        for (int owner = 0; owner < 2; owner++)
        {
            for (int hand = 0; hand < handSize; hand++)
            {
                UiRect bounds = layout.Hand(hand, owner);
                Assert.InRange(bounds.X, 0, 960);
                Assert.InRange(bounds.Right, 0, 960);
                Assert.InRange(bounds.Bottom, 0, 624);
                Assert.True(bounds.Width > 20);
            }
        }
        if (handSize > 5)
        {
            Assert.True(Enumerable.Range(0, handSize).Select(hand => layout.Hand(hand, 0).X).Distinct().Count() > 1);
        }
        TripleTriadCard[] cards = Enumerable.Range(0, 110).Select(id => new TripleTriadCard(id, 1, 1, 1, 1, TripleTriadElement.None)).ToArray();
        TripleTriadController controller = new(cards, Enumerable.Range(0, 110).ToDictionary(id => id, _ => 1), Enumerable.Range(50, handSize).ToArray(), id => id.ToString(), options: new() { Width = width, Height = height, HandSize = handSize });
        foreach (int id in Enumerable.Range(0, handSize))
        {
            controller.ChooseCard(id);
        }
        controller.SelectHand(handSize - 1);
        UiView view = controller.BuildView();
        _ = Draw(view);
        Click(view, layout.Cell(width * height - 1));
        Assert.Equal(handSize - 1, controller.Match!.Board[^1]!.CardId);
    }

    [Theory]
    [InlineData(TripleTriadAssistance.Direct, 0)]
    [InlineData(TripleTriadAssistance.Rules, 2)]
    [InlineData(TripleTriadAssistance.Combo, 4)]
    [InlineData(TripleTriadAssistance.Hand, 4)]
    public void AssistanceSeparatesDirectRuleAndComboCapturesWithoutChangingMatch(TripleTriadAssistance assistance, int flips)
    {
        TripleTriadCard[] cards = Enumerable.Range(0, 110).Select(id => new TripleTriadCard(id, 1, 1, 1, 1, TripleTriadElement.None)).ToArray();
        cards[0] = new(0, 5, 5, 5, 5, TripleTriadElement.Fire);
        cards[5] = new(5, 5, 9, 5, 1, TripleTriadElement.None);
        cards[6] = new(6, 5, 5, 5, 5, TripleTriadElement.None);
        TripleTriadMatch match = TripleTriadMatch.Create(cards, [0, 1, 2, 3, 4], [5, 6, 7, 8, 9], TripleTriadRules.Same);
        match.Play(1, 8);
        match.Play(0, 1);
        match.Play(2, 6);
        match.Play(1, 5);
        match.Play(3, 0);
        match.Play(2, 2);
        TripleTriadController controller = new(cards, Enumerable.Range(0, 5).ToDictionary(id => id, _ => 1), [5, 6, 7, 8, 9], id => id.ToString(), TripleTriadRules.Same, options: new() { Assistance = assistance });
        controller.RestoreMatch(match);
        controller.SelectHand(0);
        controller.HoverCell(4);
        var before = match.CaptureSnapshot();
        RecordingCanvas canvas = Draw(controller.BuildView());
        Assert.Single(canvas.Images, image => image.Source.Opacity == 135 && image.Source.Id == 0);
        Assert.Equal(flips, canvas.Images.Count(image => image.Source.Opacity == 105));
        Assert.Equal(before.Board, match.CaptureSnapshot().Board);
        Assert.Equal(before.Hands[0], match.Hands[0]);
        Assert.Equal(assistance >= TripleTriadAssistance.Rules, canvas.Texts.Any(text => text.Text.StartsWith("Same:", StringComparison.Ordinal)));
        Assert.Equal(assistance >= TripleTriadAssistance.Combo, canvas.Images.Any(image => image.Source.Kind == UiImageKind.TripleTriadCapture && image.Source.Id == 2));
        Assert.Equal(assistance == TripleTriadAssistance.Hand, canvas.Texts.Any(text => text.Text == "Same"));
    }

    [Theory]
    [InlineData(2, 2, 5)]
    [InlineData(3, 3, 5)]
    [InlineData(4, 4, 13)]
    [InlineData(5, 5, 30)]
    public void OriginalArtworkRendersExtendedBoardsAndHands(int width, int height, int handSize)
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Original resources required.");
        TripleTriadCatalog catalog = TripleTriadCatalog.Load(directory);
        TripleTriadSettings settings = new() { Width = width, Height = height, HandSize = handSize, Assistance = TripleTriadAssistance.Hand };
        TripleTriadController controller = new(catalog.Cards, Enumerable.Range(0, 110).ToDictionary(id => id, _ => 1), Enumerable.Range(55, handSize).ToArray(), catalog.Name, TripleTriadRules.Open | TripleTriadRules.Same | TripleTriadRules.Plus, options: settings);
        foreach (int id in Enumerable.Range(0, handSize))
        {
            controller.ChooseCard(id);
        }
        controller.Match!.Play(0, 0);
        controller.Match.Play(0, 1);
        if (width * height > 4)
        {
            controller.Match.Play(1, width);
            controller.Match.Play(1, width + 1);
        }
        controller.SelectHand(width * height > 4 ? 2 : 1);
        controller.HoverCell(width * height - 1);
        using Bitmap bitmap = new(1440, 1008, SkiaSharp.SKColorType.Rgba8888, SkiaSharp.SKAlphaType.Unpremul);
        using BattlePreviewCanvas canvas = new(bitmap, directory);
        UiView view = controller.BuildView();
        view.Resize(1440, 1008);
        view.Draw(canvas, new UiTheme());
        string output = Environment.GetEnvironmentVariable("MEMORIA_FF8_TRIAD_UI_OUTPUT_DIRECTORY") ?? Path.Combine(Path.GetTempPath(), "ff8-triad-extended");
        Directory.CreateDirectory(output);
        bitmap.SavePng(Path.Combine(output, $"extended-{width}x{height}-hand{handSize}.png"));
    }

    [Theory]
    [InlineData(0, "0")]
    [InlineData(9, "9")]
    [InlineData(10, "A")]
    [InlineData(11, "B")]
    public void ElementalRanksUseTheExtendedCardAlphabet(int value, string text)
    {
        Assert.Equal(text, CardFaceComposer.RankText(value));
    }

    [Fact]
    public void OriginalArtworkRendersRuleArrowsComboAndElementalB()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Original resources required.");
        TripleTriadCatalog catalog = TripleTriadCatalog.Load(directory);
        TripleTriadCard source = catalog.Cards[0];
        int top = catalog.Cards.First(card => card.Bottom == source.Top).Id;
        int bottom = catalog.Cards.First(card => card.Top == source.Bottom).Id;
        TripleTriadMatch match = TripleTriadMatch.Create(catalog.Cards, [0, 1, 2, 3, 4], [top, bottom, 5, 6, 7], TripleTriadRules.Open | TripleTriadRules.Same);
        match.Play(1, 8);
        match.Play(0, 1);
        match.Play(2, 6);
        match.Play(1, 7);
        TripleTriadController controller = new(catalog.Cards, Enumerable.Range(0, 110).ToDictionary(id => id, _ => 1), [top, bottom, 5, 6, 7], catalog.Name, match.Rules, options: new() { Assistance = TripleTriadAssistance.Hand });
        controller.RestoreMatch(match);
        controller.SelectHand(0);
        controller.HoverCell(4);
        Assert.True(match.PreviewMove(0, 4).Same);
        string output = Environment.GetEnvironmentVariable("MEMORIA_FF8_TRIAD_UI_OUTPUT_DIRECTORY") ?? Path.Combine(Path.GetTempPath(), "ff8-triad-extended");
        Directory.CreateDirectory(output);
        using Bitmap bitmap = new(1440, 1008, SkiaSharp.SKColorType.Rgba8888, SkiaSharp.SKAlphaType.Unpremul);
        using BattlePreviewCanvas canvas = new(bitmap, directory);
        UiView view = controller.BuildView();
        view.Resize(1440, 1008);
        view.Draw(canvas, new UiTheme());
        bitmap.SavePng(Path.Combine(output, "assistance-rules.png"));
        canvas.ImageStretched(new UiImageSource(UiImageKind.CardFace, catalog.Cards.First(card => card.Top == 10).Id, 0, 1), new UiRect(400, 250, 160, 192));
        bitmap.SavePng(Path.Combine(output, "elemental-b.png"));
    }

    [Fact]
    public void ElementalDigitsHaveDistinctGreenAndRedPalettesEvenWhenCached()
    {
        CardFaceComposer composer = new("Arial");
        Albeoris.Games.FF8.Imaging.PixelImage artwork = new(256, 256);
        TripleTriadCard card = new(0, 5, 5, 5, 5, TripleTriadElement.None);
        var normalSix = composer.Compose(artwork, card with { Top = 6, Right = 6, Bottom = 6, Left = 6 }, 0, null);
        var raisedSix = composer.Compose(artwork, card, 0, null, 1);
        var loweredSix = composer.Compose(artwork, card with { Top = 7, Right = 7, Bottom = 7, Left = 7 }, 0, null, -1);
        var region = Enumerable.Range(0, 48).SelectMany(y => Enumerable.Range(0, 48).Select(x => (X: 24 + x, Y: 8 + y))).ToArray();
        Assert.Contains(region, pixel => raisedSix[pixel.X, pixel.Y].G > raisedSix[pixel.X, pixel.Y].R + 40 && raisedSix[pixel.X, pixel.Y].G > raisedSix[pixel.X, pixel.Y].B + 40);
        Assert.Contains(region, pixel => loweredSix[pixel.X, pixel.Y].R > loweredSix[pixel.X, pixel.Y].G + 70 && loweredSix[pixel.X, pixel.Y].R > loweredSix[pixel.X, pixel.Y].B + 70);
        Assert.NotEqual(normalSix.Pixels.ToArray(), raisedSix.Pixels.ToArray());
        Assert.Equal(raisedSix.Pixels.ToArray(), composer.Compose(artwork, card, 0, null, 1).Pixels.ToArray());
    }

    [Theory]
    [InlineData(TripleTriadRules.Same, false, false)]
    [InlineData(TripleTriadRules.Plus, false, false)]
    [InlineData(TripleTriadRules.Same | TripleTriadRules.Plus, false, false)]
    [InlineData(TripleTriadRules.Same, true, true)]
    [InlineData(TripleTriadRules.Plus, true, true)]
    [InlineData(TripleTriadRules.None, false, true)]
    [InlineData(TripleTriadRules.None, true, true)]
    [InlineData(TripleTriadRules.SameWall, false, true)]
    public void ElementalRanksAndBadgesFollowTheChosenComparisonMode(TripleTriadRules rules, bool modified, bool displayEffective)
    {
        TripleTriadCard[] cards = Enumerable.Range(0, 110).Select(id => new TripleTriadCard(id, 5, 5, 5, 5, TripleTriadElement.None)).ToArray();
        cards[0] = cards[0] with { Element = TripleTriadElement.Fire };
        TripleTriadElement[] elements = new TripleTriadElement[9];
        elements[8] = elements[1] = elements[4] = TripleTriadElement.Fire;
        rules |= TripleTriadRules.Elemental | TripleTriadRules.Open;
        TripleTriadMatch match = TripleTriadMatch.Create(cards, [0, 1, 2, 3, 4], [5, 6, 7, 8, 9], rules, boardElements: elements, useModifiedSamePlus: modified);
        match.Play(1, 8);
        match.Play(0, 1);
        TripleTriadController controller = new(cards, Enumerable.Range(0, 5).ToDictionary(id => id, _ => 1), [5, 6, 7, 8, 9], id => id.ToString(), rules, options: new() { UseModifiedSamePlus = modified, Assistance = TripleTriadAssistance.Direct });
        controller.RestoreMatch(match);
        controller.SelectHand(0);
        controller.HoverCell(4);
        RecordingCanvas canvas = Draw(controller.BuildView());
        var ghost = Assert.Single(canvas.Images, image => image.Source.Opacity == 135);
        Assert.Equal(displayEffective ? 1 : 0, ghost.Source.RankModifier);
        Assert.All(canvas.Images.Where(image => image.Source.Kind == UiImageKind.CardFace && image.Source.Id is 1 or 5), image => Assert.Equal(displayEffective ? -1 : 0, image.Source.RankModifier));
        Assert.Contains(canvas.TextColors, text => text.Text == "+1" && text.Color == (displayEffective ? new UiColor(90, 235, 105) : new UiTheme().Foreground));
        Assert.Contains(canvas.TextColors, text => text.Text == "−1" && text.Color == (displayEffective ? new UiColor(245, 75, 65) : new UiTheme().Foreground));
        Assert.Equal(6, match.GetEffectiveRank(0, 4, 0));
        Assert.Equal(4, match.GetEffectiveRank(5, 1, 2));
    }

    [Fact]
    public void BlockedCellCannotBeHoveredOrClickedAndExplainsSameWall()
    {
        TripleTriadCard[] cards = Enumerable.Range(0, 110).Select(id => new TripleTriadCard(id, 10, 10, 10, 10, TripleTriadElement.None)).ToArray();
        TripleTriadRules rules = TripleTriadRules.Same | TripleTriadRules.SameWall;
        TripleTriadMatch match = TripleTriadMatch.Create(cards, [0, 1, 2, 3, 4], [5, 6, 7, 8, 9], rules, blockedCells: [3]);
        match.Play(1, 8);
        match.Play(0, 1);
        TripleTriadController controller = new(cards, Enumerable.Range(0, 5).ToDictionary(id => id, _ => 1), [5, 6, 7, 8, 9], id => id.ToString(), rules, options: new() { BlockedCellCount = 1, Assistance = TripleTriadAssistance.Rules });
        controller.RestoreMatch(match);
        controller.SelectHand(0);
        controller.HoverCell(4);
        TripleTriadElementView element = new(controller);
        element.PointerHover(300, 300);
        Assert.Equal(4, controller.SelectedCell);
        UiView view = controller.BuildView();
        RecordingCanvas canvas = Draw(view);
        Assert.Contains(canvas.Texts, text => text.Text.Contains("Wall A", StringComparison.Ordinal));
        Assert.Contains(canvas.TextColors, text => text.Text == "A" && text.Color.R == 255 && text.Color.G >= 202);
        string before = System.Text.Json.JsonSerializer.Serialize(match.CaptureSnapshot());
        Click(view, new TripleTriadLayout(3, 3, 5).Cell(3));
        Assert.Equal(before, System.Text.Json.JsonSerializer.Serialize(match.CaptureSnapshot()));
        controller.HoverCell(3);
        Assert.Equal(4, controller.SelectedCell);
    }

    [Fact]
    public void OriginalArtworkRendersStoneWallsAndSameWallExplanation()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Original resources required.");
        TripleTriadCatalog catalog = TripleTriadCatalog.Load(directory);
        TripleTriadCard source = catalog.Cards.First(card => card.Left == 10);
        TripleTriadCard top = catalog.Cards.First(card => card.Bottom == source.Top && card.Id != source.Id);
        int[] player = [source.Id, 1, 2, 3, 4];
        int[] opponent = [top.Id, 5, 6, 7, 8];
        TripleTriadRules rules = TripleTriadRules.Open | TripleTriadRules.Same | TripleTriadRules.SameWall;
        TripleTriadMatch match = TripleTriadMatch.Create(catalog.Cards, player, opponent, rules, blockedCells: [3, 7]);
        match.Play(1, 8);
        match.Play(0, 1);
        Assert.True(match.PreviewMove(0, 4).Same);
        TripleTriadController controller = new(catalog.Cards, Enumerable.Range(0, 110).ToDictionary(id => id, _ => 1), opponent, catalog.Name, rules, options: new() { BlockedCellCount = 2, Assistance = TripleTriadAssistance.Hand });
        controller.RestoreMatch(match);
        controller.SelectHand(0);
        controller.HoverCell(4);
        using Bitmap bitmap = new(1440, 1008, SkiaSharp.SKColorType.Rgba8888, SkiaSharp.SKAlphaType.Unpremul);
        using BattlePreviewCanvas canvas = new(bitmap, directory);
        UiView view = controller.BuildView();
        view.Resize(1440, 1008);
        view.Draw(canvas, new UiTheme());
        string output = Environment.GetEnvironmentVariable("MEMORIA_FF8_TRIAD_UI_OUTPUT_DIRECTORY") ?? Path.Combine(Path.GetTempPath(), "ff8-triad-extended");
        Directory.CreateDirectory(output);
        bitmap.SavePng(Path.Combine(output, "stone-same-wall.png"));
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
        public List<(string Text, UiColor Color)> TextColors { get; } = [];
        public List<UiRect> Pointers { get; } = [];
        public List<string> Operations { get; } = [];

        public void Fill(UiRect bounds, UiColor color)
        {
        }

        public void Text(string text, float fontSize, UiRect bounds, UiColor color)
        {
            Texts.Add((text, bounds));
            TextColors.Add((text, color));
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
