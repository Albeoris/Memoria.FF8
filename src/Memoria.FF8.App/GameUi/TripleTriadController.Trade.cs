using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

public enum TripleTriadTradePhase
{
    None,
    Entering,
    Selecting,
    Flipping,
    ConfirmingSelection,
    Moving,
    AwaitingConfirmation,
    Exiting
}

/// <summary>The original card exchange is transient; inventory is committed only after its last acknowledgement.</summary>
public sealed partial class TripleTriadController
{
    // TODO(TD-016): Convert native callback counts with the shared presentation clock until its limiter is proved.
    public const double TradeEntranceDuration = 30d / 60;
    public const double TradeFlipDuration = 20d / 60;
    public const double TradeMoveDuration = 30d / 60;
    public const double TradeExitDuration = 15d / 60;
    private int[] tradeCards = [];
    private int[] tradeOwners = [];
    private bool[] tradeEligibility = [];
    private readonly HashSet<int> removedTradeCards = [];
    private readonly Queue<int> automaticTradeChoices = [];
    private readonly Queue<int> directTradeChoices = [];
    private int[] desiredTradeOwners = [];
    private readonly Queue<int> tradeTransfers = [];
    private bool manualTrade;
    private double tradeTime;
    private int flippingTradeSlot = -1;
    private Dictionary<int, int> cardModRequirements = [];

    public void ConfigureCardModHints(IReadOnlyList<TripleTriadRefinement> recipes, bool available, bool once, IReadOnlyCollection<int> refinedCardIds)
    {
        cardModRequirements = available ? recipes.Where(recipe => !once || !refinedCardIds.Contains(recipe.CardId)).ToDictionary(recipe => recipe.CardId, recipe => recipe.RequiredCards) : [];
    }

    public string TradeCardName(int id)
    {
        string name = Name(id);
        int owned = Math.Max(0, CollectionCount(id));
        return cardModRequirements.TryGetValue(id, out int required) && owned < required ? $"{name} ({owned}/{required})" : name;
    }

    public TripleTriadTradePhase TradePhase { get; private set; }
    public IReadOnlyList<int> TradeCards => tradeCards;
    public int TradingCardIndex { get; private set; } = -1;
    public bool TradingToPlayer { get; private set; }
    public bool AwaitingTradeConfirmation => TradePhase == TripleTriadTradePhase.AwaitingConfirmation;
    public bool IsChoosingTradeCards => TradePhase is TripleTriadTradePhase.Selecting or TripleTriadTradePhase.ConfirmingSelection || manualTrade && TradePhase == TripleTriadTradePhase.Flipping;
    public int TradeConfirmationSelection { get; private set; }
    public bool IsTradeAnimating => TradePhase is TripleTriadTradePhase.Entering or TripleTriadTradePhase.Flipping or TripleTriadTradePhase.Moving or TripleTriadTradePhase.Exiting;
    public double TradeProgress => Math.Clamp(tradeTime / (TradePhase switch
    {
        TripleTriadTradePhase.Entering => TradeEntranceDuration,
        TripleTriadTradePhase.Flipping => TradeFlipDuration,
        TripleTriadTradePhase.Moving => TradeMoveDuration,
        TripleTriadTradePhase.Exiting => TradeExitDuration,
        _ => 1
    }), 0, 1);

    public bool HasAcquiredCard(int id) => collection.TryGetValue(id, out int count) && count >= 0;
    public int CollectionCount(int id) => collection.GetValueOrDefault(id, -1);
    public bool IsTradeCardEligible(int index) => (uint)index < tradeEligibility.Length && tradeEligibility[index];
    private bool IsLosingTradeSlotEligible(int slot) => IsTradeCardEligible((TradingToPlayer ? Options.HandSize : 0) + slot);
    public bool IsTradeCardRemoved(int index) => removedTradeCards.Contains(index);

    public int PresentedTradeOwner(int index)
    {
        if (TradePhase == TripleTriadTradePhase.Flipping && index == flippingTradeSlot && TradeProgress < 0.5)
        {
            return 1 - tradeOwners[index];
        }
        return tradeOwners[index];
    }

    public float PresentedTradeWidth(int index) => TradePhase == TripleTriadTradePhase.Flipping && index == flippingTradeSlot ? (float)Math.Max(0.015, Math.Abs(Math.Cos(TradeProgress * Math.PI * 2))) : 1;
    public bool PresentedTradeBack(int index) => TradePhase == TripleTriadTradePhase.Flipping && index == flippingTradeSlot && TradeProgress is > 0.25 and < 0.75;

    public void BeginTradeSettlement(IReadOnlyList<int> playerHand, IReadOnlyList<int> opponentHand, IReadOnlyList<int> finalOwners, TripleTriadTradeRule rule, int required, bool chooseCards, IReadOnlyList<int> selectedSlots, Action<int[]> completed, IReadOnlyList<bool>? rewardParticipants = null)
    {
        ArgumentNullException.ThrowIfNull(completed);
        if (playerHand.Count != Options.HandSize || opponentHand.Count != Options.HandSize || finalOwners.Count != 2 * Options.HandSize || finalOwners.Any(owner => owner is not (0 or 1)))
        {
            throw new ArgumentException("Card exchange requires all original card instances and their final owners.");
        }
        bool[] eligible = rewardParticipants?.ToArray() ?? Enumerable.Repeat(true, playerHand.Count * 2).ToArray();
        if (eligible.Length != playerHand.Count * 2)
        {
            throw new ArgumentException("Card exchange eligibility requires every original instance.");
        }
        if (!presentationEnabled)
        {
            if (chooseCards)
            {
                BeginTradeSelection(opponentHand, required, completed, eligible.Skip(Options.HandSize).ToArray());
                tradeEligibility = eligible;
            }
            else
            {
                completed(selectedSlots.ToArray());
            }
            return;
        }
        ClearTradePresentation();
        TradingToPlayer = Match?.Outcome != TripleTriadOutcome.OpponentWin;
        TradingHand = (TradingToPlayer ? opponentHand : playerHand).ToArray();
        RequiredTradeCards = required;
        tradeCompleted = completed;
        tradeSelection.Clear();
        tradeCards = playerHand.Concat(opponentHand).ToArray();
        tradeEligibility = eligible;
        tradeOwners = Enumerable.Range(0, 2 * Options.HandSize).Select(index => index < Options.HandSize ? 0 : 1).ToArray();
        manualTrade = chooseCards;
        Selection = Enumerable.Range(0, Options.HandSize).FirstOrDefault(IsLosingTradeSlotEligible);
        if (rule == TripleTriadTradeRule.Direct)
        {
            Dictionary<int, int> retained = Enumerable.Range(0, tradeCards.Length).Where(index => eligible[index] && finalOwners[index] == 0).Select(index => tradeCards[index]).GroupBy(id => id).ToDictionary(group => group.Key, group => group.Count());
            desiredTradeOwners = tradeOwners.ToArray();
            for (int index = 0; index < tradeCards.Length; index++)
            {
                if (!eligible[index])
                {
                    continue;
                }
                int id = tradeCards[index];
                bool keep = retained.GetValueOrDefault(id) > 0;
                desiredTradeOwners[index] = keep ? 0 : 1;
                if (keep)
                {
                    retained[id]--;
                }
            }
            foreach (int index in Enumerable.Range(Options.HandSize, Options.HandSize).Concat(Enumerable.Range(0, Options.HandSize)))
            {
                if (tradeOwners[index] != desiredTradeOwners[index])
                {
                    directTradeChoices.Enqueue(index);
                }
            }
        }
        else
        {
            foreach (int slot in selectedSlots)
            {
                automaticTradeChoices.Enqueue(slot);
            }
        }
        SetTradePhase(TripleTriadTradePhase.Entering);
        SetSystemMessage(() => chooseCards ? FormatSystemText("card.ui.begintradesettlement.choose.card.s.to.take", "Choose {0} card(s) to take.", required) : SystemText("card.ui.begintradesettlement.exchanging.cards", "Exchanging cards."));
    }

    private void StartTradeChoice(int slot, bool human = false)
    {
        if (!IsLosingTradeSlotEligible(slot))
        {
            RequestSound(TripleTriadSound.Error);
            return;
        }
        bool deselect = tradeSelection.Contains(slot);
        if (!deselect && tradeSelection.Count == RequiredTradeCards)
        {
            RequestSound(TripleTriadSound.Error);
            return;
        }
        if (deselect)
        {
            tradeSelection.Remove(slot);
        }
        else
        {
            tradeSelection.Add(slot);
        }
        if (human)
        {
            RequestSound(deselect ? TripleTriadSound.Cancel : TripleTriadSound.Confirm);
        }
        flippingTradeSlot = (TradingToPlayer ? Options.HandSize : 0) + slot;
        tradeOwners[flippingTradeSlot] = deselect ? TradingToPlayer ? 1 : 0 : TradingToPlayer ? 0 : 1;
        RequestSound(TripleTriadSound.Flip);
        SetTradePhase(TripleTriadTradePhase.Flipping);
    }

    private void AdvanceTradePresentation(TimeSpan elapsed)
    {
        if (!IsTradeAnimating)
        {
            return;
        }
        tradeTime += elapsed.TotalSeconds;
        if (TradeProgress < 1)
        {
            return;
        }
        switch (TradePhase)
        {
            case TripleTriadTradePhase.Entering:
            case TripleTriadTradePhase.Flipping:
                if (directTradeChoices.TryDequeue(out int directIndex))
                {
                    flippingTradeSlot = directIndex;
                    tradeOwners[directIndex] = desiredTradeOwners[directIndex];
                    RequestSound(TripleTriadSound.Flip);
                    SetTradePhase(TripleTriadTradePhase.Flipping);
                }
                else if (automaticTradeChoices.TryDequeue(out int slot))
                {
                    StartTradeChoice(slot);
                }
                else if (manualTrade && tradeSelection.Count < RequiredTradeCards)
                {
                    SetTradePhase(TripleTriadTradePhase.Selecting);
                }
                else if (manualTrade)
                {
                    TradeConfirmationSelection = 0;
                    SetTradePhase(TripleTriadTradePhase.ConfirmingSelection);
                }
                else
                {
                    PrepareTradeTransfers();
                }
                break;
            case TripleTriadTradePhase.Moving:
                SetTradePhase(TripleTriadTradePhase.AwaitingConfirmation);
                Message = $"{Name(tradeCards[TradingCardIndex])} — {(tradeOwners[TradingCardIndex] == 0 ? "Card acquired" : "Card lost")}";
                break;
            case TripleTriadTradePhase.Exiting:
                removedTradeCards.Add(TradingCardIndex);
                StartNextTradeTransfer();
                break;
        }
    }

    private void NavigateTradeConfirmation(UiKey key)
    {
        if (key is UiKey.Up or UiKey.Down)
        {
            TradeConfirmationSelection = 1 - TradeConfirmationSelection;
            RequestSound(TripleTriadSound.Move);
        }
        else if (key == UiKey.Cancel || key == UiKey.Confirm && TradeConfirmationSelection == 1)
        {
            SetTradePhase(TripleTriadTradePhase.Selecting);
            RequestSound(TripleTriadSound.Cancel);
        }
        else if (key == UiKey.Confirm)
        {
            RequestSound(TripleTriadSound.Confirm);
            PrepareTradeTransfers();
        }
    }

    private void PrepareTradeTransfers()
    {
        for (int index = Options.HandSize; index < 2 * Options.HandSize; index++)
        {
            if (tradeOwners[index] == 0)
            {
                tradeTransfers.Enqueue(index);
            }
        }
        for (int index = 0; index < Options.HandSize; index++)
        {
            if (tradeOwners[index] == 1)
            {
                tradeTransfers.Enqueue(index);
            }
        }
        StartNextTradeTransfer();
    }

    private void StartNextTradeTransfer()
    {
        if (tradeTransfers.TryDequeue(out int index))
        {
            TradingCardIndex = index;
            RequestSound(TripleTriadSound.CardReceived);
            SetSystemMessage(() => SystemText("card.ui.startnexttradetransfer.exchanging.cards", "Exchanging cards."));
            SetTradePhase(TripleTriadTradePhase.Moving);
        }
        else
        {
            FinishTrade();
        }
    }

    private void ConfirmTradeTransfer()
    {
        RequestSound(tradeOwners[TradingCardIndex] == 0 && !HasAcquiredCard(tradeCards[TradingCardIndex]) ? TripleTriadSound.NewCard : TripleTriadSound.CardReceived);
        SetTradePhase(TripleTriadTradePhase.Exiting);
    }

    private void SetTradePhase(TripleTriadTradePhase phase)
    {
        TradePhase = phase;
        tradeTime = 0;
    }

    private void FinishTrade()
    {
        Action<int[]> completed = tradeCompleted!;
        int[] selected = tradeSelection.ToArray();
        TradingHand = null;
        tradeCompleted = null;
        ClearTradePresentation();
        completed(selected);
    }

    private void ClearTradePresentation()
    {
        SetTradePhase(TripleTriadTradePhase.None);
        tradeCards = [];
        tradeOwners = [];
        tradeEligibility = [];
        removedTradeCards.Clear();
        automaticTradeChoices.Clear();
        directTradeChoices.Clear();
        desiredTradeOwners = [];
        tradeTransfers.Clear();
        TradingCardIndex = -1;
        flippingTradeSlot = -1;
        manualTrade = false;
    }
}
