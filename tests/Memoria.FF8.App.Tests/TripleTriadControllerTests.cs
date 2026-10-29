using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class TripleTriadControllerTests
{
    private static readonly TripleTriadCard[] Cards = Enumerable.Range(0, 110).Select(id => new TripleTriadCard(id, 1 + id % 10, 1 + id % 9, 1 + id % 8, 1 + id % 7, TripleTriadElement.None)).ToArray();

    [Fact]
    public void BlockedCellsCannotBeHoveredPlayedOrSelectedByNavigation()
    {
        TripleTriadController controller = new(Cards, Enumerable.Range(0, 20).ToDictionary(id => id, _ => 1), [10, 11, 12, 13, 14], id => id.ToString(), options: new TripleTriadSettings { BlockedCellCount = 3 });
        controller.RestoreMatch(TripleTriadMatch.Create(Cards, [0, 1, 2, 3, 4], [10, 11, 12, 13, 14], blockedCells: [1, 4, 7]));
        controller.SelectHand(0);
        Assert.False(controller.Match!.IsBlocked(controller.SelectedCell));
        int selected = controller.SelectedCell;
        controller.HoverCell(4);
        Assert.Equal(selected, controller.SelectedCell);
        controller.Play(0, 4);
        Assert.Null(controller.Match.Board[4]);
        controller.HoverCell(3);
        controller.Navigate(UiKey.Right);
        Assert.Equal(5, controller.SelectedCell);
    }

    [Fact]
    public void ManualTradeRejectsCardsOutsideTheRewardPool()
    {
        TripleTriadController controller = Create();
        int[]? completed = null;
        controller.BeginTradeSelection([5, 6, 7, 8, 9], 1, slots => completed = slots, [false, false, false, false, true]);
        Assert.False(controller.IsTradeCardEligible(5));
        Assert.True(controller.IsTradeCardEligible(9));
        controller.SelectTradeCard(0);
        Assert.Empty(controller.TradeSelection);
        Assert.Null(completed);
        controller.SelectTradeCard(4);
        Assert.Equal(new[] { 4 }, completed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ComparisonSettingSurvivesControllerCreationRedrawAndSnapshotRestore(bool modified)
    {
        TripleTriadSettings options = new() { RandomRedraws = 1, UseModifiedSamePlus = modified };
        TripleTriadController controller = new(Cards, Enumerable.Range(0, 20).ToDictionary(id => id, _ => 1), [10, 11, 12, 13, 14], id => id.ToString(), TripleTriadRules.Random | TripleTriadRules.Same | TripleTriadRules.Plus, options: options);
        foreach (int id in Enumerable.Range(0, 5))
        {
            controller.ChooseCard(id);
        }
        Assert.Equal(modified, controller.Match!.UseModifiedSamePlus);
        controller.RedrawHand = () => new[] { 5, 6, 7, 8, 9 };
        Assert.True(controller.Redraw());
        Assert.Equal(modified, controller.Match.UseModifiedSamePlus);
        TripleTriadSnapshot snapshot = System.Text.Json.JsonSerializer.Deserialize<TripleTriadSnapshot>(System.Text.Json.JsonSerializer.Serialize(controller.Match.CaptureSnapshot()))!;
        controller.RestoreMatch(TripleTriadMatch.Restore(Cards, snapshot));
        Assert.Equal(modified, controller.Match.UseModifiedSamePlus);
    }

    [Theory]
    [InlineData(0, UiKey.Right, 6)]
    [InlineData(6, UiKey.Left, 0)]
    [InlineData(5, UiKey.Right, 7)]
    [InlineData(7, UiKey.Left, 1)]
    [InlineData(0, UiKey.Left, 6)]
    [InlineData(6, UiKey.Right, 0)]
    public void HorizontalHandNavigationMovesBetweenVisualColumns(int selected, UiKey key, int expected)
    {
        TripleTriadSettings options = new() { Width = 4, Height = 4, HandSize = 8 };
        TripleTriadController controller = new(Cards, Enumerable.Range(0, 8).ToDictionary(id => id, _ => 1), Enumerable.Range(20, 8).ToArray(), id => id.ToString(), options: options);
        foreach (int id in Enumerable.Range(0, 8))
        {
            controller.ChooseCard(id);
        }
        controller.SelectHand(selected);
        controller.Navigate(UiKey.Cancel);
        controller.Navigate(key);
        Assert.Equal(expected, controller.SelectedHand);
        Assert.False(controller.ChoosingCell);
        Assert.Empty(controller.Message);
    }

    [Fact]
    public void HorizontalHandNavigationSkipsPlayedCardsAndEmptyColumns()
    {
        TripleTriadSettings options = new() { Width = 4, Height = 4, HandSize = 8 };
        TripleTriadController controller = new(Cards, Enumerable.Range(0, 8).ToDictionary(id => id, _ => 1), Enumerable.Range(20, 8).ToArray(), id => id.ToString(), options: options);
        TripleTriadMatch match = TripleTriadMatch.Create(Cards, Enumerable.Range(0, 8).ToArray(), Enumerable.Range(20, 8).ToArray(), TripleTriadRules.Open, width: 4, height: 4);
        match.Play(6, 0);
        match.Play(0, 1);
        controller.RestoreMatch(match);
        controller.Navigate(UiKey.Right);
        Assert.Equal(7, controller.SelectedHand);
        match.Play(7, 2);
        match.Play(1, 3);
        controller.RestoreMatch(match);
        controller.Navigate(UiKey.Right);
        Assert.Equal(0, controller.SelectedHand);
        controller.Navigate(UiKey.Down);
        Assert.Equal(1, controller.SelectedHand);
    }

    [Fact]
    public void ExpandedHandsSelectNavigateTradeAndFilterHistoryBySize()
    {
        TripleTriadSettings options = new() { Width = 4, Height = 4, HandSize = 8 };
        TripleTriadController controller = new(Cards, Enumerable.Range(0, 30).ToDictionary(id => id, _ => 1), Enumerable.Range(20, 8).ToArray(), id => id.ToString(), options: options);
        controller.SetRecentDecks([new[] { 0, 1, 2, 3, 4 }, Enumerable.Range(0, 8).ToArray()]);
        Assert.Single(controller.RecentDecks);
        controller.ChooseDeck(0);
        Assert.Equal(8, controller.Match!.HandSize);
        Assert.Equal(16, controller.Match.Board.Count);
        controller.SelectHand(7);
        controller.HoverCell(15);
        controller.Navigate(UiKey.Up);
        Assert.Equal(11, controller.SelectedCell);
        controller.Play(7, 15);
        Assert.NotNull(controller.Match.Board[15]);
        controller.BeginTradeSelection(Enumerable.Range(20, 8).ToArray(), 1, slots => Assert.Equal(new[] { 7 }, slots));
        controller.SelectTradeCard(7);
        Assert.Null(controller.TradingHand);
    }

    [Fact]
    public void RandomRedrawIsLimitedToUntouchedOpeningAndWaitsForOpponentConfirmation()
    {
        TripleTriadController controller = new(Cards, Enumerable.Range(0, 20).ToDictionary(id => id, _ => 1), [10, 11, 12, 13, 14], id => id.ToString(), TripleTriadRules.Random, options: new TripleTriadSettings { RandomRedraws = 2 });
        controller.CreateMatch = hand => TripleTriadMatch.Create(Cards, hand, [10, 11, 12, 13, 14], TripleTriadRules.Random, startingPlayer: 1);
        controller.RedrawHand = () => new[] { 5, 6, 7, 8, 9 };
        foreach (int id in Enumerable.Range(0, 5))
        {
            controller.ChooseCard(id);
        }
        Assert.True(controller.AwaitingOpeningConfirmation);
        Assert.False(controller.AdvanceOpponent());
        TripleTriadMatch opening = controller.Match!;
        controller.CreateMatch = _ => throw new InvalidOperationException("Redraw must preserve the opening board and starter.");
        Assert.True(controller.Redraw());
        Assert.Equal(opening.BoardElements, controller.Match!.BoardElements);
        Assert.Equal(opening.StartingPlayer, controller.Match.StartingPlayer);
        Assert.Equal(opening.InitialHands[1], controller.Match.InitialHands[1]);
        Assert.Equal(1, controller.RedrawsRemaining);
        Assert.Equal(new[] { 5, 6, 7, 8, 9 }, controller.Deck);
        controller.Navigate(UiKey.Confirm);
        Assert.False(controller.AwaitingOpeningConfirmation);
        Assert.True(controller.AdvanceOpponent());
        Assert.False(controller.CanRedraw);
        Assert.False(controller.Redraw());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void InstantResultPublishesStateAndSoundThenUsesNormalConfirmation(bool victory)
    {
        TripleTriadController controller = Create();
        controller.RestoreMatch(TripleTriadMatch.Create(Cards, [0, 1, 2, 3, 4], [5, 6, 7, 8, 9]));
        controller.SelectHand(2);
        List<TripleTriadSound> sounds = [];
        controller.SoundRequested += sounds.Add;
        int changes = 0;
        TripleTriadSnapshot? captured = null;
        controller.Changed += () =>
        {
            changes++;
            captured = controller.Match!.CaptureSnapshot();
        };
        int completions = 0;
        controller.CompletionRequested += () => completions++;

        Assert.True(controller.CanCompleteInstantly);
        Assert.True(controller.CompleteInstantly(victory));
        Assert.False(controller.CanCompleteInstantly);
        Assert.False(controller.ChoosingCell);
        Assert.False(controller.IsAnimating);
        Assert.Equal(victory ? "You win!" : "You lose.", controller.Message);
        Assert.Equal(new[] { victory ? TripleTriadSound.Win : TripleTriadSound.Lose }, sounds);
        Assert.Equal(1, changes);
        Assert.NotNull(captured);
        Assert.Equal(victory ? TripleTriadOutcome.PlayerWin : TripleTriadOutcome.OpponentWin, TripleTriadMatch.Restore(Cards, captured).Outcome);
        Assert.Equal(0, completions);
        Assert.False(controller.CompleteInstantly(!victory));
        controller.Navigate(UiKey.Confirm);
        Assert.Equal(1, completions);
    }

    [Fact]
    public void InstantResultIsBlockedOutsideIdlePlayerTurns()
    {
        TripleTriadController controller = Create();
        Assert.False(controller.CanCompleteInstantly);
        Assert.False(controller.CompleteInstantly(true));
        TripleTriadMatch match = TripleTriadMatch.Create(Cards, [0, 1, 2, 3, 4], [5, 6, 7, 8, 9], startingPlayer: 1);
        controller.RestoreMatch(match);
        string unchanged = System.Text.Json.JsonSerializer.Serialize(controller.Match!.CaptureSnapshot());
        Assert.False(controller.CanCompleteInstantly);
        Assert.False(controller.CompleteInstantly(false));
        Assert.Equal(unchanged, System.Text.Json.JsonSerializer.Serialize(controller.Match.CaptureSnapshot()));

        controller.ChooseOpponentMove = state => state.GetLegalMoves().Last();
        Assert.True(controller.AdvanceOpponent());
        controller.EnablePresentation();
        controller.Play(0, 0);
        Assert.True(controller.IsAnimating);
        Assert.False(controller.CompleteInstantly(true));
        controller.AdvancePresentation(TimeSpan.FromSeconds(10));
        Assert.True(controller.AdvanceOpponent());
        Assert.False(controller.CompleteInstantly(true));
        controller.AdvancePresentation(TimeSpan.FromSeconds(10));
        Assert.True(controller.CanCompleteInstantly);
        controller.BeginRulesPrompt("One", () => { }, () => { });
        Assert.False(controller.CompleteInstantly(true));
        controller.CompleteRulesPrompt(true);
        controller.BeginTradeSelection([5, 6, 7, 8, 9], 1, _ => { });
        Assert.False(controller.CompleteInstantly(true));
        controller.RestoreMatch(match);
        controller.BeginReplayPrompt(() => { }, () => { });
        Assert.False(controller.CompleteInstantly(true));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void EveryPlayerTurnStartsAtCenterEvenWhenOccupied(int startingPlayer)
    {
        TripleTriadController controller = Create();
        controller.CreateMatch = hand => TripleTriadMatch.Create(Cards, hand, [5, 6, 7, 8, 9], startingPlayer: startingPlayer);
        controller.ChooseOpponentMove = match => new TripleTriadMove(Array.FindIndex(match.Hands[1].ToArray(), card => card is not null), match.Board[4] is null ? 4 : 8);
        foreach (int id in Enumerable.Range(0, 5))
        {
            controller.ChooseCard(id);
        }
        if (startingPlayer == 1)
        {
            Assert.True(controller.AdvanceOpponent());
        }
        controller.Navigate(UiKey.Confirm);
        Assert.True(controller.ChoosingCell);
        Assert.Equal(4, controller.SelectedCell);
        controller.Navigate(UiKey.Left);
        Assert.Equal(3, controller.SelectedCell);
        controller.Navigate(UiKey.Confirm);
        Assert.NotNull(controller.Match!.Board[3]);
        Assert.True(controller.AdvanceOpponent());
        controller.SelectHand(controller.SelectedHand);
        Assert.True(controller.ChoosingCell);
        Assert.Equal(4, controller.SelectedCell);
        Assert.NotNull(controller.Match.Board[4]);
        controller.Navigate(UiKey.Confirm);
        Assert.True(controller.ChoosingCell);
        Assert.Equal(0, controller.Match.CurrentPlayer);
    }

    [Fact]
    public void SelectionReservesInstancesAndCancelReturnsThemWithoutChangingCollection()
    {
        Dictionary<int, int> collection = new() { [0] = 2, [1] = 1, [2] = 1, [3] = 1 };
        TripleTriadController controller = new(Cards, collection, [4, 5, 6, 7, 8], id => id.ToString());
        controller.ChooseCard(0);
        controller.ChooseCard(0);
        controller.ChooseCard(0);
        Assert.Equal(2, controller.Deck.Count);
        Assert.Equal(0, controller.Remaining(0));
        controller.Navigate(UiKey.Cancel);
        Assert.Single(controller.Deck);
        Assert.Equal(1, controller.Remaining(0));
        Assert.Equal(2, collection[0]);
    }

    [Fact]
    public void NativeRulesPromptCannotSelectCardsUntilAcceptedAndCanCancel()
    {
        TripleTriadController controller = Create();
        int accepted = 0;
        int cancelled = 0;
        controller.BeginRulesPrompt("One", () => accepted++, () => cancelled++);
        controller.ChooseCard(0);
        Assert.Empty(controller.Deck);
        controller.Navigate(UiKey.Cancel);
        Assert.Equal(1, cancelled);
        controller.Navigate(UiKey.Confirm);
        Assert.Equal(1, accepted);
        Assert.False(controller.AwaitingRules);
        controller.ChooseCard(0);
        Assert.Single(controller.Deck);
    }

    [Fact]
    public void NativeOpponentCallbackOwnsMoveSelectionAndNeverFallsBackToPracticeAi()
    {
        TripleTriadController controller = Create();
        controller.CreateMatch = hand => TripleTriadMatch.Create(Cards, hand, [5, 6, 7, 8, 9], startingPlayer: 1);
        int calls = 0;
        controller.ChooseOpponentMove = _ =>
        {
            calls++;
            return new TripleTriadMove(3, 4);
        };
        foreach (int id in Enumerable.Range(0, 5))
        {
            controller.ChooseCard(id);
        }
        Assert.True(controller.AdvanceOpponent());
        Assert.Equal(1, calls);
        Assert.Equal(8, controller.Match!.Board[4]!.CardId);
        Assert.Equal(0, controller.Match.CurrentPlayer);
        Assert.False(controller.AdvanceOpponent());
    }

    [Fact]
    public void ReloadingDeckAndMatchDoesNotInvokeNewDealFactory()
    {
        TripleTriadController controller = Create();
        controller.CreateMatch = _ => throw new InvalidOperationException("A restored deal must not reroll.");
        controller.RestoreDeck([0, 1, 2, 3, 4]);
        TripleTriadMatch match = TripleTriadMatch.Create(Cards, [0, 1, 2, 3, 4], [5, 6, 7, 8, 9]);
        match.Play(2, 4);
        controller.RestoreMatch(match);
        Assert.Equal([0, 1, 2, 3, 4], controller.Deck);
        Assert.Equal(2, controller.Match!.Board[4]!.CardId);
        Assert.Equal("Opponent is thinking\u2026", controller.Message);
    }

    [Fact]
    public void TradeChoicesAreDistinctAndPublishOnlyAfterCompleteSelection()
    {
        TripleTriadController controller = Create();
        int[]? received = null;
        controller.BeginTradeSelection([10, 11, 12, 13, 14], 2, slots => received = slots);
        controller.SelectTradeCard(3);
        controller.SelectTradeCard(3);
        Assert.Null(received);
        controller.SelectTradeCard(1);
        Assert.Equal([3, 1], received!);
        Assert.Null(controller.TradingHand);
    }

    [Fact]
    public void OriginalCollectionProjectsPrintedRanksElementAndRecipeEvenWhenOutputIsNotOwned()
    {
        string gameDirectory = TestResources.GameDirectory;
        Assert.SkipUnless(File.Exists(Path.Combine(gameDirectory, "FF8_EN.exe")), "Original game resources required.");
        using GameAssets assets = new(gameDirectory);
        LiveMenuCatalog catalog = LiveMenuCatalog.ReadInstalled(assets);
        NativeCardInventory inventory = NativeCardInventory.CreateNewGame().Acquire(0, out _).Acquire(5, out _);
        MenuData menu = LiveMenuData.Build(new GameState { CardInventory = inventory }, catalog: catalog);
        MenuCard geezard = menu.Cards.Single(card => card.NativeId == 0);
        Assert.Equal("1 / 4 / 1 / 5", geezard.Ranks);
        Assert.Equal(1, geezard.Level);
        Assert.Equal(1, geezard.RefinementRequiredCards);
        Assert.Equal(5, geezard.RefinementQuantity);
        Assert.Equal("Screw", geezard.RefinementItemName);
        Assert.Equal("Thunder", menu.Cards.Single(card => card.NativeId == 5).Element);
        Assert.DoesNotContain("card-mod", menu.SupportedActions);
    }

    [Fact]
    public void RecentDeckReplacesSelectionCountsCopiesAndAllowsCancelThenCompletion()
    {
        Dictionary<int, int> collection = new() { [0] = 2, [1] = 1, [2] = 1, [3] = 1, [4] = 1 };
        TripleTriadController controller = new(Cards, collection, [5, 6, 7, 8, 9], id => id.ToString(), recentDecks: [[0, 0, 0, 1, 90]]);
        Assert.True(controller.IsRecentDeckCardAvailable(0, 0));
        Assert.True(controller.IsRecentDeckCardAvailable(0, 1));
        Assert.False(controller.IsRecentDeckCardAvailable(0, 2));
        Assert.False(controller.IsRecentDeckCardAvailable(0, 4));
        controller.ChooseCard(4);
        int[]? confirmed = null;
        controller.DeckConfirmed += hand => confirmed = hand;
        controller.ChooseDeck(0);
        Assert.Equal([0, 0, 1], controller.Deck);
        Assert.Null(controller.Match);
        Assert.Null(confirmed);
        Assert.Contains("2 more", controller.Message);
        controller.Navigate(UiKey.Cancel);
        Assert.Equal([0, 0], controller.Deck);
        Assert.Contains("3 more", controller.Message);
        controller.ChooseCard(1);
        controller.ChooseCard(2);
        controller.ChooseCard(3);
        Assert.NotNull(controller.Match);
        Assert.Equal([0, 0, 1, 2, 3], confirmed!);
        Assert.Equal(2, collection[0]);
    }

    [Fact]
    public void HistoryConfirmationStartsOnceAndRestoreDoesNotPublishDeck()
    {
        TripleTriadController controller = new(Cards, Enumerable.Range(0, 5).ToDictionary(id => id, _ => 1), [5, 6, 7, 8, 9], id => id.ToString(), recentDecks: [[4, 3, 2, 1, 0]]);
        List<int[]> confirmed = [];
        controller.DeckConfirmed += hand => confirmed.Add(hand);
        controller.Navigate(UiKey.Confirm);
        Assert.NotNull(controller.Match);
        Assert.Equal([4, 3, 2, 1, 0], Assert.Single(confirmed));
        controller.ChooseDeck(0);
        controller.ChooseCard(0);
        controller.RestoreMatch(controller.Match!);
        Assert.Single(confirmed);
        TripleTriadController restored = Create();
        restored.DeckConfirmed += hand => confirmed.Add(hand);
        restored.RestoreDeck([0, 1, 2, 3, 4]);
        restored.RestoreMatch(controller.Match!);
        Assert.Single(confirmed);
    }

    [Fact]
    public void FailedDealDoesNotPublishRecentDeck()
    {
        TripleTriadController controller = Create();
        controller.SetRecentDecks([[0, 1, 2, 3, 4]]);
        int confirmations = 0;
        controller.DeckConfirmed += _ => confirmations++;
        controller.CreateMatch = _ => throw new InvalidOperationException("Unsupported deal.");
        Assert.Throws<InvalidOperationException>(() => controller.ChooseDeck(0));
        Assert.Null(controller.Match);
        Assert.Equal(0, confirmations);
    }

    private static TripleTriadController Create() => new(Cards, Enumerable.Range(0, 5).ToDictionary(id => id, _ => 1), [5, 6, 7, 8, 9], id => id.ToString());

    [Fact]
    public void PracticeCannotOverwriteItsSourceStorySnapshot()
    {
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "standalone story snapshot sentinel");
            Assert.Throws<ArgumentException>(() => TripleTriadPractice.Run(["--state", path, "--save", path, "--autoplay"]));
            Assert.Equal("standalone story snapshot sentinel", File.ReadAllText(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void InvalidCardAndCellIndicesLeaveTheMatchUnchanged()
    {
        TripleTriadController controller = Create();
        foreach (int id in Enumerable.Range(0, 5))
        {
            controller.ChooseCard(id);
        }
        controller.SelectHand(-1);
        controller.SelectHand(5);
        controller.Play(0, -1);
        controller.Play(5, 0);
        controller.Play(0, 9);
        Assert.All(controller.Match!.Board, Assert.Null);
        Assert.Equal(5, controller.Match.PlayerScore);
    }
}
