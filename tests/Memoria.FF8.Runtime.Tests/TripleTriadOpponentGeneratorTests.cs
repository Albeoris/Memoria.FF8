namespace Memoria.FF8.Runtime.Tests;

public sealed class TripleTriadOpponentGeneratorTests
{
    [Fact]
    public void ExpandedOpponentUsesWholeLevelPoolBeforeRepeatingCommonCards()
    {
        TripleTriadOpponentHand hand = TripleTriadOpponentGenerator.Generate(NativeCardInventory.CreateNewGame(), 0, 0, 1, 1, 50);
        Assert.Equal(50, hand.Cards.Count);
        Assert.Equal(11, hand.Cards.Take(11).Distinct().Count());
        Assert.All(hand.Cards, card => Assert.InRange(card, 0, 10));
    }

    [Fact]
    public void NativeIndependentRandomSequenceIncludesUnsignedOverflow()
    {
        TripleTriadRandom random = new(1);
        int[] expected = [0, 3628, 25004, 5897, 29585, 29158, 12681, 15595, 28907, 6087, 20932, 8821, 8083, 4462, 5997, 25706];
        foreach (int value in expected)
        {
            random = random.Draw(out int actual);
            Assert.Equal(value, actual);
        }
        Assert.Equal(3369345009u, random.State);
        Assert.Equal(unchecked(uint.MaxValue * 69069u + 1u), new TripleTriadRandom(uint.MaxValue).Draw(out _).State);
    }

    [Fact]
    public void ZeroMaskDefaultsToLevelOneAndDuplicateRerollsConsumeBothDraws()
    {
        TripleTriadOpponentHand result = TripleTriadOpponentGenerator.Generate(NativeCardInventory.CreateNewGame(), 0, 0, 0, 1);
        Assert.Equal(new[] { 9, 1, 8, 4, 10 }, result.Cards);
        Assert.Equal(1156259413u, result.RandomState);
        Assert.Equal(12, result.RandomDraws);
    }

    [Fact]
    public void ForcedRareInsertionUsesSavedOwnerWithoutAcquisitionBitsAndCapsAtFive()
    {
        NativeCardInventory inventory = NativeCardInventory.CreateNewGame();
        for (int card = 77; card <= 83; card++)
        {
            inventory = inventory.Transfer(card, 121, out _);
        }
        TripleTriadOpponentHand result = TripleTriadOpponentGenerator.Generate(inventory, 121, 200, 128, 1);
        Assert.Equal(new[] { 77, 78, 79, 80, 81 }, result.Cards);
        Assert.Equal(5, result.RandomDraws);
        Assert.All(inventory.ObtainedRareCards, value => Assert.Equal((byte)0, value));
        Assert.Equal(121, inventory.Location(77));
    }

    [Fact]
    public void ChanceAfterEverySuccessIsOriginalHalvedOnceAndFailedChecksConsumeDraws()
    {
        NativeCardInventory inventory = NativeCardInventory.CreateNewGame();
        for (int card = 77; card <= 84; card++)
        {
            inventory = inventory.Transfer(card, 121, out _);
        }
        // The first eight random %100 values are0,28,4,97,85,58,81,95.
        TripleTriadOpponentHand result = TripleTriadOpponentGenerator.Generate(inventory, 121, 100, 1, 1);
        Assert.Equal(new[] { 77, 78, 79 }, result.Cards.Take(3));
        Assert.All(result.Cards.Skip(3), card => Assert.InRange(card, 0, 10));
        Assert.True(result.RandomDraws >= 12);
    }

    [Fact]
    public void NormalCommonGenerationExcludesPuPuAndDuplicatesAcrossAllSeeds()
    {
        NativeCardInventory inventory = NativeCardInventory.CreateNewGame();
        for (uint seed = 0; seed < 100; seed++)
        {
            TripleTriadOpponentHand result = TripleTriadOpponentGenerator.Generate(inventory, 121, 0, 16, seed);
            Assert.Equal(5, result.Cards.Distinct().Count());
            Assert.All(result.Cards, card => Assert.InRange(card, 44, 54));
            Assert.DoesNotContain(47, result.Cards);
        }
    }

    [Fact]
    public void ZeroOwnerSkipsRareScanAndInvalidNonzeroMaskFailsBeforeDivision()
    {
        NativeCardInventory inventory = NativeCardInventory.CreateNewGame();
        TripleTriadOpponentHand result = TripleTriadOpponentGenerator.Generate(inventory, 0, 255, 1, 1);
        Assert.Equal(12, result.RandomDraws);
        Assert.Throws<NotSupportedException>(() => TripleTriadOpponentGenerator.Generate(inventory, 121, 0, 128, 1));
    }
}
