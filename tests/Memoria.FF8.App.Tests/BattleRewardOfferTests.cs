using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class BattleRewardOfferTests
{
    [Theory]
    [InlineData(10, 15, 5, true)]
    [InlineData(95, 100, 5, false)]
    [InlineData(98, 100, 2, false)]
    [InlineData(100, 100, 0, false)]
    public void ItemOffersRetainAttemptedQuantityAndNativeAcceptanceWhileGrantsReportDelta(byte count, byte finalCount, int granted, bool accepted)
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        NativeEconomy economy = assets.ReadNewGameEconomy();
        NativeItemStock[] stocks = economy.Items.ToArray();
        int potion = Array.FindIndex(stocks, stock => stock.ItemId == 1);
        Assert.True(potion >= 0);
        stocks[potion] = stocks[potion] with { Count = count };
        economy = economy with { Items = stocks };
        string before = JsonSerializer.Serialize(economy);

        BattleLootOwners result = NativeBattleRewardDistribution.Loot([], new Dictionary<int, BattleEnemyRewardDefinition>(), economy, null, [new BattleStock(1, 5)]);

        Assert.Equal(new BattleRewardOffer(false, 1, 5, accepted), Assert.Single(result.Offers));
        Assert.Equal(finalCount, result.Economy.Items[potion].Count);
        if (granted == 0)
        {
            Assert.Empty(result.ItemsGranted);
        }
        else
        {
            Assert.Equal(new BattleStock(1, granted), Assert.Single(result.ItemsGranted));
        }
        Assert.Equal(before, JsonSerializer.Serialize(economy));
    }

    [Theory]
    [InlineData(99, true)]
    [InlineData(100, false)]
    public void CardOffersRetainRejectedCapAttemptsAndLeaveOwnersUntouched(int count, bool accepted)
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        NativeEconomy economy = assets.ReadNewGameEconomy();
        NativeCardInventory cards = NativeCardInventory.CreateNewGame();
        for (int index = 0; index < count; index++)
        {
            cards = cards.Acquire(0, out int acquired);
            Assert.Equal(0, acquired);
        }
        string beforeEconomy = JsonSerializer.Serialize(economy);
        string beforeCards = JsonSerializer.Serialize(cards);
        BattleEnemyDefeat[] defeats = [new(0, 8, BattleDefeatCredit.Character, 0, null, true, -1)];
        Dictionary<int, BattleEnemyRewardDefinition> definitions = new() { [0] = new(10, 20, 20, 30, 0, false, 255, Enumerable.Repeat(new BattleStock(1, 5), 12).ToImmutableArray()) };

        BattleLootOwners result = NativeBattleRewardDistribution.Loot(defeats, definitions, economy, cards);

        Assert.Equal(new BattleRewardOffer(true, 0, 1, accepted), Assert.Single(result.Offers));
        Assert.Equal(100, result.Cards!.AvailableCount(0));
        if (accepted)
        {
            Assert.Equal((byte)0, Assert.Single(result.CardsGranted));
        }
        else
        {
            Assert.Empty(result.CardsGranted);
        }
        Assert.Empty(result.ItemsGranted);
        Assert.Equal(beforeEconomy, JsonSerializer.Serialize(economy));
        Assert.Equal(beforeCards, JsonSerializer.Serialize(cards));
    }
}
