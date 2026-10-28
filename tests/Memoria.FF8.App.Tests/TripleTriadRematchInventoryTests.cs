using System.Buffers.Binary;
using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class TripleTriadRematchInventoryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LegacyRequestKeepsOriginalBoardWhileFuturePreferencesUseLargerBoard(bool activeMatch)
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed card resources are required.");
        NativeCardInventory cards = NativeCardInventory.CreateNewGame();
        foreach (int id in Enumerable.Range(0, 5))
        {
            cards = cards.Acquire(id, out _);
        }
        GameSession cold = GameSession.CreateColdProcess();
        cold.StartNewGame();
        GameSession session = new(cold.State with
        {
            Scene = SceneKind.Field,
            FieldId = 600,
            FieldLoadContext = NativeFieldLoadContext.FromOriginalSave(600),
            CardInventory = cards,
            SeeD = null,
            WorldControls = NativeWorldControlState.CreateNewGame() with { EventFlags104 = 0x10 }
        });
        using GameWorld world = CreateWorld(session, directory, TripleTriadTradeRule.One, true);
        Assert.True(world.Events.TalkTo(0, true));
        world.Events.Step();
        if (activeMatch)
        {
            TripleTriadController original = world.EnsureTripleTriad()!;
            for (int tick = 0; tick < 15; tick++)
            {
                world.AdvanceTripleTriadPrompt();
            }
            world.AdvanceTripleTriadPrompt(0);
            for (int tick = 0; tick < 15 && original.AwaitingRules; tick++)
            {
                world.AdvanceTripleTriadPrompt();
            }
            Assert.Equal(5, original.Match!.HandSize);
        }
        session.SetEventState(world.Events.Capture());
        GameplayPreferences futurePreferences = session.GameplayPreferences with { TripleTriad = new TripleTriadSettings { Width = 4, Height = 4, HandSize = 8 } };
        JsonObject legacy = JsonSerializer.SerializeToNode(session.State with { GameplayPreferences = futurePreferences })!.AsObject();
        JsonObject request = legacy["EventState"]!["Scene"]!["TripleTriadRequest"]!.AsObject();
        request.Remove("Width");
        request.Remove("Height");
        request.Remove("HandSize");
        GameSession migrated = new(legacy.Deserialize<GameState>()!);
        using GameWorld restored = CreateWorld(migrated, directory, TripleTriadTradeRule.One, true);
        Assert.Equal(5, restored.Events.PendingTripleTriad!.HandSize);
        TripleTriadController resumed = restored.EnsureTripleTriad()!;
        Assert.Equal((3, 3, 5), (resumed.Options.Width, resumed.Options.Height, resumed.Options.HandSize));
        Assert.Equal((4, 4, 8), (migrated.GameplayPreferences.TripleTriad.Width, migrated.GameplayPreferences.TripleTriad.Height, migrated.GameplayPreferences.TripleTriad.HandSize));
        if (activeMatch)
        {
            Assert.Equal(9, resumed.Match!.Board.Count);
        }
        string path = TestResources.ArtifactFile($"legacy-triad-request-{activeMatch}.json");
        migrated.Save(path);
        _ = GameSession.Load(path);
        foreach (int id in Enumerable.Range(5, 3))
        {
            cards = cards.Acquire(id, out _);
        }
        GameSession future = new(migrated.State with { EventState = null, TripleTriadPlay = null, CardInventory = cards });
        using GameWorld nextWorld = CreateWorld(future, directory, TripleTriadTradeRule.One, true);
        Assert.True(nextWorld.Events.TalkTo(0, true));
        nextWorld.Events.Step();
        Assert.Equal((4, 4, 8), (nextWorld.Events.PendingTripleTriad!.Width, nextWorld.Events.PendingTripleTriad.Height, nextWorld.Events.PendingTripleTriad.HandSize));
        Assert.Equal(8, nextWorld.EnsureTripleTriad()!.Options.HandSize);
    }

    [Theory]
    [InlineData(2, 2)]
    [InlineData(4, 8)]
    [InlineData(5, 13)]
    public void ConfiguredRandomHostPreservesRedrawAndNextTurnAcrossStandaloneReload(int size, int handSize)
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed card resources are required.");
        NativeCardInventory cards = NativeCardInventory.CreateNewGame();
        foreach (int id in Enumerable.Range(0, Math.Max(2, handSize)))
        {
            cards = cards.Acquire(id, out _);
        }
        GameSession cold = GameSession.CreateColdProcess();
        cold.StartNewGame();
        GameSession session = new(cold.State with
        {
            Scene = SceneKind.Field,
            FieldId = 600,
            FieldLoadContext = NativeFieldLoadContext.FromOriginalSave(600),
            CardInventory = cards,
            SeeD = null,
            GameplayPreferences = cold.State.GameplayPreferences with { TripleTriad = new TripleTriadSettings { Width = size, Height = size, HandSize = handSize, RandomRedraws = 2 } },
            WorldControls = NativeWorldControlState.CreateNewGame() with { EventFlags104 = 0x10 }
        });
        using GameWorld world = CreateWorld(session, directory, TripleTriadTradeRule.All, true);
        Assert.True(world.Events.TalkTo(0, true));
        world.Events.Step();
        TripleTriadController controller = world.EnsureTripleTriad()!;
        for (int tick = 0; tick < 15; tick++)
        {
            world.AdvanceTripleTriadPrompt();
        }
        world.AdvanceTripleTriadPrompt(0);
        for (int tick = 0; tick < 15 && controller.AwaitingRules; tick++)
        {
            world.AdvanceTripleTriadPrompt();
        }
        Assert.Equal(size * size, controller.Match!.Board.Count);
        Assert.Equal(handSize, controller.Deck.Count);
        TripleTriadSnapshot opening = controller.Match.CaptureSnapshot();
        uint openingRandom = session.State.TripleTriadPlay!.RandomState;
        TripleTriadPreparedHand expectedRedraw = TripleTriadPreparation.SelectRandomHand(cards, openingRandom, handSize);
        Assert.True(controller.Redraw());
        Assert.Equal(opening.StartingPlayer, controller.Match.StartingPlayer);
        Assert.Equal(opening.BoardElements, controller.Match.BoardElements);
        Assert.Equal(expectedRedraw.RandomState, session.State.TripleTriadPlay!.RandomState);
        Assert.Equal(1, controller.RedrawsRemaining);
        Assert.Equal(1, session.State.TripleTriadPlay!.RedrawsUsed);
        string path = TestResources.ArtifactFile($"configured-triad-{size}-redraw.json");
        session.Save(path);
        GameSession loadedSession = new(GameSession.Load(path));
        using GameWorld loadedWorld = CreateWorld(loadedSession, directory, TripleTriadTradeRule.All, true);
        TripleTriadController loaded = loadedWorld.EnsureTripleTriad()!;
        Assert.Equal(1, loaded.RedrawsRemaining);
        Assert.Equal(controller.Deck, loaded.Deck);
        Assert.Equal(JsonSerializer.Serialize(controller.Match.CaptureSnapshot()), JsonSerializer.Serialize(loaded.Match!.CaptureSnapshot()));
        Assert.True(controller.Redraw());
        Assert.True(loaded.Redraw());
        Assert.Equal(0, loaded.RedrawsRemaining);
        Assert.False(loaded.Redraw());
        Assert.Equal(JsonSerializer.Serialize(controller.Match!.CaptureSnapshot()), JsonSerializer.Serialize(loaded.Match!.CaptureSnapshot()));
        Assert.Equal(session.State.TripleTriadPlay.RandomState, loadedSession.State.TripleTriadPlay!.RandomState);
        if (controller.Match.CurrentPlayer == 1)
        {
            Assert.True(controller.AdvanceOpponent());
            Assert.True(loaded.AdvanceOpponent());
        }
        else
        {
            TripleTriadMove move = controller.Match.ChooseMove();
            controller.Play(move.HandIndex, move.CellIndex);
            loaded.Play(move.HandIndex, move.CellIndex);
        }
        Assert.False(loaded.CanRedraw);
        Assert.Equal(JsonSerializer.Serialize(controller.Match.CaptureSnapshot()), JsonSerializer.Serialize(loaded.Match.CaptureSnapshot()));
        Assert.Equal(cards.Cards, loadedSession.CardInventory!.Cards);
    }

    [Theory]
    [InlineData(TripleTriadTradeRule.One, false, false)]
    [InlineData(TripleTriadTradeRule.All, false, false)]
    [InlineData(TripleTriadTradeRule.Direct, false, false)]
    [InlineData(TripleTriadTradeRule.One, true, false)]
    [InlineData(TripleTriadTradeRule.All, true, false)]
    [InlineData(TripleTriadTradeRule.Direct, true, false)]
    [InlineData(TripleTriadTradeRule.One, false, true)]
    [InlineData(TripleTriadTradeRule.All, false, true)]
    [InlineData(TripleTriadTradeRule.Direct, false, true)]
    public void ConsecutiveHostMatchesPublishExactlyOneSettlementAndRestoreFreshControllers(TripleTriadTradeRule rule, bool strongDeck, bool randomDeck)
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed card resources are required.");
        TripleTriadCatalog catalog = TripleTriadCatalog.Load(directory);
        NativeCardInventory cards = NativeCardInventory.CreateNewGame();
        foreach (int id in Enumerable.Range(0, 20).Append(0).Append(84).Concat(Enumerable.Range(105, 5)))
        {
            cards = cards.Acquire(id, out _);
        }
        cards = cards.Transfer(85, 121, out _);
        GameSession cold = GameSession.CreateColdProcess();
        cold.StartNewGame();
        GameSession session = new(cold.State with
        {
            Scene = SceneKind.Field,
            FieldId = 600,
            FieldLoadContext = NativeFieldLoadContext.FromOriginalSave(600),
            CardInventory = cards,
            SeeD = null,
            WorldControls = NativeWorldControlState.CreateNewGame() with { EventFlags104 = 0x10 }
        });
        using GameWorld world = CreateWorld(session, directory, rule, randomDeck);
        Assert.True(world.Events.TalkTo(0, true));
        world.Events.Step();
        TripleTriadController controller = world.EnsureTripleTriad()!;
        for (int tick = 0; tick < 15; tick++)
        {
            world.AdvanceTripleTriadPrompt();
        }
        world.AdvanceTripleTriadPrompt(0);
        for (int tick = 0; tick < 15 && controller.AwaitingRules; tick++)
        {
            world.AdvanceTripleTriadPrompt();
        }
        Assert.False(controller.AwaitingRules);
        long frame = world.Events.Frame;
        for (int round = 0; round < 3; round++)
        {
            IEnumerable<int> ids = strongDeck ? Enumerable.Range(0, 110).Reverse() : Enumerable.Range(0, 110);
            int[] selected = randomDeck ? controller.Deck.ToArray() : ids.SelectMany(id => Enumerable.Repeat(id, Math.Max(0, session.CardInventory!.AvailableCount(id)))).Take(5).ToArray();
            foreach (int id in selected)
            {
                controller.ChooseCard(id);
            }
            TripleTriadMatch match = controller.Match!;
            for (int turn = 0; turn < 9; turn++)
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
            if (round == 0 && !randomDeck)
            {
                Assert.Equal(strongDeck ? TripleTriadOutcome.PlayerWin : TripleTriadOutcome.OpponentWin, match.Outcome);
            }
            NativeTripleTriadPlayState play = session.State.TripleTriadPlay!;
            int[] slots = rule == TripleTriadTradeRule.Direct || match.Outcome == TripleTriadOutcome.Draw ? [] : rule == TripleTriadTradeRule.All ? [0, 1, 2, 3, 4] : [0];
            if (rule == TripleTriadTradeRule.One && match.Outcome == TripleTriadOutcome.OpponentWin)
            {
                slots = [Enumerable.Range(0, 5).OrderByDescending(slot => catalog.TradePriority(selected[slot])).ThenBy(slot => slot).First()];
            }
            int result = match.Outcome == TripleTriadOutcome.PlayerWin ? 0 : match.Outcome == TripleTriadOutcome.OpponentWin ? 1 : 2;
            TripleTriadTradeResult settlement = TripleTriadTrade.Settle(session.CardInventory!, selected, play.OpponentHand, match.OriginalInstanceFinalOwners, rule, 121, slots);
            NativeCardInventory expected = settlement.Inventory.CompleteMatch(result, play.RandomState);
            controller.Navigate(UiKey.Confirm);
            if (!controller.AwaitingReplay)
            {
                controller.SelectTradeCard(0);
            }
            Assert.True(controller.AwaitingReplay);
            Assert.True(controller.CanReplay);
            controller.Navigate(UiKey.Confirm);
            TripleTriadController replay = world.EnsureTripleTriad()!;
            Assert.NotSame(controller, replay);
            string achievementContext = "cards/" + play.RequestIdentity + "/" + play.InitialRandomState.ToString(System.Globalization.CultureInfo.InvariantCulture);
            Assert.Equal(settlement.LostCardIds.Any(id => id >= NativeCardInventory.CommonCardCount), session.State.PendingOriginalAchievementActions.Any(fact => fact.ContextId.Value.StartsWith(achievementContext + "/actions/", StringComparison.Ordinal) && fact.Kind == OriginalAchievementActionKind.RareCardLost));
            Assert.DoesNotContain(session.State.PendingOriginalAchievementActions, fact => fact.Kind == OriginalAchievementActionKind.CardsClubMaster);
            Assert.Contains(session.State.PendingOriginalAchievementActions, fact => fact.Kind == OriginalAchievementActionKind.CardPlayed);
            Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(session.CardInventory));
            Assert.Equal(round + 1, expected.TripleTriadStatistics!.Wins + expected.TripleTriadStatistics.Losses + expected.TripleTriadStatistics.Draws);
            Assert.Equal(frame, world.Events.Frame);
            Assert.Equal(randomDeck ? 5 : 0, replay.Deck.Count);
            Assert.Equal(randomDeck, replay.Match is not null);
            if (randomDeck)
            {
                NativeTripleTriadPlayState nextPlay = session.State.TripleTriadPlay!;
                TripleTriadOpponentHand nextOpponent = TripleTriadOpponentGenerator.Generate(expected, 121, 200, 127, expected.TripleTriadRandomState!.Value);
                TripleTriadPreparedHand nextPlayer = TripleTriadPreparation.SelectRandomHand(expected, nextOpponent.RandomState);
                TripleTriadPreparedBoard nextBoard = TripleTriadPreparation.GenerateElements(replay.Rules, nextPlayer.RandomState);
                TripleTriadStartingSide nextStarter = TripleTriadPreparation.TossStartingSide(nextBoard.RandomState);
                TripleTriadMatch freshMatch = TripleTriadMatch.Create(catalog.Cards, nextPlayer.Cards, nextOpponent.Cards, replay.Rules, 1 - nextStarter.NativeSide, nextBoard.Elements);
                Assert.Equal(nextPlayer.Cards, replay.Deck);
                Assert.Equal(nextStarter.RandomState, nextPlay.RandomState);
                Assert.Equal(JsonSerializer.Serialize(freshMatch.CaptureSnapshot()), JsonSerializer.Serialize(replay.Match!.CaptureSnapshot()));
                GameSession beforeSelection = new(session.State with { TripleTriadPlay = nextPlay with { SelectedCards = [], Match = null, RandomState = nextOpponent.RandomState } });
                using GameWorld resumedSelection = CreateWorld(beforeSelection, directory, rule, randomDeck);
                Assert.Equal(nextPlayer.Cards, resumedSelection.EnsureTripleTriad()!.Deck);
                Assert.Equal(JsonSerializer.Serialize(freshMatch.CaptureSnapshot()), JsonSerializer.Serialize(resumedSelection.ActiveTripleTriad!.Match!.CaptureSnapshot()));
            }
            Assert.Null(replay.TradingHand);
            Assert.False(replay.AwaitingRules);
            Assert.False(replay.AwaitingReplay);
            Assert.False(replay.IsAnimating);
            Assert.False(replay.ChoosingCell);
            Assert.Equal(TripleTriadTradePhase.None, replay.TradePhase);
            Assert.NotNull(replay.CreateMatch);
            Assert.NotNull(replay.ChooseOpponentMove);
            Assert.NotNull(replay.RestartDraw);
            Assert.Equal(0, replay.PromptFade);
            Assert.Equal(0, replay.Selection);
            Assert.Equal(0, replay.SelectedHand);
            Assert.Equal(4, replay.SelectedCell);
            foreach (int id in Enumerable.Range(0, 110))
            {
                Assert.Equal(expected.AvailableCount(id) - replay.Deck.Count(card => card == id), replay.Remaining(id));
            }
            GameSession reloaded = new(JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State))!);
            using GameWorld restored = CreateWorld(reloaded, directory, rule, randomDeck);
            TripleTriadController loaded = restored.EnsureTripleTriad()!;
            Assert.Equal(replay.Available, loaded.Available);
            Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(reloaded.CardInventory));
            Assert.Equal(replay.Deck, loaded.Deck);
            Assert.Equal(randomDeck, loaded.Match is not null);
            int[] nextHand = Enumerable.Range(0, 110).SelectMany(id => Enumerable.Repeat(id, Math.Max(0, expected.AvailableCount(id)))).Take(5).ToArray();
            if (!randomDeck)
            {
                loaded.ChooseCard(nextHand[0]);
            }
            GameSession selectedReload = new(JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(reloaded.State))!);
            using GameWorld selectedWorld = CreateWorld(selectedReload, directory, rule, randomDeck);
            TripleTriadController selectedController = selectedWorld.EnsureTripleTriad()!;
            Assert.Equal(loaded.Deck, selectedController.Deck);
            foreach (int id in nextHand.Skip(1))
            {
                loaded.ChooseCard(id);
                selectedController.ChooseCard(id);
            }
            Assert.Equal(JsonSerializer.Serialize(loaded.Match!.CaptureSnapshot()), JsonSerializer.Serialize(selectedController.Match!.CaptureSnapshot()));
            Assert.Equal(reloaded.State.TripleTriadPlay!.RandomState, selectedReload.State.TripleTriadPlay!.RandomState);
            controller.Navigate(UiKey.Confirm);
            Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(session.CardInventory));
            controller = replay;
        }
    }

    private static GameWorld CreateWorld(GameSession session, string directory, TripleTriadTradeRule rule, bool randomDeck)
    {
        GameWorld world = new(session, directory);
        List<(Jsm.Opcode Opcode, int Parameter)> operations = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 8), (Jsm.Opcode.LBL, 1), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8), (Jsm.Opcode.LBL, 2)];
        operations.AddRange(new[] { 121, randomDeck ? 9 : 1, (int)rule, 200, 7, 4, 127 }.Select(value => (Jsm.Opcode.PSHN_L, value)));
        operations.AddRange([(Jsm.Opcode.CARDGAME, 0), (Jsm.Opcode.RET, 8)]);
        byte[] bytes = new byte[18 + operations.Count * 4];
        bytes[3] = 1;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4), 10);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(6), 18);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(12), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(14), 5);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(16), (ushort)operations.Count);
        for (int index = 0; index < operations.Count; index++)
        {
            var operation = operations[index];
            int encoded = operation.Parameter != 0 || operation.Opcode == Jsm.Opcode.PSHN_L ? (int)operation.Opcode << 24 | operation.Parameter & 0xFFFFFF : (int)operation.Opcode;
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(18 + index * 4), encoded);
        }
        world.Events.LoadField(600, bytes, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        if (session.State.EventState is { } snapshot)
        {
            world.Events.Restore(snapshot);
        }
        else
        {
            world.Events.Step();
        }
        return world;
    }
}
