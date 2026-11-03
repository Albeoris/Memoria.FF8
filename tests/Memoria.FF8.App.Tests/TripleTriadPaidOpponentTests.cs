using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class TripleTriadPaidOpponentTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PaidItemMatchReturnsToItsFieldScriptWithoutOfferingReplay(bool useSimpleOpponent)
    {
        string directory = TestResources.GameDirectory;
        string source = TestResources.FixturePath("App.Tests.TripleTriadPaidOpponentTests.1");
        GameSession session = new(GameSession.Load(source));
        using GameWorld world = new(session, directory);
        world.EnsureField();
        Assert.Equal(965, world.Events.FieldId);
        Assert.Null(world.Events.PendingTripleTriad);
        uint gil = session.State.Economy!.Gil;
        string items = JsonSerializer.Serialize(session.State.Economy.Items);
        TripleTriadStatistics statistics = session.CardInventory!.TripleTriadStatistics!;
        Assert.True(world.Events.TalkTo(15, true));
        for (int callback = 0; callback < 180 && world.Events.PendingTripleTriad is null; callback++)
        {
            AdvanceField(world);
        }
        NativeTripleTriadRequest request = Assert.IsType<NativeTripleTriadRequest>(world.Events.PendingTripleTriad);
        Assert.Equal((965, 15, 72, 1261, 41), (request.FieldId, request.ObjectId, request.ScriptId, request.Operation, (int)request.OpponentOwner));
        TripleTriadController controller = world.EnsureTripleTriad()!;
        for (int callback = 0; callback < 60 && controller.AwaitingRules; callback++)
        {
            world.AdvanceTripleTriadPrompt(0);
        }
        Assert.False(controller.AwaitingRules);
        if (controller.Match is null)
        {
            int[] hand = Enumerable.Range(0, 110).Reverse().SelectMany(id => Enumerable.Repeat(id, Math.Max(0, session.CardInventory.AvailableCount(id)))).Take(controller.Options.HandSize).ToArray();
            foreach (int card in hand)
            {
                controller.ChooseCard(card);
            }
        }
        controller.ConfirmOpening();
        if (useSimpleOpponent)
        {
            controller.ChooseOpponentMove = current => new TripleTriadMove(Enumerable.Range(0, current.HandSize).First(slot => current.Hands[1][slot] is not null), Enumerable.Range(0, current.Board.Count).First(cell => current.Board[cell] is null && !current.BlockedCells.Contains(cell)));
        }
        TripleTriadMatch match = controller.Match!;
        for (int turn = 0; turn < 100 && match.Outcome == TripleTriadOutcome.InProgress; turn++)
        {
            if (match.CurrentPlayer == 0)
            {
                TripleTriadMove move = match.ChooseMove();
                controller.Play(move.HandIndex, move.CellIndex);
            }
            else
            {
                Assert.True(controller.AdvanceOpponent());
            }
        }
        Assert.NotEqual(TripleTriadOutcome.InProgress, match.Outcome);
        controller.Navigate(UiKey.Confirm);
        if (controller.TradingHand is not null)
        {
            for (int slot = 0; slot < match.HandSize && world.Events.PendingTripleTriad is not null; slot++)
            {
                controller.SelectTradeCard(slot);
            }
        }
        Assert.False(controller.AwaitingReplay);
        Assert.Null(world.Events.PendingTripleTriad);
        Assert.Null(world.ActiveTripleTriad);
        Assert.Null(session.State.TripleTriadPlay);
        for (int callback = 0; callback < 180 && (world.Events.IsDialogueOpen || !world.Events.IsUserControlEnabled); callback++)
        {
            AdvanceField(world);
        }
        Assert.False(world.Events.IsDialogueOpen);
        Assert.True(world.Events.IsUserControlEnabled);
        Assert.Equal(gil - 500, session.State.Economy!.Gil);
        if (useSimpleOpponent)
        {
            Assert.Equal(TripleTriadOutcome.PlayerWin, match.Outcome);
            Assert.NotEqual(items, JsonSerializer.Serialize(session.State.Economy.Items));
        }
        TripleTriadStatistics after = session.CardInventory.TripleTriadStatistics!;
        Assert.Equal(statistics.Wins + statistics.Losses + statistics.Draws + 1, after.Wins + after.Losses + after.Draws);
        string settled = JsonSerializer.Serialize(session.CardInventory);
        controller.Navigate(UiKey.Confirm);
        controller.Navigate(UiKey.Cancel);
        Assert.Equal(settled, JsonSerializer.Serialize(session.CardInventory));
        session.SetEventState(world.Events.Capture());
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_TRIAD_OUTPUT_DIRECTORY");
        string name = useSimpleOpponent ? "paid-item-match-controlled-win.json" : "paid-item-match-complete.json";
        string destination = output is null ? TestResources.ArtifactFile(name) : Path.Combine(output, name);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        session.Save(destination, compact: true);
        GameSession loaded = new(GameSession.Load(destination));
        using GameWorld restored = new(loaded, directory);
        restored.EnsureField();
        Assert.Null(restored.Events.PendingTripleTriad);
        Assert.Null(restored.EnsureTripleTriad());
        Assert.Equal(gil - 500, loaded.State.Economy!.Gil);
        Assert.Equal(settled, JsonSerializer.Serialize(loaded.CardInventory));
        Console.WriteLine($"Completed paid match: {match.Outcome}; field {world.Events.FieldId}, frame {world.Events.Frame}; {destination}");
    }

    private static void AdvanceField(GameWorld world)
    {
        if (world.Events.CurrentDialogue is { CanConfirm: true } dialogue)
        {
            world.Events.ConfirmDialogue(dialogue.FirstChoice);
        }
        else
        {
            world.Events.Step();
        }
    }
}
