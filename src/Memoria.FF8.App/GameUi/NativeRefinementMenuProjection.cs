using System.Globalization;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.GameUi;

public static class NativeRefinementMenuProjection
{
    public static void Augment(MenuData data, GameState state, NativeRefinementDefinitions definitions, LiveMenuCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (state.CharacterVitals is not { } vitals || state.Economy is not { } economy)
        {
            return;
        }
        foreach (int abilityId in definitions.LearnedAbilities(vitals))
        {
            NativeRefinementAbility ability = definitions.Abilities[abilityId - 92];
            data.RefinementAbilities.Add(ability.Name);
            for (int index = ability.First; index <= ability.Last; index++)
            {
                NativeRefinementRecipe recipe = definitions.Recipe(abilityId, index);
                int ingredientStock = ability.Kind == 4 ? state.CardInventory?.AvailableCount(recipe.Ingredient) ?? 0 : ability.Kind == 2 ? Enumerable.Range(0, 8).Where(row => vitals.IsMenuCharacterAvailable(row, state.WorldControls)).Select(row => MagicInventoryResolver.Resolve(vitals, row)).DistinctBy(inventory => inventory.BankId).Sum(inventory => inventory.Count(recipe.Ingredient)) : economy.Items.FirstOrDefault(stock => stock.ItemId == recipe.Ingredient)?.Count ?? 0;
                if (ingredientStock <= 0 || ability.Kind == 4 && state.GameplayPreferences.CardModOnce && state.RefinedCardIds.Contains(recipe.Ingredient))
                {
                    continue;
                }
                bool magicOutput = ability.Kind is 0 or 2;
                string ingredientName = ability.Kind == 4 ? catalog.CardName(recipe.Ingredient) ?? $"Card {recipe.Ingredient}" : ability.Kind == 2 ? catalog.MagicName(recipe.Ingredient) ?? $"Magic {recipe.Ingredient}" : catalog.ItemName(recipe.Ingredient) ?? $"Item {recipe.Ingredient}";
                string resultName = magicOutput ? catalog.MagicName(recipe.Output) ?? $"Magic {recipe.Output}" : catalog.ItemName(recipe.Output) ?? $"Item {recipe.Output}";
                Dictionary<int, int> maxima = (magicOutput ? Enumerable.Range(0, 8) : [0]).ToDictionary(row => row, row => magicOutput && (!vitals.IsMenuCharacterAvailable(row, state.WorldControls) || vitals.Records[row].HitPoints.Minimum != vitals.Records[row].HitPoints.Maximum) ? 0 : definitions.MaximumBatches(vitals, economy, state.CardInventory, abilityId, index, row, state.GameplayPreferences.CardModOnce, state.RefinedCardIds));
                data.Recipes.Add(new MenuRecipe($"native-refine-{abilityId}-{index}", ability.Name, $"{(ability.Kind == 4 ? "card" : ability.Kind == 2 ? "magic" : "item")}-{recipe.Ingredient}", recipe.IngredientCount, $"{(magicOutput ? "magic" : "item")}-{recipe.Output}", resultName, recipe.OutputCount) { CardModOnce = ability.Kind == 4 && state.GameplayPreferences.CardModOnce, CardAlreadyRefined = ability.Kind == 4 && state.RefinedCardIds.Contains(recipe.Ingredient), NativeAbility = abilityId, NativeRecipeIndex = index, NativeKind = ability.Kind, IngredientName = ingredientName, IngredientStock = ingredientStock, MaximumBatches = maxima, OutputCapacity = magicOutput ? Enumerable.Range(0, 8).ToDictionary(row => row, row => 100 - vitals.GetMagicCount(row, recipe.Output)) : new Dictionary<int, int>() });
            }
        }
        if (data.RefinementAbilities.Count > 0)
        {
            data.SupportedActions.Add("native-refine");
        }
    }

    public static IReadOnlyList<MenuEntry> Quantities(MenuController controller)
    {
        if (controller.Recipe is not { NativeAbility: int ability, NativeRecipeIndex: int index } recipe)
        {
            return [];
        }
        int row = 0;
        if (recipe.NativeKind is 0 or 2)
        {
            string id = controller.Character?.Id ?? "";
            row = id.StartsWith("character-", StringComparison.Ordinal) && int.TryParse(id.AsSpan(10), NumberStyles.None, CultureInfo.InvariantCulture, out int physicalRow) ? physicalRow : -1;
        }
        int maximum = Math.Min(100, recipe.MaximumBatches.GetValueOrDefault(row));
        return new[] { 1, 5, 10, maximum }.Distinct().Where(quantity => quantity > 0).Select(quantity => new MenuEntry(quantity.ToString(CultureInfo.InvariantCulture), quantity == maximum ? $"All possible x{quantity}" : $"Refine x{quantity}", recipe.CardModOnce && recipe.CardAlreadyRefined ? "Already refined in this game. Switching modes or acquiring more copies does not reset this card ID." : recipe.CardModOnce ? $"Requires {recipe.IngredientQuantity} {recipe.IngredientName}; receive {recipe.ResultQuantity} {recipe.ResultName}. Cards are kept. This card ID can be refined once per game." : MagicUiText.Format("RefinementCost", recipe.IngredientQuantity * quantity, recipe.IngredientName ?? "", Math.Min(recipe.OutputCapacity.GetValueOrDefault(row, int.MaxValue), recipe.ResultQuantity * quantity), recipe.ResultName), Action: new MenuAction("native-refine", $"{ability}:{index}", row.ToString(CultureInfo.InvariantCulture), quantity.ToString(CultureInfo.InvariantCulture)), Enabled: quantity <= maximum)).ToArray();
    }

    public static PlayerAction PlayerAction(MenuAction action)
    {
        string[] source = action.SubjectId.Split(':');
        if (action.Kind != "native-refine" || source.Length != 2 || !int.TryParse(source[0], NumberStyles.None, CultureInfo.InvariantCulture, out int ability) || !int.TryParse(source[1], NumberStyles.None, CultureInfo.InvariantCulture, out int recipe) || !int.TryParse(action.TargetId, NumberStyles.None, CultureInfo.InvariantCulture, out int row) || !int.TryParse(action.Value, NumberStyles.None, CultureInfo.InvariantCulture, out int batches))
        {
            throw new ArgumentException("Refinement requires an explicit ability, recipe, recipient and batch count.", nameof(action));
        }
        return new PlayerAction(PlayerActionKind.RefineItems, ability, Text: $"{recipe}:{row}", Quantity: batches);
    }
}
