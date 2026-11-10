using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;
using Memoria.FF8.GameUi;

namespace Memoria.FF8.App.Tests;

public sealed class Ff7ShopMenuTests
{
    [Fact]
    public void SharedShopUsesOwnerCapacityAndSalePermissionWithoutChangingFf8Defaults()
    {
        MenuData data = new() { Currency = 1000 };
        data.Items.Add(new() { Id = "ff7", Name = "Original item", Quantity = 99, Price = 1, MaximumQuantity = 99, CanSell = false });
        data.Items.Add(new() { Id = "ff8", Name = "Existing item", Quantity = 99, Price = 1 });
        MenuController menu = new(() => data);
        menu.Open("shop-buy");
        Assert.False(menu.Entries.Single(entry => entry.Id == "ff7").Enabled);
        Assert.True(menu.Entries.Single(entry => entry.Id == "ff8").Enabled);
        menu.Open("shop-sell");
        Assert.Equal("ff8", Assert.Single(menu.Entries).Id);
        Assert.True(new MenuItem().CanSell);
        Assert.Equal(100, new MenuItem().MaximumQuantity);
    }

    [Fact]
    public void MateriaRowsRetainExactNativeSlotRatherThanSelectingAnotherMatchingMateria()
    {
        Ff7ShopState state = new(100, 7, Ff7ShopPhase.Interactive, 0, 0);
        Ff7ShopTransaction transaction = Ff7ShopMenuProjection.Transaction(state, new("shop-sell", "ff7-shop/materia/53/17", Value: "1"));
        Assert.Equal(new(1, Ff7ShopTransactionKind.Sell, Ff7ShopStockKind.Materia, 53, 1, 17), transaction);
        Assert.Throws<InvalidDataException>(() => Ff7ShopMenuProjection.Transaction(state, new("shop-sell", "ff7-shop/materia/53", Value: "1")));
        Assert.Throws<InvalidDataException>(() => Ff7ShopMenuProjection.Transaction(state, new("shop-sell", "item-53", Value: "1")));
    }
}
