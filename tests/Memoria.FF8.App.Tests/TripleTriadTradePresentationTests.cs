using System.Drawing;
using System.Drawing.Imaging;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class TripleTriadTradePresentationTests
{
    [Fact]
    public void ExchangeDrawsCardModShortagesWithinEachCardCaption()
    {
        TripleTriadController controller = Create(collection: new Dictionary<int, int> { [5] = 0, [6] = 2 }, name: id => $"Long card name {id}");
        controller.ConfigureCardModHints([new(5, 40, 1, 1), new(6, 4, 1, 1)], true, false, []);
        controller.BeginTradeSettlement(PlayerHand, OpponentHand, OriginalOwners, TripleTriadTradeRule.One, 1, true, [], _ =>
        {
        });
        Advance(controller, TripleTriadController.TradeEntranceDuration);

        RecordingCanvas canvas = Draw(controller.BuildView());
        foreach (int id in new[] { 5, 6 })
        {
            var card = Assert.Single(canvas.Images, image => image.Source.Kind == UiImageKind.CardFace && image.Source.Id == id);
            var label = Assert.Single(canvas.Texts, line => line.Text == controller.TradeCardName(id));
            Assert.True(label.Bounds.X >= card.Bounds.X);
            Assert.True(label.Bounds.Right <= card.Bounds.Right);
        }
        Assert.Contains(canvas.Texts, line => line.Text == "Long card name 5 (0/40)");
        Assert.Contains(canvas.Texts, line => line.Text == "Long card name 6 (2/4)");
        Assert.Contains(canvas.Texts, line => line.Text == "Long card name 7");
    }

    private static readonly TripleTriadCard[] Cards = Enumerable.Range(0, 110).Select(id => new TripleTriadCard(id, 1, 1, 1, 1, TripleTriadElement.None)).ToArray();
    private static readonly int[] PlayerHand = [0, 1, 2, 3, 4];
    private static readonly int[] OpponentHand = [5, 6, 7, 8, 9];
    private static readonly int[] OriginalOwners = [0, 0, 0, 0, 0, 1, 1, 1, 1, 1];

    [Theory]
    [InlineData(UiKey.Confirm)]
    [InlineData(UiKey.Cancel)]
    public void ManualVictoryWaitsForFlipCenterAcknowledgementAndExit(UiKey acknowledgement)
    {
        TripleTriadController controller = Create();
        string matchBefore = JsonSerializer.Serialize(controller.Match!.CaptureSnapshot());
        int calls = 0;
        int[]? settled = null;
        controller.BeginTradeSettlement(PlayerHand, OpponentHand, OriginalOwners, TripleTriadTradeRule.One, 1, true, [], slots =>
        {
            calls++;
            settled = slots;
        });

        Assert.Equal(TripleTriadTradePhase.Entering, controller.TradePhase);
        Assert.True(controller.IsAnimating);
        controller.SelectTradeCard(4);
        controller.Navigate(UiKey.Down);
        controller.Navigate(UiKey.Confirm);
        Assert.Empty(controller.TradeSelection);
        Assert.Equal(0, controller.Selection);
        Advance(controller, TripleTriadController.TradeEntranceDuration);
        Assert.Equal(TripleTriadTradePhase.Selecting, controller.TradePhase);
        Assert.False(controller.IsAnimating);
        controller.SelectTradeCard(2);
        Assert.Equal(TripleTriadTradePhase.Flipping, controller.TradePhase);
        Assert.Equal(1, controller.PresentedTradeOwner(7));
        controller.SelectTradeCard(1);
        controller.Navigate(UiKey.Confirm);
        Assert.Equal(new[] { 2 }, controller.TradeSelection);
        Advance(controller, TripleTriadController.TradeFlipDuration * 0.24);
        Assert.Equal(1, controller.PresentedTradeOwner(7));
        Assert.True(controller.PresentedTradeWidth(7) < 0.07);
        Advance(controller, TripleTriadController.TradeFlipDuration * 0.28);
        Assert.Equal(0, controller.PresentedTradeOwner(7));
        Advance(controller, TripleTriadController.TradeFlipDuration);
        Assert.Equal(TripleTriadTradePhase.ConfirmingSelection, controller.TradePhase);
        controller.Navigate(UiKey.Confirm);
        Assert.Equal(TripleTriadTradePhase.Moving, controller.TradePhase);
        Assert.Null(settled);
        controller.Navigate(acknowledgement);
        Assert.Equal(TripleTriadTradePhase.Moving, controller.TradePhase);
        Advance(controller, TripleTriadController.TradeMoveDuration);
        Assert.Equal(TripleTriadTradePhase.AwaitingConfirmation, controller.TradePhase);
        Assert.False(controller.IsAnimating);
        Assert.Contains("Card acquired", controller.Message);
        Assert.Equal(7, controller.TradingCardIndex);
        Advance(controller, 20);
        Assert.Null(settled);
        controller.Navigate(acknowledgement);
        Assert.Equal(TripleTriadTradePhase.Exiting, controller.TradePhase);
        Assert.Null(settled);
        controller.Navigate(UiKey.Confirm);
        controller.SelectTradeCard(3);
        Advance(controller, TripleTriadController.TradeExitDuration);
        Assert.Equal(new[] { 2 }, settled);
        Assert.Equal(1, calls);
        Assert.Null(controller.TradingHand);
        Assert.Equal(TripleTriadTradePhase.None, controller.TradePhase);
        Advance(controller, 20);
        controller.SelectTradeCard(0);
        controller.Navigate(UiKey.Confirm);
        Assert.Equal(1, calls);
        Assert.Equal(matchBefore, JsonSerializer.Serialize(controller.Match.CaptureSnapshot()));
    }

    [Fact]
    public void AutomaticLossFlipsChosenCardAndWaitsOnCardLostMessage()
    {
        TripleTriadController controller = Create(winner: 1);
        int[]? settled = null;
        controller.BeginTradeSettlement(PlayerHand, OpponentHand, OriginalOwners, TripleTriadTradeRule.One, 1, false, [3], slots => settled = slots);
        Assert.False(controller.TradingToPlayer);
        Assert.Equal(PlayerHand, controller.TradingHand);
        Advance(controller, TripleTriadController.TradeEntranceDuration);
        Assert.Equal(TripleTriadTradePhase.Flipping, controller.TradePhase);
        Assert.Equal(new[] { 3 }, controller.TradeSelection);
        Assert.Equal(0, controller.PresentedTradeOwner(3));
        Advance(controller, TripleTriadController.TradeFlipDuration);
        Assert.Equal(1, controller.PresentedTradeOwner(3));
        Advance(controller, TripleTriadController.TradeMoveDuration);
        Assert.Equal(3, controller.TradingCardIndex);
        Assert.Contains("Card lost", controller.Message);
        Assert.Null(settled);
        controller.Navigate(UiKey.Cancel);
        Advance(controller, TripleTriadController.TradeExitDuration);
        Assert.Equal(new[] { 3 }, settled);
    }

    [Fact]
    public void DiffRequiresDistinctChoicesAndAcknowledgesEachTransferBeforeSettlement()
    {
        TripleTriadController controller = Create();
        int calls = 0;
        int[]? settled = null;
        controller.BeginTradeSettlement(PlayerHand, OpponentHand, OriginalOwners, TripleTriadTradeRule.Diff, 2, true, [], slots =>
        {
            calls++;
            settled = slots;
        });
        Advance(controller, TripleTriadController.TradeEntranceDuration);
        controller.SelectTradeCard(1);
        Advance(controller, TripleTriadController.TradeFlipDuration);
        Assert.Equal(TripleTriadTradePhase.Selecting, controller.TradePhase);
        controller.SelectTradeCard(4);
        Advance(controller, TripleTriadController.TradeFlipDuration);
        Assert.Equal(TripleTriadTradePhase.ConfirmingSelection, controller.TradePhase);
        Assert.Null(settled);
        controller.Navigate(UiKey.Confirm);
        Assert.Equal(6, controller.TradingCardIndex);
        Acknowledge(controller);
        Assert.True(controller.IsTradeCardRemoved(6));
        Assert.Equal(9, controller.TradingCardIndex);
        Assert.Null(settled);
        Acknowledge(controller);
        Assert.Equal(new[] { 1, 4 }, settled);
        Assert.Equal(1, calls);
    }

    [Fact]
    public void DirectPresentsBothGainedAndLostInstancesWithoutChangingTheMatch()
    {
        TripleTriadController controller = Create();
        string before = JsonSerializer.Serialize(controller.Match!.CaptureSnapshot());
        int[] owners = [0, 1, 0, 0, 0, 1, 1, 0, 1, 1];
        int calls = 0;
        controller.BeginTradeSettlement(PlayerHand, OpponentHand, owners, TripleTriadTradeRule.Direct, 0, false, [], slots =>
        {
            calls++;
            Assert.Empty(slots);
        });
        owners[7] = 1;
        Advance(controller, TripleTriadController.TradeEntranceDuration);
        Assert.Equal(TripleTriadTradePhase.Flipping, controller.TradePhase);
        Assert.Equal(0, controller.PresentedTradeOwner(1));
        Advance(controller, TripleTriadController.TradeFlipDuration);
        Assert.Equal(TripleTriadTradePhase.Flipping, controller.TradePhase);
        Assert.Equal(0, controller.PresentedTradeOwner(1));
        Assert.Equal(0, controller.PresentedTradeOwner(7));
        Advance(controller, TripleTriadController.TradeFlipDuration);
        Assert.Equal(TripleTriadTradePhase.Moving, controller.TradePhase);
        Assert.Equal(7, controller.TradingCardIndex);
        Advance(controller, TripleTriadController.TradeMoveDuration);
        Assert.Contains("Card acquired", controller.Message);
        controller.Navigate(UiKey.Confirm);
        Advance(controller, TripleTriadController.TradeExitDuration);
        Assert.Equal(1, controller.TradingCardIndex);
        Assert.Equal(0, calls);
        Advance(controller, TripleTriadController.TradeMoveDuration);
        Assert.Contains("Card lost", controller.Message);
        controller.Navigate(UiKey.Cancel);
        Advance(controller, TripleTriadController.TradeExitDuration);
        Assert.Equal(1, calls);
        Assert.Equal(before, JsonSerializer.Serialize(controller.Match.CaptureSnapshot()));
    }

    [Theory]
    [InlineData(UiKey.Confirm)]
    [InlineData(UiKey.Cancel)]
    public void DecliningSelectionConfirmationAllowsDeselectingAndReselectingBeforeSettlement(UiKey deselect)
    {
        TripleTriadController controller = Create();
        int[]? settled = null;
        controller.BeginTradeSettlement(PlayerHand, OpponentHand, OriginalOwners, TripleTriadTradeRule.One, 1, true, [], slots => settled = slots);
        Advance(controller, TripleTriadController.TradeEntranceDuration);
        controller.SelectTradeCard(0);
        Advance(controller, TripleTriadController.TradeFlipDuration);
        Assert.Equal(TripleTriadTradePhase.ConfirmingSelection, controller.TradePhase);
        controller.Navigate(UiKey.Down);
        controller.Navigate(UiKey.Confirm);
        Assert.Equal(TripleTriadTradePhase.Selecting, controller.TradePhase);
        Assert.Equal(new[] { 0 }, controller.TradeSelection);
        controller.Navigate(deselect);
        Assert.Equal(TripleTriadTradePhase.Flipping, controller.TradePhase);
        Assert.Empty(controller.TradeSelection);
        Advance(controller, TripleTriadController.TradeFlipDuration);
        Assert.Equal(1, controller.PresentedTradeOwner(5));
        Assert.Equal(TripleTriadTradePhase.Selecting, controller.TradePhase);
        controller.SelectTradeCard(3);
        Advance(controller, TripleTriadController.TradeFlipDuration);
        Assert.Equal(TripleTriadTradePhase.ConfirmingSelection, controller.TradePhase);
        Assert.Null(settled);
        controller.Navigate(UiKey.Confirm);
        Assert.Equal(8, controller.TradingCardIndex);
        Acknowledge(controller);
        Assert.Equal(new[] { 3 }, settled);
    }

    [Fact]
    public void DirectCancelsExchangedIdenticalCommonCardsBeforePresentingTransfers()
    {
        TripleTriadController controller = Create();
        int calls = 0;
        controller.BeginTradeSettlement(PlayerHand, [0, 6, 7, 8, 9], [1, 0, 0, 0, 0, 0, 1, 1, 1, 1], TripleTriadTradeRule.Direct, 0, false, [], selected =>
        {
            Assert.Empty(selected);
            calls++;
        });
        Advance(controller, TripleTriadController.TradeEntranceDuration);
        Assert.Equal(1, calls);
        Assert.Null(controller.TradingHand);
        Assert.Equal(TripleTriadTradePhase.None, controller.TradePhase);
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(2, true)]
    public void AcquisitionHistoryDistinguishesNeverOwnedFromPreviouslyOwnedAndHeld(int count, bool known)
    {
        TripleTriadController controller = Create(collection: new Dictionary<int, int> { [5] = count });
        Assert.Equal(known, controller.HasAcquiredCard(5));
        Assert.False(controller.HasAcquiredCard(109));
        List<TripleTriadSound> sounds = [];
        controller.SoundRequested += sounds.Add;
        controller.BeginTradeSettlement(PlayerHand, OpponentHand, OriginalOwners, TripleTriadTradeRule.One, 1, false, [0], _ => throw new InvalidOperationException("Acquisition must wait until the exit completes."));
        Advance(controller, TripleTriadController.TradeEntranceDuration);
        Advance(controller, TripleTriadController.TradeFlipDuration);
        Advance(controller, TripleTriadController.TradeMoveDuration);
        sounds.Clear();
        controller.Navigate(UiKey.Confirm);
        Assert.Equal(known ? TripleTriadSound.CardReceived : TripleTriadSound.NewCard, Assert.Single(sounds));
        Assert.Equal(known, controller.HasAcquiredCard(5));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RestoreCancelsTransientTradeWithoutInvokingSettlement(bool awaitingConfirmation)
    {
        TripleTriadController controller = Create();
        TripleTriadMatch restored = controller.Match!;
        int calls = 0;
        controller.BeginTradeSettlement(PlayerHand, OpponentHand, OriginalOwners, TripleTriadTradeRule.One, 1, false, [0], _ => calls++);
        if (awaitingConfirmation)
        {
            Advance(controller, TripleTriadController.TradeEntranceDuration);
            Advance(controller, TripleTriadController.TradeFlipDuration);
            Advance(controller, TripleTriadController.TradeMoveDuration);
            Assert.True(controller.AwaitingTradeConfirmation);
        }
        controller.RestoreMatch(restored);
        Assert.Equal(TripleTriadTradePhase.None, controller.TradePhase);
        Assert.Empty(controller.TradeCards);
        Assert.Null(controller.TradingHand);
        Assert.False(controller.IsAnimating);
        Assert.False(controller.AwaitingTradeConfirmation);
        Assert.Equal(-1, controller.TradingCardIndex);
        Advance(controller, 20);
        controller.Navigate(UiKey.Confirm);
        Assert.Equal(0, calls);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void ExchangeDrawsHistoryColorsAndMovesTheTransferredCardToItsAcknowledgement(int winner)
    {
        TripleTriadController controller = Create(winner, new Dictionary<int, int> { [5] = -1, [6] = 0, [7] = 2 });
        int calls = 0;
        bool manual = winner == 0;
        controller.BeginTradeSettlement(PlayerHand, OpponentHand, OriginalOwners, TripleTriadTradeRule.One, 1, manual, manual ? [] : [0], _ => calls++);
        UiView view = controller.BuildView();
        RecordingCanvas entrance = Draw(view);
        Assert.Equal(10, entrance.Images.Count(image => image.Source.Kind == UiImageKind.CardFace));
        Assert.Equal(new UiColor(100, 149, 237), Assert.Single(entrance.Texts, line => line.Text == "Card 5").Color);
        Assert.Equal(new UiColor(255, 255, 0), Assert.Single(entrance.Texts, line => line.Text == "Card 6").Color);
        Assert.Equal(new UiTheme().Foreground, Assert.Single(entrance.Texts, line => line.Text == "Card 7").Color);
        Advance(controller, TripleTriadController.TradeEntranceDuration);
        if (manual)
        {
            RecordingCanvas selection = Draw(view);
            var prompt = Assert.Single(selection.Texts, line => line.Text == "Select 1 card(s) you want");
            Assert.Equal(480, prompt.Bounds.X + prompt.Bounds.Width / 2);
            Assert.Equal(24, prompt.Bounds.Y);
            Assert.DoesNotContain(selection.Texts, line => line.Text == controller.Message);
            foreach (var card in selection.Images.Where(image => image.Source.Kind == UiImageKind.CardFace))
            {
                var label = Assert.Single(selection.Texts, line => line.Text == controller.Name(card.Source.Id));
                Assert.Equal(card.Bounds.X + card.Bounds.Width / 2, label.Bounds.X + label.Bounds.Width / 2);
                Assert.Equal(selection.MeasureText(label.Text, 18, float.PositiveInfinity).Width, label.Bounds.Width);
            }
            Assert.Single(selection.Pointers);
            controller.SelectTradeCard(0);
            Assert.Contains(Draw(view).Texts, line => line.Text == "Select 1 card(s) you want");
        }
        Advance(controller, TripleTriadController.TradeFlipDuration);
        if (manual)
        {
            Assert.Equal(TripleTriadTradePhase.ConfirmingSelection, controller.TradePhase);
            Assert.Contains(Draw(view).Texts, line => line.Text == "Select 1 card(s) you want");
            controller.Navigate(UiKey.Confirm);
        }
        Advance(controller, TripleTriadController.TradeMoveDuration / 2);
        RecordingCanvas moving = Draw(view);
        Assert.DoesNotContain(moving.Texts, line => line.Text.StartsWith("Select ", StringComparison.Ordinal));
        int transferredId = manual ? 5 : 0;
        var movingCard = Assert.Single(moving.Images, image => image.Source.Kind == UiImageKind.CardFace && image.Source.Id == transferredId);
        Assert.Equal(winner, movingCard.Source.Palette);
        Assert.InRange(movingCard.Bounds.Width, 210, 215);
        Assert.DoesNotContain(moving.Texts, line => line.Text == $"Card {transferredId}");
        Advance(controller, TripleTriadController.TradeMoveDuration / 2);
        RecordingCanvas acknowledgement = Draw(view);
        var centeredCard = Assert.Single(acknowledgement.Images, image => image.Source.Kind == UiImageKind.CardFace && image.Source.Id == transferredId);
        Assert.Equal(320, centeredCard.Bounds.Width);
        Assert.Equal(480, centeredCard.Bounds.X + centeredCard.Bounds.Width / 2);
        Assert.Equal(336, centeredCard.Bounds.Y + centeredCard.Bounds.Height / 2);
        Assert.Contains(acknowledgement.Texts, line => line.Text == $"Card {transferredId} — {(manual ? "Card acquired" : "Card lost")}");
        Assert.Equal(manual ? new UiColor(100, 149, 237) : new UiTheme().Foreground, Assert.Single(acknowledgement.Texts, line => line.Text == $"Card {transferredId}").Color);
        Assert.Equal(0, calls);
    }

    [Fact]
    public void OriginalHostDefersInventoryAndStatisticsUntilAnimatedExchangeCompletes()
    {
        string directory = TestResources.GameDirectory;
        string source = TestResources.FixturePath("App.Tests.TripleTriadTradePresentationTests.Match");
        Assert.SkipUnless(Directory.Exists(directory) && File.Exists(source), "Original match standalone and installed resources are required.");
        GameSession session = new(GameSession.Load(source));
        using GameWorld world = new(session, directory);
        world.EnsureField();
        TripleTriadController controller = world.EnsureTripleTriad()!;
        Assert.NotNull(controller.Match);
        var statisticsBefore = session.CardInventory!.TripleTriadStatistics!;
        int roundsAndMoves = 0;
        while (controller.Match.Outcome == TripleTriadOutcome.InProgress || controller.Match.CanRestartSuddenDeath)
        {
            Assert.True(roundsAndMoves++ < 200, "The supplied match did not finish within the bounded continuation.");
            PlayerActionKind action = controller.Match.CanRestartSuddenDeath ? PlayerActionKind.Confirm : PlayerActionKind.TriadAutoMove;
            Assert.True(world.Apply(new PlayerAction(action)).Accepted);
        }
        TripleTriadOutcome outcome = controller.Match.Outcome;
        string inventoryBefore = JsonSerializer.Serialize(session.CardInventory);
        controller.EnablePresentation();
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Confirm)).Accepted);
        int acknowledgements = 0;
        int ticks = 0;
        while (!controller.AwaitingReplay && ticks++ < 100)
        {
            Assert.NotNull(world.Events.PendingTripleTriad);
            Assert.Equal(inventoryBefore, JsonSerializer.Serialize(session.CardInventory));
            switch (controller.TradePhase)
            {
                case TripleTriadTradePhase.Selecting:
                    int slot = Enumerable.Range(0, 5).First(index => !controller.TradeSelection.Contains(index));
                    controller.SelectTradeCard(slot);
                    break;
                case TripleTriadTradePhase.ConfirmingSelection:
                    controller.Navigate(UiKey.Confirm);
                    break;
                case TripleTriadTradePhase.AwaitingConfirmation:
                    Assert.Contains(outcome == TripleTriadOutcome.PlayerWin ? "Card acquired" : "Card lost", controller.Message);
                    acknowledgements++;
                    controller.Navigate(UiKey.Confirm);
                    break;
                default:
                    controller.AdvancePresentation(TimeSpan.FromSeconds(1));
                    break;
            }
        }
        Assert.True(controller.AwaitingReplay);
        Assert.NotNull(world.ActiveTripleTriad);
        Assert.NotNull(world.Events.PendingTripleTriad);
        Assert.Equal(inventoryBefore, JsonSerializer.Serialize(session.CardInventory));
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Cancel)).Accepted);
        Assert.Null(world.ActiveTripleTriad);
        Assert.Null(world.Events.PendingTripleTriad);
        Assert.Null(session.State.TripleTriadPlay);
        if (outcome != TripleTriadOutcome.Draw)
        {
            Assert.True(acknowledgements > 0, "A decisive match must present its card exchange acknowledgement.");
        }
        var statisticsAfter = session.CardInventory!.TripleTriadStatistics!;
        Assert.Equal(statisticsBefore.Wins + statisticsBefore.Losses + statisticsBefore.Draws + 1, statisticsAfter.Wins + statisticsAfter.Losses + statisticsAfter.Draws);
        Assert.Equal(statisticsBefore.Wins + (outcome == TripleTriadOutcome.PlayerWin ? 1 : 0), statisticsAfter.Wins);
        Assert.Equal(statisticsBefore.Losses + (outcome == TripleTriadOutcome.OpponentWin ? 1 : 0), statisticsAfter.Losses);
        Assert.Equal(statisticsBefore.Draws + (outcome == TripleTriadOutcome.Draw ? 1 : 0), statisticsAfter.Draws);
        controller.AdvancePresentation(TimeSpan.FromSeconds(1));
        Assert.Equal(statisticsAfter, session.CardInventory.TripleTriadStatistics);
        string output = Environment.GetEnvironmentVariable("MEMORIA_FF8_TRIAD_OUTPUT_DIRECTORY") ?? Path.Combine(Path.GetTempPath(), "ff8-triad-host-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(output);
        string final = Path.Combine(output, "animated-exchange-after-match.json");
        session.Save(final);
        GameSession restored = new(GameSession.Load(final));
        Assert.Null(restored.State.TripleTriadPlay);
        Assert.Equal(JsonSerializer.Serialize(session.CardInventory), JsonSerializer.Serialize(restored.CardInventory));
        Console.WriteLine($"Animated exchange outcome: {outcome}; acknowledgements: {acknowledgements}; standalone: {final}");
    }

    [Fact]
    public void OriginalArtworkRendersVictoryLossAndDirectExchangeStages()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Original resources required.");
        TripleTriadCatalog catalog = TripleTriadCatalog.Load(directory);
        string output = Environment.GetEnvironmentVariable("MEMORIA_FF8_TRIAD_UI_OUTPUT_DIRECTORY") ?? Path.Combine(Path.GetTempPath(), "ff8-triad-trade-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(output);
        Dictionary<int, int> history = new() { [5] = -1, [6] = 0, [7] = 1 };
        TripleTriadController victory = Create(collection: history, definitions: catalog.Cards, name: catalog.Name);
        victory.BeginTradeSettlement(PlayerHand, OpponentHand, OriginalOwners, TripleTriadTradeRule.One, 1, true, [], _ => throw new InvalidOperationException("Rendering must not settle cards."));
        Advance(victory, TripleTriadController.TradeEntranceDuration / 2);
        Render(victory, "victory-entering");
        Advance(victory, TripleTriadController.TradeEntranceDuration / 2);
        Render(victory, "victory-selection");
        victory.SelectTradeCard(0);
        Advance(victory, TripleTriadController.TradeFlipDuration / 4);
        Render(victory, "victory-quarter-turn");
        Advance(victory, TripleTriadController.TradeFlipDuration / 4);
        Render(victory, "victory-half-turn");
        Advance(victory, TripleTriadController.TradeFlipDuration / 2);
        Assert.Equal(TripleTriadTradePhase.ConfirmingSelection, victory.TradePhase);
        Render(victory, "victory-confirm-selection");
        victory.Navigate(UiKey.Confirm);
        Advance(victory, TripleTriadController.TradeMoveDuration / 2);
        Render(victory, "victory-moving");
        Advance(victory, TripleTriadController.TradeMoveDuration / 2);
        Render(victory, "victory-card-acquired");
        victory.Navigate(UiKey.Confirm);
        Advance(victory, TripleTriadController.TradeExitDuration / 2);
        Render(victory, "victory-exiting");

        TripleTriadController loss = Create(1, history, catalog.Cards, catalog.Name);
        loss.BeginTradeSettlement(PlayerHand, OpponentHand, OriginalOwners, TripleTriadTradeRule.One, 1, false, [2], _ => throw new InvalidOperationException("Rendering must not settle cards."));
        Advance(loss, TripleTriadController.TradeEntranceDuration);
        Render(loss, "loss-selected");
        Advance(loss, TripleTriadController.TradeFlipDuration / 2);
        Render(loss, "loss-half-turn");
        Advance(loss, TripleTriadController.TradeFlipDuration / 2);
        Advance(loss, TripleTriadController.TradeMoveDuration / 2);
        Render(loss, "loss-moving");
        Advance(loss, TripleTriadController.TradeMoveDuration / 2);
        Render(loss, "loss-card-lost");

        TripleTriadController direct = Create(collection: history, definitions: catalog.Cards, name: catalog.Name);
        int[] owners = [0, 1, 0, 0, 0, 0, 1, 1, 1, 1];
        direct.BeginTradeSettlement(PlayerHand, OpponentHand, owners, TripleTriadTradeRule.Direct, 0, false, [], _ => throw new InvalidOperationException("Rendering must not settle cards."));
        Advance(direct, TripleTriadController.TradeEntranceDuration);
        Advance(direct, TripleTriadController.TradeFlipDuration);
        Advance(direct, TripleTriadController.TradeFlipDuration);
        Advance(direct, TripleTriadController.TradeMoveDuration);
        Render(direct, "direct-card-acquired");
        direct.Navigate(UiKey.Confirm);
        Advance(direct, TripleTriadController.TradeExitDuration);
        Advance(direct, TripleTriadController.TradeMoveDuration);
        Render(direct, "direct-card-lost");

        void Render(TripleTriadController controller, string stage)
        {
            using Bitmap bitmap = new(960, 672, PixelFormat.Format32bppArgb);
            using BattlePreviewCanvas canvas = new(bitmap, directory);
            UiView view = controller.BuildView();
            view.Resize(960, 672);
            view.Draw(canvas, new UiTheme());
            bitmap.Save(Path.Combine(output, "trade-" + stage + ".png"), ImageFormat.Png);
        }
    }

    private static TripleTriadController Create(int winner = 0, Dictionary<int, int>? collection = null, IReadOnlyList<TripleTriadCard>? definitions = null, Func<int, string>? name = null)
    {
        definitions ??= Cards;
        TripleTriadMatch match = TripleTriadMatch.Create(definitions, PlayerHand, OpponentHand);
        for (int cell = 0; cell < 9; cell++)
        {
            int player = match.CurrentPlayer;
            int slot = Enumerable.Range(0, 5).First(index => match.Hands[player][index] is not null);
            match.Play(slot, cell);
        }
        TripleTriadSnapshot snapshot = match.CaptureSnapshot();
        for (int cell = 0; cell < 9; cell++)
        {
            snapshot.Board[cell] = snapshot.Board[cell]! with { Owner = winner };
        }
        Dictionary<int, int> inventory = Enumerable.Range(0, 5).ToDictionary(id => id, _ => 1);
        if (collection is not null)
        {
            foreach ((int id, int count) in collection)
            {
                inventory[id] = count;
            }
        }
        TripleTriadController controller = new(definitions, inventory, OpponentHand, name ?? (id => $"Card {id}"));
        controller.RestoreMatch(TripleTriadMatch.Restore(definitions, snapshot));
        controller.EnablePresentation();
        return controller;
    }

    private static void Advance(TripleTriadController controller, double seconds)
    {
        controller.AdvancePresentation(TimeSpan.FromSeconds(seconds + 0.000001));
    }

    private static void Acknowledge(TripleTriadController controller)
    {
        Advance(controller, TripleTriadController.TradeMoveDuration);
        Assert.True(controller.AwaitingTradeConfirmation);
        controller.Navigate(UiKey.Confirm);
        Advance(controller, TripleTriadController.TradeExitDuration);
    }

    private static RecordingCanvas Draw(UiView view)
    {
        view.Resize(960, 672);
        RecordingCanvas canvas = new();
        view.Draw(canvas, new UiTheme());
        return canvas;
    }

    private sealed class RecordingCanvas : IUiCanvas
    {
        public List<(UiImageSource Source, UiRect Bounds)> Images { get; } = [];
        public List<(string Text, UiColor Color, UiRect Bounds)> Texts { get; } = [];
        public List<UiRect> Pointers { get; } = [];

        public void Fill(UiRect bounds, UiColor color)
        {
        }

        public void Text(string text, float fontSize, UiRect bounds, UiColor color)
        {
            Texts.Add((text, color, bounds));
        }

        public UiSize MeasureText(string text, float fontSize, float maximumWidth) => new(text.Length * fontSize / 2, fontSize);

        public void Text(string text, float fontSize, UiRect bounds, UiColor color, UiAlignment verticalAlignment)
        {
            Text(text, fontSize, bounds, color);
        }

        public void OutlinedText(string text, float fontSize, UiRect bounds, UiColor color, UiColor outline, float thickness)
        {
            Text(text, fontSize, bounds, color);
        }

        public void Image(UiImageSource source, UiRect bounds)
        {
            Images.Add((source, bounds));
        }

        public void MenuPointer(UiRect bounds)
        {
            Pointers.Add(bounds);
        }

        public void Line(float x1, float y1, float x2, float y2, UiColor color, float thickness)
        {
        }

        public void PushClip(UiRect bounds)
        {
        }

        public void PopClip()
        {
        }
    }
}
