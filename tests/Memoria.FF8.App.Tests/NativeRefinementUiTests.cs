using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeRefinementUiTests
{
    [Fact]
    public void OwnedRecipesProjectOnlyForLearnedAbilitiesWithoutMutation()
    {
        using GameAssets assets = InstalledAssets();
        NativeRefinementDefinitions definitions = assets.ReadNativeRefinementDefinitions();
        LiveMenuCatalog catalog = LiveMenuCatalog.ReadInstalled(assets);
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals().DeepCopy();
        for (int id = 0; id < vitals.GuardianForces.Length; id++)
        {
            vitals.GuardianForces[id] = vitals.GuardianForces[id] with { Flags = 0 };
        }
        vitals.GuardianForces[0] = vitals.GuardianForces[0] with { Flags = 1, LearnedBank = new(0, ((1UL << 19) - 1) << 33) };
        GameState state = new() { CharacterVitals = vitals, Economy = assets.ReadNewGameEconomy(), CardInventory = NativeCardInventory.CreateNewGame() };
        string before = JsonSerializer.Serialize(state);
        MenuData data = new();
        NativeRefinementMenuProjection.Augment(data, state, definitions, catalog);
        Assert.Equal(19, data.RefinementAbilities.Count);
        Assert.All(data.Recipes, recipe => Assert.True(recipe.IngredientStock > 0));
        foreach (MenuRecipe recipe in data.Recipes)
        {
            NativeRefinementRecipe native = definitions.Recipe(recipe.NativeAbility!.Value, recipe.NativeRecipeIndex!.Value);
            Assert.Equal(native.IngredientCount, recipe.IngredientQuantity);
            Assert.Equal(native.OutputCount, recipe.ResultQuantity);
            Assert.Equal(recipe.NativeKind is 0 or 2 ? catalog.MagicName(native.Output) : catalog.ItemName(native.Output), recipe.ResultName);
            Assert.Equal(definitions.MaximumBatches(vitals, state.Economy!, state.CardInventory, recipe.NativeAbility.Value, recipe.NativeRecipeIndex.Value, 0), recipe.MaximumBatches[0]);
        }
        Assert.Contains("native-refine", data.SupportedActions);
        Assert.Equal(before, JsonSerializer.Serialize(state));
        vitals.GuardianForces[0] = vitals.GuardianForces[0] with { Flags = 0 };
        MenuData unavailable = new();
        NativeRefinementMenuProjection.Augment(unavailable, state, definitions, catalog);
        Assert.Empty(unavailable.Recipes);
        Assert.DoesNotContain("native-refine", unavailable.SupportedActions);
        MenuController controller = new(() => unavailable);
        controller.Open("ability");
        Assert.Empty(controller.Entries);
    }

    [Fact]
    public void QuantitySelectionUsesPhysicalRecipientAndExplicitAllBatchCount()
    {
        MenuData data = new();
        data.Characters.Add(new MenuCharacter { Id = "character-1", NativeId = 9, Name = "Dream recipient" });
        data.Recipes.Add(new MenuRecipe("native-refine-97-3", "Refine", "item-1", 1, "magic-2", "Magic", 5) { NativeAbility = 97, NativeRecipeIndex = 3, NativeKind = 0, IngredientName = "Ingredient", MaximumBatches = new Dictionary<int, int> { [1] = 7 } });
        data.SupportedActions.Add("native-refine");
        List<PlayerAction> actions = [];
        MenuController controller = new(() => data, action => actions.Add(NativeRefinementMenuProjection.PlayerAction(action)));
        controller.Open("ability");
        controller.Navigate(MenuInput.Confirm);
        controller.Navigate(MenuInput.Confirm);
        Assert.Equal("ability-refine", controller.ScreenId);
        Assert.False(controller.Entries.Single(entry => entry.Id == "10").Enabled);
        int selected = controller.Entries.ToList().FindIndex(entry => entry.Id == "7");
        for (int index = 0; index < selected; index++)
        {
            controller.Navigate(MenuInput.Down);
        }
        Assert.NotNull(controller.BuildView());
        controller.Navigate(MenuInput.Confirm);
        Assert.Equal(new PlayerAction(PlayerActionKind.RefineItems, 97, Text: "3:1", Quantity: 7), Assert.Single(actions));
    }

    [Theory]
    [InlineData("97:3", "1", "")]
    [InlineData("97", "1", "2")]
    [InlineData("97:3", "", "2")]
    public void MissingExplicitRecipeRecipientOrQuantityRefuses(string source, string target, string quantity)
    {
        Assert.Throws<ArgumentException>(() => NativeRefinementMenuProjection.PlayerAction(new MenuAction("native-refine", source, target, quantity)));
    }

    private static GameAssets InstalledAssets()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original menu recipes and initialization resources are required.");
        return new GameAssets(directory);
    }
}
