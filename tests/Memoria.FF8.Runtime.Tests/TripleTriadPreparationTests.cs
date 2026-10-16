using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class TripleTriadPreparationTests
{
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
