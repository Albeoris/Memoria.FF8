using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

/// <summary>Deck selection and human input for the single Triple Triad match implementation.</summary>
public sealed partial class TripleTriadController
{
    internal Func<string, string, string>? LocalizeText { get; set; }

    internal string SystemText(string id, string original) => Memoria.FF8.Resources.Localization.SystemTextCatalog.Translate(LocalizeText, id, original);

    internal string FormatSystemText(string id, string original, params object?[] arguments) => Memoria.FF8.Resources.Localization.SystemTextCatalog.Format(SystemText(id, original), original, arguments);

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

    public TripleTriadController(IReadOnlyList<TripleTriadCard> definitions, IReadOnlyDictionary<int, int> collection, IReadOnlyList<int> opponent, Func<int, string> name, TripleTriadRules rules = TripleTriadRules.Open, IReadOnlyList<int[]>? recentDecks = null, TripleTriadSettings? options = null)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        ArgumentNullException.ThrowIfNull(collection);
        ArgumentNullException.ThrowIfNull(opponent);
        ArgumentNullException.ThrowIfNull(name);
        Options = options ?? new TripleTriadSettings();
        Options.Validate();
        RedrawsRemaining = Options.RandomRedraws;
        SelectedCell = (Options.Height / 2) * Options.Width + Options.Width / 2;
        this.definitions = definitions;
        this.collection = collection.ToDictionary();
        this.opponent = opponent.ToArray();
        Name = name;
        Rules = rules;
        available = collection.Where(pair => pair.Value > 0).Select(pair => pair.Key).OrderDescending().ToArray();
        if (available.Sum(id => collection[id]) < Options.HandSize || available.Any(id => !definitions.Any(card => card.Id == id)) || this.opponent.Length != 0 && this.opponent.Length != Options.HandSize)
        {
            throw new ArgumentException("Triple Triad requires enough available player cards and a complete opponent hand.");
        }
        SetRecentDecks(recentDecks ?? []);
        // Validate the supplied opponent without consuming any collection or random state.
        if (this.opponent.Length == Options.HandSize)
        {
            _ = TripleTriadMatch.Create(definitions, available.SelectMany(id => Enumerable.Repeat(id, collection[id])).Take(Options.HandSize).ToArray(), this.opponent, rules, width: Options.Width, height: Options.Height, useModifiedSamePlus: Options.UseModifiedSamePlus, blockedCells: Enumerable.Range(0, Options.BlockedCellCount).ToArray());
        }
    }

    public TripleTriadSettings Options { get; }
    public int RedrawsRemaining { get; private set; }
    public Func<IReadOnlyList<int>>? RedrawHand { get; set; }
    private bool openingConfirmed;
    private OpeningControl openingControl;
    private enum OpeningControl
    {
        None,
        Redraw,
        Start
    }
    internal bool RedrawSelected => CanRedraw && openingControl == OpeningControl.Redraw;
    internal bool StartSelected => AwaitingOpeningConfirmation && openingControl == OpeningControl.Start;
    internal bool OpeningControlSelected => RedrawSelected || StartSelected;

    public void ConfirmOpening()
    {
        if (AwaitingOpeningConfirmation)
        {
            openingConfirmed = true;
            openingControl = OpeningControl.None;
            RequestSound(TripleTriadSound.Confirm);
            Changed?.Invoke();
        }
    }

    public bool AwaitingOpeningConfirmation => CanRedraw && !openingConfirmed && Match?.CurrentPlayer == 1;
    public bool CanRedraw => RedrawsRemaining > 0 && Rules.HasFlag(TripleTriadRules.Random) && !AwaitingRules && !AwaitingReplay && !IsAnimating && TradingHand is null && Match is { Round: 0, Outcome: TripleTriadOutcome.InProgress } && Match.Board.All(card => card is null) && RedrawHand is not null;

    public bool Redraw()
    {
        if (!CanRedraw)
        {
            return false;
        }
        openingConfirmed = false;
        int[] replacement = RedrawHand!().ToArray();
        TripleTriadMatch original = Match!;
        TripleTriadMatch redrawn = TripleTriadMatch.Create(definitions, replacement, original.InitialHands[1], original.Rules, original.StartingPlayer, original.BoardElements, original.Width, original.Height, original.UseModifiedSamePlus, original.BlockedCells);
        deck.Clear();
        deck.AddRange(replacement);
        Match = redrawn;
        RedrawsRemaining--;
        ChoosingCell = false;
        ResetTurnSelection();
        SetSystemMessage(() => "");
        DeckConfirmed?.Invoke(deck.ToArray());
        RequestSound(TripleTriadSound.Confirm);
        Changed?.Invoke();
        return true;
    }

    public void RestoreRedrawsUsed(int used)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(used);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(used, Options.RandomRedraws);
        RedrawsRemaining = Options.RandomRedraws - used;
    }

    public void HoverCell(int cell)
    {
        if (ChoosingCell && Match is not null && (uint)cell < Match.Board.Count && Match.Board[cell] is null && !Match.IsBlocked(cell))
        {
            SelectedCell = cell;
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
    public int SelectedCell { get; private set; } = 4;
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
    private string? message;
    private Func<string>? systemMessage;
    public string Message
    {
        get => AwaitingOpeningConfirmation ? "Redraw your hand or confirm to begin." : systemMessage?.Invoke() ?? message ?? (Options.HandSize == 5 ? SystemText("card.ui.initial-prompt", "Choose five cards. Cancel removes the last choice.") : $"Choose {Options.HandSize} cards. Cancel removes the last choice.");
        private set
        {
            systemMessage = null;
            message = value;
        }
    }

    private void SetSystemMessage(Func<string> text)
    {
        systemMessage = text;
    }
    public event Action<int[]>? DeckConfirmed;
    public event Action? Changed;
    public event Action? CompletionRequested;

    public int Remaining(int id) => collection.GetValueOrDefault(id) - deck.Count(card => card == id);
    public TripleTriadCard Card(int id) => definitions.Single(card => card.Id == id);

    public void SetRecentDecks(IReadOnlyList<int[]> decks)
    {
        ArgumentNullException.ThrowIfNull(decks);
        recentDecks = decks.Where(hand => hand is not null && hand.Length == Options.HandSize && hand.All(id => definitions.Any(card => card.Id == id))).Take(3).Select(hand => hand.ToArray()).ToArray();
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
        if (AwaitingRules || AwaitingReplay || Match is not null || (uint)index >= recentDecks.Length)
        {
            return;
        }
        deck.Clear();
        for (int slot = 0; slot < Options.HandSize; slot++)
        {
            if (IsRecentDeckCardAvailable(index, slot))
            {
                deck.Add(recentDecks[index][slot]);
            }
        }
        Selection = 0;
        RecentDeckSelection = -1;
        completingRecentDeck = deck.Count < Options.HandSize;
        RequestSound(TripleTriadSound.Confirm);
        CompleteDeckSelection();
        Changed?.Invoke();
    }

    private void CompleteDeckSelection()
    {
        if (deck.Count == Options.HandSize)
        {
            Match = CreateMatch?.Invoke(deck) ?? TripleTriadMatch.Create(definitions, deck, opponent, Rules, width: Options.Width, height: Options.Height, useModifiedSamePlus: Options.UseModifiedSamePlus, blockedCells: Enumerable.Range(0, Options.BlockedCellCount).ToArray());
            ResetTurnSelection();
            SetSystemMessage(() => "");
            DeckConfirmed?.Invoke(deck.ToArray());
        }
        else if (completingRecentDeck)
        {
            SetSystemMessage(() => FormatSystemText("card.ui.completedeckselection.deck.incomplete.choose.more.card.s", "Deck incomplete: choose {0} more card(s).", Options.HandSize - deck.Count));
        }
    }

    public void ChooseCard(int id)
    {
        if (AwaitingRules || AwaitingReplay || Match is not null || deck.Count >= Options.HandSize || !available.Contains(id) || Remaining(id) <= 0)
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
        replayResolved = false;
        ClearReplayPrompt();
        ClearPresentation();
        ClearTradePresentation();
        TradingHand = null;
        tradeCompleted = null;
        tradeSelection.Clear();
        Match = TripleTriadMatch.Restore(definitions, match.CaptureSnapshot());
        ChoosingCell = false;
        ResetTurnSelection();
        SetSystemMessage(() => Match.Outcome != TripleTriadOutcome.InProgress ? ResultText() : Match.CurrentPlayer == 0 ? "" : SystemText("card.ui.restorematch.opponent.is.thinking", "Opponent is thinking\u2026"));
    }

    public void RestoreDeck(IReadOnlyList<int> selected)
    {
        if (Match is not null || selected.Count > Options.HandSize || selected.GroupBy(id => id).Any(group => collection.GetValueOrDefault(group.Key) < group.Count()))
        {
            throw new InvalidDataException("Saved Triple Triad selection exceeds its original collection.");
        }
        deck.Clear();
        deck.AddRange(selected);
    }

    private void NavigateCore(UiKey key)
    {
        if (AwaitingReplay)
        {
            NavigateReplay(key);
            return;
        }
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
            if (key is UiKey.PageUp or UiKey.PageDown)
            {
                int tradePageDirection = key == UiKey.PageUp ? -1 : 1;
                int page = Selection / TradeCardsPerPage;
                for (int offset = 1; offset < TradePageCount; offset++)
                {
                    int targetPage = (page + tradePageDirection * offset + TradePageCount) % TradePageCount;
                    int first = targetPage * TradeCardsPerPage;
                    int candidate = Enumerable.Range(first, Math.Min(TradeCardsPerPage, TradingHand.Count - first)).FirstOrDefault(IsLosingTradeSlotEligible, -1);
                    if (candidate >= 0)
                    {
                        Selection = candidate;
                        break;
                    }
                }
            }
            else if (key is UiKey.Up or UiKey.Left or UiKey.Down or UiKey.Right)
            {
                int tradeDirection = key is UiKey.Up or UiKey.Left ? -1 : 1;
                for (int attempt = 0; attempt < TradingHand.Count; attempt++)
                {
                    Selection = (Selection + tradeDirection + TradingHand.Count) % TradingHand.Count;
                    if (IsLosingTradeSlotEligible(Selection))
                    {
                        break;
                    }
                }
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
                ResetTurnSelection();
                SetSystemMessage(() => SystemText("card.ui.navigatecore.sudden.death.cards.follow.their.owners.into", "Sudden Death. Cards follow their owners into the next round."));
                Changed?.Invoke();
            }
            else if (key == UiKey.Confirm)
            {
                if (CompletionRequested is not null)
                {
                    CompletionRequested();
                }
                else
                {
                    BeginReplayPrompt(ResetForReplay, () => ReplayDeclined?.Invoke());
                }
            }
            return;
        }
        if (key is UiKey.PageUp or UiKey.PageDown && CanRedraw)
        {
            Redraw();
            return;
        }
        if (!OpeningControlSelected)
        {
            openingControl = OpeningControl.None;
        }
        if (OpeningControlSelected)
        {
            if (key == UiKey.Confirm)
            {
                if (RedrawSelected)
                {
                    Redraw();
                }
                else
                {
                    ConfirmOpening();
                }
            }
            else if (key is UiKey.Down or UiKey.Cancel)
            {
                openingControl = OpeningControl.None;
            }
            else if (key is UiKey.Left or UiKey.Right && AwaitingOpeningConfirmation)
            {
                openingControl = RedrawSelected ? OpeningControl.Start : OpeningControl.Redraw;
            }
            return;
        }
        if (key == UiKey.Up && CanRedraw && !ChoosingCell && SelectedHand % new TripleTriadLayout(Match.Width, Match.Height, Match.HandSize).HandRows == 0)
        {
            openingControl = OpeningControl.Redraw;
            return;
        }
        if (key == UiKey.Confirm && AwaitingOpeningConfirmation)
        {
            ConfirmOpening();
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
            int step = key is UiKey.Up or UiKey.Down ? Match.Width * direction : direction;
            int candidate = SelectedCell;
            for (int attempt = 0; attempt < Match.Board.Count; attempt++)
            {
                candidate = (candidate + step + Match.Board.Count) % Match.Board.Count;
                if (!Match.IsBlocked(candidate))
                {
                    SelectedCell = candidate;
                    return;
                }
            }
        }
        else if (key is UiKey.Left or UiKey.Right && new TripleTriadLayout(Match.Width, Match.Height, Match.HandSize).HandColumns > 1)
        {
            TripleTriadLayout layout = new(Match.Width, Match.Height, Match.HandSize);
            int column = SelectedHand / layout.HandRows;
            int row = SelectedHand % layout.HandRows;
            for (int offset = 1; offset < layout.HandColumns; offset++)
            {
                int targetColumn = (column + direction * offset + layout.HandColumns) % layout.HandColumns;
                int candidate = Enumerable.Range(targetColumn * layout.HandRows, Math.Min(layout.HandRows, Match.HandSize - targetColumn * layout.HandRows))
                    .Where(index => Match.Hands[0][index] is not null)
                    .OrderBy(index => Math.Abs(index % layout.HandRows - row))
                    .FirstOrDefault(-1);
                if (candidate >= 0)
                {
                    SelectedHand = candidate;
                    return;
                }
            }
        }
        else
        {
            do
            {
                SelectedHand = (SelectedHand + direction + Match.HandSize) % Match.HandSize;
            }
            while (Match.Hands[0][SelectedHand] is null);
        }
    }

    public void Play(int handIndex, int cellIndex)
    {
        if (AwaitingReplay)
        {
            return;
        }
        if (Match is null || (uint)handIndex >= Match.HandSize || (uint)cellIndex >= Match.Board.Count || Match.CurrentPlayer != 0 || Match.Outcome != TripleTriadOutcome.InProgress || Match.Hands[0][handIndex] is null || Match.Board[cellIndex] is not null || Match.IsBlocked(cellIndex))
        {
            SetSystemMessage(() => SystemText("card.ui.play.choose.an.empty.square", "Choose an empty square."));
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
        ResetTurnSelection();
        SetSystemMessage(() => Match.Outcome == TripleTriadOutcome.InProgress ? SystemText("card.ui.play.opponent.is.thinking", "Opponent is thinking…") : ResultText());
        Changed?.Invoke();
    }

    public bool AdvanceOpponent()
    {
        if (AwaitingReplay || AwaitingOpeningConfirmation || IsAnimating || Match is null || Match.CurrentPlayer != 1 || Match.Outcome != TripleTriadOutcome.InProgress)
        {
            return false;
        }
        TripleTriadMove move = ChooseOpponentMove?.Invoke(Match) ?? Match.ChooseMove();
        TripleTriadPlacedCard?[] before = Match.Board.ToArray();
        TripleTriadMoveResult result = Match.Play(move.HandIndex, move.CellIndex);
        PresentMove(result, before);
        ResetTurnSelection();
        SetSystemMessage(() => Match.Outcome == TripleTriadOutcome.InProgress ? "" : ResultText());
        Changed?.Invoke();
        return true;
    }

    public bool CanCompleteInstantly => !replayResolved && !AwaitingRules && !AwaitingReplay && !IsAnimating && TradingHand is null && TradePhase == TripleTriadTradePhase.None && Match is { CurrentPlayer: 0, Outcome: TripleTriadOutcome.InProgress };

    public bool CompleteInstantly(bool victory)
    {
        if (!CanCompleteInstantly)
        {
            return false;
        }
        Match!.CompleteInstantly(victory);
        ClearPresentation();
        ChoosingCell = false;
        ResetTurnSelection();
        Message = ResultText();
        RequestSound(victory ? TripleTriadSound.Win : TripleTriadSound.Lose);
        Changed?.Invoke();
        return true;
    }

    public void SelectHand(int index)
    {
        if (!AwaitingReplay && !IsAnimating && (uint)index < Options.HandSize && Match is { CurrentPlayer: 0, Outcome: TripleTriadOutcome.InProgress } && Match.Hands[0][index] is not null)
        {
            openingControl = OpeningControl.None;
            SelectedHand = index;
            ChoosingCell = true;
        }
    }

    public void BeginTradeSelection(IReadOnlyList<int> losingHand, int required, Action<int[]> completed, IReadOnlyList<bool>? eligibility = null)
    {
        ArgumentNullException.ThrowIfNull(losingHand);
        ArgumentNullException.ThrowIfNull(completed);
        if (losingHand.Count != Options.HandSize || required < 1 || required > Options.HandSize)
        {
            throw new ArgumentException("Card trading requires a complete losing hand and a valid number of choices.");
        }
        bool[] allowed = eligibility?.ToArray() ?? Enumerable.Repeat(true, losingHand.Count).ToArray();
        if (allowed.Length != losingHand.Count || allowed.Count(value => value) < required)
        {
            throw new ArgumentException("Card trading requires enough cards in the reward pool.");
        }
        if (presentationEnabled)
        {
            IReadOnlyList<int> playerHand = Match?.InitialHands[0] ?? available.SelectMany(id => Enumerable.Repeat(id, collection[id])).Take(Options.HandSize).ToArray();
            BeginTradeSettlement(playerHand, losingHand, Enumerable.Range(0, 2 * Options.HandSize).Select(index => index < Options.HandSize ? 0 : 1).ToArray(), TripleTriadTradeRule.One, required, true, [], completed, Enumerable.Repeat(true, Options.HandSize).Concat(allowed).ToArray());
            return;
        }
        TradingHand = losingHand.ToArray();
        RequiredTradeCards = required;
        tradeCompleted = completed;
        tradeSelection.Clear();
        Selection = 0;
        TradingToPlayer = true;
        tradeEligibility = Enumerable.Repeat(true, Options.HandSize).Concat(allowed).ToArray();
        Selection = Array.FindIndex(allowed, value => value);
        IReadOnlyList<int> originalPlayerHand = Match?.InitialHands[0] ?? available.SelectMany(id => Enumerable.Repeat(id, collection[id])).Take(Options.HandSize).ToArray();
        tradeCards = originalPlayerHand.Concat(losingHand).ToArray();
        tradeOwners = Enumerable.Range(0, 2 * Options.HandSize).Select(index => index < Options.HandSize ? 0 : 1).ToArray();
        SetTradePhase(TripleTriadTradePhase.Selecting);
        SetSystemMessage(() => FormatSystemText("card.ui.begintradeselection.choose.card.s.to.take", "Choose {0} card(s) to take.", required));
    }

    public void BeginRulesPrompt(string tradeRule, Action accepted, Action cancelled)
    {
        TradeRuleDescription = tradeRule;
        acceptRules = accepted;
        cancelRules = cancelled;
        AwaitingRules = true;
        RulesSelection = 0;
        SetSystemMessage(() => SystemText("card.ui.beginrulesprompt.review.the.rules.before.playing", "Review the rules before playing."));
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
            SetSystemMessage(() => Options.HandSize == 5 ? SystemText("card.ui.completerulesprompt.choose.five.cards", "Choose five cards.") : $"Choose {Options.HandSize} cards.");
            acceptRules?.Invoke();
        }
        else
        {
            cancelRules?.Invoke();
        }
    }

    public void SelectTradeCard(int slot)
    {
        if (IsAnimating || AwaitingTradeConfirmation || TradePhase == TripleTriadTradePhase.ConfirmingSelection || TradingHand is null || (uint)slot >= TradingHand.Count || !IsLosingTradeSlotEligible(slot))
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
        TripleTriadOutcome.PlayerWin => SystemText("card.ui.resulttext.you.win", "You win!"),
        TripleTriadOutcome.OpponentWin => SystemText("card.ui.resulttext.you.lose", "You lose."),
        _ => Match.CanRestartSuddenDeath ? SystemText("card.ui.resulttext.draw.sudden.death.continues", "Draw. Sudden Death continues.") : SystemText("card.ui.resulttext.draw", "Draw.")
    };

    private void ResetTurnSelection()
    {
        openingControl = OpeningControl.None;
        SelectedCell = (Options.Height / 2) * Options.Width + Options.Width / 2;
        if (Match is not null)
        {
            if (Match.IsBlocked(SelectedCell))
            {
                SelectedCell = Enumerable.Range(0, Match.Board.Count).FirstOrDefault(cell => !Match.IsBlocked(cell));
            }
            SelectedHand = Enumerable.Range(0, Match.HandSize).FirstOrDefault(index => Match.Hands[0][index] is not null);
        }
    }

    public UiView BuildView()
    {
        return view ??= new UiView(new TripleTriadElementView(this)) { DesignWidth = 960, DesignHeight = 672, Cancel = () => Navigate(UiKey.Cancel) };
    }
}
