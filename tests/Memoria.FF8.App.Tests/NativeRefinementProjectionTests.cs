using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeRefinementProjectionTests
{
    [Fact]
    public void EmptyInventoryHidesRecipesButRetainsLearnedAbilitiesWithoutMutation()
    {
        using GameAssets assets = InstalledAssets();
        GameState state = EmptyState(assets);
        NativeRefinementDefinitions definitions = assets.ReadNativeRefinementDefinitions();
        string before = JsonSerializer.Serialize(state);

        MenuData data = Project(assets, state, definitions);

        Assert.Empty(data.Recipes);
        Assert.Equal(definitions.LearnedAbilities(state.CharacterVitals!).Select(id => definitions.Abilities[id - 92].Name), data.RefinementAbilities);
        Assert.Equal(19, data.RefinementAbilities.Count);
        Assert.Contains("native-refine", data.SupportedActions);
        Assert.Equal(before, JsonSerializer.Serialize(state));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void OwnedIngredientsRemainVisibleWithNoOutputCapacity(int kind)
    {
        using GameAssets assets = InstalledAssets();
        GameState state = EmptyState(assets);
        NativeRefinementDefinitions definitions = assets.ReadNativeRefinementDefinitions();
        (int abilityId, int recipeIndex, NativeRefinementRecipe recipe) = FirstRecipe(definitions, kind);
        if (kind == 4)
        {
            state = state with { CardInventory = state.CardInventory!.Acquire(recipe.Ingredient, out _) };
        }
        else if (kind == 2)
        {
            state.CharacterVitals!.Records[0].MagicInventory[0] = new(recipe.Ingredient, 1);
        }
        else
        {
            state.Economy!.Items[0] = new(recipe.Ingredient, 1);
        }
        if (kind is 0 or 2)
        {
            foreach (NativeCharacterVital record in state.CharacterVitals!.Records)
            {
                record.MagicInventory[1] = new(recipe.Output, 100);
            }
        }
        else
        {
            state.Economy!.Items[1] = new(recipe.Output, 100);
        }

        MenuRecipe projected = Assert.Single(Project(assets, state, definitions).Recipes, entry => entry.NativeAbility == abilityId && entry.NativeRecipeIndex == recipeIndex);

        Assert.Equal(1, projected.IngredientStock);
        Assert.All(projected.MaximumBatches.Values, maximum => Assert.Equal(0, maximum));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CardHistoryHidesOwnedRecipeOnlyInOnceMode(bool once)
    {
        using GameAssets assets = InstalledAssets();
        GameState state = EmptyState(assets);
        NativeRefinementDefinitions definitions = assets.ReadNativeRefinementDefinitions();
        (int abilityId, int recipeIndex, NativeRefinementRecipe recipe) = FirstRecipe(definitions, 4);
        state = state with
        {
            CardInventory = state.CardInventory!.Acquire(recipe.Ingredient, out _),
            GameplayPreferences = state.GameplayPreferences with { CardModOnce = once },
            RefinedCardIds = [recipe.Ingredient]
        };

        MenuData data = Project(assets, state, definitions);

        Assert.Equal(!once, data.Recipes.Any(entry => entry.NativeAbility == abilityId && entry.NativeRecipeIndex == recipeIndex));
        Assert.Contains(definitions.Abilities[abilityId - 92].Name, data.RefinementAbilities);
    }

    [Fact]
    public void OnceModeRefinesOwnedCardDirectlyAndRefreshesWithoutConsumingCards()
    {
        using GameAssets assets = InstalledAssets();
        GameState state = EmptyState(assets);
        NativeRefinementDefinitions definitions = assets.ReadNativeRefinementDefinitions();
        LiveMenuCatalog catalog = LiveMenuCatalog.ReadInstalled(assets);
        (int abilityId, int recipeIndex, NativeRefinementRecipe recipe) = FirstRecipe(definitions, 4);
        NativeCardInventory cards = state.CardInventory!;
        int owned = Math.Min(100, recipe.IngredientCount * 2);
        for (int copy = 0; copy < owned; copy++)
        {
            cards = cards.Acquire(recipe.Ingredient, out _);
        }
        state = state with { CardInventory = cards, GameplayPreferences = state.GameplayPreferences with { CardModOnce = true } };
        string abilityName = definitions.Abilities[abilityId - 92].Name;
        string recipeId = $"native-refine-{abilityId}-{recipeIndex}";
        List<PlayerAction> actions = [];
        MenuController? menu = null;
        menu = new MenuController(() =>
        {
            MenuData data = LiveMenuData.Build(state, catalog: catalog);
            NativeRefinementMenuProjection.Augment(data, state, definitions, catalog);
            return data;
        }, action =>
        {
            Assert.Equal("ability-recipes", menu!.ScreenId);
            PlayerAction input = NativeRefinementMenuProjection.PlayerAction(action);
            actions.Add(input);
            (int selectedRecipe, int row) = NativeRefinementInput.Validate(input);
            NativeRefinementResult result = definitions.Refine(state.CharacterVitals!, state.Economy!, state.CardInventory, input.Value, selectedRecipe, row, input.Quantity!.Value, state.GameplayPreferences.CardModOnce, state.RefinedCardIds);
            state = state with { CharacterVitals = result.Vitals, Economy = result.Economy, CardInventory = result.Cards, RefinedCardIds = state.RefinedCardIds.Add(recipe.Ingredient) };
        });
        menu.Open("ability");
        int abilityIndex = menu.Entries.ToList().FindIndex(entry => entry.Id == abilityName);
        Assert.True(abilityIndex >= 0);
        for (int index = 0; index < abilityIndex; index++)
        {
            menu.Navigate(MenuInput.Down);
        }
        menu.Navigate(MenuInput.Confirm);
        Assert.Equal("ability-recipes", menu.ScreenId);
        int recipeEntryIndex = menu.Entries.ToList().FindIndex(entry => entry.Id == recipeId);
        Assert.True(recipeEntryIndex >= 0);
        for (int index = 0; index < recipeEntryIndex; index++)
        {
            menu.Navigate(MenuInput.Down);
        }

        menu.Navigate(MenuInput.Confirm);

        Assert.Equal(new PlayerAction(PlayerActionKind.RefineItems, abilityId, Text: $"{recipeIndex}:0", Quantity: 1), Assert.Single(actions));
        Assert.Equal("ability-recipes", menu.ScreenId);
        Assert.DoesNotContain(menu.Entries, entry => entry.Id == recipeId);
        Assert.Equal(cards.Cards, state.CardInventory!.Cards);
        Assert.Equal(cards.ObtainedRareCards, state.CardInventory.ObtainedRareCards);
        Assert.Equal(owned, state.CardInventory.AvailableCount(recipe.Ingredient));
        Assert.Equal(recipe.OutputCount, state.Economy!.Items.Single(stock => stock.ItemId == recipe.Output).Count);
        menu.Navigate(MenuInput.Cancel);
        Assert.Equal("ability", menu.ScreenId);
        Assert.Contains(menu.Entries, entry => entry.Id == abilityName);
    }

    [Fact]
    public void MagicOwnedOnlyByUnavailableCharacterDoesNotExposeRecipe()
    {
        using GameAssets assets = InstalledAssets();
        GameState state = EmptyState(assets);
        NativeRefinementDefinitions definitions = assets.ReadNativeRefinementDefinitions();
        (int abilityId, int recipeIndex, NativeRefinementRecipe recipe) = FirstRecipe(definitions, 2);
        state = state with { CharacterVitals = state.CharacterVitals!.WithMemberFlags(7, false) };
        state.CharacterVitals.Records[7].MagicInventory[0] = new(recipe.Ingredient, 5);

        Assert.DoesNotContain(Project(assets, state, definitions).Recipes, entry => entry.NativeAbility == abilityId && entry.NativeRecipeIndex == recipeIndex);

        state.CharacterVitals.Records[0].MagicInventory[0] = new(recipe.Ingredient, 1);
        MenuRecipe projected = Assert.Single(Project(assets, state, definitions).Recipes, entry => entry.NativeAbility == abilityId && entry.NativeRecipeIndex == recipeIndex);
        Assert.Equal(1, projected.IngredientStock);
    }

    [Theory]
    [InlineData(false, 7)]
    [InlineData(true, 3)]
    public void MagicStockCountsEachInventoryBankOnce(bool shared, int expected)
    {
        using GameAssets assets = InstalledAssets();
        GameState state = EmptyState(assets);
        NativeRefinementDefinitions definitions = assets.ReadNativeRefinementDefinitions();
        (int abilityId, int recipeIndex, NativeRefinementRecipe recipe) = FirstRecipe(definitions, 2);
        state = state with { CharacterVitals = state.CharacterVitals!.WithMemberFlags(1, true) };
        state.CharacterVitals.Records[0].MagicInventory[0] = new(recipe.Ingredient, 3);
        state.CharacterVitals.Records[1].MagicInventory[0] = new(recipe.Ingredient, 4);
        if (shared)
        {
            NativeCharacterVitals vitals = state.CharacterVitals;
            state = state with { CharacterVitals = vitals with { MagicSystem = vitals.MagicSystem with { SharedMagicStock = true } } };
        }

        MenuRecipe projected = Assert.Single(Project(assets, state, definitions).Recipes, entry => entry.NativeAbility == abilityId && entry.NativeRecipeIndex == recipeIndex);

        Assert.Equal(expected, projected.IngredientStock);
    }

    private static GameState EmptyState(GameAssets assets)
    {
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals().DeepCopy();
        for (int id = 0; id < vitals.GuardianForces.Length; id++)
        {
            vitals.GuardianForces[id] = vitals.GuardianForces[id] with { Flags = 0 };
        }
        vitals.GuardianForces[0] = vitals.GuardianForces[0] with { Flags = 1, LearnedBank = new(0, ((1UL << 19) - 1) << 33) };
        foreach (NativeCharacterVital record in vitals.Records)
        {
            Array.Fill(record.MagicInventory, new NativeMagicStock(0, 0));
        }
        NativeEconomy economy = assets.ReadNewGameEconomy().DeepCopy();
        Array.Fill(economy.Items, new NativeItemStock(0, 0));
        return new GameState { CharacterVitals = vitals, Economy = economy, CardInventory = NativeCardInventory.CreateNewGame() };
    }

    private static (int AbilityId, int RecipeIndex, NativeRefinementRecipe Recipe) FirstRecipe(NativeRefinementDefinitions definitions, int kind)
    {
        int abilityId = Enumerable.Range(97, 19).First(id => definitions.Abilities[id - 92].Kind == kind);
        NativeRefinementAbility ability = definitions.Abilities[abilityId - 92];
        int recipeIndex = Enumerable.Range(ability.First, ability.Last - ability.First + 1).First(index => definitions.Recipe(abilityId, index) is { Ingredient: > 0 } recipe && recipe.Ingredient != recipe.Output);
        return (abilityId, recipeIndex, definitions.Recipe(abilityId, recipeIndex));
    }

    private static MenuData Project(GameAssets assets, GameState state, NativeRefinementDefinitions definitions)
    {
        MenuData data = new();
        NativeRefinementMenuProjection.Augment(data, state, definitions, LiveMenuCatalog.ReadInstalled(assets));
        return data;
    }

    private static GameAssets InstalledAssets()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original refinement resources are required.");
        return new GameAssets(directory);
    }
}
