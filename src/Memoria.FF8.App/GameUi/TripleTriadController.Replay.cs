using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

public sealed partial class TripleTriadController
{
    private Action? replayAccepted;
    private Action? replayDeclined;
    private bool replayResolved;

    public event Action? ReplayDeclined;

    public bool AwaitingReplay { get; private set; }
    public bool CanReplay { get; private set; } = true;
    public int ReplaySelection { get; private set; }

    public void BeginReplayPrompt(Action accepted, Action declined, bool canReplay = true)
    {
        ArgumentNullException.ThrowIfNull(accepted);
        ArgumentNullException.ThrowIfNull(declined);
        replayAccepted = accepted;
        replayDeclined = declined;
        replayResolved = false;
        CanReplay = canReplay;
        ReplaySelection = canReplay ? 0 : 1;
        AwaitingReplay = true;
        Message = canReplay ? "Play again?" : "Not enough cards to play again.";
    }

    public void SelectReplayChoice(int choice)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(choice);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(choice, 1);
        if (AwaitingReplay && (choice != 0 || CanReplay) && ReplaySelection != choice)
        {
            ReplaySelection = choice;
            RequestSound(TripleTriadSound.Move);
        }
    }

    private void NavigateReplay(UiKey key)
    {
        if (key is UiKey.Up or UiKey.Down or UiKey.Left or UiKey.Right)
        {
            SelectReplayChoice(1 - ReplaySelection);
        }
        else if (key is UiKey.Confirm or UiKey.Cancel)
        {
            bool accepted = key == UiKey.Confirm && ReplaySelection == 0;
            Action callback = (accepted ? replayAccepted : replayDeclined)!;
            ClearReplayPrompt();
            // The host may replace this controller while its view still has queued input.
            replayResolved = true;
            RequestSound(accepted ? TripleTriadSound.Confirm : TripleTriadSound.Cancel);
            callback();
        }
    }

    private void ClearReplayPrompt()
    {
        AwaitingReplay = false;
        replayAccepted = null;
        replayDeclined = null;
    }

    private void ResetForReplay()
    {
        replayResolved = false;
        ClearPresentation();
        ClearTradePresentation();
        Match = null;
        deck.Clear();
        tradeSelection.Clear();
        TradingHand = null;
        tradeCompleted = null;
        ChoosingCell = false;
        completingRecentDeck = false;
        Selection = 0;
        SelectedHand = 0;
        SelectedCell = 4;
        RecentDeckSelection = recentDecks.Length > 0 ? 0 : -1;
        Message = "Choose five cards.";
        Changed?.Invoke();
    }
}
