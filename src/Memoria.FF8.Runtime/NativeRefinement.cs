using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeRefinementRecipe(ushort OutputCount, byte Requirement, byte Ingredient, byte IngredientCount, byte Output);
public sealed record NativeRefinementAbility(string Name, byte Kind, byte First, byte Last);
public sealed record NativeRefinementResult(NativeCharacterVitals Vitals, NativeEconomy Economy, NativeCardInventory? Cards, int Batches);

/// <summary>Original kernel menu-ability ranges and m000..m004 recipes consumed by4D7180/4D7410.</summary>
public sealed record NativeRefinementDefinitions(string ExecutableHash, string KernelHash, ImmutableArray<NativeRefinementAbility> Abilities, ImmutableArray<ImmutableArray<NativeRefinementRecipe>> Tables)
{
    public void Validate()
    {
        if (ExecutableHash != NativeFieldRuntimeProfile.FF8EnExecutableHash || KernelHash != "E378FB8F198EDE3DAE858F0DED6670F9BA423AA79ABFFF7237E701DFC7F9E7F6" || Abilities.IsDefault || Abilities.Length != 24 || Tables.IsDefault || Tables.Length != 5 || Tables.Where((table, kind) => table.IsDefault || table.Length != new[] { 102, 143, 10, 12, 110 }[kind] || table.Any(recipe => recipe is null || recipe.IngredientCount == 0 || recipe.Output == 0 || recipe.OutputCount == 0)).Any() || Abilities.Where((ability, index) => ability is null || index >= 5 && (ability.Kind >= 5 || ability.First > ability.Last || ability.Last >= Tables[ability.Kind].Length)).Any())
        {
            throw new InvalidDataException("Native refinement requires the authenticated complete kernel ranges and five original recipe tables.");
        }
    }

    public IReadOnlyList<int> LearnedAbilities(NativeCharacterVitals vitals)
    {
        Validate();
        vitals.Validate();
        if (!string.Equals(vitals.KernelSha256, KernelHash, StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException("Refinement requires character definitions from its authenticated recipe kernel.");
        }
        //4C2B40 includes every acquired GF, whether junctioned or not.
        return Enumerable.Range(0, 16).Where(id => (vitals.GuardianForces[id].Flags & 1) != 0).SelectMany(id => vitals.GuardianForceAbilities(id)).Where(ability => ability.Learned && ability.Ability is >= 97 and < 116).Select(ability => ability.Ability).Distinct().Order().ToArray();
    }

    public NativeRefinementRecipe Recipe(int abilityId, int recipeIndex)
    {
        Validate();
        if (abilityId is < 97 or >= 116)
        {
            throw new ArgumentOutOfRangeException(nameof(abilityId));
        }
        NativeRefinementAbility ability = Abilities[abilityId - 92];
        if (recipeIndex < ability.First || recipeIndex > ability.Last)
        {
            throw new ArgumentOutOfRangeException(nameof(recipeIndex));
        }
        return Tables[ability.Kind][recipeIndex];
    }

    public int MaximumBatches(NativeCharacterVitals vitals, NativeEconomy economy, NativeCardInventory? cards, int abilityId, int recipeIndex, int row)
    {
        NativeRefinementRecipe recipe = Recipe(abilityId, recipeIndex);
        economy.Validate();
        if (!string.Equals(vitals.InitOutSha256, economy.InitOutSha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Refinement requires character and inventory owners from the same authenticated initialization.");
        }
        if (!LearnedAbilities(vitals).Contains(abilityId) || recipe.Requirement > 100)
        {
            return 0;
        }
        byte kind = Abilities[abilityId - 92].Kind;
        if (kind is 0 or 2 && (uint)row >= 8)
        {
            return 0;
        }
        if (vitals.Records.Any(record => record.MagicInventory.Any(stock => stock.SpellId != 0 && stock.Count > 100) || record.MagicInventory.Where(stock => stock.SpellId != 0 && stock.Count > 0).GroupBy(stock => stock.SpellId).Any(group => group.Count() > 1)) || economy.Items.Any(stock => stock.ItemId != 0 && stock.Count > 100) || economy.Items.Where(stock => stock.ItemId != 0).GroupBy(stock => stock.ItemId).Any(group => group.Count() > 1))
        {
            throw new InvalidDataException("Refinement requires unambiguous native stock IDs with counts at most100.");
        }
        NativeCharacterVital? character = kind is 0 or 2 ? NativeCharacterVitals.NormalizeMagicInventory(vitals.Records[row]) : null;
        if (character is not null && NativeCharacterVitals.GetMagicCapacity(character, recipe.Output) != 0)
        {
            return 0;
        }
        int ingredients = kind == 4 ? cards?.AvailableCount(recipe.Ingredient) ?? 0 : kind == 2 ? character!.MagicInventory.FirstOrDefault(stock => stock.SpellId == recipe.Ingredient)?.Count ?? 0 : economy.Items.FirstOrDefault(stock => stock.ItemId == recipe.Ingredient)?.Count ?? 0;
        int output = character is not null ? character.MagicInventory.FirstOrDefault(stock => stock.SpellId == recipe.Output)?.Count ?? 0 : economy.Items.FirstOrDefault(stock => stock.ItemId == recipe.Output)?.Count ?? 0;
        //4D8FAE rounds capacity UP: the last complete batch may lose excess output at100.
        return Math.Max(0, Math.Min(ingredients / recipe.IngredientCount, (100 - output + recipe.OutputCount - 1) / recipe.OutputCount));
    }

    public NativeRefinementResult Refine(NativeCharacterVitals vitals, NativeEconomy economy, NativeCardInventory? cards, int abilityId, int recipeIndex, int row, int batches)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(batches, 1);
        int maximum = MaximumBatches(vitals, economy, cards, abilityId, recipeIndex, row);
        if (batches > maximum)
        {
            throw new InvalidOperationException("Refinement requires a learned GF ability, sufficient ingredients and output capacity.");
        }
        vitals = vitals.NormalizeMagicInventories();
        NativeRefinementRecipe recipe = Recipe(abilityId, recipeIndex);
        byte kind = Abilities[abilityId - 92].Kind;
        int consumed = recipe.IngredientCount * batches;
        int produced = recipe.OutputCount * batches;
        NativeEconomy nextEconomy = economy.DeepCopy();
        NativeCharacterVitals nextVitals = vitals.DeepCopy();
        NativeCardInventory? nextCards = cards?.DeepCopy();
        if (kind is 0 or 2)
        {
            NativeCharacterVital character = nextVitals.Records[row];
            NativeMagicStock[] magic = character.MagicInventory.ToArray();
            int slot = Array.FindIndex(magic, stock => stock.SpellId == recipe.Output && stock.Count != 0);
            if (slot < 0)
            {
                slot = Array.FindIndex(magic, stock => stock.SpellId == 0);
            }
            magic[slot] = new NativeMagicStock(recipe.Output, checked((byte)Math.Min(100, (magic[slot].SpellId == recipe.Output ? magic[slot].Count : 0) + produced)));
            byte[] junctions = character.Junction.MagicJunctions.ToArray();
            if (kind == 2)
            {
                int ingredientSlot = Array.FindIndex(magic, stock => stock.SpellId == recipe.Ingredient);
                int remaining = magic[ingredientSlot].Count - consumed;
                magic[ingredientSlot] = remaining == 0 ? new NativeMagicStock(0, 0) : magic[ingredientSlot] with { Count = checked((byte)remaining) };
                if (remaining == 0)
                {
                    //4C2D50 clears the nineteen stat junctions on exhausted ingredient magic.
                    for (int index = 0; index < 19; index++)
                    {
                        if (junctions[index] == recipe.Ingredient)
                        {
                            junctions[index] = 0;
                        }
                    }
                }
            }
            nextVitals.Records[row] = character with { MagicInventory = magic, Junction = character.Junction with { MagicJunctions = junctions } };
            nextVitals = nextVitals.RebuildMagicMenuRow(row);
        }
        else
        {
            //4D7B34/4D7C2C/4D7CBE grant output before removing the input.
            nextEconomy = nextEconomy.AddItem(recipe.Output, produced, out _);
            nextEconomy = nextEconomy.UpdateItemOrder(recipe.Output);
        }
        if (kind == 4)
        {
            for (int index = 0; index < consumed; index++)
            {
                nextCards = nextCards!.Transfer(recipe.Ingredient, 0, out _);
            }
        }
        else if (kind != 2)
        {
            int slot = Array.FindIndex(nextEconomy.Items, stock => stock.ItemId == recipe.Ingredient);
            int remaining = nextEconomy.Items[slot].Count - consumed;
            nextEconomy.Items[slot] = remaining == 0 ? new NativeItemStock(0, 0) : nextEconomy.Items[slot] with { Count = checked((byte)remaining) };
        }
        return new NativeRefinementResult(nextVitals, nextEconomy, nextCards, batches);
    }
}
