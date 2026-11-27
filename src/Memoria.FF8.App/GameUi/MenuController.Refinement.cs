using System.Globalization;

namespace Memoria.FF8.GameUi;

public sealed partial class MenuController
{
    private string _refinementAbilityId = "";

    internal string RefinementAbility => RefinementChoices().FirstOrDefault(ability => ability.Id == _refinementAbilityId).Label ?? Recipe?.Ability ?? "";

    private string CurrentRefinementAbilityId => _refinementAbilityId.Length > 0 ? _refinementAbilityId : Recipe is { } selected ? RefinementAbilityId(selected) : "";

    private static string RefinementAbilityId(int id) => "native-refinement-" + id.ToString(CultureInfo.InvariantCulture);

    private static string RefinementAbilityId(MenuRecipe recipe) => recipe.NativeAbility is int id ? RefinementAbilityId(id) : recipe.Ability;

    private IEnumerable<(string Id, string Label)> RefinementChoices()
    {
        return Data.RefinementAbilities.Select(ability => (Id: RefinementAbilityId(ability.Key), Label: ability.Value))
            .Concat(Data.Recipes.Select(recipe => (Id: RefinementAbilityId(recipe), Label: recipe.Ability)))
            .DistinctBy(ability => ability.Id);
    }

    internal IReadOnlyList<MenuEntry> RefinementAbilities()
    {
        return RefinementChoices().Select(ability => new MenuEntry(ability.Id, ability.Label, SystemText("ui.refinementabilities.entry.help.select.an.item.to.refine", "Select an item to refine."), "ability-recipes", new MenuAction("select-refinement-ability", ability.Id))).ToArray();
    }

    internal IReadOnlyList<MenuEntry> RefinementRecipes()
    {
        string abilityId = CurrentRefinementAbilityId;
        return Data.Recipes.Where(recipe => RefinementAbilityId(recipe) == abilityId && !(recipe.CardModOnce && recipe.CardAlreadyRefined)).Select(recipe =>
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

    private int RefinementResultStock(MenuRecipe recipe)
    {
        if (recipe.ResultStock is { } stock)
        {
            return stock;
        }
        return recipe.NativeKind is 0 or 2
            ? Data.Magic.Where(magic => magic.Id == recipe.ResultId && (!Data.SharedMagicStock || magic.CharacterId == Data.Characters.FirstOrDefault()?.Id)).Sum(magic => magic.Quantity)
            : Data.Items.FirstOrDefault(item => item.Id == recipe.ResultId)?.Quantity ?? 0;
    }

    private string[] RefinementResultOwners(MenuRecipe recipe)
    {
        if (Data.SharedMagicStock || recipe.NativeKind is not (0 or 2))
        {
            return [];
        }
        return Data.Characters.Select(character => $"{character.Name} - {recipe.ResultStockByCharacter.GetValueOrDefault(character.Id, Data.Magic.FirstOrDefault(magic => magic.Id == recipe.ResultId && magic.CharacterId == character.Id)?.Quantity ?? 0)}").ToArray();
    }
}
