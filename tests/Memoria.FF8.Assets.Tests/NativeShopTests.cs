using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeShopTests
{
    private const string GameDirectory = @"C:\Steam\steamapps\common\FINAL FANTASY VIII";

    [Fact]
    public void AuthenticCatalogUsesNamesPricesAndPhysicalArgumentMap()
    {
        using GameAssets assets = new(GameDirectory);
        NativeShopDefinitions definitions = assets.ReadNativeShopDefinitions();
        Assert.Equal("2351FEDF7EAB6FCBB5823A344A727E29096D3237543B322C8F9372E52A847E82", definitions.ShopHash);
        Assert.Equal("9E833415A4740056EF98B7D9D0F89A93E76871EF9661326553A17915EFE6DB22", definitions.PriceHash);
        Assert.Equal(0, definitions.ResolveCatalog(256));
        Assert.Equal(3, definitions.ResolveCatalog(3));
        Assert.Equal(21, definitions.ArgumentMap[34]);
        Assert.Throws<NotSupportedException>(() => definitions.ResolveCatalog(34));
        Assert.Equal("Pet Pals Vol.3", definitions.ResolvePrice(191).Name);
        Assert.Equal("Pet Pals Vol.4", definitions.ResolvePrice(192).Name);
        Assert.Equal(1000u, definitions.BuyPrice(191, 0));
        Assert.Equal(750u, definitions.BuyPrice(191, 1));
        Assert.Equal(200u, definitions.SellPrice(191, 0));
        Assert.Equal(300u, definitions.SellPrice(191, 2));
    }

    [Fact]
    public void TwoMagazinePurchasesStayLocalUntilNativeInventoryReconstruction()
    {
        using GameAssets assets = new(GameDirectory);
        NativeEconomy original = assets.ReadNewGameEconomy();
        NativeShopTransaction shop = Open(assets, original);
        NativeShopTransaction first = shop.Buy(191, 1, out bool firstAccepted);
        NativeShopTransaction second = first.Buy(192, 1, out bool secondAccepted);
        Assert.True(firstAccepted);
        Assert.True(secondAccepted);
        Assert.Equal(5000u, original.Gil);
        Assert.Equal(3000u, second.Gil);
        Assert.Equal(0, shop.Counts[191]);
        Assert.DoesNotContain(original.Items, stock => stock.ItemId is 191 or 192);
        NativeEconomy committed = second.Commit(original);
        Assert.Equal(3000u, committed.Gil);
        Assert.Contains(new NativeItemStock(191, 1), committed.Items);
        Assert.Contains(new NativeItemStock(192, 1), committed.Items);
        Assert.Equal(original.EventBankGil72, committed.EventBankGil72);
        Assert.Equal(original.EventBankPastGil76, committed.EventBankPastGil76);
        Assert.NotSame(original.Items, committed.Items);
        NativeShopTransaction restored = JsonSerializer.Deserialize<NativeShopTransaction>(JsonSerializer.Serialize(second))!;
        Assert.Equal(JsonSerializer.Serialize(committed), JsonSerializer.Serialize(restored.Commit(original)));
    }

    [Fact]
    public void NativeAdmissionPreservesRejectedCacheAndEnforcesFundsAndHundredCount()
    {
        using GameAssets assets = new(GameDirectory);
        NativeShopTransaction shop = Open(assets, assets.ReadNewGameEconomy()) with { Gil = 999 };
        Assert.Same(shop, shop.Buy(191, 1, out bool accepted));
        Assert.False(accepted);
        shop = shop with { Gil = 99999999, Counts = shop.Counts.SetItem(191, 99) };
        Assert.Equal(1, shop.MaximumPurchase(191));
        Assert.Same(shop, shop.Buy(191, 2, out accepted));
        Assert.False(accepted);
        NativeShopTransaction bought = shop.Buy(191, 1, out accepted);
        Assert.True(accepted);
        Assert.Equal(100, bought.Counts[191]);
        Assert.Equal(99, shop.Counts[191]);
        Assert.Equal(0, shop.MaximumPurchase(42));
    }

    [Fact]
    public void PhysicalDuplicateCacheUsesLastCountAndCommitsFirstPair()
    {
        using GameAssets assets = new(GameDirectory);
        NativeEconomy original = assets.ReadNewGameEconomy().DeepCopy();
        original.Items[0] = new NativeItemStock(191, 2);
        original.Items[1] = new NativeItemStock(191, 7);
        original.Items[2] = new NativeItemStock(0, 9);
        NativeEconomy normalized = NativeShopTransaction.NormalizeInventory(original);
        Assert.Equal(new NativeItemStock(0, 0), normalized.Items[2]);
        Assert.Equal(new NativeItemStock(0, 9), original.Items[2]);
        NativeShopTransaction shop = Open(assets, normalized);
        Assert.Equal(7, shop.Counts[191]);
        NativeEconomy committed = shop.Commit(normalized);
        Assert.Equal(new NativeItemStock(191, 7), committed.Items[0]);
        Assert.Equal(new NativeItemStock(0, 0), committed.Items[1]);
    }

    [Fact]
    public void FullPhysicalInventoryRefusesUnretainedWriteWithoutPublishing()
    {
        using GameAssets assets = new(GameDirectory);
        NativeEconomy full = assets.ReadNewGameEconomy().DeepCopy();
        for (int slot = 0; slot < full.Items.Length; slot++)
        {
            full.Items[slot] = new NativeItemStock((byte)(slot + 1), 1);
        }
        NativeShopTransaction shop = Open(assets, full);
        shop = shop with { Counts = shop.Counts.SetItem(199, 1) };
        string before = JsonSerializer.Serialize(full);
        Assert.Throws<NotSupportedException>(() => shop.Commit(full));
        Assert.Equal(before, JsonSerializer.Serialize(full));
    }

    [Fact]
    public void ActualDiscoveryAndMenuRandomRemainDistinctFromEventRandom()
    {
        using GameAssets assets = new(GameDirectory);
        NativeShopDefinitions definitions = assets.ReadNativeShopDefinitions();
        NativeShopHistory original = assets.ReadNewGameShopHistory();
        NativeShopHistory refreshed = original.Refresh(definitions, 0, 0);
        Assert.Equal(0, refreshed.Bytes[9]);
        Assert.Equal(1, refreshed.Bytes[14]);
        Assert.Equal(1, refreshed.Bytes[15]);
        Assert.Equal(1, refreshed.Bytes[16]);
        Assert.Equal(original.Bytes.Skip(20), refreshed.Bytes.Skip(20));
        Assert.Equal(1, original.Refresh(definitions, 0, 4).Bytes[9]);
        ushort seed = 0x4611;
        for (int call = 0; call < 32; call++)
        {
            seed = (ushort)((125 * seed + 14) % 32768);
        }
        NativeMenuRandomState random = NativeMenuRandomState.FromColdProcess().Advance(32);
        Assert.Equal(seed, random.Seed);
        NativeMenuRandomState restored = JsonSerializer.Deserialize<NativeMenuRandomState>(JsonSerializer.Serialize(random))!;
        Assert.Equal(random.Advance(1), restored.Advance(1));
    }

    [Fact]
    public void ShopAbilitiesUseAll128LearnedBitsButOnlyFirst22PerAcquiredGf()
    {
        using GameAssets assets = new(GameDirectory);
        NativeCharacterVitals original = assets.ReadNewGameCharacterVitals();
        NativeGuardianForceVital[] rows = original.GuardianForces.Select(row => row with { Flags = 0 }).ToArray();
        rows[0] = rows[0] with { Flags = 1, LearnedBank = new(0, 1UL << (92 - 64)) };
        Assert.Equal(1u, (original with { GuardianForces = rows }).ShopAbilityMask());
        rows[0] = rows[0] with { LearnedBank = new((1UL << 22) - 1, 1UL << (92 - 64)) };
        Assert.Equal(0u, (original with { GuardianForces = rows }).ShopAbilityMask());
        rows[1] = rows[1] with { Flags = 1, LearnedBank = new(0, 1UL << (115 - 64)) };
        Assert.Equal(1u << 23, (original with { GuardianForces = rows }).ShopAbilityMask());
        Assert.Equal(original.GuardianForces[0].LearnedBank, JsonSerializer.Deserialize<NativeCharacterVitals>(JsonSerializer.Serialize(original))!.GuardianForces[0].LearnedBank);
    }

    [Fact]
    public void DreamShopCommitsPastGilAndMissingLearnedBankHasNoInferredZeros()
    {
        using GameAssets assets = new(GameDirectory);
        NativeEconomy economy = assets.ReadNewGameEconomy();
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals() with { DreamEnabled = true };
        NativeShopDefinitions definitions = assets.ReadNativeShopDefinitions();
        NativeShopHistory history = assets.ReadNewGameShopHistory().Refresh(definitions, 0, vitals.ShopAbilityMask());
        NativeShopTransaction shop = NativeShopTransaction.Begin(0, definitions, economy, vitals, history);
        Assert.Equal(3000u, shop.Gil);
        NativeEconomy committed = shop.Buy(191, 1, out bool accepted).Commit(economy);
        Assert.True(accepted);
        Assert.Equal(2000u, committed.PastGil);
        Assert.Equal(5000u, committed.Gil);
        Assert.Equal(economy.EventBankPastGil76, committed.EventBankPastGil76);
        NativeGuardianForceVital[] rows = vitals.GuardianForces.ToArray();
        rows[0] = rows[0] with { LearnedBank = null! };
        Assert.Throws<InvalidDataException>(() => (vitals with { GuardianForces = rows }).ShopAbilityMask());
    }

    private static NativeShopTransaction Open(GameAssets assets, NativeEconomy economy)
    {
        NativeShopDefinitions definitions = assets.ReadNativeShopDefinitions();
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals();
        NativeShopHistory history = assets.ReadNewGameShopHistory().Refresh(definitions, 0, vitals.ShopAbilityMask());
        return NativeShopTransaction.Begin(0, definitions, NativeShopTransaction.NormalizeInventory(economy), vitals, history);
    }
}
