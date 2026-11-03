using Memoria.FF8.GameUi;

namespace Memoria.FF8.App.Tests;

public sealed class MagicMenuSortingTests
{
    [Fact]
    public void SortedMagicNavigationDoesNotMutateTheCachedStockList()
    {
        MenuData data = CreateData();
        int reads = 0;
        MenuController menu = new(() =>
        {
            reads++;
            return data;
        });
        menu.Open("magic-stock");
        menu.Execute(new MenuAction("magic-sort"));
        Assert.Equal("Spell 00", menu.Entries[0].Label);

        // List.Sort invalidates enumerators even when the order does not change.
        // Navigation and projection reads must leave the cached list intact.
        using List<MenuMagic>.Enumerator stock = data.Magic.GetEnumerator();
        for (int index = 0; index < 10; index++)
        {
            Assert.Same(data, menu.Data);
            _ = menu.Entries;
            menu.Navigate(MenuInput.Down);
            _ = menu.BuildView();
        }

        Assert.True(stock.MoveNext());
        Assert.Equal(1, reads);
    }

    [Fact]
    public void SortingPreservesSelectedSpellAndSortsTheNextLiveProjection()
    {
        MenuData data = CreateData();
        MenuController menu = new(() => data);
        menu.Open("magic-stock");
        string selected = menu.SelectedEntry!.Id;

        menu.Execute(new MenuAction("magic-sort"));

        Assert.Equal(selected, menu.SelectedEntry!.Id);
        Assert.Equal("Spell 00", menu.Entries[0].Label);

        data = CreateData();
        data.Magic.Add(new MenuMagic { Id = "new-spell", CharacterId = "character-0", Name = "A new spell", Quantity = 100 });
        menu.InvalidateLiveData();

        Assert.Equal("new-spell", menu.Entries[0].Id);
        Assert.Equal(selected, menu.SelectedEntry!.Id);
        using List<MenuMagic>.Enumerator stock = data.Magic.GetEnumerator();
        _ = menu.Data;
        _ = menu.Entries;
        Assert.True(stock.MoveNext());
    }

    private static MenuData CreateData()
    {
        MenuData data = new() { SharedMagicStock = true };
        for (int row = 0; row < 8; row++)
        {
            string character = $"character-{row}";
            data.Characters.Add(new MenuCharacter { Id = character, Name = $"Character {row}", InParty = row < 3 });
            for (int spell = 63; spell >= 0; spell--)
            {
                data.Magic.Add(new MenuMagic { Id = $"magic-{row}-{spell}", CharacterId = character, Name = $"Spell {spell:D2}", Quantity = 100 });
            }
        }
        return data;
    }
}
