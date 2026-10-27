using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeRefinementSelectorTests
{
    [Fact]
    public void DuplicateIngredientsUseFirstMatchingRecipeAndMissingCardCannotMutateOwners()
    {
        NativeCharacterVitals vitals = MagicSystemFixture.Create();
        vitals = vitals with { KernelSha256 = new string('A', 64), MenuDefinitions = vitals.MenuDefinitions with { Learning = vitals.MenuDefinitions.Learning.SetItem(0, vitals.MenuDefinitions.Learning[0].SetItem(0, new(1, 255, 115))) } };
        vitals.GuardianForces[0] = vitals.GuardianForces[0] with { LearnedBank = new(0, 1UL << (115 - 64)) };
        NativeEconomy economy = new() { Version = 1, Rule = NativeEconomy.InitializationRule, InitOutSha256 = vitals.InitOutSha256, Items = Enumerable.Repeat(new NativeItemStock(0, 0), NativeEconomy.ItemSlotCount).ToArray(), Gil = 0, PastGil = 0, EventBankGil72 = 0, EventBankPastGil76 = 0 };
        NativeCardInventory cards = NativeCardInventory.CreateNewGame().Acquire(24, out _).Acquire(27, out _);
        NativeRefinementDefinitions definitions = Definitions();
        ImmutableArray<NativeRefinementRecipe> cardTable = definitions.Tables[4].SetItem(24, new(1, 1, 24, 1, 198)).SetItem(27, new(1, 1, 24, 1, 197));
        definitions = definitions with { Tables = definitions.Tables.SetItem(4, cardTable) };
        Assert.Equal(24, definitions.FindRecipeIndex(115, 24));
        Assert.Equal(-1, definitions.FindRecipeIndex(115, 27));
        string before = JsonSerializer.Serialize(new { vitals, economy, cards });
        Assert.Equal(0, definitions.MaximumBatches(vitals, economy, cards, 115, 27, 0));
        Assert.Throws<InvalidOperationException>(() => definitions.Refine(vitals, economy, cards, 115, 27, 0, 1));
        Assert.Equal(before, JsonSerializer.Serialize(new { vitals, economy, cards }));
        NativeRefinementDefinitions restored = JsonSerializer.Deserialize<NativeRefinementDefinitions>(JsonSerializer.Serialize(definitions))!;
        Assert.Equal(definitions.TableHashes, restored.TableHashes);
        Assert.Equal(-1, restored.FindRecipeIndex(115, 27));
        NativeRefinementResult result = restored.Refine(vitals, economy, cards, 115, 24, 0, 1);
        Assert.Equal(1, result.Economy.Items.Single(stock => stock.ItemId == 198).Count);
        Assert.DoesNotContain(result.Economy.Items, stock => stock.ItemId == 197);
        Assert.Equal(0, result.Cards!.AvailableCount(24));
        Assert.Equal(1, result.Cards.AvailableCount(27));
        Assert.Equal(before, JsonSerializer.Serialize(new { vitals, economy, cards }));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void NonCardSelectorsAlsoUseTheFirstMatchWithinTheirKernelRange(int kind)
    {
        NativeRefinementDefinitions definitions = Definitions();
        NativeRefinementAbility ability = new("Synthetic", (byte)kind, 1, 3);
        ImmutableArray<NativeRefinementRecipe> table = definitions.Tables[kind].SetItem(0, new(1, 1, 7, 1, 1)).SetItem(1, new(1, 1, 8, 1, 1)).SetItem(2, new(1, 1, 7, 1, 2)).SetItem(3, new(1, 1, 7, 1, 3));
        definitions = definitions with { Abilities = definitions.Abilities.SetItem(5, ability), Tables = definitions.Tables.SetItem(kind, table) };
        Assert.Equal(2, definitions.FindRecipeIndex(97, 7));
        Assert.Equal(-1, definitions.FindRecipeIndex(97, 9));
    }

    [Fact]
    public void FullByteRangeRetainsNativeZeroCountWrap()
    {
        NativeRefinementDefinitions definitions = Definitions();
        definitions = definitions with { Abilities = definitions.Abilities.SetItem(5, new("Synthetic", 0, 0, 255)), Tables = definitions.Tables.SetItem(0, Enumerable.Repeat(new NativeRefinementRecipe(1, 1, 1, 1, 1), 256).ToImmutableArray()) };
        Assert.Equal(-1, definitions.FindRecipeIndex(97, 1));
    }

    private static NativeRefinementDefinitions Definitions()
    {
        ImmutableArray<ImmutableArray<NativeRefinementRecipe>> tables = new[] { 102, 143, 10, 12, 110 }.Select((count, kind) => Enumerable.Range(0, count).Select(index => new NativeRefinementRecipe(1, 1, kind == 4 ? (byte)index : (byte)1, 1, 1)).ToImmutableArray()).ToImmutableArray();
        ImmutableArray<NativeRefinementAbility> abilities = Enumerable.Range(0, 24).Select(_ => new NativeRefinementAbility("Synthetic", 4, 0, 109)).ToImmutableArray();
        return new(NativeFieldRuntimeProfile.FF8EnExecutableHash, new string('A', 64), abilities, tables) { TableHashes = Enumerable.Repeat(new string('B', 64), 5).ToImmutableArray() };
    }
}
