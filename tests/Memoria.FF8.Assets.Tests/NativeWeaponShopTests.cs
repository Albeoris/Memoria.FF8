using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeWeaponShopTests
{
    private const string GameDirectory = @"C:\Steam\steamapps\common\FINAL FANTASY VIII";

    [Fact]
    public void AuthenticWeaponsRetain33RowsAndGenuineNonzeroRecipeWord()
    {
        using GameAssets assets = new(GameDirectory);
        NativeWeaponShopDefinitions definitions = assets.ReadNativeWeaponShopDefinitions();
        Assert.Equal("BB9B499DDE640362F8A16A76534E3FFFD695B2F3C58A73CB65EDC16500BD3BA8", definitions.RecipeHash);
        Assert.Equal(33, definitions.Recipes.Length);
        Assert.Equal("Revolver", definitions.Recipes[0].Name);
        Assert.Equal("Lion Heart", definitions.Recipes[6].Name);
        Assert.Equal(0, definitions.Recipes[0].Character);
        Assert.Equal(6, definitions.Recipes[28].Character);
        Assert.Equal(7, definitions.Recipes[29].Character);
        Assert.Equal(8, definitions.Recipes[30].Character);
        Assert.Equal(new NativeItemStock(109, 6), definitions.Recipes[0].Materials[0]);
        Assert.Equal(new NativeItemStock(118, 2), definitions.Recipes[0].Materials[1]);
        Assert.Equal(0x01088881u, assets.ReadNewGameWeaponRecipes().Bits);
        Assert.Equal(21, definitions.ArgumentMap[34]);
    }

    [Fact]
    public void ConstructorAndCancelPreserveWeaponsMembershipAndSeparateGilBanks()
    {
        using GameAssets assets = new(GameDirectory);
        NativeCharacterVitals original = assets.ReadNewGameCharacterVitals();
        NativeEconomy economy = assets.ReadNewGameEconomy();
        NativeWeaponRecipes recipes = assets.ReadNewGameWeaponRecipes();
        var prepared = NativeWeaponShopTransaction.Begin(34, assets.ReadNativeWeaponShopDefinitions(), economy, original, recipes);
        Assert.Equal(0, prepared.Shop.SelectedCharacter);
        Assert.Equal(1, prepared.Shop.EligibleMask);
        Assert.Contains((byte)0x40, prepared.Shop.AvailableRecipes);
        Assert.Equal(100u, prepared.Shop.Price(0));
        Assert.Equal(original.CombatParty, prepared.Vitals.CombatParty);
        Assert.Equal(original.Records.Select(row => row.Weapon), prepared.Vitals.Records.Select(row => row.Weapon));
        Assert.Equal(original.Records.Select(row => row.MemberFlags), prepared.Vitals.Records.Select(row => row.MemberFlags));
        Assert.All(prepared.Vitals.Records, row => Assert.Equal(row.HitPoints.Maximum, row.HitPoints.Minimum));
        Assert.Equal(9999, original.Records[7].HitPoints.Maximum);
        Assert.True(prepared.Vitals.Records[7].HitPoints.Maximum < 9999);
        NativeEconomy committed = prepared.Shop.Commit(prepared.Economy);
        Assert.Equal(economy.Gil, committed.Gil);
        Assert.Equal(economy.PastGil, committed.PastGil);
        Assert.Equal(economy.EventBankGil72, committed.EventBankGil72);
        Assert.Equal(economy.EventBankPastGil76, committed.EventBankPastGil76);
        Assert.Equal(prepared.Economy.Items, committed.Items);
        NativeWeaponShopTransaction restored = JsonSerializer.Deserialize<NativeWeaponShopTransaction>(JsonSerializer.Serialize(prepared.Shop))!;
        Assert.Equal(JsonSerializer.Serialize(committed), JsonSerializer.Serialize(restored.Commit(prepared.Economy)));
    }

    [Fact]
    public void EntryDiscoversAffordableMaterialRecipeWithoutUpgradeOrHistoryRng()
    {
        using GameAssets assets = new(GameDirectory);
        NativeWeaponShopDefinitions definitions = assets.ReadNativeWeaponShopDefinitions();
        NativeEconomy economy = assets.ReadNewGameEconomy().DeepCopy();
        economy.Items[0] = new(66, 1);
        economy.Items[1] = new(118, 4);
        NativeWeaponRecipes recipes = assets.ReadNewGameWeaponRecipes();
        Assert.Equal(0u, recipes.Bits & 2);
        var prepared = NativeWeaponShopTransaction.Begin(34, definitions, economy, assets.ReadNewGameCharacterVitals(), recipes);
        Assert.Equal(2u, prepared.Recipes.Bits & 2);
        Assert.Contains((byte)0xC1, prepared.Shop.AvailableRecipes);
        Assert.Equal(0u, recipes.Bits & 2);
        Assert.Equal(economy.Items, prepared.Economy.Items);
        Assert.Equal(economy.Gil, prepared.Shop.Gil);
        Assert.Equal(0, prepared.Vitals.Records[0].Weapon);
        Assert.Equal(1, prepared.Shop.Counts[66]);
        Assert.Equal(4, prepared.Shop.Counts[118]);
        var poor = NativeWeaponShopTransaction.Begin(34, definitions, economy with { Gil = 199 }, assets.ReadNewGameCharacterVitals(), recipes);
        Assert.Equal(0u, poor.Recipes.Bits & 2);
        Assert.DoesNotContain((byte)0xC1, poor.Shop.AvailableRecipes);
    }

    [Theory]
    [InlineData(100, 0x0300)]
    [InlineData(150, 0x0200)]
    [InlineData(300, 0x0000)]
    [InlineData(0, 0x0001)]
    public void PreviewUsesNativeSignedThresholdStatusWrite(int hp, int expected)
    {
        using GameAssets assets = new(GameDirectory);
        NativeCharacterVitals original = assets.ReadNewGameCharacterVitals().DeepCopy();
        original.Records[0] = original.Records[0] with { HitPoints = new((ushort)hp, (ushort)hp), Status = 0xFFFD };
        int maximum = original.Definitions.CharacterMaximum(original.Records[0]);
        Assert.Equal(486, maximum);
        NativeCharacterVitals next = original.PreviewWeaponShopRow(0, 6);
        ushort cleared = (ushort)(0xFFFD & 0xFCFE);
        Assert.Equal(hp == 0 ? (ushort)1 : (ushort)(cleared | expected), next.Records[0].Status);
        Assert.Equal(original.Records[0].MemberFlags, next.Records[0].MemberFlags);
        Assert.Equal(original.Records[0].Weapon, next.Records[0].Weapon);
    }

    [Fact]
    public void RemodelConsumesLocalMaterialsEquipsImmediatelyAndCommitsOnlyOnExit()
    {
        using GameAssets assets = new(GameDirectory);
        NativeEconomy economy = assets.ReadNewGameEconomy().DeepCopy();
        economy.Items[0] = new(66, 1);
        economy.Items[1] = new(118, 4);
        var prepared = NativeWeaponShopTransaction.Begin(34, assets.ReadNativeWeaponShopDefinitions(), economy, assets.ReadNewGameCharacterVitals(), assets.ReadNewGameWeaponRecipes());
        var remodeled = prepared.Shop.Remodel(1, prepared.Vitals, prepared.Recipes, out bool accepted);
        Assert.True(accepted);
        Assert.Equal(1, remodeled.Vitals.Records[0].Weapon);
        Assert.Equal(0, prepared.Vitals.Records[0].Weapon);
        Assert.Equal(prepared.Shop.Gil - prepared.Shop.Price(1), remodeled.Shop.Gil);
        Assert.Equal(0, remodeled.Shop.Counts[66]);
        Assert.Equal(0, remodeled.Shop.Counts[118]);
        Assert.Equal(economy.Gil, prepared.Economy.Gil);
        Assert.Equal(1, prepared.Shop.Counts[66]);
        Assert.Contains((byte)0x41, remodeled.Shop.AvailableRecipes);
        var rejected = remodeled.Shop.Remodel(1, remodeled.Vitals, remodeled.Recipes, out accepted);
        Assert.False(accepted);
        Assert.Same(remodeled.Shop, rejected.Shop);
        NativeWeaponShopTransaction restored = JsonSerializer.Deserialize<NativeWeaponShopTransaction>(JsonSerializer.Serialize(remodeled.Shop))!;
        NativeEconomy committed = restored.Commit(prepared.Economy);
        Assert.Equal(remodeled.Shop.Gil, committed.Gil);
        Assert.DoesNotContain(committed.Items, item => item.ItemId is 66 or 118);
        Assert.Equal(economy.PastGil, committed.PastGil);
    }

    [Fact]
    public void CharacterSelectionUsesPhysicalRowsAndRejectsAnotherCharactersRecipe()
    {
        using GameAssets assets = new(GameDirectory);
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals().DeepCopy();
        vitals.Records[1] = vitals.Records[1] with { MemberFlags = (ushort)(vitals.Records[1].MemberFlags | 1) };
        var prepared = NativeWeaponShopTransaction.Begin(34, assets.ReadNativeWeaponShopDefinitions(), assets.ReadNewGameEconomy(), vitals, assets.ReadNewGameWeaponRecipes());
        var selected = prepared.Shop.SelectCharacter(1, prepared.Recipes);
        Assert.Equal(1, selected.Shop.SelectedCharacter);
        Assert.NotEmpty(selected.Shop.AvailableRecipes);
        Assert.All(selected.Shop.AvailableRecipes, offer => Assert.Equal(1, selected.Shop.Definitions.Recipes[offer & 0x3F].Character));
        selected.Shop.Remodel(0, prepared.Vitals, selected.Recipes, out bool accepted);
        Assert.False(accepted);
        Assert.Throws<ArgumentOutOfRangeException>(() => prepared.Shop.SelectCharacter(6, prepared.Recipes));
    }

    [Fact]
    public void RemodelUsesDiscountButAdmissionStillRequiresBaseFeeAndRejectsEquippedRecipe()
    {
        using GameAssets assets = new(GameDirectory);
        NativeEconomy economy = assets.ReadNewGameEconomy().DeepCopy();
        economy.Items[0] = new(66, 2);
        economy.Items[1] = new(118, 8);
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals().DeepCopy();
        vitals.GuardianForces[0] = vitals.GuardianForces[0] with { Flags = 1, LearnedBank = new(0, 1UL << (92 - 64)) };
        var prepared = NativeWeaponShopTransaction.Begin(34, assets.ReadNativeWeaponShopDefinitions(), economy with { Gil = 200 }, vitals, assets.ReadNewGameWeaponRecipes());
        Assert.Equal(150u, prepared.Shop.Price(1));
        var remodeled = prepared.Shop.Remodel(1, prepared.Vitals, prepared.Recipes, out bool accepted);
        Assert.True(accepted);
        Assert.Equal(50u, remodeled.Shop.Gil);
        NativeWeaponShopTransaction funded = remodeled.Shop with { Gil = 200, AvailableRecipes = prepared.Shop.AvailableRecipes };
        var repeated = funded.Remodel(1, remodeled.Vitals, remodeled.Recipes, out accepted);
        Assert.False(accepted);
        Assert.Same(funded, repeated.Shop);
        var poor = NativeWeaponShopTransaction.Begin(34, assets.ReadNativeWeaponShopDefinitions(), economy with { Gil = 199 }, vitals, assets.ReadNewGameWeaponRecipes());
        poor.Shop.Remodel(1, poor.Vitals, poor.Recipes, out accepted);
        Assert.False(accepted);
    }

    [Fact]
    public void AmbiguousNativeHpStatusRefusesBeforeAnyInputOwnerMutation()
    {
        using GameAssets assets = new(GameDirectory);
        NativeCharacterVitals original = assets.ReadNewGameCharacterVitals().DeepCopy();
        original.Records[0] = original.Records[0] with { HitPoints = new(0, 100) };
        string before = JsonSerializer.Serialize(original);
        Assert.Throws<NotSupportedException>(() => NativeWeaponShopTransaction.Begin(34, assets.ReadNativeWeaponShopDefinitions(), assets.ReadNewGameEconomy(), original, assets.ReadNewGameWeaponRecipes()));
        Assert.Equal(before, JsonSerializer.Serialize(original));
        Assert.Throws<NotSupportedException>(() => NativeWeaponShopTransaction.Begin(0, assets.ReadNativeWeaponShopDefinitions(), assets.ReadNewGameEconomy(), assets.ReadNewGameCharacterVitals(), assets.ReadNewGameWeaponRecipes()));
    }
}
