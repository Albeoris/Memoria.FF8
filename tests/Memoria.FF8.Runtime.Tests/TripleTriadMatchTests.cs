using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class TripleTriadMatchTests
{
    [Theory]
    [InlineData(true, 0)]
    [InlineData(false, 0)]
    [InlineData(true, 1)]
    [InlineData(false, 1)]
    public void InstantResultPreservesInstancesAndRestoresThroughJson(bool victory, int startingPlayer)
    {
        TripleTriadCard[] cards = Catalog();
        int[] playerHand = [0, 0, 1, 2, 3];
        int[] opponentHand = [4, 5, 6, 7, 8];
        TripleTriadMatch match = TripleTriadMatch.Create(cards, playerHand, opponentHand, TripleTriadRules.Same | TripleTriadRules.Plus, startingPlayer);
        match.Play(2, 4);
        TripleTriadPlacedCard original = match.Board[4]!;
        match.CompleteInstantly(victory);

        int winner = victory ? 0 : 1;
        Assert.Equal(victory ? TripleTriadOutcome.PlayerWin : TripleTriadOutcome.OpponentWin, match.Outcome);
        Assert.All(match.Board, card => Assert.Equal(winner, card!.Owner));
        Assert.Equal(original with { Owner = winner }, match.Board[4]);
        Assert.Single(match.Hands.SelectMany(hand => hand), card => card.HasValue);
        Assert.Equal(10, match.PlayerScore + match.OpponentScore);
        Assert.Equal(match.PlayerScore, match.OriginalInstanceFinalOwners.Count(owner => owner == 0));
        Assert.Equal(playerHand, match.InitialHands[0]);
        Assert.Equal(opponentHand, match.InitialHands[1]);
        Assert.Empty(match.GetLegalMoves());

        string json = JsonSerializer.Serialize(match.CaptureSnapshot());
        TripleTriadMatch restored = TripleTriadMatch.Restore(cards, JsonSerializer.Deserialize<TripleTriadSnapshot>(json)!);
        Assert.Equal(json, JsonSerializer.Serialize(restored.CaptureSnapshot()));
        Assert.Equal(match.Outcome, restored.Outcome);
        Assert.Equal(match.OriginalInstanceFinalOwners, restored.OriginalInstanceFinalOwners);
        Assert.Throws<InvalidOperationException>(() => restored.CompleteInstantly(!victory));

        NativeCardInventory inventory = NativeCardInventory.CreateNewGame();
        foreach (int card in playerHand)
        {
            inventory = inventory.Acquire(card, out _);
        }
        TripleTriadTradeResult trade = TripleTriadTrade.Settle(inventory, playerHand, opponentHand, restored.OriginalInstanceFinalOwners, TripleTriadTradeRule.Direct, 121);
        int[] owners = restored.OriginalInstanceFinalOwners.ToArray();
        Assert.Equal(opponentHand.Where((_, slot) => owners[5 + slot] == 0), trade.GainedCardIds);
        Assert.Equal(playerHand.Where((_, slot) => owners[slot] == 1), trade.LostCardIds);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void InstantResultPreservesSuddenDeathOriginalInstanceOrder(bool victory)
    {
        TripleTriadCard[] cards = Catalog();
        TripleTriadMatch match = TripleTriadMatch.Create(cards, [0, 1, 2, 3, 4], [5, 6, 7, 8, 9], TripleTriadRules.SuddenDeath);
        for (int cell = 0; cell < 9; cell++)
        {
            match.Play(cell / 2, cell);
        }
        Assert.Equal(TripleTriadOutcome.Draw, match.Outcome);
        match.RestartSuddenDeath(1);
        int[] instanceOrder = match.CaptureSnapshot().InstanceOrder;
        match.CompleteInstantly(victory);
        TripleTriadMatch restored = TripleTriadMatch.Restore(cards, match.CaptureSnapshot());
        Assert.Equal(1, restored.Round);
        Assert.Equal(instanceOrder, restored.CaptureSnapshot().InstanceOrder);
        Assert.Equal(restored.PlayerScore, restored.OriginalInstanceFinalOwners.Count(owner => owner == 0));
        Assert.All(restored.Board, card => Assert.Equal(victory ? 0 : 1, card!.Owner));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(7)]
    public void BasicCaptureUsesOppositeSidesAndNeverWrapsRows(int neighbor)
    {
        TripleTriadCard[] cards = Catalog();
        cards[1] = new(1, 6, 6, 6, 6, TripleTriadElement.None);
        TripleTriadMatch match = Position(cards, TripleTriadRules.None, [(0, 0), (neighbor, 1)]);
        TripleTriadMoveResult move = match.Play(1, 4);
        Assert.Equal(new[] { neighbor }, move.CapturedCells);
        Assert.Equal(0, match.Board[neighbor]!.Owner);
        Assert.Equal(6, match.PlayerScore);
        Assert.Equal(4, match.OpponentScore);
        Assert.Equal(10, match.PlayerScore + match.OpponentScore);
    }

    [Fact]
    public void EqualRanksDoNotCaptureAndOrdinaryCapturesDoNotStartCombos()
    {
        TripleTriadCard[] cards = Catalog();
        cards[2] = new(2, 6, 6, 6, 6, TripleTriadElement.None);
        cards[5] = new(5, 5, 9, 5, 5, TripleTriadElement.None);
        TripleTriadMatch match = Position(cards, TripleTriadRules.None, [(0, 0), (1, 1), (8, 0), (2, 1)]);
        TripleTriadMoveResult move = match.Play(2, 4);
        Assert.Equal(0, match.Board[1]!.Owner);
        Assert.Equal(1, match.Board[2]!.Owner);
        Assert.False(move.Combo);
        TripleTriadMatch equal = TripleTriadMatch.Create(Catalog(), [0, 1, 2, 3, 4], [5, 6, 7, 8, 9]);
        equal.Play(0, 0);
        Assert.Empty(equal.Play(0, 1).CapturedCells);
    }

    [Fact]
    public void SameAllowsFriendlyMatchAndChainsOnlyFromCapturedCards()
    {
        TripleTriadCard[] cards = Catalog();
        cards[2] = new(2, 5, 5, 5, 5, TripleTriadElement.None);
        cards[5] = new(5, 5, 9, 5, 5, TripleTriadElement.None);
        TripleTriadMatch match = Position(cards, TripleTriadRules.Same, [(3, 0), (1, 1), (8, 0), (2, 1)]);
        TripleTriadMoveResult move = match.Play(2, 4);
        Assert.True(move.Same);
        Assert.True(move.Combo);
        Assert.Equal(new[] { 1, 2 }, move.CapturedCells);
        Assert.Equal(new[] { new TripleTriadCapture(1, 4, 0), new TripleTriadCapture(2, 1, 1) }, move.Captures);
        Assert.Equal(0, match.Board[2]!.Owner);
    }

    [Fact]
    public void NativeAiCapturePassOmitsSourcePromotionWhileGameplayStillPropagatesCombo()
    {
        TripleTriadCard[] cards = Catalog();
        cards[2] = new(2, 5, 5, 5, 5, TripleTriadElement.None);
        cards[5] = new(5, 5, 9, 5, 5, TripleTriadElement.None);
        TripleTriadMatch gameplay = Position(cards, TripleTriadRules.Same, [(3, 0), (1, 1), (8, 0), (2, 1)]);
        TripleTriadMatch search = TripleTriadMatch.Restore(cards, gameplay.CaptureSnapshot());
        var searchMove = typeof(TripleTriadMatch).GetMethod("PlayForNativeAi", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        TripleTriadMoveResult searchResult = (TripleTriadMoveResult)searchMove.Invoke(search, [2, 4])!;
        TripleTriadMoveResult gameplayResult = gameplay.Play(2, 4);
        Assert.True(searchResult.Same);
        Assert.False(searchResult.Combo);
        Assert.True(gameplayResult.Combo);
        Assert.Equal(1, search.Board[2]!.Owner);
        Assert.Equal(0, gameplay.Board[2]!.Owner);
    }

    [Fact]
    public void NativeSameThresholdCanUseTwoFriendlyCardsAndPromotesPrintedStrongerEnemyToCombo()
    {
        TripleTriadCard[] cards = Catalog();
        cards[3] = new(3, 5, 6, 5, 5, TripleTriadElement.None);
        cards[7] = new(7, 5, 5, 9, 5, TripleTriadElement.None);
        TripleTriadMatch match = Position(cards, TripleTriadRules.Same, [(1, 0), (0, 1), (3, 0), (8, 1), (6, 0), (5, 1)]);
        TripleTriadMoveResult move = match.Play(3, 4);
        Assert.True(move.Same);
        Assert.True(move.Combo);
        Assert.Equal(0, match.Board[5]!.Owner);
        Assert.Equal(0, match.Board[8]!.Owner);
    }

    [Fact]
    public void NativePlusBreaksTwoPairTieByFirstPairToReachTwoRatherThanFirstSumSeen()
    {
        TripleTriadCard[] cards = Catalog();
        cards[2] = new(2, 2, 3, 4, 5, TripleTriadElement.None);
        cards[0] = new(0, 5, 5, 5, 5, TripleTriadElement.None);
        cards[5] = new(5, 5, 5, 7, 5, TripleTriadElement.None);
        cards[1] = new(1, 6, 5, 5, 5, TripleTriadElement.None);
        cards[6] = new(6, 5, 5, 5, 6, TripleTriadElement.None);
        TripleTriadMatch match = Position(cards, TripleTriadRules.Plus, [(3, 1), (1, 1), (7, 0), (5, 1)]);
        TripleTriadMoveResult move = match.Play(2, 4);
        // Native direction order gives sums 9,10,10,9: the bottom/left pair reaches two first.
        Assert.True(move.Plus);
        Assert.Contains(3, move.CapturedCells);
        Assert.DoesNotContain(1, move.CapturedCells);
        Assert.DoesNotContain(5, move.CapturedCells);
    }

    [Fact]
    public void PlusChoosesFirstGreatestSumGroupInNativeDirectionOrderIncludingFriendlySupport()
    {
        TripleTriadCard[] cards = Catalog();
        cards[2] = new(2, 2, 3, 4, 5, TripleTriadElement.None);
        cards[0] = new(0, 5, 4, 5, 5, TripleTriadElement.None);
        cards[5] = new(5, 5, 5, 7, 5, TripleTriadElement.None);
        cards[1] = new(1, 6, 5, 5, 5, TripleTriadElement.None);
        cards[6] = new(6, 5, 5, 5, 7, TripleTriadElement.None);
        TripleTriadMatch match = Position(cards, TripleTriadRules.Plus, [(3, 0), (1, 1), (7, 0), (5, 1)]);
        TripleTriadMoveResult move = match.Play(2, 4);
        Assert.True(move.Plus);
        Assert.Equal(new[] { 1 }, move.CapturedCells);
        Assert.Equal(1, match.Board[5]!.Owner);
    }

    [Fact]
    public void SameWallRequiresSameAndTreatsAnOutsideEdgeAsTen()
    {
        TripleTriadCard[] cards = Catalog();
        cards[1] = new(1, 10, 5, 5, 1, TripleTriadElement.None);
        TripleTriadRules[] rules = [TripleTriadRules.SameWall, TripleTriadRules.Same, TripleTriadRules.Same | TripleTriadRules.SameWall, TripleTriadRules.Plus | TripleTriadRules.SameWall];
        foreach (TripleTriadRules rule in rules)
        {
            TripleTriadMatch match = Position(cards, rule, [(8, 0), (1, 1)]);
            TripleTriadMoveResult move = match.Play(1, 0);
            Assert.Equal(rule == (TripleTriadRules.Same | TripleTriadRules.SameWall), move.Same);
            Assert.Equal(move.Same ? 0 : 1, match.Board[1]!.Owner);
        }
    }

    [Fact]
    public void ElementalUsesPrintedRanksForSameAndModifiedRanksForCombo()
    {
        TripleTriadCard[] cards = Catalog();
        cards[2] = new(2, 5, 5, 5, 5, TripleTriadElement.Fire);
        cards[5] = new(5, 5, 5, 5, 5, TripleTriadElement.Fire);
        TripleTriadElement[] elements = new TripleTriadElement[9];
        elements[4] = TripleTriadElement.Ice;
        elements[1] = TripleTriadElement.Fire;
        TripleTriadMatch match = Position(cards, TripleTriadRules.Same | TripleTriadRules.Elemental, [(3, 0), (1, 1), (8, 0), (2, 1)], elements);
        TripleTriadMoveResult move = match.Play(2, 4);
        Assert.True(move.Same);
        Assert.True(move.Combo);
        Assert.Equal(0, match.Board[2]!.Owner);
    }

    [Fact]
    public void ElementalMismatchingAndElementlessCardsLoseOneAndMatchingCardsGainOne()
    {
        foreach (TripleTriadElement element in new[] { TripleTriadElement.None, TripleTriadElement.Ice, TripleTriadElement.Fire })
        {
            TripleTriadCard[] cards = Catalog();
            cards[1] = new(1, 5, 5, 5, 5, element);
            TripleTriadElement[] elements = new TripleTriadElement[9];
            elements[4] = TripleTriadElement.Fire;
            TripleTriadMatch match = Position(cards, TripleTriadRules.Elemental, [(0, 0), (1, 1)], elements);
            match.Play(1, 4);
            Assert.Equal(element == TripleTriadElement.Fire ? 0 : 1, match.Board[1]!.Owner);
        }
    }

    [Fact]
    public void OpenOnlyChangesOpponentHandVisibility()
    {
        foreach (TripleTriadRules rules in new[] { TripleTriadRules.None, TripleTriadRules.Open })
        {
            TripleTriadMatch match = TripleTriadMatch.Create(Catalog(), [0, 1, 2, 3, 4], [5, 6, 7, 8, 9], rules);
            Assert.Equal(new int?[] { 0, 1, 2, 3, 4 }, match.GetVisibleHand(0, 0));
            Assert.Equal(rules.HasFlag(TripleTriadRules.Open) ? new int?[] { 5, 6, 7, 8, 9 } : new int?[5], match.GetVisibleHand(0, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => match.GetVisibleHand(2, 0));
        }
    }

    [Fact]
    public void SuddenDeathRedistributesAllTenInstancesByFinalOwnerInOriginalInstanceOrder()
    {
        TripleTriadCard[] cards = Catalog();
        TripleTriadMatch original = TripleTriadMatch.Create(cards, [0, 1, 2, 3, 4], [5, 6, 7, 8, 9], TripleTriadRules.SuddenDeath);
        for (int cell = 0; cell < 9; cell++)
        {
            original.Play(cell / 2, cell);
        }
        Assert.Equal(TripleTriadOutcome.Draw, original.Outcome);
        Assert.Equal(5, original.PlayerScore);
        Assert.Equal(5, original.OpponentScore);
        TripleTriadSnapshot snapshot = original.CaptureSnapshot();
        snapshot.Board[0] = snapshot.Board[0]! with { Owner = 1 };
        snapshot.Board[1] = snapshot.Board[1]! with { Owner = 0 };
        TripleTriadMatch match = TripleTriadMatch.Restore(cards, snapshot);
        Assert.True(match.CanRestartSuddenDeath);
        match.RestartSuddenDeath(1);
        Assert.Equal(new[] { 5, 1, 2, 3, 4 }, match.InitialHands[0]);
        Assert.Equal(new[] { 6, 7, 8, 9, 0 }, match.InitialHands[1]);
        Assert.Equal(new[] { 1, 0, 0, 0, 0, 0, 1, 1, 1, 1 }, match.OriginalInstanceFinalOwners);
        Assert.Equal(1, match.Round);
        Assert.Equal(1, match.CurrentPlayer);
        Assert.Equal(TripleTriadOutcome.InProgress, match.Outcome);
        Assert.All(match.Board, Assert.Null);
        Assert.Throws<InvalidOperationException>(() => match.RestartSuddenDeath(0));
        for (int cell = 0; cell < 9; cell++)
        {
            match.Play(cell / 2, cell);
        }
        match = TripleTriadMatch.Restore(cards, JsonSerializer.Deserialize<TripleTriadSnapshot>(JsonSerializer.Serialize(match.CaptureSnapshot()))!);
        match.RestartSuddenDeath(match.CurrentPlayer);
        Assert.Equal(2, match.Round);
        Assert.Equal(new[] { 5, 1, 2, 3, 4 }, match.InitialHands[0]);
        Assert.Equal(new[] { 6, 7, 8, 9, 0 }, match.InitialHands[1]);
        Assert.Equal(new[] { 1, 0, 0, 0, 0, 0, 1, 1, 1, 1 }, match.OriginalInstanceFinalOwners);
    }

    [Fact]
    public void SnapshotsRoundTripAndCopiesCannotMutateLiveState()
    {
        TripleTriadCard[] cards = Catalog();
        TripleTriadMatch match = TripleTriadMatch.Create(cards, [0, 1, 2, 3, 4], [5, 6, 7, 8, 9]);
        match.Play(0, 4);
        TripleTriadSnapshot snapshot = match.CaptureSnapshot();
        string json = JsonSerializer.Serialize(snapshot);
        TripleTriadMatch restored = TripleTriadMatch.Restore(cards, JsonSerializer.Deserialize<TripleTriadSnapshot>(json)!);
        Assert.Equal(json, JsonSerializer.Serialize(restored.CaptureSnapshot()));
        Assert.Equal(match.Play(0, 0), restored.Play(0, 0), new MoveResultComparer());
        snapshot.Hands[0][1] = 109;
        snapshot.InitialHands[0][1] = 109;
        snapshot.Board[4] = null;
        Assert.Equal(1, match.Hands[0][1]);
        Assert.NotNull(match.Board[4]);
        snapshot = restored.CaptureSnapshot();
        TripleTriadMatch separate = TripleTriadMatch.Restore(cards, snapshot);
        snapshot.Hands[0][1] = 109;
        Assert.Equal(1, separate.Hands[0][1]);
    }

    [Fact]
    public void MalformedSnapshotsCatalogsAndMovesAreRejectedWithoutMutation()
    {
        TripleTriadCard[] cards = Catalog();
        TripleTriadMatch match = TripleTriadMatch.Create(cards, [0, 1, 2, 3, 4], [5, 6, 7, 8, 9]);
        match.Play(0, 0);
        string before = JsonSerializer.Serialize(match.CaptureSnapshot());
        Assert.Throws<ArgumentException>(() => match.Play(-1, 1));
        Assert.Throws<ArgumentException>(() => match.Play(0, 9));
        Assert.Throws<ArgumentException>(() => match.Play(0, 0));
        Assert.Equal(before, JsonSerializer.Serialize(match.CaptureSnapshot()));
        TripleTriadSnapshot snapshot = match.CaptureSnapshot();
        Assert.Throws<InvalidDataException>(() => TripleTriadMatch.Restore(cards, snapshot with { CurrentPlayer = 0 }));
        Assert.Throws<InvalidDataException>(() => TripleTriadMatch.Restore(cards, snapshot with { Version = 2 }));
        Assert.Throws<InvalidDataException>(() => TripleTriadMatch.Restore(cards, snapshot with { BoardElements = new TripleTriadElement[8] }));
        Assert.Throws<InvalidDataException>(() => TripleTriadMatch.Restore(cards, snapshot with { BoardElements = null! }));
        Assert.Throws<InvalidDataException>(() => TripleTriadMatch.Restore(cards, snapshot with { Rules = (TripleTriadRules)128 }));
        Assert.Throws<InvalidDataException>(() => TripleTriadMatch.Restore(cards, snapshot with { Round = 1 }));
        Assert.Throws<InvalidDataException>(() => TripleTriadMatch.Restore(cards, snapshot with { InstanceOrder = [5, 6, 7, 8, 9, 0, 1, 2, 3, 4] }));
        snapshot.Board[1] = snapshot.Board[0];
        Assert.Throws<InvalidDataException>(() => TripleTriadMatch.Restore(cards, snapshot));
        snapshot = match.CaptureSnapshot();
        snapshot.Hands[0][1] = null;
        Assert.Throws<InvalidDataException>(() => TripleTriadMatch.Restore(cards, snapshot));
        snapshot = match.CaptureSnapshot();
        snapshot.Board[0] = snapshot.Board[0]! with { Owner = 2 };
        Assert.Throws<InvalidDataException>(() => TripleTriadMatch.Restore(cards, snapshot));
        cards[0] = cards[0] with { Top = 11 };
        Assert.Throws<InvalidDataException>(() => TripleTriadMatch.Create(cards, [0, 1, 2, 3, 4], [5, 6, 7, 8, 9]));
        cards = Catalog();
        cards[0] = cards[0] with { Top = 6 };
        Assert.Throws<InvalidDataException>(() => TripleTriadMatch.Restore(cards, match.CaptureSnapshot()));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<TripleTriadSnapshot>("{}"));
    }

    [Fact]
    public void LegalAiFinishesDeterministicallyUnderAllRuleCombinationsAndPreservesSnapshots()
    {
        TripleTriadCard[] cards = Enumerable.Range(0, 110).Select(id => new TripleTriadCard(id, id % 10 + 1, (id + 3) % 10 + 1, (id + 7) % 10 + 1, (id + 1) % 10 + 1, (TripleTriadElement)(id % 9))).ToArray();
        for (int combination = 0; combination < 128; combination++)
        {
            TripleTriadRules rules = (TripleTriadRules)combination;
            TripleTriadElement[] elements = Enumerable.Range(0, 9).Select(cell => rules.HasFlag(TripleTriadRules.Elemental) ? (TripleTriadElement)cell : TripleTriadElement.None).ToArray();
            TripleTriadMatch match = TripleTriadMatch.Create(cards, [0, 22, 44, 66, 88], [1, 23, 45, 67, 109], rules, combination % 2, elements);
            for (int turn = 0; turn < 9; turn++)
            {
                TripleTriadMove choice = match.ChooseMove();
                Assert.Equal(choice, match.ChooseMove());
                Assert.Contains(choice, match.GetLegalMoves());
                match.Play(choice.HandIndex, choice.CellIndex);
                Assert.Equal(10, match.PlayerScore + match.OpponentScore);
                match = TripleTriadMatch.Restore(cards, JsonSerializer.Deserialize<TripleTriadSnapshot>(JsonSerializer.Serialize(match.CaptureSnapshot()))!);
            }
            Assert.NotEqual(TripleTriadOutcome.InProgress, match.Outcome);
            Assert.Empty(match.GetLegalMoves());
            Assert.Throws<InvalidOperationException>(() => match.ChooseMove());
            Assert.Throws<InvalidOperationException>(() => match.Play(0, 0));
        }
    }

    private static TripleTriadCard[] Catalog() => Enumerable.Range(0, 110).Select(id => new TripleTriadCard(id, 5, 5, 5, 5, TripleTriadElement.None)).ToArray();

    private static TripleTriadMatch Position(TripleTriadCard[] cards, TripleTriadRules rules, (int Cell, int Owner)[] placed, TripleTriadElement[]? elements = null)
    {
        TripleTriadSnapshot snapshot = TripleTriadMatch.Create(cards, [0, 1, 2, 3, 4], [5, 6, 7, 8, 9], rules, boardElements: elements).CaptureSnapshot();
        for (int turn = 0; turn < placed.Length; turn++)
        {
            int sourcePlayer = turn % 2;
            int sourceSlot = turn / 2;
            snapshot.Hands[sourcePlayer][sourceSlot] = null;
            snapshot.Board[placed[turn].Cell] = new TripleTriadPlacedCard(sourcePlayer * 5 + sourceSlot, placed[turn].Owner, sourcePlayer, sourceSlot);
        }
        return TripleTriadMatch.Restore(cards, snapshot with { CurrentPlayer = placed.Length % 2 });
    }

    private sealed class MoveResultComparer : IEqualityComparer<TripleTriadMoveResult>
    {
        public bool Equals(TripleTriadMoveResult? left, TripleTriadMoveResult? right) => JsonSerializer.Serialize(left) == JsonSerializer.Serialize(right);
        public int GetHashCode(TripleTriadMoveResult value) => value.GetHashCode();
    }
}
