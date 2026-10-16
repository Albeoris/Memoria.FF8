using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

public enum TripleTriadSound
{
    Move,
    Confirm,
    Cancel,
    Error,
    Place,
    Flip,
    Win,
    Lose,
    Draw,
    Same,
    Plus,
    Combo
}

/// <summary>Transient presentation never changes the saved match, AI, RNG or inventory.</summary>
public sealed partial class TripleTriadController
{
    // TODO(TD-016): Native callback proportions use a 60 Hz presentation clock until the original limiter is proved.
    public const double PlacementDuration = 0.20;
    public const double FlipDuration = 25d / 60;
    public const double FlipSettleDuration = 6d / 60;
    private bool presentationEnabled;
    private double presentationTime;
    private int soundedWaves;
    private bool announced;
    private bool comboAnnounced;
    private TripleTriadPlacedCard?[]? previousBoard;

    public event Action<TripleTriadSound>? SoundRequested;
    public TripleTriadMoveResult? PresentedMove { get; private set; }
    public bool IsAnimating => PresentedMove is not null;
    public double PlacementProgress => Math.Clamp(presentationTime / PlacementDuration, 0, 1);
    public string? CaptureAnnouncement
    {
        get
        {
            if (PresentedMove is not { } move || presentationTime < PlacementDuration)
            {
                return null;
            }
            if (move.Combo && presentationTime >= PlacementDuration + FlipDuration + FlipSettleDuration)
            {
                return "COMBO";
            }
            return move.Plus ? "PLUS" : move.Same ? "SAME" : null;
        }
    }

    public void EnablePresentation()
    {
        presentationEnabled = true;
    }

    public void Navigate(UiKey key)
    {
        if (IsAnimating)
        {
            return;
        }
        int oldSelection = Selection;
        int oldHand = SelectedHand;
        int oldCell = SelectedCell;
        bool oldChoosing = ChoosingCell;
        bool oldRules = AwaitingRules;
        int oldDeck = deck.Count;
        NavigateCore(key);
        if (key is UiKey.Up or UiKey.Down or UiKey.Left or UiKey.Right or UiKey.PageUp or UiKey.PageDown)
        {
            if (oldSelection != Selection || oldHand != SelectedHand || oldCell != SelectedCell)
            {
                RequestSound(TripleTriadSound.Move);
            }
        }
        else if (key == UiKey.Cancel && (oldChoosing || oldRules || oldDeck != deck.Count))
        {
            RequestSound(TripleTriadSound.Cancel);
        }
        else if (key == UiKey.Confirm && (oldRules || !oldChoosing && ChoosingCell))
        {
            RequestSound(TripleTriadSound.Confirm);
        }
    }

    public void AdvancePresentation(TimeSpan elapsed)
    {
        if (elapsed < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(elapsed));
        }
        if (PresentedMove is not { } move)
        {
            return;
        }
        presentationTime += elapsed.TotalSeconds;
        if (presentationTime >= PlacementDuration && !announced)
        {
            announced = true;
            if (move.Same)
            {
                RequestSound(TripleTriadSound.Same);
            }
            if (move.Plus)
            {
                RequestSound(TripleTriadSound.Plus);
            }
        }
        if (move.Combo && !comboAnnounced && presentationTime >= PlacementDuration + FlipDuration + FlipSettleDuration)
        {
            comboAnnounced = true;
            RequestSound(TripleTriadSound.Combo);
        }
        int waves = move.Captures.Count == 0 ? 0 : move.Captures.Max(capture => capture.Wave) + 1;
        while (soundedWaves < waves && presentationTime >= PlacementDuration + soundedWaves * (FlipDuration + FlipSettleDuration))
        {
            RequestSound(TripleTriadSound.Flip);
            soundedWaves++;
        }
        double duration = PlacementDuration + (move.CapturedCells.Count == 0 ? 0 : (move.Captures.Max(capture => capture.Wave) + 1) * (FlipDuration + FlipSettleDuration));
        if (presentationTime >= duration)
        {
            ClearPresentation();
            if (move.Outcome != TripleTriadOutcome.InProgress)
            {
                RequestSound(move.Outcome == TripleTriadOutcome.PlayerWin ? TripleTriadSound.Win : move.Outcome == TripleTriadOutcome.OpponentWin ? TripleTriadSound.Lose : TripleTriadSound.Draw);
            }
        }
    }

    public (int Owner, float Width, float Height) PresentedCard(int cell, int finalOwner)
    {
        TripleTriadCapture? capture = PresentedMove?.Captures.FirstOrDefault(capture => capture.CellIndex == cell);
        if (capture is null || previousBoard is null)
        {
            return (finalOwner, 1, 1);
        }
        double progress = Math.Clamp((presentationTime - PlacementDuration - capture.Wave * (FlipDuration + FlipSettleDuration)) / FlipDuration, 0, 1);
        float squeeze = (float)Math.Max(0.015, Math.Abs(Math.Cos(progress * Math.PI)));
        bool vertical = Math.Abs(cell - capture.SourceCellIndex) == 3;
        return (progress < 0.5 ? previousBoard[cell]!.Owner : finalOwner, vertical ? 1 : squeeze, vertical ? squeeze : 1);
    }

    public int PresentedScore(int player)
    {
        int score = player == 0 ? Match!.PlayerScore : Match!.OpponentScore;
        if (PresentedMove is { } move && previousBoard is not null)
        {
            foreach (TripleTriadCapture capture in move.Captures)
            {
                int finalOwner = Match!.Board[capture.CellIndex]!.Owner;
                int shownOwner = PresentedCard(capture.CellIndex, finalOwner).Owner;
                if (shownOwner != finalOwner)
                {
                    score += shownOwner == player ? 1 : -1;
                }
            }
        }
        return score;
    }

    private void PresentMove(TripleTriadMoveResult move, TripleTriadPlacedCard?[] before)
    {
        if (!presentationEnabled)
        {
            return;
        }
        PresentedMove = move;
        previousBoard = before;
        presentationTime = 0;
        soundedWaves = 0;
        announced = false;
        comboAnnounced = false;
        RequestSound(TripleTriadSound.Place);
    }

    private void ClearPresentation()
    {
        PresentedMove = null;
        previousBoard = null;
        presentationTime = 0;
        soundedWaves = 0;
        announced = false;
        comboAnnounced = false;
    }

    private void RequestSound(TripleTriadSound sound)
    {
        SoundRequested?.Invoke(sound);
    }
}
