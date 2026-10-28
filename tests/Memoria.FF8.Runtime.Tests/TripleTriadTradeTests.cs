namespace Memoria.FF8.Runtime.Tests;

public sealed class TripleTriadTradeTests
{
    [Theory]
    [InlineData(TripleTriadTradeRule.One)]
    [InlineData(TripleTriadTradeRule.Diff)]
    [InlineData(TripleTriadTradeRule.All)]
    [InlineData(TripleTriadTradeRule.Direct)]
    public void EveryRewardRuleExcludesOriginalInstancesOutsideThePoolIncludingDuplicates(TripleTriadTradeRule rule)
    {
        int[] player = [0, 1, 2];
        int[] opponent = [10, 10, 11];
        int[] owners = [0, 0, 1, 0, 0, 0];
        bool[] participants = [true, true, false, true, false, false];
        TripleTriadTradeResult result = TripleTriadTrade.Settle(Own(player), player, opponent, owners, rule, 121, participants, rule == TripleTriadTradeRule.Direct ? null : [0]);
        Assert.Equal(new[] { 10 }, result.GainedCardIds);
        Assert.Empty(result.LostCardIds);
        Assert.Equal(1, result.Inventory.AvailableCount(2));
        Assert.Equal(1, result.Inventory.AvailableCount(10));
        Assert.Equal(-1, result.Inventory.AvailableCount(11));
        Assert.True(result.TookOpponentInstance);
    }

    [Theory]
    [InlineData(TripleTriadTradeRule.One)]
    [InlineData(TripleTriadTradeRule.Diff)]
    [InlineData(TripleTriadTradeRule.All)]
    [InlineData(TripleTriadTradeRule.Direct)]
    public void EveryLosingRuleRetainsPlayerCardsOutsideThePool(TripleTriadTradeRule rule)
    {
        int[] player = [0, 0, 1];
        int[] opponent = [10, 11, 12];
        int[] owners = [1, 1, 1, 0, 1, 1];
        bool[] participants = [true, false, false, false, true, true];
        TripleTriadTradeResult result = TripleTriadTrade.Settle(Own(player), player, opponent, owners, rule, 121, participants, rule == TripleTriadTradeRule.Direct ? null : [0]);
        Assert.Equal(new[] { 0 }, result.LostCardIds);
        Assert.Empty(result.GainedCardIds);
        Assert.Equal(1, result.Inventory.AvailableCount(0));
        Assert.Equal(1, result.Inventory.AvailableCount(1));
        Assert.False(result.TookOpponentInstance);
    }

    [Theory]
    [InlineData(TripleTriadTradeRule.One, 1)]
    [InlineData(TripleTriadTradeRule.Diff, 2)]
    [InlineData(TripleTriadTradeRule.All, 2)]
    [InlineData(TripleTriadTradeRule.Direct, 1)]
    public void SuddenDeathPoolEligibilityUsesOriginalIdentityRatherThanRedistributedSlots(TripleTriadTradeRule rule, int gained)
    {
        int[] player = [0, 0, 1];
        int[] opponent = [10, 10, 11];
        int[] owners = [1, 0, 0, 0, 0, 1];
        bool[] participants = [false, true, true, true, false, true];
        int[]? selected = rule == TripleTriadTradeRule.Direct ? null : gained == 1 ? [0] : [0, 2];
        TripleTriadTradeResult result = TripleTriadTrade.SettleAfterSuddenDeath(Own(player), player, opponent, [10, 0, 1], [0, 10, 11], owners, rule, 121, participants, selected);
        Assert.Equal(gained == 1 ? new[] { 10 } : new[] { 10, 11 }, result.GainedCardIds);
        Assert.Empty(result.LostCardIds);
        Assert.Equal(2, result.Inventory.AvailableCount(0));
    }

    [Theory]
    [InlineData(TripleTriadTradeRule.One)]
    [InlineData(TripleTriadTradeRule.Diff)]
    [InlineData(TripleTriadTradeRule.All)]
    public void NoEligibleLosingCardCapsRewardToZeroAndOutsidePoolSelectionIsRejected(TripleTriadTradeRule rule)
    {
        int[] player = [0, 1];
        int[] opponent = [10, 10];
        int[] owners = [0, 0, 0, 1];
        bool[] participants = [true, true, false, false];
        TripleTriadTradeResult result = TripleTriadTrade.Settle(Own(player), player, opponent, owners, rule, 121, participants);
        Assert.Empty(result.GainedCardIds);
        Assert.False(result.TookOpponentInstance);
        Assert.Throws<ArgumentException>(() => TripleTriadTrade.Settle(Own(player), player, opponent, owners, rule, 121, participants, [0]));
        Assert.Throws<ArgumentException>(() => TripleTriadTrade.Settle(Own(player), player, opponent, owners, rule, 121, [true]));
    }

    [Theory]
    [InlineData(TripleTriadTradeRule.One, 1)]
    [InlineData(TripleTriadTradeRule.Diff, 2)]
    [InlineData(TripleTriadTradeRule.All, 13)]
    public void LargerHandsSettleEveryOriginalInstance(TripleTriadTradeRule rule, int count)
    {
        int[] player = Enumerable.Range(0, 13).ToArray();
        int[] opponent = Enumerable.Range(20, 13).ToArray();
        int[] owners = Enumerable.Range(0, 26).Select(index => index < 14 ? 0 : 1).ToArray();
        TripleTriadTradeResult result = TripleTriadTrade.Settle(Own(player), player, opponent, owners, rule, 121, Participants(player.Length * 2), Enumerable.Range(0, count).ToArray());
        Assert.Equal(opponent.Take(count), result.GainedCardIds);
        Assert.Empty(result.LostCardIds);
        Assert.True(result.TookOpponentInstance);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(100)]
    public void PlayableTotalCountsInstancesWithoutSubtractingNeverAcquiredCards(int quantity)
    {
        NativeCardInventory inventory = Own(Enumerable.Repeat(0, quantity));
        Assert.Equal(quantity, inventory.TotalAvailableCount);
        Assert.Equal(-1, inventory.AvailableCount(1));
        NativeCardInventory withRare = inventory.Acquire(84, out _);
        Assert.Equal(quantity + 1, withRare.TotalAvailableCount);
        NativeCardInventory lostRare = withRare.Transfer(84, 121, out _);
        Assert.Equal(quantity, lostRare.TotalAvailableCount);
        Assert.Equal(0, lostRare.AvailableCount(84));
        if (quantity > 0)
        {
            Assert.Equal(quantity - 1, inventory.Transfer(0, 121, out _).TotalAvailableCount);
        }
        Assert.Equal(quantity, inventory.TotalAvailableCount);
    }

    [Theory]
    [InlineData(TripleTriadTradeRule.One, 6, 1)]
    [InlineData(TripleTriadTradeRule.Diff, 6, 2)]
    [InlineData(TripleTriadTradeRule.Diff, 7, 4)]
    [InlineData(TripleTriadTradeRule.Diff, 8, 5)]
    [InlineData(TripleTriadTradeRule.All, 6, 5)]
    public void WinningTradeUsesNativeCountAndDoesNotMutateSource(TripleTriadTradeRule rule, int score, int count)
    {
        NativeCardInventory inventory = Own([0, 1, 2, 3, 4]);
        TripleTriadTradeResult result = TripleTriadTrade.Settle(inventory, [0, 1, 2, 3, 4], [10, 11, 12, 13, 14], Owners(score), rule, 121, Participants(10), Enumerable.Range(0, count).ToArray());
        Assert.Equal(Enumerable.Range(10, count), result.GainedCardIds);
        Assert.Empty(result.LostCardIds);
        Assert.Empty(result.OverflowCardIds);
        Assert.Equal(1, result.Inventory.AvailableCount(0));
        Assert.Equal(-1, inventory.AvailableCount(10));
        Assert.Equal(1, result.Inventory.AvailableCount(10));
    }

    [Theory]
    [InlineData(TripleTriadTradeRule.One, 1)]
    [InlineData(TripleTriadTradeRule.Diff, 2)]
    [InlineData(TripleTriadTradeRule.All, 5)]
    public void LosingTradeRetainsAcquisitionHistoryAndAssignsRareNpcOwner(TripleTriadTradeRule rule, int count)
    {
        int[] hand = [84, 1, 2, 3, 4];
        NativeCardInventory inventory = Own(hand);
        TripleTriadTradeResult result = TripleTriadTrade.Settle(inventory, hand, [10, 11, 12, 13, 14], Owners(4), rule, 121, Participants(10), Enumerable.Range(0, count).ToArray());
        Assert.Equal(hand.Take(count), result.LostCardIds);
        Assert.Equal(121, result.Inventory.Location(84));
        Assert.Equal(0, result.Inventory.AvailableCount(84));
        Assert.Equal(inventory.ObtainedRareCards, result.Inventory.ObtainedRareCards);
        Assert.Equal(0xF0, inventory.Location(84));
    }

    [Fact]
    public void DirectDrawTransfersOpposingCapturedCardsAndMarksFirstRareAcquisition()
    {
        NativeCardInventory inventory = Own([0, 1, 2, 3, 4]).Transfer(84, 121, out _);
        int[] owners = [1, 0, 0, 0, 0, 0, 1, 1, 1, 1];
        TripleTriadTradeResult result = TripleTriadTrade.Settle(inventory, [0, 1, 2, 3, 4], [84, 11, 12, 13, 14], owners, TripleTriadTradeRule.Direct, 121, Participants(10));
        Assert.Equal(new[] { 84 }, result.GainedCardIds);
        Assert.Equal(new[] { 0 }, result.LostCardIds);
        Assert.Equal(0, result.Inventory.AvailableCount(0));
        Assert.Equal(1, result.Inventory.AvailableCount(84));
        Assert.Equal(0xF0, result.Inventory.Location(84));
        Assert.Equal(-1, inventory.AvailableCount(84));
    }

    [Theory]
    [InlineData(TripleTriadTradeRule.One)]
    [InlineData(TripleTriadTradeRule.Diff)]
    [InlineData(TripleTriadTradeRule.All)]
    public void DrawReturnsOriginalHandForNonDirectRules(TripleTriadTradeRule rule)
    {
        NativeCardInventory inventory = Own([0, 1, 2, 3, 4]);
        TripleTriadTradeResult result = TripleTriadTrade.Settle(inventory, [0, 1, 2, 3, 4], [10, 11, 12, 13, 14], Owners(5), rule, 121, Participants(10));
        Assert.Equal(inventory.Cards, result.Inventory.Cards);
        Assert.Equal(inventory.ObtainedRareCards, result.Inventory.ObtainedRareCards);
        Assert.Empty(result.GainedCardIds);
        Assert.Empty(result.LostCardIds);
    }

    [Fact]
    public void CommonDuplicateOwnershipAndCapMatchWithdrawThenAcquireOrder()
    {
        NativeCardInventory inventory = Own(Enumerable.Repeat(0, 100));
        TripleTriadTradeResult result = TripleTriadTrade.Settle(inventory, [0, 0, 0, 0, 0], [0, 11, 12, 13, 14], Owners(6), TripleTriadTradeRule.One, 121, Participants(10), [0]);
        Assert.Equal(100, result.Inventory.AvailableCount(0));
        Assert.Equal(new[] { 0 }, result.OverflowCardIds);
        Assert.Equal((byte)0xE4, result.Inventory.Cards[0]);
        Assert.Equal(100, inventory.AvailableCount(0));
    }

    [Fact]
    public void SuddenDeathOneStillTradesOriginalHandsWhileDirectUsesFinalRedistributedOwnership()
    {
        int[] originalPlayer = [0, 1, 2, 3, 84];
        int[] originalOpponent = [10, 11, 12, 13, 85];
        NativeCardInventory inventory = Own(originalPlayer).Transfer(85, 121, out _);
        int[] currentPlayer = [10, 1, 2, 3, 85];
        int[] currentOpponent = [0, 11, 12, 13, 84];
        TripleTriadTradeResult result = TripleTriadTrade.SettleAfterSuddenDeath(inventory, originalPlayer, originalOpponent, currentPlayer, currentOpponent, Owners(6), TripleTriadTradeRule.One, 121, Participants(10), [1]);
        Assert.Equal(new[] { 11 }, result.GainedCardIds);
        Assert.Empty(result.LostCardIds);
        Assert.Equal(1, result.Inventory.AvailableCount(0));
        Assert.Equal(-1, result.Inventory.AvailableCount(10));
        Assert.Equal(1, result.Inventory.AvailableCount(11));
        Assert.Equal(1, result.Inventory.AvailableCount(84));
        Assert.Equal(-1, result.Inventory.AvailableCount(85));
        TripleTriadTradeResult direct = TripleTriadTrade.SettleAfterSuddenDeath(inventory, originalPlayer, originalOpponent, currentPlayer, currentOpponent, [0, 0, 0, 0, 1, 0, 1, 1, 1, 0], TripleTriadTradeRule.Direct, 121, Participants(10));
        Assert.Equal(new[] { 10, 85 }, direct.GainedCardIds);
        Assert.Equal(new[] { 84 }, direct.LostCardIds);
        Assert.Equal(1, direct.Inventory.AvailableCount(0));
        Assert.Equal(1, direct.Inventory.AvailableCount(85));
        Assert.Equal(121, direct.Inventory.Location(84));
    }

    [Fact]
    public void InvalidHandsOwnersAndSelectionsFailAtomically()
    {
        NativeCardInventory inventory = Own([0, 1, 2, 3, 84]);
        byte[] before = inventory.Cards.ToArray();
        Assert.Throws<InvalidOperationException>(() => TripleTriadTrade.Settle(inventory, [0, 0, 2, 3, 84], [10, 11, 12, 13, 14], Owners(6), TripleTriadTradeRule.One, 121, Participants(10), [0]));
        Assert.Throws<ArgumentException>(() => TripleTriadTrade.Settle(inventory, [0, 1, 2, 3, 84], [84, 11, 12, 13, 14], Owners(6), TripleTriadTradeRule.One, 121, Participants(10), [0]));
        Assert.Throws<InvalidOperationException>(() => TripleTriadTrade.Settle(inventory, [0, 1, 2, 3, 84], [85, 11, 12, 13, 14], Owners(6), TripleTriadTradeRule.One, 121, Participants(10), [0]));
        Assert.Throws<ArgumentException>(() => TripleTriadTrade.Settle(inventory, [0, 1, 2, 3, 84], [10, 11, 12, 13, 14], Owners(6), TripleTriadTradeRule.Diff, 121, Participants(10), [0, 0]));
        Assert.Equal(before, inventory.Cards);
    }

    private static bool[] Participants(int count) => Enumerable.Repeat(true, count).ToArray();

    private static int[] Owners(int score)
    {
        return Enumerable.Range(0, 10).Select(slot => slot < score ? 0 : 1).ToArray();
    }

    private static NativeCardInventory Own(IEnumerable<int> cards)
    {
        NativeCardInventory inventory = NativeCardInventory.CreateNewGame();
        foreach (int card in cards)
        {
            inventory = inventory.Acquire(card, out _);
        }
        return inventory;
    }
}
