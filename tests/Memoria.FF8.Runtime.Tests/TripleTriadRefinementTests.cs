namespace Memoria.FF8.Runtime.Tests;

public sealed class TripleTriadRefinementTests
{
    [Fact]
    public void CommonRecipePublishesBothCandidatesAndRetainsAcquisitionFlag()
    {
        NativeCardInventory cards = NativeCardInventory.CreateNewGame().Acquire(0, out _).Acquire(0, out _);
        NativeEconomy economy = Economy();
        Assert.True(TripleTriadRefinementService.TryApply(cards, economy, new TripleTriadRefinement(0, 1, 118, 5), out TripleTriadRefinementResult result, 2));
        Assert.Equal(2, result.CardsConsumed);
        Assert.Equal(10, result.ItemsCreated);
        Assert.Equal(0x80, result.Cards.Cards[0]);
        Assert.Equal(new NativeItemStock(118, 10), result.Economy.Items[0]);
        Assert.Equal(2, cards.AvailableCount(0));
        Assert.Equal(0, economy.Items[0].ItemId);
        Assert.Equal(economy.Gil, result.Economy.Gil);
    }

    [Fact]
    public void RareCardConsumptionKeepsHistoryAndClearsOnlyItsCurrentLocation()
    {
        NativeCardInventory cards = NativeCardInventory.CreateNewGame().Acquire(109, out _);
        Assert.True(TripleTriadRefinementService.TryApply(cards, Economy(), new TripleTriadRefinement(109, 1, 99, 3), out TripleTriadRefinementResult result));
        Assert.Equal(0, result.Cards.Location(109));
        Assert.Equal(0, result.Cards.AvailableCount(109));
        Assert.Equal(cards.ObtainedRareCards, result.Cards.ObtainedRareCards);
        Assert.Equal(0xF0, cards.Location(109));
        Assert.False(TripleTriadRefinementService.TryApply(result.Cards, result.Economy, new TripleTriadRefinement(109, 1, 99, 3), out _));
    }

    [Fact]
    public void InsufficientInputOrFullOutputDoesNotConsumeCardsOrChangeItems()
    {
        NativeCardInventory cards = NativeCardInventory.CreateNewGame().Acquire(0, out _);
        NativeEconomy economy = Economy();
        economy.Items[0] = new NativeItemStock(118, 96);
        TripleTriadRefinement recipe = new(0, 1, 118, 5);
        Assert.False(TripleTriadRefinementService.TryApply(cards, economy, recipe, out TripleTriadRefinementResult full));
        Assert.Equal(cards.Cards, full.Cards.Cards);
        Assert.Equal(economy.Items, full.Economy.Items);
        Assert.False(TripleTriadRefinementService.TryApply(cards, Economy(), recipe, out TripleTriadRefinementResult insufficient, 2));
        Assert.Equal(cards.Cards, insufficient.Cards.Cards);
        Assert.Equal(0, insufficient.ItemsCreated);
        economy.Items[0] = new NativeItemStock(118, 95);
        Assert.True(TripleTriadRefinementService.TryApply(cards, economy, recipe, out TripleTriadRefinementResult exact));
        Assert.Equal(100, exact.Economy.Items[0].Count);
    }

    [Fact]
    public void FullSlotsRejectNewItemButAllowAnExistingItemWithCapacity()
    {
        NativeCardInventory cards = NativeCardInventory.CreateNewGame().Acquire(0, out _);
        NativeEconomy economy = Economy();
        Array.Fill(economy.Items, new NativeItemStock(1, 1));
        Assert.False(TripleTriadRefinementService.TryApply(cards, economy, new TripleTriadRefinement(0, 1, 118, 5), out _));
        economy.Items[17] = new NativeItemStock(118, 1);
        Assert.True(TripleTriadRefinementService.TryApply(cards, economy, new TripleTriadRefinement(0, 1, 118, 5), out TripleTriadRefinementResult result));
        Assert.Equal(6, result.Economy.Items[17].Count);
    }

    private static NativeEconomy Economy()
    {
        return new NativeEconomy
        {
            Version = 1,
            Rule = NativeEconomy.InitializationRule,
            InitOutSha256 = new string('0', 64),
            Items = Enumerable.Repeat(new NativeItemStock(0, 0), NativeEconomy.ItemSlotCount).ToArray(),
            Gil = 123,
            PastGil = 456,
            EventBankGil72 = 123,
            EventBankPastGil76 = 456
        };
    }
}
