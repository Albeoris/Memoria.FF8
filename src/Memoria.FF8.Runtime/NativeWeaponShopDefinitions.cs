using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeWeaponRecipe([property: JsonRequired] string Name, [property: JsonRequired] byte Character, [property: JsonRequired] byte CostBase, [property: JsonRequired] ImmutableArray<NativeItemStock> Materials, [property: JsonRequired] ImmutableArray<byte> KernelWords, [property: JsonRequired] ImmutableArray<byte> RecipeWords);

public sealed record NativeWeaponShopDefinitions([property: JsonRequired] string ExecutableHash, [property: JsonRequired] string KernelHash, [property: JsonRequired] string RecipeHash, [property: JsonRequired] string MessageHash, [property: JsonRequired] ImmutableArray<NativeWeaponRecipe> Recipes, [property: JsonRequired] ImmutableArray<byte> Messages, [property: JsonRequired] ImmutableArray<byte> ArgumentMap)
{
    public void Validate()
    {
        if (!NativeFieldRuntimeProfile.IsSupportedImage(ExecutableHash) || new[] { KernelHash, RecipeHash, MessageHash }.Any(hash => hash is not { Length: 64 } || hash.Any(value => !Uri.IsHexDigit(value))) || ArgumentMap.IsDefault || ArgumentMap.Length != 256 || Recipes.IsDefault || Recipes.Length != 33 || Messages.IsDefault || Messages.Length != 68 || Recipes.Any(row => row is null || row.Name is null || row.Materials.IsDefault || row.Materials.Length != 4 || row.Materials.Any(stock => stock is null) || row.KernelWords.IsDefault || row.KernelWords.Length != 12 || row.RecipeWords.IsDefault || row.RecipeWords.Length != 12 || row.Character != row.KernelWords[4] || row.CostBase != row.RecipeWords[3]))
        {
            throw new InvalidDataException("Native weapon shop requires its complete original thirty-three kernel/recipe rows and message words.");
        }
        for (int row = 0; row < Recipes.Length; row++)
        {
            NativeWeaponRecipe recipe = Recipes[row];
            for (int slot = 0; slot < 4; slot++)
            {
                if (recipe.Materials[slot].ItemId >= 199 || recipe.Materials[slot].ItemId != recipe.RecipeWords[4 + 2 * slot] || recipe.Materials[slot].Count != recipe.RecipeWords[5 + 2 * slot])
                {
                    throw new InvalidDataException("Native weapon materials disagree with their retained original recipe bytes.");
                }
            }
        }
    }
}
