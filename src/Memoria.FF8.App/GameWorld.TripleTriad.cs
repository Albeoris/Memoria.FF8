using System.Globalization;
using SkiaSharp;
using System.Runtime.InteropServices;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8;

internal sealed partial class GameWorld
{
    private TripleTriadCatalog? tripleTriadCatalog;
    private TripleTriadController? tripleTriad;
    public TripleTriadController? ActiveTripleTriad => tripleTriad;

    private void ConfigureTripleTriadHost()
    {
        Events.ValidateTripleTriad = request =>
        {
            if (session.CardInventory?.TripleTriadRandomState is null || session.CardInventory.TripleTriadStatistics is null)
            {
                return "CARDGAME requires its saved native RNG DWORD1CFEFB4 and result WORD counters; migrate this standalone explicitly.";
            }
            if (!Enum.IsDefined((TripleTriadTradeRule)request.TradeRule) || request.OpponentOwner == 0xF0)
            {
                return "CARDGAME requires an original trade rule and non-player opponent owner.";
            }
            _ = request.Rules;
            tripleTriadCatalog ??= TripleTriadCatalog.Load(assets.GameDirectory, assets.Resources);
            for (int empty = 1; empty <= request.Width * request.Height; empty++)
            {
                if (!TripleTriadNativeAi.SupportsParameters(tripleTriadCatalog.AiParameters, request.Argument4, request.Argument5, empty))
                {
                    return "CARDGAME AI parameters require an unsupported native search profile.";
                }
            }
            return null;
        };
    }

    internal static string TripleTriadIdentity(NativeTripleTriadRequest request)
    {
        return string.Create(CultureInfo.InvariantCulture, $"{request.FieldId}:{request.ProgramHash}:{request.Frame}:{request.ObjectId}:{request.ScriptId}:{request.Operation}:{request.FiberSequence}");
    }

    public TripleTriadController? EnsureTripleTriad()
    {
        NativeTripleTriadRequest? request = Events.PendingTripleTriad;
        if (request is null)
        {
            if (session.State.TripleTriadPlay is not null)
            {
                throw new InvalidDataException("A saved card module has no issuing CARDGAME wait.");
            }
            tripleTriad = null;
            return null;
        }
        if (tripleTriad is not null)
        {
            return tripleTriad;
        }
        tripleTriadCatalog ??= TripleTriadCatalog.Load(assets.GameDirectory, assets.Resources);
        NativeCardInventory cards = session.CardInventory ?? throw new InvalidDataException("CARDGAME has no saved inventory.");
        if (cards.TripleTriadStatistics is null)
        {
            throw new InvalidDataException("CARDGAME has no saved native outcome counters.");
        }
        TripleTriadSettings options = session.GameplayPreferences.TripleTriad with { Width = request.Width, Height = request.Height, HandSize = request.HandSize };
        uint seed = cards.TripleTriadRandomState ?? throw new InvalidDataException("CARDGAME has no saved RNG.");
        NativeTripleTriadPlayState? play = session.State.TripleTriadPlay;
        bool entering = play is null;
        if (play is null)
        {
            play = new NativeTripleTriadPlayState { Version = 1, Width = options.Width, Height = options.Height, HandSize = options.HandSize, RequestIdentity = TripleTriadIdentity(request), InitialRandomState = seed, RandomState = seed, DealRandomState = null, OpponentHand = [], SelectedCards = [], RulesAccepted = false, Prompt = TripleTriadPromptState.Create(seed), Match = null };
            session.SetTripleTriadPlay(play);
        }
        play.Validate();
        options = options with { Width = play.Width, Height = play.Height, HandSize = play.HandSize };
        if (play.RequestIdentity != TripleTriadIdentity(request) || play.InitialRandomState != seed)
        {
            throw new InvalidDataException("Saved Triple Triad module disagrees with its original request or saved RNG.");
        }
        ValidateTripleTriadDeal(request, cards, play);
        Dictionary<int, int> collection = Enumerable.Range(0, 110).ToDictionary(id => id, id => cards.AvailableCount(id));
        TripleTriadController controller = new(tripleTriadCatalog.Cards, collection, play.OpponentHand, tripleTriadCatalog.Name, request.Rules, options: options);
        controller.RestoreRedrawsUsed(play.RedrawsUsed);
        controller.RedrawHand = () =>
        {
            NativeTripleTriadPlayState current = session.State.TripleTriadPlay!;
            TripleTriadPreparedHand hand = TripleTriadPreparation.SelectRandomHand(cards, current.RandomState, options.HandSize);
            session.SetTripleTriadPlay(current with { RandomState = hand.RandomState, RedrawsUsed = current.RedrawsUsed + 1 });
            return hand.Cards;
        };
        controller.ConfigureCardModHints(tripleTriadCatalog.Refinements, GameSession.CanUseCardMod(session.State.CharacterVitals), session.State.GameplayPreferences.CardModOnce, session.State.RefinedCardIds);
        controller.CreateMatch = selected =>
        {
            NativeTripleTriadPlayState current = session.State.TripleTriadPlay!;
            TripleTriadPreparedBoard board = TripleTriadPreparation.GenerateElements(request.Rules, current.RandomState, options.Width, options.Height);
            TripleTriadStartingSide starter = TripleTriadPreparation.TossStartingSide(board.RandomState);
            session.SetTripleTriadPlay(current with { RandomState = starter.RandomState });
            return TripleTriadMatch.Create(tripleTriadCatalog.Cards, selected, current.OpponentHand, request.Rules, 1 - starter.NativeSide, board.Elements, options.Width, options.Height);
        };
        controller.ChooseOpponentMove = match =>
        {
            NativeTripleTriadPlayState current = session.State.TripleTriadPlay!;
            TripleTriadNativeAiResult choice = TripleTriadNativeAi.ChooseMove(tripleTriadCatalog.Cards, tripleTriadCatalog.TradePriorities, match, tripleTriadCatalog.AiParameters, request.Argument4, request.Argument5, current.RandomState, current.OpponentHand);
            session.SetTripleTriadPlay(current with { RandomState = choice.RandomState });
            return choice.Move;
        };
        controller.RestartDraw = match =>
        {
            NativeTripleTriadPlayState current = session.State.TripleTriadPlay!;
            TripleTriadPreparedBoard board = TripleTriadPreparation.GenerateElements(request.Rules, current.RandomState, options.Width, options.Height);
            match.RestartSuddenDeath(match.CurrentPlayer, board.Elements);
            session.SetTripleTriadPlay(current with { RandomState = board.RandomState });
        };
        controller.RestoreDeck(play.SelectedCards);
        if (play.Match is not null)
        {
            controller.RestoreMatch(TripleTriadMatch.Restore(tripleTriadCatalog.Cards, play.Match));
        }
        tripleTriad = controller;
        if (entering)
        {
            session.AcceptOriginalAchievementAction(OriginalAchievementActionKind.CardPlayed, "entry", "cards/" + play.RequestIdentity);
        }
        controller.Changed += CaptureTripleTriad;
        controller.CompletionRequested += BeginTripleTriadSettlement;
        if (!play.RulesAccepted)
        {
            controller.RuleChoiceRequested = choice => AdvanceTripleTriadPrompt(choice);
            controller.BeginRulesPrompt(((TripleTriadTradeRule)request.TradeRule).ToString(), () =>
            {
                SelectTripleTriadRandomHand(controller, cards, request);
                CaptureTripleTriad();
            }, () =>
            {
                NativeCardInventory cancelled = cards.CompleteMatch(-1, session.State.TripleTriadPlay!.RandomState);
                Events.ReturnFromTripleTriad(-1, () => session.CompleteTripleTriad(cancelled));
                tripleTriad = null;
                session.SetEventState(Events.Capture());
            });
        }
        else if (play.SelectedCards.Length == 0)
        {
            SelectTripleTriadRandomHand(controller, cards, request);
        }
        CaptureTripleTriad();
        SetTripleTriadPromptFade(controller, session.State.TripleTriadPlay!.Prompt);
        return controller;
    }

    public void AdvanceTripleTriadPrompt(int? choice = null)
    {
        TripleTriadController? controller = EnsureTripleTriad();
        if (controller is not { AwaitingRules: true } || session.State.TripleTriadPlay is not { } play)
        {
            return;
        }
        TripleTriadPromptState prompt = play.Prompt.Advance(choice);
        SetTripleTriadPromptFade(controller, prompt);
        // Accepted is published together with the generated deck by the prompt callback below.
        if (prompt.Phase == TripleTriadPromptPhase.Accepted)
        {
            NativeTripleTriadRequest request = Events.PendingTripleTriad!;
            TripleTriadOpponentHand opponent = TripleTriadOpponentGenerator.Generate(session.CardInventory!, request.OpponentOwner, request.RareChance, request.CommonLevelMask, prompt.RandomState, play.HandSize);
            session.SetTripleTriadPlay(play with { Prompt = prompt, RandomState = opponent.RandomState, RulesAccepted = true, DealRandomState = prompt.RandomState, OpponentHand = opponent.Cards.ToArray() });
            controller.CompleteRulesPrompt(true);
        }
        else if (prompt.Phase == TripleTriadPromptPhase.Cancelled)
        {
            // The final fade-out callback returns before any deck is generated.
            controller.CompleteRulesPrompt(false);
        }
        else
        {
            session.SetTripleTriadPlay(play with { Prompt = prompt, RandomState = prompt.RandomState });
            // Prompt callbacks change only the card module; retain the suspended field snapshot.
        }
    }

    private static void SetTripleTriadPromptFade(TripleTriadController controller, TripleTriadPromptState prompt)
    {
        controller.PromptFade = prompt.Phase switch
        {
            TripleTriadPromptPhase.FadingIn => 1 - prompt.PhaseCallbacks / 15f,
            TripleTriadPromptPhase.FadingOut => prompt.PhaseCallbacks / 15f,
            _ => 0
        };
    }

    private byte[] RenderTripleTriad()
    {
        UiView view = EnsureTripleTriad()!.BuildView();
        using SKBitmap bitmap = new(renderWidth, renderHeight, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using (BattlePreviewCanvas canvas = new(bitmap, assets.GameDirectory, resources))
        {
            view.Resize(renderWidth, renderHeight);
            view.Draw(canvas, new UiTheme());
        }
        return PortableRaster.Rgba(bitmap);
    }

    private void ValidateTripleTriadDeal(NativeTripleTriadRequest request, NativeCardInventory cards, NativeTripleTriadPlayState play)
    {
        if (!play.RulesAccepted)
        {
            return;
        }
        if (play.RedrawsUsed > 0 && !request.Rules.HasFlag(TripleTriadRules.Random))
        {
            throw new InvalidDataException("Only a Random-rule hand can use redraws.");
        }
        TripleTriadOpponentHand expectedOpponent = TripleTriadOpponentGenerator.Generate(cards, request.OpponentOwner, request.RareChance, request.CommonLevelMask, play.DealRandomState!.Value, play.HandSize);
        if (!play.OpponentHand.SequenceEqual(expectedOpponent.Cards))
        {
            throw new InvalidDataException("Saved Triple Triad opponent hand disagrees with its native deal.");
        }
        uint initialBoardSeed = expectedOpponent.RandomState;
        TripleTriadPreparedHand? expectedPlayer = null;
        if (request.Rules.HasFlag(TripleTriadRules.Random) && play.SelectedCards.Length != 0)
        {
            expectedPlayer = TripleTriadPreparation.SelectRandomHand(cards, expectedOpponent.RandomState, play.HandSize);
            initialBoardSeed = expectedPlayer.RandomState;
        }
        TripleTriadPreparedBoard expectedBoard = TripleTriadPreparation.GenerateElements(request.Rules, initialBoardSeed, play.Width, play.Height);
        TripleTriadStartingSide expectedStarter = TripleTriadPreparation.TossStartingSide(expectedBoard.RandomState);
        uint redrawSeed = expectedStarter.RandomState;
        for (int redraw = 0; redraw < play.RedrawsUsed; redraw++)
        {
            expectedPlayer = TripleTriadPreparation.SelectRandomHand(cards, redrawSeed, play.HandSize);
            redrawSeed = expectedPlayer.RandomState;
        }
        if (expectedPlayer is not null)
        {
            if (play.SelectedCards.Length != play.HandSize || !play.SelectedCards.SequenceEqual(expectedPlayer.Cards))
            {
                throw new InvalidDataException("Saved Random hand disagrees with its native selection.");
            }
        }
        if (play.Match is { } savedMatch)
        {
            _ = TripleTriadMatch.Restore(tripleTriadCatalog!.Cards, savedMatch);
            int[] originalCards = play.SelectedCards.Concat(play.OpponentHand).ToArray();
            if (savedMatch.Rules != request.Rules || Enumerable.Range(0, 2 * play.HandSize).Any(index => savedMatch.InitialHands[savedMatch.InstanceOrder[index] / play.HandSize][savedMatch.InstanceOrder[index] % play.HandSize] != originalCards[index]))
            {
                throw new InvalidDataException("Saved Triple Triad rules or card instances disagree with their native request and selected decks.");
            }
            if (savedMatch.Round == 0)
            {
                if (!savedMatch.BoardElements.SequenceEqual(expectedBoard.Elements) || savedMatch.StartingPlayer != 1 - expectedStarter.NativeSide)
                {
                    throw new InvalidDataException("Saved Triple Triad initial board or starter disagrees with its native random draws.");
                }
            }
        }
        else if (play.RandomState != expectedOpponent.RandomState)
        {
            throw new InvalidDataException("An unfinished manual selection has consumed unexpected native random draws.");
        }
    }

    private void CaptureTripleTriad()
    {
        if (tripleTriad is null || session.State.TripleTriadPlay is not { } play)
        {
            return;
        }
        session.SetTripleTriadPlay(play with { SelectedCards = tripleTriad.Deck.ToArray(), Match = tripleTriad.Match?.CaptureSnapshot() });
        session.SetEventState(Events.Capture());
    }

    private void BeginTripleTriadSettlement()
    {
        TripleTriadMatch match = tripleTriad?.Match ?? throw new InvalidOperationException("No card match is ready for settlement.");
        NativeTripleTriadRequest request = Events.PendingTripleTriad!;
        if (match.Outcome == TripleTriadOutcome.InProgress || match.CanRestartSuddenDeath)
        {
            throw new InvalidOperationException("Finish all required rounds before trading cards.");
        }
        TripleTriadTradeRule rule = (TripleTriadTradeRule)request.TradeRule;
        int count = rule switch
        {
            TripleTriadTradeRule.One => 1,
            TripleTriadTradeRule.Diff => Math.Min(match.HandSize, Math.Abs(match.PlayerScore - match.OpponentScore)),
            TripleTriadTradeRule.All => match.HandSize,
            _ => 0
        };
        if (match.Outcome == TripleTriadOutcome.Draw)
        {
            count = 0;
        }
        NativeTripleTriadPlayState play = session.State.TripleTriadPlay!;
        bool chooseCards = match.Outcome == TripleTriadOutcome.PlayerWin && count < match.HandSize && rule is TripleTriadTradeRule.One or TripleTriadTradeRule.Diff;
        IReadOnlyList<int> losingHand = match.Outcome == TripleTriadOutcome.PlayerWin ? play.OpponentHand : play.SelectedCards;
        int[] slots = [];
        if (!chooseCards)
        {
            slots = count == match.HandSize ? Enumerable.Range(0, match.HandSize).ToArray() : Enumerable.Range(0, match.HandSize).OrderByDescending(slot => tripleTriadCatalog!.TradePriority(losingHand[slot])).ThenBy(slot => slot).Take(count).ToArray();
        }
        tripleTriad!.BeginTradeSettlement(play.SelectedCards, play.OpponentHand, match.OriginalInstanceFinalOwners, rule, count, chooseCards, slots, CompleteTripleTriad);
    }

    private void CompleteTripleTriad(int[] selectedSlots)
    {
        NativeTripleTriadRequest request = Events.PendingTripleTriad!;
        NativeTripleTriadPlayState play = session.State.TripleTriadPlay!;
        TripleTriadMatch match = tripleTriad!.Match!;
        TripleTriadTradeResult settlement = TripleTriadTrade.Settle(session.CardInventory!, play.SelectedCards, play.OpponentHand, match.OriginalInstanceFinalOwners, (TripleTriadTradeRule)request.TradeRule, request.OpponentOwner, selectedSlots);
        int result = match.Outcome == TripleTriadOutcome.PlayerWin ? 0 : match.Outcome == TripleTriadOutcome.OpponentWin ? 1 : 2;
        NativeCardInventory next = settlement.Inventory.CompleteMatch(result, play.RandomState);
        bool rareCardLost = settlement.LostCardIds.Any(id => id >= NativeCardInventory.CommonCardCount);
        bool cardsClubMaster = request.OpponentOwner == 0xCA && settlement.TookOpponentInstance;
        tripleTriad.BeginReplayPrompt(() => RestartTripleTriad(next, rareCardLost, cardsClubMaster), () =>
        {
            Events.ReturnFromTripleTriad(result, () => session.CompleteTripleTriad(next, rareCardLost, cardsClubMaster));
            tripleTriad = null;
            session.SetEventState(Events.Capture());
        }, next.TotalAvailableCount >= match.HandSize);
    }

    private void RestartTripleTriad(NativeCardInventory inventory, bool rareCardLost = false, bool cardsClubMaster = false)
    {
        NativeTripleTriadRequest request = Events.PendingTripleTriad!;
        TripleTriadSettings options = tripleTriad!.Options;
        uint seed = inventory.TripleTriadRandomState!.Value;
        TripleTriadOpponentHand opponent = TripleTriadOpponentGenerator.Generate(inventory, request.OpponentOwner, request.RareChance, request.CommonLevelMask, seed, options.HandSize);
        NativeTripleTriadPlayState play = new()
        {
            Version = 1,
            Width = options.Width,
            Height = options.Height,
            HandSize = options.HandSize,
            RequestIdentity = TripleTriadIdentity(request),
            InitialRandomState = seed,
            RandomState = opponent.RandomState,
            DealRandomState = seed,
            OpponentHand = opponent.Cards.ToArray(),
            SelectedCards = [],
            RulesAccepted = true,
            IsReplay = true,
            Prompt = new TripleTriadPromptState(TripleTriadPromptPhase.Accepted, 0, seed),
            Match = null
        };
        session.CompleteTripleTriad(inventory, rareCardLost, cardsClubMaster);
        session.SetTripleTriadPlay(play);
        tripleTriad = null;
        EnsureTripleTriad();
    }

    private void SelectTripleTriadRandomHand(TripleTriadController controller, NativeCardInventory cards, NativeTripleTriadRequest request)
    {
        if (!request.Rules.HasFlag(TripleTriadRules.Random))
        {
            return;
        }
        TripleTriadPreparedHand random = TripleTriadPreparation.SelectRandomHand(cards, session.State.TripleTriadPlay!.RandomState, controller.Options.HandSize);
        session.SetTripleTriadPlay(session.State.TripleTriadPlay! with { RandomState = random.RandomState });
        foreach (int id in random.Cards)
        {
            controller.ChooseCard(id);
        }
    }

    private PlayerActionResult ApplyTripleTriad(PlayerAction action)
    {
        TripleTriadController? controller = EnsureTripleTriad();
        if (controller is null)
        {
            return new PlayerActionResult(false, NextTrigger, 0, "No CARDGAME is active.");
        }
        if (action.Frames < 1)
        {
            return new PlayerActionResult(false, NextTrigger, 0, "Card module input requires a positive callback count.");
        }
        switch (action.Kind)
        {
            case PlayerActionKind.TriadSelectCard:
                controller.ChooseCard(action.Value);
                break;
            case PlayerActionKind.TriadPlaceCard:
                if (controller.Match is null || (uint)action.Value >= controller.Match.HandSize || action.Quantity is not int cell || (uint)cell >= controller.Match.Board.Count || controller.Match.Hands[0][action.Value] is null)
                {
                    return new PlayerActionResult(false, NextTrigger, 0, "Card placement requires a valid hand slot and Quantity=board cell.");
                }
                controller.Play(action.Value, cell);
                break;
            case PlayerActionKind.TriadAutoMove:
                if (controller.Match?.CurrentPlayer == 1)
                {
                    controller.AdvanceOpponent();
                }
                else if (controller.Match?.Outcome == TripleTriadOutcome.InProgress)
                {
                    TripleTriadMove move = controller.Match.ChooseMove();
                    controller.Play(move.HandIndex, move.CellIndex);
                }
                break;
            case PlayerActionKind.TriadTradeCard:
                controller.SelectTradeCard(action.Value);
                break;
            case PlayerActionKind.Confirm:
                controller.Navigate(UiKey.Confirm);
                break;
            case PlayerActionKind.Cancel when controller.AwaitingRules || controller.AwaitingReplay:
                controller.Navigate(UiKey.Cancel);
                break;
            case PlayerActionKind.Wait when controller.AwaitingRules:
                for (int callback = 0; callback < action.Frames && Events.PendingTripleTriad is not null && controller.AwaitingRules; callback++)
                {
                    AdvanceTripleTriadPrompt();
                }
                break;
            default:
                return new PlayerActionResult(false, NextTrigger, 0, "Unsupported card-module input.");
        }
        return new PlayerActionResult(true, NextTrigger, 0);
    }
}
