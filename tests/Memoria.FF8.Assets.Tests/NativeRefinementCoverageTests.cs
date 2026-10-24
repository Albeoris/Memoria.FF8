using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeRefinementCoverageTests
{
    public static IEnumerable<object[]> OriginalAbilities()
    {
        for (int ability = 97; ability <= 115; ability++)
        {
            foreach (bool shared in new[] { false, true })
            {
                foreach (byte outputStock in new byte[] { 0, 99 })
                {
                    yield return [ability, shared, outputStock];
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(OriginalAbilities))]
    public void EveryOriginalRecipeHonorsItsOwnerBatchLimitAndAtomicity(int abilityId, bool shared, byte outputStock)
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        NativeRefinementDefinitions definitions = assets.ReadNativeRefinementDefinitions();
        NativeRefinementAbility ability = definitions.Abilities[abilityId - 92];
        NativeCharacterVitals initial = assets.ReadNewGameCharacterVitals();
        NativeEconomy initialEconomy = assets.ReadNewGameEconomy();
        const int row = 1;
        for (int recipeIndex = ability.First; recipeIndex <= ability.Last; recipeIndex++)
        {
            NativeRefinementRecipe recipe = definitions.Recipe(abilityId, recipeIndex);
            NativeCharacterVitals vitals = initial.DeepCopy();
            vitals.GuardianForces[0] = vitals.GuardianForces[0] with { Flags = 1, LearnedBank = new(0, 1UL << (abilityId - 64)) };
            foreach (NativeCharacterVital record in vitals.Records)
            {
                Array.Fill(record.MagicInventory, new NativeMagicStock(0, 0));
                Array.Clear(record.Junction.MagicJunctions);
            }
            vitals = vitals with { MagicSystem = vitals.MagicSystem with { SharedMagicStock = shared } };
            NativeEconomy economy = initialEconomy.DeepCopy();
            Array.Fill(economy.Items, new NativeItemStock(0, 0));
            NativeCardInventory cards = NativeCardInventory.CreateNewGame();
            int ingredients = ability.Kind == 4 && recipe.Ingredient >= NativeCardInventory.CommonCardCount ? 1 : 100;
            if (ability.Kind == 2)
            {
                vitals = vitals.ChangeMagicStock(row, recipe.Ingredient, ingredients, MagicOperationReason.ScriptReward, out _);
            }
            else if (ability.Kind == 4)
            {
                for (int copy = 0; copy < ingredients; copy++)
                {
                    cards = cards.Acquire(recipe.Ingredient, out _);
                }
            }
            else
            {
                economy.Items[0] = new(recipe.Ingredient, (byte)ingredients);
            }
            if (ability.Kind is 0 or 2)
            {
                vitals = vitals.ChangeMagicStock(row, recipe.Output, outputStock, MagicOperationReason.ScriptReward, out _);
            }
            else if (outputStock > 0)
            {
                economy.Items[1] = new(recipe.Output, outputStock);
            }
            string context = $"Ability {abilityId}, recipe {recipeIndex}, kind {ability.Kind}, shared {shared}, output {outputStock}";
            string before = JsonSerializer.Serialize(new { vitals, economy, cards });
            int expected = recipe.Requirement > 100 ? 0 : Math.Min(ingredients / recipe.IngredientCount, (100 - outputStock + recipe.OutputCount - 1) / recipe.OutputCount);
            int maximum = definitions.MaximumBatches(vitals, economy, cards, abilityId, recipeIndex, row);
            Assert.True(expected == maximum, $"{context}: expected maximum {expected}, actual {maximum}.");
            Assert.Throws<InvalidOperationException>(() => definitions.Refine(vitals, economy, cards, abilityId, recipeIndex, row, maximum + 1));
            if (maximum > 0)
            {
                NativeRefinementResult result = definitions.Refine(vitals, economy, cards, abilityId, recipeIndex, row, maximum);
                int output = ability.Kind is 0 or 2 ? result.Vitals.GetMagicCount(row, recipe.Output) : result.Economy.Items.Single(stock => stock.ItemId == recipe.Output).Count;
                int remaining = ability.Kind == 2 ? result.Vitals.GetMagicCount(row, recipe.Ingredient) : ability.Kind == 4 ? result.Cards!.AvailableCount(recipe.Ingredient) : result.Economy.Items.FirstOrDefault(stock => stock.ItemId == recipe.Ingredient)?.Count ?? 0;
                Assert.True(output == Math.Min(100, outputStock + maximum * recipe.OutputCount), $"{context}: output {output}.");
                Assert.True(remaining == ingredients - maximum * recipe.IngredientCount, $"{context}: remaining ingredient {remaining}.");
                Assert.Equal(cards.ObtainedRareCards, result.Cards!.ObtainedRareCards);
                if (ability.Kind is 0 or 2)
                {
                    Assert.Equal(output, result.Vitals.Records[shared ? 0 : row].MagicInventory.Single(stock => stock.SpellId == recipe.Output).Count);
                    Assert.All(result.Vitals.Records[shared ? row : 0].MagicInventory, stock => Assert.Equal(0, stock.Count));
                }
                Assert.Equal(0, definitions.MaximumBatches(result.Vitals, result.Economy, result.Cards, abilityId, recipeIndex, row));
            }
            Assert.Equal(before, JsonSerializer.Serialize(new { vitals, economy, cards }));
        }
    }
}
