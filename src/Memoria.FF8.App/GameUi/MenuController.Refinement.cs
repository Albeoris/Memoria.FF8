using System.Globalization;

namespace Memoria.FF8.GameUi;

public sealed partial class MenuController
{
    private string _refinementAbility = "";

    internal string RefinementAbility => _refinementAbility.Length > 0 ? _refinementAbility : Recipe?.Ability ?? "";

    internal IReadOnlyList<MenuEntry> RefinementAbilities()
    {
        return Data.RefinementAbilities.Concat(Data.Recipes.Select(recipe => recipe.Ability)).Distinct().Select(ability => new MenuEntry(ability, ability, SystemText("ui.refinementabilities.entry.help.select.an.item.to.refine", "Select an item to refine."), "ability-recipes", new MenuAction("select-refinement-ability", ability))).ToArray();
    }

    internal IReadOnlyList<MenuEntry> RefinementRecipes()
    {
        return Data.Recipes.Where(recipe => recipe.Ability == RefinementAbility && !(recipe.CardModOnce && recipe.CardAlreadyRefined)).Select(recipe =>
        {
            bool available = IsDemo || recipe.MaximumBatches.Values.Any(maximum => maximum > 0);
            string help = available ? recipe.CardModOnce ? FormatSystemText("ui.refinementrecipes.help.receive.cards.are.kept.this.card.can.be", "Receive {0} {1}. Cards are kept; this card can be refined once per game.", recipe.ResultQuantity, recipe.ResultName) : SystemText("ui.refinementrecipes.help.select.an.item.to.refine", "Select an item to refine.") : SystemText("ui.refinementrecipes.help.not.enough.ingredients.or.no.room.for.the", "Not enough ingredients or no room for the result.");
            bool once = !IsDemo && recipe.CardModOnce && recipe.NativeAbility.HasValue && recipe.NativeRecipeIndex.HasValue;
            MenuAction action = once ? new MenuAction("native-refine", $"{recipe.NativeAbility}:{recipe.NativeRecipeIndex}", "0", "1") : new MenuAction("select-recipe", recipe.Id);
            return new MenuEntry(recipe.Id, recipe.IngredientName ?? recipe.IngredientId, help, once ? null : "ability-refine", action, available);
        }).OrderByDescending(entry => entry.Enabled).ToArray();
    }

    private MenuRecipe? HighlightedRefinementRecipe => ScreenId == "ability-recipes" ? Data.Recipes.FirstOrDefault(recipe => recipe.Id == SelectedEntry?.Id) : Recipe;

    private string RefinementStock(MenuRecipe recipe)
    {
        int? stock = recipe.IngredientStock ?? (recipe.NativeKind == 4 ? Data.Cards.FirstOrDefault(card => card.Id == recipe.IngredientId)?.Quantity : Data.Items.FirstOrDefault(item => item.Id == recipe.IngredientId)?.Quantity);
        return stock?.ToString(CultureInfo.InvariantCulture) ?? "";
    }
}
