using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeRefinementTests
{
    [Theory]
    [InlineData(97)]
    [InlineData(98)]
    [InlineData(99)]
    [InlineData(100)]
    [InlineData(101)]
    [InlineData(102)]
    [InlineData(103)]
    [InlineData(104)]
    [InlineData(105)]
    [InlineData(106)]
    [InlineData(107)]
    [InlineData(108)]
    [InlineData(109)]
    [InlineData(110)]
    [InlineData(111)]
    [InlineData(112)]
    [InlineData(113)]
    [InlineData(114)]
    [InlineData(115)]
    public void EveryOriginalRefinementAbilityConsumesItsActualIngredientAndPersistsOutput(int abilityId)
    {
        using GameAssets assets = Assets();
        NativeRefinementDefinitions definitions = assets.ReadNativeRefinementDefinitions();
        NativeRefinementAbility ability = definitions.Abilities[abilityId - 92];
        NativeRefinementRecipe recipe = definitions.Recipe(abilityId, ability.First);
        NativeCharacterVitals vitals = Learned(assets, abilityId);
        NativeEconomy economy = assets.ReadNewGameEconomy() with { Items = Enumerable.Repeat(new NativeItemStock(0, 0), 198).ToArray() };
        NativeCardInventory cards = NativeCardInventory.CreateNewGame();
        if (ability.Kind == 2)
        {
            vitals.Records[0].MagicInventory[0] = new(recipe.Ingredient, recipe.IngredientCount);
        }
        else if (ability.Kind == 4)
        {
            for (int index = 0; index < recipe.IngredientCount; index++)
            {
                cards = cards.Acquire(recipe.Ingredient, out _);
            }
        }
        else
        {
            economy.Items[0] = new(recipe.Ingredient, recipe.IngredientCount);
        }
        string before = JsonSerializer.Serialize(new { vitals, economy, cards });
        Assert.Equal(1, definitions.MaximumBatches(vitals, economy, cards, abilityId, ability.First, 0));
        NativeRefinementResult result = definitions.Refine(vitals, economy, cards, abilityId, ability.First, 0, 1);
        int output = ability.Kind is 0 or 2 ? result.Vitals.Records[0].MagicInventory.Single(stock => stock.SpellId == recipe.Output).Count : result.Economy.Items.Single(stock => stock.ItemId == recipe.Output).Count;
        Assert.Equal(Math.Min(100, (int)recipe.OutputCount), output);
        if (ability.Kind == 2)
        {
            Assert.DoesNotContain(result.Vitals.Records[0].MagicInventory, stock => stock.SpellId == recipe.Ingredient);
        }
        else if (ability.Kind == 4)
        {
            Assert.Equal(0, result.Cards!.AvailableCount(recipe.Ingredient));
        }
        else
        {
            Assert.DoesNotContain(result.Economy.Items, stock => stock.ItemId == recipe.Ingredient);
        }
        Assert.Equal(before, JsonSerializer.Serialize(new { vitals, economy, cards }));
        NativeRefinementResult restored = JsonSerializer.Deserialize<NativeRefinementResult>(JsonSerializer.Serialize(result))!;
        Assert.Equal(output, ability.Kind is 0 or 2 ? restored.Vitals.Records[0].MagicInventory.Single(stock => stock.SpellId == recipe.Output).Count : restored.Economy.Items.Single(stock => stock.ItemId == recipe.Output).Count);
    }

    [Fact]
    public void LastBatchRoundsUpAtCapacityAndDoesNotRequireJunctionedGuardianForce()
    {
        using GameAssets assets = Assets();
        NativeRefinementDefinitions definitions = assets.ReadNativeRefinementDefinitions();
        NativeCharacterVitals vitals = Learned(assets, 97);
        NativeRefinementRecipe recipe = definitions.Recipe(97, 0);
        vitals.Records[0].MagicInventory[0] = new(recipe.Output, 99);
        Assert.Empty(vitals.Records[0].Junction.GuardianForceAssignments);
        NativeEconomy economy = assets.ReadNewGameEconomy().AddItem(recipe.Ingredient, 2, out _);
        Assert.Equal(1, definitions.MaximumBatches(vitals, economy, null, 97, 0, 0));
        NativeRefinementResult result = definitions.Refine(vitals, economy, null, 97, 0, 0, 1);
        Assert.Equal(100, result.Vitals.Records[0].MagicInventory[0].Count);
        Assert.Equal(1, result.Economy.Items.Single(stock => stock.ItemId == recipe.Ingredient).Count);
        Assert.Equal(0, definitions.MaximumBatches(result.Vitals, result.Economy, null, 97, 0, 0));
    }

    [Fact]
    public void MagicUpgradeClearsExhaustedStatJunctionAndRebuildsHitPointCap()
    {
        using GameAssets assets = Assets();
        NativeRefinementDefinitions definitions = assets.ReadNativeRefinementDefinitions();
        NativeCharacterVitals vitals = Learned(assets, 112);
        NativeRefinementRecipe recipe = definitions.Recipe(112, 0);
        vitals.Records[0].MagicInventory[0] = new(recipe.Ingredient, recipe.IngredientCount);
        vitals.Records[0].Junction.MagicJunctions[0] = recipe.Ingredient;
        NativeRefinementResult result = definitions.Refine(vitals, assets.ReadNewGameEconomy(), null, 112, 0, 0, 1);
        Assert.Equal(0, result.Vitals.Records[0].Junction.MagicJunctions[0]);
        Assert.True(result.Vitals.Records[0].HitPoints.Maximum <= result.Vitals.Definitions.CharacterMaximum(result.Vitals.Records[0]));
    }

    [Fact]
    public void RefinementConstructorNormalizesZeroCountOutputBeforeAdmissionAndInsertion()
    {
        using GameAssets assets = Assets();
        NativeRefinementDefinitions definitions = assets.ReadNativeRefinementDefinitions();
        NativeRefinementRecipe recipe = definitions.Recipe(97, 0);
        NativeCharacterVitals vitals = Learned(assets, 97);
        for (int index = 0; index < 32; index++)
        {
            int spell = index + 1;
            if (spell >= recipe.Output)
            {
                spell++;
            }
            vitals.Records[0].MagicInventory[index] = new((byte)spell, 1);
        }
        vitals.Records[0].MagicInventory[0] = new(recipe.Output, 0);
        vitals.Records[1].MagicInventory[0] = new(recipe.Output, 0);
        vitals.Records[1].Junction.MagicJunctions[19] = recipe.Output;
        string original = JsonSerializer.Serialize(vitals);
        NativeEconomy economy = assets.ReadNewGameEconomy().AddItem(recipe.Ingredient, 1, out _);
        Assert.Equal(1, definitions.MaximumBatches(vitals, economy, null, 97, 0, 0));
        NativeRefinementResult result = definitions.Refine(vitals, economy, null, 97, 0, 0, 1);
        Assert.Equal(recipe.OutputCount, result.Vitals.Records[0].MagicInventory[0].Count);
        Assert.Equal(recipe.Output, result.Vitals.Records[0].MagicInventory[0].SpellId);
        Assert.Equal(new NativeMagicStock(0, 0), result.Vitals.Records[1].MagicInventory[0]);
        Assert.Equal(0, result.Vitals.Records[1].Junction.MagicJunctions[19]);
        Assert.Equal(original, JsonSerializer.Serialize(vitals));
    }

    [Fact]
    public void ReserveMagicUpgradeRebuildsItsOwnHitPointsStatusAndAllTwentyJunctions()
    {
        using GameAssets assets = Assets();
        NativeRefinementDefinitions definitions = assets.ReadNativeRefinementDefinitions();
        NativeCharacterVitals vitals = Learned(assets, 112);
        NativeRefinementRecipe recipe = definitions.Recipe(112, 0);
        const int row = 1;
        Assert.DoesNotContain((byte)row, vitals.CombatParty);
        NativeCharacterVital target = vitals.Records[row] with { MemberFlags = (ushort)(vitals.Records[row].MemberFlags | 1), Status = 0x0381 };
        target.MagicInventory[0] = new(recipe.Ingredient, recipe.IngredientCount);
        target.MagicInventory[1] = new(0, 7);
        target.Junction.MagicJunctions[0] = recipe.Ingredient;
        target.Junction.MagicJunctions[19] = recipe.Ingredient;
        ushort beforeMaximum = vitals.Definitions.CharacterMaximum(target);
        vitals.Records[row] = target with { HitPoints = new(beforeMaximum, beforeMaximum) };
        vitals.Records[2] = vitals.Records[2] with { HitPoints = new(65535, 65535) };
        string original = JsonSerializer.Serialize(vitals);
        NativeRefinementResult result = definitions.Refine(vitals, assets.ReadNewGameEconomy(), null, 112, 0, row, 1);
        NativeCharacterVital rebuilt = result.Vitals.Records[row];
        ushort maximum = result.Vitals.Definitions.CharacterMaximum(rebuilt);
        Assert.True(beforeMaximum > maximum);
        Assert.Equal(new NativeHpWord(maximum, maximum), rebuilt.HitPoints);
        Assert.Equal(0x80, rebuilt.Status);
        Assert.Equal(0, rebuilt.Junction.MagicJunctions[0]);
        Assert.Equal(0, rebuilt.Junction.MagicJunctions[19]);
        Assert.DoesNotContain(rebuilt.MagicInventory, stock => stock.SpellId == 0 && stock.Count != 0);
        Assert.Equal(new NativeHpWord(65535, 65535), result.Vitals.Records[2].HitPoints);
        Assert.Equal(vitals.CombatParty, result.Vitals.CombatParty);
        Assert.Equal(original, JsonSerializer.Serialize(vitals));
    }

    [Fact]
    public void UnlearnedWrongRecipeAndFullMagicInventoryRefuseBeforeSpending()
    {
        using GameAssets assets = Assets();
        NativeRefinementDefinitions definitions = assets.ReadNativeRefinementDefinitions();
        NativeRefinementRecipe recipe = definitions.Recipe(97, 0);
        NativeEconomy economy = assets.ReadNewGameEconomy().AddItem(recipe.Ingredient, 1, out _);
        NativeCharacterVitals original = assets.ReadNewGameCharacterVitals();
        Assert.Throws<InvalidOperationException>(() => definitions.Refine(original, economy, null, 97, 0, 0, 1));
        NativeCharacterVitals vitals = Learned(assets, 97);
        for (int index = 0; index < 32; index++)
        {
            int spell = index + 1;
            if (spell >= recipe.Output)
            {
                spell++;
            }
            vitals.Records[0].MagicInventory[index] = new((byte)spell, 1);
        }
        string before = JsonSerializer.Serialize(new { vitals, economy });
        Assert.Throws<InvalidOperationException>(() => definitions.Refine(vitals, economy, null, 97, 0, 0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => definitions.Refine(vitals, economy, null, 97, 7, 0, 1));
        Assert.Equal(before, JsonSerializer.Serialize(new { vitals, economy }));
    }

    [Fact]
    public void RareCardModConsumesPossessionAndRetainsEverObtainedFlag()
    {
        using GameAssets assets = Assets();
        NativeRefinementDefinitions definitions = assets.ReadNativeRefinementDefinitions();
        NativeCardInventory cards = NativeCardInventory.CreateNewGame().Acquire(109, out _);
        NativeRefinementResult result = definitions.Refine(Learned(assets, 115), assets.ReadNewGameEconomy(), cards, 115, 109, 0, 1);
        Assert.Equal(0, result.Cards!.AvailableCount(109));
        Assert.Equal(0, result.Cards.Cards[109]);
        Assert.Equal(cards.ObtainedRareCards, result.Cards.ObtainedRareCards);
        Assert.Equal(1, cards.AvailableCount(109));
    }

    private static NativeCharacterVitals Learned(GameAssets assets, int ability)
    {
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals().DeepCopy();
        vitals.GuardianForces[0] = vitals.GuardianForces[0] with { Flags = 1, LearnedBank = new(0, 1UL << (ability - 64)) };
        return vitals;
    }

    private static GameAssets Assets()
    {
        const string directory = @"C:\Steam\steamapps\common\FINAL FANTASY VIII";
        Assert.SkipUnless(Directory.Exists(directory), "Installed original refinement resources are required.");
        return new GameAssets(directory);
    }
}
