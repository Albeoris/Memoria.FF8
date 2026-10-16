namespace Memoria.FF8.Runtime.Tests;

public sealed class TripleTriadTradeTests
{
    [Theory]
    [InlineData(TripleTriadTradeRule.One, 6, 1)]
    [InlineData(TripleTriadTradeRule.Diff, 6, 2)]
    [InlineData(TripleTriadTradeRule.Diff, 7, 4)]
    [InlineData(TripleTriadTradeRule.Diff, 8, 5)]
    [InlineData(TripleTriadTradeRule.All, 6, 5)]
    public void WinningTradeUsesNativeCountAndDoesNotMutateSource(TripleTriadTradeRule rule, int score, int count)
    {
        NativeCardInventory inventory = Own([0, 1, 2, 3, 4]);
        TripleTriadTradeResult result = TripleTriadTrade.Settle(inventory, [0, 1, 2, 3, 4], [10, 11, 12, 13, 14], Owners(score), rule, 121, Enumerable.Range(0, count).ToArray());
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
        TripleTriadTradeResult result = TripleTriadTrade.Settle(inventory, hand, [10, 11, 12, 13, 14], Owners(4), rule, 121, Enumerable.Range(0, count).ToArray());
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
        TripleTriadTradeResult result = TripleTriadTrade.Settle(inventory, [0, 1, 2, 3, 4], [84, 11, 12, 13, 14], owners, TripleTriadTradeRule.Direct, 121);
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
        TripleTriadTradeResult result = TripleTriadTrade.Settle(inventory, [0, 1, 2, 3, 4], [10, 11, 12, 13, 14], Owners(5), rule, 121);
        Assert.Equal(inventory.Cards, result.Inventory.Cards);
        Assert.Equal(inventory.ObtainedRareCards, result.Inventory.ObtainedRareCards);
        Assert.Empty(result.GainedCardIds);
        Assert.Empty(result.LostCardIds);
    }

    [Fact]
    public void CommonDuplicateOwnershipAndCapMatchWithdrawThenAcquireOrder()
    {
        NativeCardInventory inventory = Own(Enumerable.Repeat(0, 100));
        TripleTriadTradeResult result = TripleTriadTrade.Settle(inventory, [0, 0, 0, 0, 0], [0, 11, 12, 13, 14], Owners(6), TripleTriadTradeRule.One, 121, [0]);
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
        TripleTriadTradeResult result = TripleTriadTrade.SettleAfterSuddenDeath(inventory, originalPlayer, originalOpponent, currentPlayer, currentOpponent, Owners(6), TripleTriadTradeRule.One, 121, [1]);
        Assert.Equal(new[] { 11 }, result.GainedCardIds);
        Assert.Empty(result.LostCardIds);
        Assert.Equal(1, result.Inventory.AvailableCount(0));
        Assert.Equal(-1, result.Inventory.AvailableCount(10));
        Assert.Equal(1, result.Inventory.AvailableCount(11));
        Assert.Equal(1, result.Inventory.AvailableCount(84));
        Assert.Equal(-1, result.Inventory.AvailableCount(85));
        TripleTriadTradeResult direct = TripleTriadTrade.SettleAfterSuddenDeath(inventory, originalPlayer, originalOpponent, currentPlayer, currentOpponent, Owners(6), TripleTriadTradeRule.Direct, 121);
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
        Assert.Throws<InvalidOperationException>(() => TripleTriadTrade.Settle(inventory, [0, 0, 2, 3, 84], [10, 11, 12, 13, 14], Owners(6), TripleTriadTradeRule.One, 121, [0]));
        Assert.Throws<ArgumentException>(() => TripleTriadTrade.Settle(inventory, [0, 1, 2, 3, 84], [84, 11, 12, 13, 14], Owners(6), TripleTriadTradeRule.One, 121, [0]));
        Assert.Throws<InvalidOperationException>(() => TripleTriadTrade.Settle(inventory, [0, 1, 2, 3, 84], [85, 11, 12, 13, 14], Owners(6), TripleTriadTradeRule.One, 121, [0]));
        Assert.Throws<ArgumentException>(() => TripleTriadTrade.Settle(inventory, [0, 1, 2, 3, 84], [10, 11, 12, 13, 14], Owners(6), TripleTriadTradeRule.Diff, 121, [0, 0]));
        Assert.Equal(before, inventory.Cards);
    }

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
