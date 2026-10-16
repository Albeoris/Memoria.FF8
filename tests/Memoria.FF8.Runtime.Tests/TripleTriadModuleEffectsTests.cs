namespace Memoria.FF8.Runtime.Tests;

public sealed class TripleTriadModuleEffectsTests
{
    [Fact]
    public void OriginalNewGameTailInitializesRareOwnersAndRngAfterTheRegionClear()
    {
        NativeCardInventory cards = NativeCardInventory.CreateNewGame();
        Assert.All(cards.Cards.Take(77), card => Assert.Equal((byte)0, card));
        Assert.Equal(Enumerable.Range(200, 33).Select(owner => (byte)owner), cards.Cards.Skip(77));
        Assert.All(cards.ObtainedRareCards, bits => Assert.Equal((byte)0, bits));
        Assert.Equal(1u, cards.TripleTriadRandomState);
        Assert.Equal(new TripleTriadStatistics(0, 0, 0), cards.TripleTriadStatistics);
        Assert.Equal((byte)0, cards.CollectionFlags);
    }

    [Fact]
    public void PromptOnlyIncrementsRngWhenChoicePhaseRunsIncludingItsFirstAndAcceptedCallbacks()
    {
        TripleTriadPromptState prompt = TripleTriadPromptState.Create(10);
        for (int callback = 0; callback < 14; callback++)
        {
            prompt = prompt.Advance(0);
            Assert.Equal(10u, prompt.RandomState);
            Assert.Equal(TripleTriadPromptPhase.FadingIn, prompt.Phase);
        }
        prompt = prompt.Advance();
        Assert.Equal(TripleTriadPromptPhase.AwaitingChoice, prompt.Phase);
        Assert.Equal(11u, prompt.RandomState);
        prompt = prompt.Advance();
        Assert.Equal(12u, prompt.RandomState);
        prompt = prompt.Advance(0);
        Assert.Equal(TripleTriadPromptPhase.Accepted, prompt.Phase);
        Assert.Equal(13u, prompt.RandomState);
        Assert.Throws<InvalidOperationException>(() => prompt.Advance());
    }

    [Fact]
    public void QuitRetainsIncrementedRngAndFadesOutWithoutFurtherRngWrites()
    {
        TripleTriadPromptState prompt = new(TripleTriadPromptPhase.AwaitingChoice, 0, uint.MaxValue);
        prompt = prompt.Advance(1);
        Assert.Equal(0u, prompt.RandomState);
        for (int callback = 0; callback < 15; callback++)
        {
            prompt = prompt.Advance();
            Assert.Equal(0u, prompt.RandomState);
        }
        Assert.Equal(TripleTriadPromptPhase.Cancelled, prompt.Phase);
        Assert.Equal(15, prompt.PhaseCallbacks);
    }

    [Theory]
    [InlineData(0, 0, 2, 3)]
    [InlineData(1, 65535, 3, 3)]
    [InlineData(2, 65535, 2, 4)]
    [InlineData(-1, 65535, 2, 3)]
    public void OutcomeWordsWrapAndCancelDoesNotCountAsAMatch(int result, int wins, int losses, int draws)
    {
        NativeCardInventory cards = NativeCardInventory.CreateNewGame() with { TripleTriadStatistics = new TripleTriadStatistics(ushort.MaxValue, 2, 3) };
        NativeCardInventory complete = cards.CompleteMatch(result, 123);
        Assert.Equal(new TripleTriadStatistics((ushort)wins, (ushort)losses, (ushort)draws), complete.TripleTriadStatistics);
        Assert.Equal(123u, complete.TripleTriadRandomState);
        Assert.Equal(cards.Cards, complete.Cards);
        Assert.Equal(1u, cards.TripleTriadRandomState);
        Assert.Throws<NotSupportedException>(() => (cards with { TripleTriadStatistics = null }).CompleteMatch(0, 123));
    }

    [Fact]
    public void CollectionStarIsHistoricalStickyMenuWriteAndPreservesOtherSavedFlagBits()
    {
        NativeCardInventory cards = NativeCardInventory.CreateNewGame() with { CollectionFlags = 0xA4 };
        Assert.Equal((byte)0xA4, cards.NativeVisitNormalMenu().CollectionFlags);
        foreach (int card in Enumerable.Range(0, 110))
        {
            cards = cards.Acquire(card, out _).Transfer(card, 121, out _);
        }
        Assert.Equal((byte)0xA4, cards.CollectionFlags);
        Assert.Equal(0, cards.AvailableCount(0));
        Assert.Equal(0, cards.AvailableCount(109));
        NativeCardInventory visited = cards.NativeVisitNormalMenu();
        Assert.Equal((byte)0xA5, visited.CollectionFlags);
        Assert.Equal((byte)0xA4, cards.CollectionFlags);
        NativeCardInventory sticky = visited.DeepCopy();
        sticky.Cards[0] = 0;
        sticky.ObtainedRareCards[0] = 0;
        Assert.Equal((byte)0xA5, sticky.NativeVisitNormalMenu().CollectionFlags);
        Assert.Throws<NotSupportedException>(() => (cards with { CollectionFlags = null }).NativeVisitNormalMenu());
    }
}
