using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class TripleTriadControllerTests
{
    private static readonly TripleTriadCard[] Cards = Enumerable.Range(0, 110).Select(id => new TripleTriadCard(id, 1 + id % 10, 1 + id % 9, 1 + id % 8, 1 + id % 7, TripleTriadElement.None)).ToArray();

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
        string gameDirectory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
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
