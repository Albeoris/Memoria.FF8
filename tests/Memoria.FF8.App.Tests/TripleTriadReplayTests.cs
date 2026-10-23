using System.Text.Json;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class TripleTriadReplayTests
{
    private static readonly int[] PlayerHand = [0, 1, 2, 3, 4];
    private static readonly int[] OpponentHand = [5, 6, 7, 8, 9];
    private static readonly TripleTriadCard[] Cards = CreateCards(1, 1);

    [Theory]
    [InlineData(UiKey.Up)]
    [InlineData(UiKey.Down)]
    public void KeyboardChoiceWrapsAndConfirmResolvesTheSelectedCallbackOnce(UiKey direction)
    {
        TripleTriadController controller = Create();
        int accepted = 0;
        int declined = 0;
        controller.BeginReplayPrompt(() => accepted++, () => declined++);
        Assert.True(controller.AwaitingReplay);
        Assert.Equal(0, controller.ReplaySelection);
        controller.Navigate(direction);
        Assert.Equal(1, controller.ReplaySelection);
        controller.Navigate(direction);
        Assert.Equal(0, controller.ReplaySelection);
        controller.Navigate(UiKey.Confirm);
        Assert.False(controller.AwaitingReplay);
        controller.Navigate(UiKey.Confirm);
        controller.Navigate(UiKey.Cancel);
        Assert.Equal(1, accepted);
        Assert.Equal(0, declined);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DecliningWithConfirmOrCancelInvokesOnlyTheDeclinedCallback(bool cancel)
    {
        TripleTriadController controller = Create();
        int accepted = 0;
        int declined = 0;
        controller.BeginReplayPrompt(() => accepted++, () => declined++);
        if (!cancel)
        {
            controller.SelectReplayChoice(1);
            Assert.Equal(1, controller.ReplaySelection);
            Assert.True(controller.AwaitingReplay);
        }
        controller.Navigate(cancel ? UiKey.Cancel : UiKey.Confirm);
        Assert.False(controller.AwaitingReplay);
        controller.Navigate(UiKey.Cancel);
        controller.Navigate(UiKey.Confirm);
        Assert.Equal(0, accepted);
        Assert.Equal(1, declined);
    }

    [Fact]
    public void ReplayPromptBlocksCollectionAndRecentDeckSelection()
    {
        TripleTriadController controller = Create();
        controller.SetRecentDecks([PlayerHand]);
        controller.ChooseCard(0);
        controller.BeginReplayPrompt(() =>
        {
        }, () =>
        {
        });
        controller.ChooseCard(1);
        controller.ChooseDeck(0);
        Assert.Equal(new[] { 0 }, controller.Deck);
        Assert.Null(controller.Match);
        Assert.True(controller.AwaitingReplay);
    }

    [Fact]
    public void ReplayPromptBlocksHandSelectionAndBoardPlacement()
    {
        TripleTriadController controller = Create();
        foreach (int id in PlayerHand)
        {
            controller.ChooseCard(id);
        }
        string before = JsonSerializer.Serialize(controller.Match!.CaptureSnapshot());
        int selected = controller.SelectedHand;
        controller.BeginReplayPrompt(() =>
        {
        }, () =>
        {
        });
        controller.SelectHand(3);
        controller.Play(0, 4);
        Assert.Equal(selected, controller.SelectedHand);
        Assert.False(controller.ChoosingCell);
        Assert.Equal(before, JsonSerializer.Serialize(controller.Match.CaptureSnapshot()));
    }

    [Theory]
    [InlineData(9, 1, TripleTriadOutcome.PlayerWin)]
    [InlineData(1, 9, TripleTriadOutcome.OpponentWin)]
    [InlineData(1, 1, TripleTriadOutcome.Draw)]
    public void PracticeCanReplayAfterEveryOutcomeWithoutAnotherRulesPrompt(int playerRank, int opponentRank, TripleTriadOutcome outcome)
    {
        TripleTriadCard[] cards = CreateCards(playerRank, opponentRank);
        TripleTriadController controller = Create(cards);
        controller.SetRecentDecks([PlayerHand]);
        controller.EnablePresentation();
        controller.ChooseOpponentMove = match => new TripleTriadMove(FirstHandSlot(match), FirstEmptyCell(match));
        controller.ChooseDeck(0);
        for (int turn = 0; turn < 9; turn++)
        {
            TripleTriadMatch match = controller.Match!;
            if (match.CurrentPlayer == 0)
            {
                controller.Play(FirstHandSlot(match), FirstEmptyCell(match));
            }
            else
            {
                Assert.True(controller.AdvanceOpponent());
            }
            controller.AdvancePresentation(TimeSpan.FromSeconds(10));
        }
        Assert.Equal(outcome, controller.Match!.Outcome);
        controller.Navigate(UiKey.Confirm);
        Assert.True(controller.AwaitingReplay);
        Assert.NotNull(controller.Match);
        Assert.Equal(PlayerHand, controller.Deck);
        controller.Navigate(UiKey.Confirm);
        Assert.False(controller.AwaitingReplay);
        Assert.False(controller.AwaitingRules);
        Assert.Null(controller.Match);
        Assert.Empty(controller.Deck);
        Assert.False(controller.ChoosingCell);
        Assert.False(controller.IsAnimating);
        Assert.Null(controller.PresentedMove);
        Assert.Null(controller.TradingHand);
        Assert.Equal(TripleTriadTradePhase.None, controller.TradePhase);
        Assert.Equal(PlayerHand, Assert.Single(controller.RecentDecks));
        controller.ChooseDeck(0);
        Assert.NotNull(controller.Match);
        Assert.Equal(TripleTriadOutcome.InProgress, controller.Match.Outcome);
        Assert.All(controller.Match.Board, Assert.Null);
        Assert.Equal(PlayerHand, controller.Match.Hands[0].Select(id => id!.Value));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void MouseCanAcceptOrDeclineReplay(int choice)
    {
        TripleTriadController controller = Create();
        int accepted = 0;
        int declined = 0;
        controller.BeginReplayPrompt(() => accepted++, () => declined++);
        UiView view = controller.BuildView();
        view.Resize(960, 672);
        view.Pointer(420, 330 + choice * 42, true);
        Assert.False(controller.AwaitingReplay);
        Assert.Equal(choice == 0 ? 1 : 0, accepted);
        Assert.Equal(choice == 1 ? 1 : 0, declined);
    }

    [Fact]
    public void ReplaySelectionStateRoundTripsWithoutRequiringMigrationOfExistingStates()
    {
        NativeTripleTriadPlayState play = new()
        {
            Version = 1,
            RequestIdentity = "test",
            InitialRandomState = 1,
            RandomState = 2,
            DealRandomState = 1,
            OpponentHand = OpponentHand,
            SelectedCards = [],
            RulesAccepted = true,
            IsReplay = true,
            Prompt = new TripleTriadPromptState(TripleTriadPromptPhase.Accepted, 0, 1),
            Match = null
        };
        string json = JsonSerializer.Serialize(play);
        NativeTripleTriadPlayState restored = JsonSerializer.Deserialize<NativeTripleTriadPlayState>(json)!;
        restored.Validate();
        Assert.True(restored.IsReplay);
        Assert.Empty(restored.SelectedCards);
        Assert.Null(restored.Match);
        string oldJson = json.Replace("\"IsReplay\":true,", "", StringComparison.Ordinal);
        NativeTripleTriadPlayState existing = JsonSerializer.Deserialize<NativeTripleTriadPlayState>(oldJson)!;
        existing.Validate();
        Assert.False(existing.IsReplay);
    }

    [Fact]
    public void InsufficientCollectionDisablesReplayAfterCardLoss()
    {
        TripleTriadController controller = Create();
        int declined = 0;
        controller.BeginReplayPrompt(() => throw new InvalidOperationException("Unavailable replay was accepted."), () => declined++, false);
        Assert.False(controller.CanReplay);
        Assert.Equal(1, controller.ReplaySelection);
        controller.Navigate(UiKey.Up);
        controller.SelectReplayChoice(0);
        Assert.Equal(1, controller.ReplaySelection);
        UiView view = controller.BuildView();
        view.Resize(960, 672);
        view.Pointer(420, 330, true);
        Assert.True(controller.AwaitingReplay);
        controller.Navigate(UiKey.Confirm);
        Assert.Equal(1, declined);
    }

    private static int FirstHandSlot(TripleTriadMatch match) => Enumerable.Range(0, 5).First(index => match.Hands[match.CurrentPlayer][index] is not null);

    private static int FirstEmptyCell(TripleTriadMatch match) => Enumerable.Range(0, 9).First(index => match.Board[index] is null);

    private static TripleTriadController Create(TripleTriadCard[]? cards = null) => new(cards ?? Cards, PlayerHand.ToDictionary(id => id, _ => 1), OpponentHand, id => id.ToString());

    private static TripleTriadCard[] CreateCards(int playerRank, int opponentRank) => Enumerable.Range(0, 110).Select(id =>
    {
        int rank = id < 5 ? playerRank : opponentRank;
        return new TripleTriadCard(id, rank, rank, rank, rank, TripleTriadElement.None);
    }).ToArray();
}
