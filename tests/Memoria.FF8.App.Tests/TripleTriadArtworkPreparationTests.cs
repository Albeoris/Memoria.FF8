using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class TripleTriadArtworkPreparationTests
{
    [Fact]
    public void ManualSelectionPreparesOnlyDistinctSavedDeckCards()
    {
        TripleTriadController controller = Create(TripleTriadRules.Open);
        Assert.Equal(new[] { 1, 2, 3, 4, 5, 6 }, MonoGameUiCanvas.InitialTripleTriadCards(controller));
    }

    [Fact]
    public void RandomSelectionDoesNotPrepareUnusedSavedDecks()
    {
        Assert.Empty(MonoGameUiCanvas.InitialTripleTriadCards(Create(TripleTriadRules.Random)));
    }

    [Theory]
    [InlineData(TripleTriadRules.Open)]
    [InlineData(TripleTriadRules.Random)]
    public void ExistingMatchPreparesOnlyItsParticipatingCards(TripleTriadRules rules)
    {
        TripleTriadController controller = Create(rules);
        controller.RestoreMatch(TripleTriadMatch.Create(Cards(), [20, 21, 22, 23, 24], [24, 25, 26, 27, 28], rules));
        Assert.Equal(Enumerable.Range(20, 9), MonoGameUiCanvas.InitialTripleTriadCards(controller));
    }

    private static TripleTriadCard[] Cards() => Enumerable.Range(0, 110).Select(id => new TripleTriadCard(id, 1, 1, 1, 1, TripleTriadElement.None)).ToArray();

    private static TripleTriadController Create(TripleTriadRules rules)
    {
        return new TripleTriadController(Cards(), Enumerable.Range(0, 110).ToDictionary(id => id, _ => 1), [20, 21, 22, 23, 24], id => id.ToString(), rules, [[1, 2, 3, 4, 5], [2, 3, 4, 5, 6]]);
    }
}
