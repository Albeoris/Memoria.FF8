using System.Globalization;
using System.Drawing;
using System.Drawing.Imaging;
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
            tripleTriadCatalog ??= TripleTriadCatalog.Load(assets.GameDirectory);
            for (int empty = 1; empty <= 9; empty++)
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
        tripleTriadCatalog ??= TripleTriadCatalog.Load(assets.GameDirectory);
        NativeCardInventory cards = session.CardInventory ?? throw new InvalidDataException("CARDGAME has no saved inventory.");
        if (cards.TripleTriadStatistics is null)
        {
            throw new InvalidDataException("CARDGAME has no saved native outcome counters.");
        }
        uint seed = cards.TripleTriadRandomState ?? throw new InvalidDataException("CARDGAME has no saved RNG.");
        NativeTripleTriadPlayState? play = session.State.TripleTriadPlay;
        if (play is null)
        {
            play = new NativeTripleTriadPlayState { Version = 1, RequestIdentity = TripleTriadIdentity(request), InitialRandomState = seed, RandomState = seed, DealRandomState = null, OpponentHand = [], SelectedCards = [], RulesAccepted = false, Prompt = TripleTriadPromptState.Create(seed), Match = null };
            session.SetTripleTriadPlay(play);
        }
        play.Validate();
        if (play.RequestIdentity != TripleTriadIdentity(request) || play.InitialRandomState != seed)
        {
            throw new InvalidDataException("Saved Triple Triad module disagrees with its original request or saved RNG.");
        }
        ValidateTripleTriadDeal(request, cards, play);
        Dictionary<int, int> collection = Enumerable.Range(0, 110).ToDictionary(id => id, id => Math.Max(0, cards.AvailableCount(id)));
        TripleTriadController controller = new(tripleTriadCatalog.Cards, collection, play.OpponentHand, tripleTriadCatalog.Name, request.Rules);
        controller.CreateMatch = selected =>
        {
            NativeTripleTriadPlayState current = session.State.TripleTriadPlay!;
            TripleTriadPreparedBoard board = TripleTriadPreparation.GenerateElements(request.Rules, current.RandomState);
            TripleTriadStartingSide starter = TripleTriadPreparation.TossStartingSide(board.RandomState);
            session.SetTripleTriadPlay(current with { RandomState = starter.RandomState });
            return TripleTriadMatch.Create(tripleTriadCatalog.Cards, selected, current.OpponentHand, request.Rules, 1 - starter.NativeSide, board.Elements);
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
            TripleTriadPreparedBoard board = TripleTriadPreparation.GenerateElements(request.Rules, current.RandomState);
            match.RestartSuddenDeath(match.CurrentPlayer, board.Elements);
            session.SetTripleTriadPlay(current with { RandomState = board.RandomState });
        };
        controller.RestoreDeck(play.SelectedCards);
        if (play.Match is not null)
        {
            controller.RestoreMatch(TripleTriadMatch.Restore(tripleTriadCatalog.Cards, play.Match));
        }
        tripleTriad = controller;
        controller.Changed += CaptureTripleTriad;
        controller.CompletionRequested += BeginTripleTriadSettlement;
        if (!play.RulesAccepted)
        {
            controller.RuleChoiceRequested = choice => AdvanceTripleTriadPrompt(choice);
            controller.BeginRulesPrompt(((TripleTriadTradeRule)request.TradeRule).ToString(), () =>
            {
                NativeTripleTriadPlayState current = session.State.TripleTriadPlay!;
                if (request.Rules.HasFlag(TripleTriadRules.Random))
                {
                    TripleTriadPreparedHand random = TripleTriadPreparation.SelectRandomHand(cards, current.RandomState);
                    session.SetTripleTriadPlay(session.State.TripleTriadPlay! with { RandomState = random.RandomState });
                    foreach (int id in random.Cards)
                    {
                        controller.ChooseCard(id);
                    }
                }
                CaptureTripleTriad();
            }, () =>
            {
                NativeCardInventory cancelled = cards.CompleteMatch(-1, session.State.TripleTriadPlay!.RandomState);
                Events.ReturnFromTripleTriad(-1, () => session.CompleteTripleTriad(cancelled));
                tripleTriad = null;
                session.SetEventState(Events.Capture());
            });
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
            TripleTriadOpponentHand opponent = TripleTriadOpponentGenerator.Generate(session.CardInventory!, request.OpponentOwner, request.RareChance, request.CommonLevelMask, prompt.RandomState);
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
            CaptureTripleTriad();
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
        using Bitmap bitmap = new(renderWidth, renderHeight, PixelFormat.Format32bppArgb);
        using (BattlePreviewCanvas canvas = new(bitmap, assets.GameDirectory))
        {
            view.Resize(renderWidth, renderHeight);
            view.Draw(canvas, new UiTheme());
        }
        BitmapData locked = bitmap.LockBits(new Rectangle(0, 0, renderWidth, renderHeight), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            byte[] rgba = new byte[renderWidth * renderHeight * 4];
            for (int y = 0; y < renderHeight; y++)
            {
                Marshal.Copy(locked.Scan0 + y * locked.Stride, rgba, y * renderWidth * 4, renderWidth * 4);
            }
            for (int index = 0; index < rgba.Length; index += 4)
            {
                (rgba[index], rgba[index + 2]) = (rgba[index + 2], rgba[index]);
            }
            return rgba;
        }
        finally
        {
            bitmap.UnlockBits(locked);
        }
    }

    private void ValidateTripleTriadDeal(NativeTripleTriadRequest request, NativeCardInventory cards, NativeTripleTriadPlayState play)
    {
        if (!play.RulesAccepted)
        {
            return;
        }
        TripleTriadOpponentHand expectedOpponent = TripleTriadOpponentGenerator.Generate(cards, request.OpponentOwner, request.RareChance, request.CommonLevelMask, play.DealRandomState!.Value);
        if (!play.OpponentHand.SequenceEqual(expectedOpponent.Cards))
        {
            throw new InvalidDataException("Saved Triple Triad opponent hand disagrees with its native deal.");
        }
        if (request.Rules.HasFlag(TripleTriadRules.Random) && play.SelectedCards.Length != 0)
        {
            TripleTriadPreparedHand expectedPlayer = TripleTriadPreparation.SelectRandomHand(cards, expectedOpponent.RandomState);
            if (play.SelectedCards.Length != 5 || !play.SelectedCards.SequenceEqual(expectedPlayer.Cards))
            {
                throw new InvalidDataException("Saved Random hand disagrees with its native selection.");
            }
        }
        if (play.Match is { } savedMatch)
        {
            _ = TripleTriadMatch.Restore(tripleTriadCatalog!.Cards, savedMatch);
            int[] originalCards = play.SelectedCards.Concat(play.OpponentHand).ToArray();
            if (savedMatch.Rules != request.Rules || Enumerable.Range(0, 10).Any(index => savedMatch.InitialHands[savedMatch.InstanceOrder[index] / 5][savedMatch.InstanceOrder[index] % 5] != originalCards[index]))
            {
                throw new InvalidDataException("Saved Triple Triad rules or card instances disagree with their native request and selected decks.");
            }
            if (savedMatch.Round == 0)
            {
                uint dealSeed = request.Rules.HasFlag(TripleTriadRules.Random) ? TripleTriadPreparation.SelectRandomHand(cards, expectedOpponent.RandomState).RandomState : expectedOpponent.RandomState;
                TripleTriadPreparedBoard expectedBoard = TripleTriadPreparation.GenerateElements(request.Rules, dealSeed);
                TripleTriadStartingSide expectedStarter = TripleTriadPreparation.TossStartingSide(expectedBoard.RandomState);
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
            TripleTriadTradeRule.Diff => Math.Min(5, Math.Abs(match.PlayerScore - match.OpponentScore)),
            TripleTriadTradeRule.All => 5,
            _ => 0
        };
        if (match.Outcome == TripleTriadOutcome.Draw || rule == TripleTriadTradeRule.Direct)
        {
            CompleteTripleTriad([]);
        }
        else if (match.Outcome == TripleTriadOutcome.PlayerWin && rule is TripleTriadTradeRule.One or TripleTriadTradeRule.Diff)
        {
            tripleTriad!.BeginTradeSelection(session.State.TripleTriadPlay!.OpponentHand, count, CompleteTripleTriad);
        }
        else
        {
            NativeTripleTriadPlayState play = session.State.TripleTriadPlay!;
            IReadOnlyList<int> losingHand = match.Outcome == TripleTriadOutcome.PlayerWin ? play.OpponentHand : play.SelectedCards;
            int[] slots = Enumerable.Range(0, 5).OrderByDescending(slot => tripleTriadCatalog!.TradePriority(losingHand[slot])).ThenBy(slot => slot).Take(count).ToArray();
            CompleteTripleTriad(slots);
        }
    }

    private void CompleteTripleTriad(int[] selectedSlots)
    {
        NativeTripleTriadRequest request = Events.PendingTripleTriad!;
        NativeTripleTriadPlayState play = session.State.TripleTriadPlay!;
        TripleTriadMatch match = tripleTriad!.Match!;
        TripleTriadTradeResult settlement = TripleTriadTrade.Settle(session.CardInventory!, play.SelectedCards, play.OpponentHand, match.OriginalInstanceFinalOwners, (TripleTriadTradeRule)request.TradeRule, request.OpponentOwner, selectedSlots);
        int result = match.Outcome == TripleTriadOutcome.PlayerWin ? 0 : match.Outcome == TripleTriadOutcome.OpponentWin ? 1 : 2;
        NativeCardInventory next = settlement.Inventory.CompleteMatch(result, play.RandomState);
        Events.ReturnFromTripleTriad(result, () => session.CompleteTripleTriad(next));
        tripleTriad = null;
        session.SetEventState(Events.Capture());
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
                if (action.Value is < 0 or >= 5 || action.Quantity is not int cell || cell is < 0 or >= 9 || controller.Match?.Hands[0][action.Value] is null)
                {
                    return new PlayerActionResult(false, NextTrigger, 0, "Card placement requires a hand slot0..4 and Quantity=cell0..8.");
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
            case PlayerActionKind.Cancel when controller.AwaitingRules:
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
