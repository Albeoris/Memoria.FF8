using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class TripleTriadCardModHintTests
{
    private static readonly TripleTriadCard[] Cards = Enumerable.Range(0, 110).Select(id => new TripleTriadCard(id, 1, 1, 1, 1, TripleTriadElement.None)).ToArray();
    private static readonly TripleTriadRefinement[] Recipes = [new(7, 5, 1, 1)];

    [Theory]
    [InlineData(null, "Card 7 (0/5)")]
    [InlineData(-2, "Card 7 (0/5)")]
    [InlineData(0, "Card 7 (0/5)")]
    [InlineData(2, "Card 7 (2/5)")]
    [InlineData(5, "Card 7")]
    [InlineData(6, "Card 7")]
    public void TradeNameShowsOwnedAndRequiredCountsOnlyBelowTheRecipeThreshold(int? owned, string expected)
    {
        TripleTriadController controller = Create(owned);
        controller.ConfigureCardModHints(Recipes, true, false, []);

        Assert.Equal(expected, controller.TradeCardName(7));
        Assert.Equal("Card 7", controller.Name(7));
    }

    [Fact]
    public void TradeNameHasNoHintWhenCardModAbilityIsUnavailable()
    {
        TripleTriadController controller = Create(2);
        controller.ConfigureCardModHints(Recipes, false, false, []);

        Assert.Equal("Card 7", controller.TradeCardName(7));
    }

    [Fact]
    public void TradeNameHasNoHintForCardsWithoutARecipe()
    {
        TripleTriadController controller = Create(2);
        controller.ConfigureCardModHints(Recipes, true, false, []);

        Assert.Equal("Card 8", controller.TradeCardName(8));
    }

    [Theory]
    [InlineData(true, true, "Card 7")]
    [InlineData(true, false, "Card 7 (2/5)")]
    [InlineData(false, true, "Card 7 (2/5)")]
    public void OnceSettingSuppressesOnlyPreviouslyRefinedCards(bool once, bool previouslyRefined, string expected)
    {
        TripleTriadController controller = Create(2);
        controller.ConfigureCardModHints(Recipes, true, once, previouslyRefined ? [7] : []);

        Assert.Equal(expected, controller.TradeCardName(7));
    }

    [Fact]
    public void TradeNameUsesTheCollectionCountWhileCardsAreReservedInTheDeck()
    {
        TripleTriadController controller = Create(5);
        controller.ConfigureCardModHints(Recipes, true, false, []);
        controller.ChooseCard(7);

        Assert.Equal(4, controller.Remaining(7));
        Assert.Equal("Card 7", controller.TradeCardName(7));
    }

    private static TripleTriadController Create(int? owned)
    {
        Dictionary<int, int> collection = new() { [0] = 5 };
        if (owned.HasValue)
        {
            collection[7] = owned.Value;
        }
        return new TripleTriadController(Cards, collection, [1, 2, 3, 4, 5], id => $"Card {id}");
    }
}
