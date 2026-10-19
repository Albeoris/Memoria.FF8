using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

/// <summary>Deck selection and human input for the single Triple Triad match implementation.</summary>
public sealed partial class TripleTriadController
{
    public const int CardsPerPage = 9;
    private readonly IReadOnlyList<TripleTriadCard> definitions;
    private readonly IReadOnlyDictionary<int, int> collection;
    private readonly int[] opponent;
    private readonly List<int> deck = [];
    private readonly int[] available;
    private int[][] recentDecks = [];
    private bool completingRecentDeck;
    private UiView? view;
    private Action<int[]>? tradeCompleted;
    private readonly List<int> tradeSelection = [];
    private Action? acceptRules;
    private Action? cancelRules;

    public TripleTriadController(IReadOnlyList<TripleTriadCard> definitions, IReadOnlyDictionary<int, int> collection, IReadOnlyList<int> opponent, Func<int, string> name, TripleTriadRules rules = TripleTriadRules.Open, IReadOnlyList<int[]>? recentDecks = null)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        ArgumentNullException.ThrowIfNull(collection);
        ArgumentNullException.ThrowIfNull(opponent);
        ArgumentNullException.ThrowIfNull(name);
        this.definitions = definitions;
        this.collection = collection.ToDictionary();
        this.opponent = opponent.ToArray();
        Name = name;
        Rules = rules;
        available = collection.Where(pair => pair.Value > 0).Select(pair => pair.Key).OrderDescending().ToArray();
        if (available.Sum(id => collection[id]) < 5 || available.Any(id => !definitions.Any(card => card.Id == id)) || this.opponent.Length is not (0 or 5))
        {
            throw new ArgumentException("Triple Triad requires five available player cards and a complete opponent hand.");
        }
        SetRecentDecks(recentDecks ?? []);
        // Validate the supplied opponent without consuming any collection or random state.
        if (this.opponent.Length == 5)
        {
            _ = TripleTriadMatch.Create(definitions, available.SelectMany(id => Enumerable.Repeat(id, collection[id])).Take(5).ToArray(), this.opponent, rules);
        }
    }

    public Func<int, string> Name { get; }
    public IReadOnlyList<int> Available => available;
    public IReadOnlyList<int> Deck => deck.AsReadOnly();
    public TripleTriadRules Rules { get; }
    public TripleTriadMatch? Match { get; private set; }
    public int Selection { get; private set; }
    public IReadOnlyList<int[]> RecentDecks => recentDecks;
    public int RecentDeckSelection { get; private set; } = -1;
    private int FirstPageCardCount => CardsPerPage - recentDecks.Length;
    public int SelectionPage => Selection < FirstPageCardCount ? 0 : 1 + (Selection - FirstPageCardCount) / CardsPerPage;
    public int SelectionPageCount => 1 + (Math.Max(0, available.Length - FirstPageCardCount) + CardsPerPage - 1) / CardsPerPage;
    public int FirstVisibleCard => SelectionPage == 0 ? 0 : FirstPageCardCount + (SelectionPage - 1) * CardsPerPage;
    public int VisibleCardCount => Math.Min(SelectionPage == 0 ? FirstPageCardCount : CardsPerPage, available.Length - FirstVisibleCard);
    public int SelectedHand { get; private set; }
    public int SelectedCell { get; private set; }
    public bool ChoosingCell { get; private set; }
    public IReadOnlyList<int>? TradingHand { get; private set; }
    public IReadOnlyList<int> TradeSelection => tradeSelection.AsReadOnly();
    public int RequiredTradeCards { get; private set; }
    public bool AwaitingRules { get; private set; }
    public int RulesSelection { get; private set; }
    public string TradeRuleDescription { get; private set; } = "";
    public Func<IReadOnlyList<int>, TripleTriadMatch>? CreateMatch { get; set; }
    public Func<TripleTriadMatch, TripleTriadMove>? ChooseOpponentMove { get; set; }
    public Action<TripleTriadMatch>? RestartDraw { get; set; }
    public Action<int>? RuleChoiceRequested { get; set; }
    public float PromptFade { get; set; }
    public string Message { get; private set; } = "Choose five cards. Cancel removes the last choice.";
    public event Action<int[]>? DeckConfirmed;
    public event Action? Changed;
    public event Action? CompletionRequested;

    public int Remaining(int id) => collection.GetValueOrDefault(id) - deck.Count(card => card == id);
    public TripleTriadCard Card(int id) => definitions.Single(card => card.Id == id);

    public void SetRecentDecks(IReadOnlyList<int[]> decks)
    {
        ArgumentNullException.ThrowIfNull(decks);
        recentDecks = decks.Where(hand => hand is { Length: 5 } && hand.All(id => definitions.Any(card => card.Id == id))).Take(3).Select(hand => hand.ToArray()).ToArray();
        RecentDeckSelection = recentDecks.Length > 0 && SelectionPage == 0 ? 0 : -1;
    }

    public bool IsRecentDeckCardAvailable(int deckIndex, int slot)
    {
        int[] hand = recentDecks[deckIndex];
        int id = hand[slot];
        return hand.Take(slot + 1).Count(card => card == id) <= collection.GetValueOrDefault(id);
    }

    public void ChooseDeck(int index)
    {
        if (AwaitingRules || Match is not null || (uint)index >= recentDecks.Length)
        {
            return;
        }
        deck.Clear();
        for (int slot = 0; slot < 5; slot++)
        {
            if (IsRecentDeckCardAvailable(index, slot))
            {
                deck.Add(recentDecks[index][slot]);
            }
        }
        Selection = 0;
        RecentDeckSelection = -1;
        completingRecentDeck = deck.Count < 5;
        RequestSound(TripleTriadSound.Confirm);
        CompleteDeckSelection();
        Changed?.Invoke();
    }

    private void CompleteDeckSelection()
    {
        if (deck.Count == 5)
        {
            Match = CreateMatch?.Invoke(deck) ?? TripleTriadMatch.Create(definitions, deck, opponent, Rules);
            SelectFirstHand();
            Message = "Choose a card, then a square.";
            DeckConfirmed?.Invoke(deck.ToArray());
        }
        else if (completingRecentDeck)
        {
            Message = $"Deck incomplete: choose {5 - deck.Count} more card(s).";
        }
    }

    public void ChooseCard(int id)
    {
        if (AwaitingRules || Match is not null || deck.Count >= 5 || !available.Contains(id) || Remaining(id) <= 0)
        {
            return;
        }
        RecentDeckSelection = -1;
        deck.Add(id);
        RequestSound(TripleTriadSound.Confirm);
        CompleteDeckSelection();
        Changed?.Invoke();
    }

    public void RestoreMatch(TripleTriadMatch match)
    {
        ArgumentNullException.ThrowIfNull(match);
        ClearPresentation();
        ClearTradePresentation();
        TradingHand = null;
        tradeCompleted = null;
        tradeSelection.Clear();
        Match = TripleTriadMatch.Restore(definitions, match.CaptureSnapshot());
        ChoosingCell = false;
        SelectFirstHand();
        Message = "Match loaded.";
    }

    public void RestoreDeck(IReadOnlyList<int> selected)
    {
        if (Match is not null || selected.Count > 5 || selected.GroupBy(id => id).Any(group => collection.GetValueOrDefault(group.Key) < group.Count()))
        {
            throw new InvalidDataException("Saved Triple Triad selection exceeds its original collection.");
        }
        deck.Clear();
        deck.AddRange(selected);
    }

    private void NavigateCore(UiKey key)
    {
        if (AwaitingRules)
        {
            if (key is UiKey.Up or UiKey.Down)
            {
                SelectRulesChoice(1 - RulesSelection);
                return;
            }
            if (key == UiKey.Confirm)
            {
                if (RuleChoiceRequested is not null)
                {
                    RuleChoiceRequested(RulesSelection);
                }
                else
                {
                    CompleteRulesPrompt(RulesSelection == 0);
                }
            }
            else if (key == UiKey.Cancel)
            {
                if (RuleChoiceRequested is not null)
                {
                    RuleChoiceRequested(1);
                }
                else
                {
                    CompleteRulesPrompt(false);
                }
            }
            return;
        }
        if (TradingHand is not null)
        {
            if (TradePhase == TripleTriadTradePhase.ConfirmingSelection)
            {
                NavigateTradeConfirmation(key);
                return;
            }
            if (AwaitingTradeConfirmation)
            {
                if (key is UiKey.Confirm or UiKey.Cancel)
                {
                    ConfirmTradeTransfer();
                }
                return;
            }
            if (TradePhase != TripleTriadTradePhase.None && TradePhase != TripleTriadTradePhase.Selecting)
            {
                return;
            }
            if (key is UiKey.Up or UiKey.Left)
            {
                Selection = (Selection + 4) % 5;
            }
            else if (key is UiKey.Down or UiKey.Right)
            {
                Selection = (Selection + 1) % 5;
            }
            else if (key == UiKey.Confirm)
            {
                SelectTradeCard(Selection);
            }
            else if (key == UiKey.Cancel && presentationEnabled && TradePhase == TripleTriadTradePhase.Selecting && tradeSelection.Contains(Selection))
            {
                StartTradeChoice(Selection, true);
            }
            return;
        }
        if (Match is null)
        {
            if (key is UiKey.Up or UiKey.Down)
            {
                int historyRows = SelectionPage == 0 ? recentDecks.Length : 0;
                int row = RecentDeckSelection >= 0 ? RecentDeckSelection : historyRows + Selection - FirstVisibleCard;
                int count = historyRows + VisibleCardCount;
                row = (row + (key == UiKey.Up ? count - 1 : 1)) % count;
                RecentDeckSelection = row < historyRows ? row : -1;
                if (RecentDeckSelection < 0)
                {
                    Selection = FirstVisibleCard + row - historyRows;
                }
            }
            else if (key is UiKey.Left or UiKey.Right or UiKey.PageUp or UiKey.PageDown)
            {
                int row = RecentDeckSelection >= 0 ? 0 : Selection - FirstVisibleCard;
                int pageDirection = key is UiKey.Left or UiKey.PageUp ? -1 : 1;
                int page = (SelectionPage + pageDirection + SelectionPageCount) % SelectionPageCount;
                int first = page == 0 ? 0 : FirstPageCardCount + (page - 1) * CardsPerPage;
                int count = Math.Min(page == 0 ? FirstPageCardCount : CardsPerPage, available.Length - first);
                Selection = first + Math.Min(row, count - 1);
                RecentDeckSelection = -1;
            }
            else if (key == UiKey.Confirm)
            {
                if (RecentDeckSelection >= 0)
                {
                    ChooseDeck(RecentDeckSelection);
                }
                else
                {
                    ChooseCard(available[Selection]);
                }
            }
            else if (key == UiKey.Cancel && deck.Count > 0)
            {
                deck.RemoveAt(deck.Count - 1);
                CompleteDeckSelection();
                Changed?.Invoke();
            }
            return;
        }
        if (Match.Outcome != TripleTriadOutcome.InProgress)
        {
            if (key == UiKey.Confirm && Match.CanRestartSuddenDeath)
            {
                if (RestartDraw is not null)
                {
                    RestartDraw(Match);
                }
                else
                {
                    Match.RestartSuddenDeath(Match.CurrentPlayer);
                }
                ChoosingCell = false;
                SelectFirstHand();
                Message = "Sudden Death. Cards follow their owners into the next round.";
                Changed?.Invoke();
            }
            else if (key == UiKey.Confirm)
            {
                CompletionRequested?.Invoke();
            }
            return;
        }
        if (Match.CurrentPlayer != 0)
        {
            return;
        }
        if (key == UiKey.Cancel)
        {
            ChoosingCell = false;
            return;
        }
        if (key == UiKey.Confirm)
        {
            if (!ChoosingCell)
            {
                ChoosingCell = true;
            }
            else
            {
                Play(SelectedHand, SelectedCell);
            }
            return;
        }
        int direction = key is UiKey.Up or UiKey.Left ? -1 : key is UiKey.Down or UiKey.Right ? 1 : 0;
        if (direction == 0)
        {
            return;
        }
        if (ChoosingCell)
        {
            SelectedCell = (SelectedCell + (key is UiKey.Up or UiKey.Down ? 3 * direction : direction) + 9) % 9;
        }
        else
        {
            do
            {
                SelectedHand = (SelectedHand + direction + 5) % 5;
            }
            while (Match.Hands[0][SelectedHand] is null);
        }
    }

    public void Play(int handIndex, int cellIndex)
    {
        if ((uint)handIndex >= 5 || (uint)cellIndex >= 9 || Match is null || Match.CurrentPlayer != 0 || Match.Outcome != TripleTriadOutcome.InProgress || Match.Hands[0][handIndex] is null || Match.Board[cellIndex] is not null)
        {
            Message = "Choose an empty square.";
            RequestSound(TripleTriadSound.Error);
            return;
        }
        if (IsAnimating)
        {
            return;
        }
        TripleTriadPlacedCard?[] before = Match.Board.ToArray();
        TripleTriadMoveResult result = Match.Play(handIndex, cellIndex);
        PresentMove(result, before);
        ChoosingCell = false;
        SelectFirstHand();
        Message = Match.Outcome == TripleTriadOutcome.InProgress ? "Opponent is thinking…" : ResultText();
        Changed?.Invoke();
    }

    public bool AdvanceOpponent()
    {
        if (IsAnimating || Match is null || Match.CurrentPlayer != 1 || Match.Outcome != TripleTriadOutcome.InProgress)
        {
            return false;
        }
        TripleTriadMove move = ChooseOpponentMove?.Invoke(Match) ?? Match.ChooseMove();
        TripleTriadPlacedCard?[] before = Match.Board.ToArray();
        TripleTriadMoveResult result = Match.Play(move.HandIndex, move.CellIndex);
        PresentMove(result, before);
        SelectFirstHand();
        Message = Match.Outcome == TripleTriadOutcome.InProgress ? "Choose a card, then a square." : ResultText();
        Changed?.Invoke();
        return true;
    }

    public void SelectHand(int index)
    {
        if (!IsAnimating && (uint)index < 5 && Match is { CurrentPlayer: 0, Outcome: TripleTriadOutcome.InProgress } && Match.Hands[0][index] is not null)
        {
            SelectedHand = index;
            ChoosingCell = true;
        }
    }

    public void BeginTradeSelection(IReadOnlyList<int> losingHand, int required, Action<int[]> completed)
    {
        ArgumentNullException.ThrowIfNull(losingHand);
        ArgumentNullException.ThrowIfNull(completed);
        if (losingHand.Count != 5 || required is < 1 or > 5)
        {
            throw new ArgumentException("Card trading requires five losing-hand cards and one to five choices.");
        }
        if (presentationEnabled)
        {
            IReadOnlyList<int> playerHand = Match?.InitialHands[0] ?? available.SelectMany(id => Enumerable.Repeat(id, collection[id])).Take(5).ToArray();
            BeginTradeSettlement(playerHand, losingHand, Enumerable.Range(0, 10).Select(index => index < 5 ? 0 : 1).ToArray(), TripleTriadTradeRule.One, required, true, [], completed);
            return;
        }
        TradingHand = losingHand.ToArray();
        RequiredTradeCards = required;
        tradeCompleted = completed;
        tradeSelection.Clear();
        Selection = 0;
        TradingToPlayer = true;
        IReadOnlyList<int> originalPlayerHand = Match?.InitialHands[0] ?? available.SelectMany(id => Enumerable.Repeat(id, collection[id])).Take(5).ToArray();
        tradeCards = originalPlayerHand.Concat(losingHand).ToArray();
        tradeOwners = Enumerable.Range(0, 10).Select(index => index < 5 ? 0 : 1).ToArray();
        SetTradePhase(TripleTriadTradePhase.Selecting);
        Message = $"Choose {required} card(s) to take.";
    }

    public void BeginRulesPrompt(string tradeRule, Action accepted, Action cancelled)
    {
        TradeRuleDescription = tradeRule;
        acceptRules = accepted;
        cancelRules = cancelled;
        AwaitingRules = true;
        RulesSelection = 0;
        Message = "Review the rules before playing.";
    }

    public void SelectRulesChoice(int choice)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(choice);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(choice, 1);
        if (AwaitingRules && RulesSelection != choice)
        {
            RulesSelection = choice;
            RequestSound(TripleTriadSound.Move);
            Changed?.Invoke();
        }
    }

    public void CompleteRulesPrompt(bool accepted)
    {
        if (accepted)
        {
            AwaitingRules = false;
            Message = "Choose five cards.";
            acceptRules?.Invoke();
        }
        else
        {
            cancelRules?.Invoke();
        }
    }

    public void SelectTradeCard(int slot)
    {
        if (IsAnimating || AwaitingTradeConfirmation || TradePhase == TripleTriadTradePhase.ConfirmingSelection || TradingHand is null || (uint)slot >= 5)
        {
            return;
        }
        if (presentationEnabled && TradePhase == TripleTriadTradePhase.Selecting)
        {
            Selection = slot;
            StartTradeChoice(slot, true);
            return;
        }
        if (tradeSelection.Contains(slot))
        {
            return;
        }
        RequestSound(TripleTriadSound.Confirm);
        tradeSelection.Add(slot);
        if (tradeSelection.Count == RequiredTradeCards)
        {
            Action<int[]> completed = tradeCompleted!;
            TradingHand = null;
            tradeCompleted = null;
            ClearTradePresentation();
            completed(tradeSelection.ToArray());
        }
    }

    private string ResultText() => Match!.Outcome switch
    {
        TripleTriadOutcome.PlayerWin => "You win!",
        TripleTriadOutcome.OpponentWin => "You lose.",
        _ => Match.CanRestartSuddenDeath ? "Draw. Sudden Death continues." : "Draw."
    };

    private void SelectFirstHand()
    {
        if (Match is not null)
        {
            SelectedHand = Enumerable.Range(0, 5).FirstOrDefault(index => Match.Hands[0][index] is not null);
        }
    }

    public UiView BuildView()
    {
        return view ??= new UiView(new TripleTriadElementView(this)) { DesignWidth = 960, DesignHeight = 672, Cancel = () => Navigate(UiKey.Cancel) };
    }
}
