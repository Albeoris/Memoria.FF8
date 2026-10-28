using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class TripleTriadPreparationTests
{
    [Fact]
    public void BlockedCellGenerationIsDistinctDeterministicAndZeroPreservesTheStream()
    {
        TripleTriadPreparedBlocks none = TripleTriadPreparation.GenerateBlockedCells(0, 123, 4, 4);
        Assert.Empty(none.Cells);
        Assert.Equal(123u, none.RandomState);
        Assert.Equal(0, none.RandomDraws);
        TripleTriadPreparedBlocks blocks = TripleTriadPreparation.GenerateBlockedCells(5, 123, 4, 4);
        Assert.Equal(5, blocks.Cells.Distinct().Count());
        Assert.All(blocks.Cells, cell => Assert.InRange(cell, 0, 15));
        Assert.Equal(blocks.Cells, TripleTriadPreparation.GenerateBlockedCells(5, 123, 4, 4).Cells);
        Assert.Equal(blocks.RandomState, TripleTriadPreparation.GenerateBlockedCells(5, 123, 4, 4).RandomState);
    }

    [Theory]
    [InlineData(5, 0)]
    [InlineData(50, 0)]
    [InlineData(50, 3)]
    public void RewardPoolIncludesPlayedInstancesAndSamplesOnlyToFullBoardQuota(int handSize, int blockedCount)
    {
        TripleTriadCard[] cards = Enumerable.Range(0, 110).Select(id => new TripleTriadCard(id, 1, 1, 1, 1, TripleTriadElement.None)).ToArray();
        TripleTriadMatch match = TripleTriadMatch.Create(cards, Enumerable.Range(0, handSize).ToArray(), Enumerable.Range(50, handSize).ToArray(), blockedCells: Enumerable.Range(0, blockedCount).ToArray());
        while (match.Outcome == TripleTriadOutcome.InProgress)
        {
            TripleTriadMove move = match.GetLegalMoves().First();
            match.Play(move.HandIndex, move.CellIndex);
        }
        TripleTriadPreparedRewardPool pool = TripleTriadPreparation.GenerateRewardPool(match.CaptureSnapshot(), 123);
        Assert.Equal(10, pool.Participants.Count(value => value));
        Assert.Equal(5, pool.Participants.Take(handSize).Count(value => value));
        Assert.Equal(5, pool.Participants.Skip(handSize).Count(value => value));
        Assert.All(Enumerable.Range(0, handSize * 2).Where(index => match.OriginalInstancePlayed[index]), index => Assert.True(pool.Participants[index]));
        Assert.Equal(pool.Participants, TripleTriadPreparation.GenerateRewardPool(match.CaptureSnapshot(), 123).Participants);
        if (handSize == 5)
        {
            Assert.Equal(123u, pool.RandomState);
            Assert.Equal(0, pool.RandomDraws);
        }
        else
        {
            Assert.True(pool.RandomDraws >= blockedCount + 1);
        }
        TripleTriadSnapshot reordered = match.CaptureSnapshot() with { InstanceOrder = Enumerable.Range(0, handSize * 2).Reverse().ToArray() };
        TripleTriadPreparedRewardPool mapped = TripleTriadPreparation.GenerateRewardPool(reordered, 123);
        Assert.Equal(pool.Participants.Reverse(), mapped.Participants);
        Assert.Equal(pool.RandomState, mapped.RandomState);
    }

    [Fact]
    public void ExpandedRandomHandReservesEveryCopyAndElementsCoverWholeBoard()
    {
        NativeCardInventory inventory = NativeCardInventory.CreateNewGame();
        for (int card = 0; card < 8; card++)
        {
            inventory = inventory.Acquire(card, out _);
        }
        TripleTriadPreparedHand hand = TripleTriadPreparation.SelectRandomHand(inventory, 1, 8);
        Assert.Equal(Enumerable.Range(0, 8), hand.Cards.Order());
        Assert.Equal(8, inventory.TotalAvailableCount);
        TripleTriadPreparedBoard board = TripleTriadPreparation.GenerateElements(TripleTriadRules.Elemental, 1, 4, 5);
        Assert.Equal(20, board.Elements.Count);
        Assert.Equal(TripleTriadPreparation.GenerateElements(TripleTriadRules.Elemental, 1).Elements, board.Elements.Take(9));
    }

    [Fact]
    public void NativeElementalGenerationPinsRowOrderConditionalDrawsAndNextSavedState()
    {
        TripleTriadPreparedBoard board = TripleTriadPreparation.GenerateElements(TripleTriadRules.Elemental, 1);
        Assert.Equal(new[] { TripleTriadElement.Poison, TripleTriadElement.Ice, TripleTriadElement.None, TripleTriadElement.None, TripleTriadElement.None, TripleTriadElement.None, TripleTriadElement.Holy, TripleTriadElement.None, TripleTriadElement.Earth }, board.Elements);
        Assert.Equal(13, board.RandomDraws);
        Assert.Equal(1059494674u, board.RandomState);
        TripleTriadPreparedBoard disabled = TripleTriadPreparation.GenerateElements(TripleTriadRules.None, 1);
        Assert.All(disabled.Elements, element => Assert.Equal(TripleTriadElement.None, element));
        Assert.Equal(0, disabled.RandomDraws);
        Assert.Equal(1u, disabled.RandomState);
    }

    [Fact]
    public void NativeRandomReservesCommonCopiesAndRareInstancesWithoutMutatingInventory()
    {
        NativeCardInventory inventory = NativeCardInventory.CreateNewGame();
        foreach (int card in new[] { 0, 0, 1, 77, 109 })
        {
            inventory = inventory.Acquire(card, out _);
        }
        byte[] before = inventory.Cards.ToArray();
        TripleTriadPreparedHand hand = TripleTriadPreparation.SelectRandomHand(inventory, 1);
        Assert.Equal(new[] { 0, 77, 1, 109, 0 }, hand.Cards);
        Assert.Equal(318, hand.RandomDraws);
        Assert.Equal(3734241723u, hand.RandomState);
        Assert.Equal(before, inventory.Cards);
        Assert.Equal(2, inventory.AvailableCount(0));
        Assert.Equal(1, inventory.AvailableCount(77));
        Assert.Throws<InvalidOperationException>(() => TripleTriadPreparation.SelectRandomHand(NativeCardInventory.CreateNewGame(), 1));
    }

    [Fact]
    public void NativeStartingCoinUsesOneDrawAndNativeSideConvention()
    {
        TripleTriadStartingSide side = TripleTriadPreparation.TossStartingSide(1);
        Assert.Equal(0, side.NativeSide);
        Assert.Equal(69070u, side.RandomState);
        TripleTriadStartingSide second = TripleTriadPreparation.TossStartingSide(2);
        Assert.Equal(1, second.NativeSide);
        Assert.Equal(138139u, second.RandomState);
    }
}
