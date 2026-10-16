using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeMagicRefinementTests
{
    private const string GameDirectory = @"C:\Steam\steamapps\common\FINAL FANTASY VIII";

    [Fact]
    public void OriginalResourcesExposeAllMagicRecipesAndActualAbilityRanges()
    {
        using GameAssets assets = new(GameDirectory);
        NativeRefinementDefinitions catalog = assets.ReadNativeRefinementDefinitions();
        Assert.Equal(112, catalog.Tables[0].Length + catalog.Tables[2].Length);
        Assert.Equal(102, catalog.Tables[0].Length);
        Assert.Equal(new NativeRefinementRecipe(20, 1, 128, 1, 8), catalog.Recipe(97, 0));
        Assert.Equal(new NativeRefinementRecipe(1, 1, 1, 5, 2), catalog.Recipe(112, 0));
        Assert.Equal(7, catalog.Abilities[97 - 92].Last - catalog.Abilities[97 - 92].First + 1);
        Assert.Equal(4, catalog.Abilities[112 - 92].Last - catalog.Abilities[112 - 92].First + 1);
    }

    [Fact]
    public void ItemManufactureUsesCeilingCapacityConsumesFullInputAndPreservesOriginalOwners()
    {
        using GameAssets assets = new(GameDirectory);
        NativeRefinementDefinitions catalog = assets.ReadNativeRefinementDefinitions();
        NativeCharacterVitals vitals = Learn(assets.ReadNewGameCharacterVitals(), 97);
        vitals.Records[0].MagicInventory[0] = new NativeMagicStock(8, 90);
        NativeEconomy economy = assets.ReadNewGameEconomy();
        economy.Items[0] = new NativeItemStock(128, 2);
        Assert.Equal(1, catalog.MaximumBatches(vitals, economy, null, 97, 0, 0));
        NativeRefinementResult result = catalog.Refine(vitals, economy, null, 97, 0, 0, 1);
        Assert.Equal(100, result.Vitals.Records[0].MagicInventory.Single(stock => stock.SpellId == 8).Count);
        Assert.Equal(new NativeItemStock(128, 1), result.Economy.Items[0]);
        Assert.Equal(90, vitals.Records[0].MagicInventory[0].Count);
        Assert.Equal(2, economy.Items[0].Count);
        Assert.Throws<InvalidOperationException>(() => catalog.Refine(vitals, economy, null, 97, 0, 0, 2));
    }

    [Fact]
    public void MagicUpgradeClearsDepletedJunctionAndRequiresLearnedAcquiredGf()
    {
        using GameAssets assets = new(GameDirectory);
        NativeRefinementDefinitions catalog = assets.ReadNativeRefinementDefinitions();
        NativeCharacterVitals original = assets.ReadNewGameCharacterVitals();
        Assert.DoesNotContain(112, catalog.LearnedAbilities(original));
        NativeCharacterVitals vitals = Learn(original, 112);
        vitals.Records[0].MagicInventory[0] = new NativeMagicStock(1, 10);
        vitals.Records[0].Junction.MagicJunctions[0] = 1;
        NativeEconomy economy = assets.ReadNewGameEconomy();
        Assert.Equal(2, catalog.MaximumBatches(vitals, economy, null, 112, 0, 0));
        NativeRefinementResult result = catalog.Refine(vitals, economy, null, 112, 0, 0, 2);
        Assert.DoesNotContain(result.Vitals.Records[0].MagicInventory, stock => stock.SpellId == 1);
        Assert.Contains(result.Vitals.Records[0].MagicInventory, stock => stock.SpellId == 2 && stock.Count == 2);
        Assert.Equal(0, result.Vitals.Records[0].Junction.MagicJunctions[0]);
        Assert.Equal(economy.Items, result.Economy.Items);
        NativeGuardianForceVital[] unacquired = vitals.GuardianForces.ToArray();
        unacquired[0] = unacquired[0] with { Flags = 0 };
        Assert.DoesNotContain(112, catalog.LearnedAbilities(vitals with { GuardianForces = unacquired }));
    }

    [Fact]
    public void FullSpellBankRefusesManufactureWithoutConsumingItems()
    {
        using GameAssets assets = new(GameDirectory);
        NativeRefinementDefinitions catalog = assets.ReadNativeRefinementDefinitions();
        NativeCharacterVitals vitals = Learn(assets.ReadNewGameCharacterVitals(), 97);
        int[] ids = Enumerable.Range(1, 56).Where(id => id != 8).Take(32).ToArray();
        for (int slot = 0; slot < 32; slot++)
        {
            vitals.Records[0].MagicInventory[slot] = new NativeMagicStock((byte)ids[slot], 1);
        }
        NativeEconomy economy = assets.ReadNewGameEconomy();
        economy.Items[0] = new NativeItemStock(128, 2);
        Assert.Equal(0, catalog.MaximumBatches(vitals, economy, null, 97, 0, 0));
        Assert.Throws<InvalidOperationException>(() => catalog.Refine(vitals, economy, null, 97, 0, 0, 1));
        Assert.Equal(2, economy.Items[0].Count);
    }

    [Fact]
    public void MixedOwnersAndCorruptNonemptyStocksRefuseBeforeMutation()
    {
        using GameAssets assets = new(GameDirectory);
        NativeRefinementDefinitions catalog = assets.ReadNativeRefinementDefinitions();
        NativeCharacterVitals vitals = Learn(assets.ReadNewGameCharacterVitals(), 97);
        NativeEconomy economy = assets.ReadNewGameEconomy();
        economy.Items[0] = new NativeItemStock(128, 2);
        Assert.Throws<InvalidDataException>(() => catalog.MaximumBatches(vitals, economy with { InitOutSha256 = new string('0', 64) }, null, 97, 0, 0));
        Assert.Throws<NotSupportedException>(() => catalog.MaximumBatches(vitals with { KernelSha256 = new string('0', 64) }, economy, null, 97, 0, 0));
        vitals.Records[0].MagicInventory[0] = new NativeMagicStock(8, 255);
        Assert.Throws<InvalidDataException>(() => catalog.MaximumBatches(vitals, economy, null, 97, 0, 0));
        vitals.Records[0].MagicInventory[0] = new NativeMagicStock(0, 255);
        Assert.Equal(2, catalog.MaximumBatches(vitals, economy, null, 97, 0, 0));
        NativeRefinementResult result = catalog.Refine(vitals, economy, null, 97, 0, 0, 1);
        Assert.Equal(new NativeMagicStock(8, 20), result.Vitals.Records[0].MagicInventory[0]);
        economy.Items[0] = new NativeItemStock(128, 255);
        Assert.Throws<InvalidDataException>(() => catalog.MaximumBatches(vitals, economy, null, 97, 0, 0));
        economy.Items[0] = new NativeItemStock(128, 2);
        economy.Items[1] = new NativeItemStock(128, 1);
        Assert.Throws<InvalidDataException>(() => catalog.MaximumBatches(vitals, economy, null, 97, 0, 0));
    }

    private static NativeCharacterVitals Learn(NativeCharacterVitals original, int ability)
    {
        NativeCharacterVitals vitals = original.DeepCopy();
        NativeGuardianForceVital gf = vitals.GuardianForces[0];
        vitals.GuardianForces[0] = gf with { Flags = (byte)(gf.Flags | 1), LearnedBank = gf.LearnedBank with { High = gf.LearnedBank.High | (1UL << (ability - 64)) } };
        return vitals;
    }
}
