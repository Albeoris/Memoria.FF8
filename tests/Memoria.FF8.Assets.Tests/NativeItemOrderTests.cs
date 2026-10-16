using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeItemOrderTests
{
    private static string GameDirectory => TestResources.GameDirectory;

    [Fact]
    public void AuthenticInitializationRetainsCompleteSavedPermutationAndCopiesDefensively()
    {
        using GameAssets assets = new(GameDirectory);
        NativeEconomy economy = assets.ReadNewGameEconomy();
        Assert.Equal(Enumerable.Range(0, 32).Select(rank => (byte)rank), economy.ItemOrder!.Order());
        NativeEconomy copy = economy.DeepCopy();
        (copy.ItemOrder![0], copy.ItemOrder[1]) = (copy.ItemOrder[1], copy.ItemOrder[0]);
        Assert.NotEqual(copy.ItemOrder, economy.ItemOrder);
        Assert.Throws<InvalidDataException>(() => (economy with { ItemOrder = new byte[32] }).Validate());
    }

    [Fact]
    public void UnownedBattleItemSwapsWithFirstUnusedRankAndPreservesInventory()
    {
        using GameAssets assets = new(GameDirectory);
        NativeEconomy economy = assets.ReadNewGameEconomy();
        NativeItemStock[] items = Enumerable.Repeat(new NativeItemStock(0, 0), NativeEconomy.ItemSlotCount).ToArray();
        items[0] = new(2, 1);
        byte[] order = Enumerable.Range(0, 32).Reverse().Select(rank => (byte)rank).ToArray();
        economy = economy with { Items = items, ItemOrder = order };
        NativeEconomy next = economy.UpdateItemOrder(1);
        Assert.Equal(0, next.ItemOrder![0]);
        Assert.Equal(31, next.ItemOrder[31]);
        Assert.Equal(30, next.ItemOrder[1]);
        Assert.Equal(31, economy.ItemOrder![0]);
        Assert.Equal(economy.Items, next.Items);
        Assert.Equal(Enumerable.Range(0, 32).Select(rank => (byte)rank), next.ItemOrder.Order());
    }

    [Fact]
    public void ExistingNonzeroItemAndHighIdsReturnWithoutInventingHistoricalOrder()
    {
        using GameAssets assets = new(GameDirectory);
        NativeEconomy unknown = assets.ReadNewGameEconomy() with { ItemOrder = null };
        Assert.Same(unknown, unknown.UpdateItemOrder(1));
        Assert.Same(unknown, unknown.UpdateItemOrder(33));
        Assert.Throws<NotSupportedException>(() => unknown.UpdateItemOrder(2));
        NativeEconomy wrapped = unknown.AddItem(1, 250, out _);
        Assert.Equal(0, wrapped.Items[0].Count);
        Assert.Throws<NotSupportedException>(() => wrapped.UpdateItemOrder(1));
        NativeEconomy acquired = unknown.AddItem(2, 1, out _);
        Assert.Same(acquired, acquired.UpdateItemOrder(2));
    }
}
