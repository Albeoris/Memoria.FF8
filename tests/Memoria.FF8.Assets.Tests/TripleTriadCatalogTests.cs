using System.Security.Cryptography;
using System.Text;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class TripleTriadCatalogTests
{
    [Fact]
    public void OriginalExecutableSuppliesAllNamesAndCorrectDirectionalRanksAndElements()
    {
        string gameDirectory = GameDirectory();
        TripleTriadCatalog catalog = TripleTriadCatalog.Load(gameDirectory);
        Assert.Equal(110, catalog.Cards.Length);
        Assert.Equal(110, catalog.Names.Distinct().Count());
        Assert.Equal("7110DEBAA131165397435EA21EF51E52AE41FD6BAF8C450F4B2878F4566FE00A", Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', catalog.Names)))));
        Assert.Equal(new TripleTriadCard(0, 1, 4, 1, 5, TripleTriadElement.None), catalog.Cards[0]);
        Assert.Equal(new TripleTriadCard(5, 2, 1, 4, 4, TripleTriadElement.Thunder), catalog.Cards[5]);
        Assert.Equal(new TripleTriadCard(7, 3, 5, 2, 1, TripleTriadElement.Earth), catalog.Cards[7]);
        Assert.Equal(new TripleTriadCard(109, 10, 4, 6, 9, TripleTriadElement.None), catalog.Cards[109]);
        Assert.Equal("Geezard", catalog.Name(0));
        Assert.Equal("Squall", catalog.Name(109));
        Assert.Equal(110, catalog.TradePriorities.Length);
        Assert.Equal(0x15, catalog.TradePriority(0));
        Assert.Equal(0x12, catalog.TradePriority(1));
        Assert.Equal(0x74, catalog.TradePriority(109));
        Assert.Equal(Enum.GetValues<TripleTriadElement>(), catalog.Cards.Select(card => card.Element).Distinct().Order().ToArray());
        Assert.All(catalog.Cards, card => Assert.All(new[] { card.Top, card.Right, card.Bottom, card.Left }, rank => Assert.InRange(rank, 1, 10)));
        Assert.Throws<ArgumentOutOfRangeException>(() => catalog.Name(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => catalog.Name(110));
    }

    [Fact]
    public void OriginalCardModResourceSuppliesEveryRecipeWithInputAndOutputInCorrectDirection()
    {
        TripleTriadCatalog catalog = TripleTriadCatalog.Load(GameDirectory());
        Assert.Equal(110, catalog.Refinements.Length);
        Assert.Equal(new TripleTriadRefinement(0, 1, 118, 5), catalog.Refinements[0]);
        Assert.Equal(new TripleTriadRefinement(77, 1, 176, 100), catalog.Refinements[77]);
        Assert.Equal(new TripleTriadRefinement(109, 1, 99, 3), catalog.Refinements[109]);
        Assert.Equal(Enumerable.Range(0, 110), catalog.Refinements.Select(recipe => recipe.CardId));
        Assert.All(catalog.Refinements, recipe =>
        {
            Assert.InRange(recipe.RequiredCards, 1, 100);
            Assert.InRange(recipe.ItemId, 1, 198);
            Assert.InRange(recipe.ItemCount, 1, 100);
        });
    }

    [Fact]
    public void ForeignOrTruncatedResourcesFailBeforeReadingAddressedTables()
    {
        Assert.Throws<NotSupportedException>(() => TripleTriadCatalog.ReadExecutable([]));
        Assert.Throws<NotSupportedException>(() => TripleTriadCatalog.ReadRefinements(new byte[880]));
    }

    private static string GameDirectory()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? @"C:\Steam\steamapps\common\FINAL FANTASY VIII";
        Assert.SkipUnless(File.Exists(Path.Combine(directory, "FF8_EN.exe")) && File.Exists(Path.Combine(directory, "Data", "lang-en", "menu.fs")), "Installed original English card resources are required.");
        return directory;
    }
}
