using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeWeaponShopTransaction([property: JsonRequired] int RawArgument, [property: JsonRequired] NativeWeaponShopDefinitions Definitions, [property: JsonRequired] string InitOutSha256, [property: JsonRequired] ImmutableArray<byte> Counts, [property: JsonRequired] uint Gil, [property: JsonRequired] bool Past, [property: JsonRequired] byte EligibleMask, [property: JsonRequired] int SelectedCharacter, [property: JsonRequired] ushort RemodelRate, [property: JsonRequired] ImmutableArray<byte> AvailableRecipes)
{
    public static (NativeCharacterVitals Vitals, NativeEconomy Economy, NativeWeaponShopTransaction Shop, NativeWeaponRecipes Recipes) Begin(int rawArgument, NativeWeaponShopDefinitions definitions, NativeEconomy economy, NativeCharacterVitals vitals, NativeWeaponRecipes recipes)
    {
        definitions.Validate();
        economy.Validate();
        vitals.Validate();
        recipes.Validate();
        if (definitions.ArgumentMap[unchecked((byte)rawArgument)] != 21 || economy.InitOutSha256 != vitals.InitOutSha256 || recipes.InitOutSha256 != vitals.InitOutSha256 || definitions.KernelHash != vitals.KernelSha256)
        {
            throw new NotSupportedException("Native weapon shop requires its actual argument mapping and matching original saved/stat inputs.");
        }
        NativeEconomy normalized = NativeShopTransaction.NormalizeInventory(economy);
        NativeCharacterVitals prepared = vitals.DeepCopy();
        for (int weapon = 0; weapon < 30; weapon++)
        {
            prepared = prepared.PreviewWeaponShopRow(definitions.Recipes[weapon].Character, (byte)weapon);
        }
        ImmutableArray<byte> counts = NativeShopTransaction.CreateCounts(normalized);
        uint gil = prepared.DreamEnabled ? normalized.PastGil : normalized.Gil;
        uint known = recipes.Bits;
        byte eligible = 0;
        if (!prepared.DreamEnabled)
        {
            for (int weapon = 0; weapon < 28; weapon++)
            {
                NativeWeaponRecipe recipe = definitions.Recipes[weapon];
                if (recipe.Character >= 6 || (prepared.Records[recipe.Character].MemberFlags & 1) == 0)
                {
                    continue;
                }
                uint bit = 1u << weapon;
                if ((known & bit) != 0 || CanRemodel(recipe, counts, gil))
                {
                    known |= bit;
                    eligible |= (byte)(1 << recipe.Character);
                }
            }
        }
        int selected = eligible == 0 ? -1 : Enumerable.Range(0, 6).First(character => (eligible & 1 << character) != 0);
        ImmutableArray<byte>.Builder available = ImmutableArray.CreateBuilder<byte>();
        if (selected >= 0)
        {
            for (int weapon = 0; weapon < 28; weapon++)
            {
                NativeWeaponRecipe recipe = definitions.Recipes[weapon];
                if (recipe.Character != selected)
                {
                    continue;
                }
                uint bit = 1u << weapon;
                byte flags = (known & bit) != 0 ? (byte)0x40 : (byte)0;
                if (CanRemodel(recipe, counts, gil))
                {
                    known |= bit;
                    flags |= 0x80;
                }
                if (flags != 0)
                {
                    available.Add((byte)(flags | weapon));
                }
            }
        }
        ushort rate = (prepared.ShopAbilityMask() & 1) != 0 ? (ushort)750 : (ushort)1000;
        //4EA728..4EA757 makes another current-weapon4BFC90 preview for
        //each character admitted to this weapon menu, after recipe selection.
        for (int character = 0; character < 8; character++)
        {
            if ((eligible & 1 << character) != 0)
            {
                prepared = prepared.PreviewWeaponShopRow(character, prepared.Records[character].Weapon);
            }
        }
        NativeWeaponShopTransaction shop = new(rawArgument, definitions, prepared.InitOutSha256, counts, gil, prepared.DreamEnabled, eligible, selected, rate, available.ToImmutable());
        shop.Validate();
        return (prepared, normalized, shop, recipes with { Bits = known });
    }

    private static bool CanRemodel(NativeWeaponRecipe recipe, ImmutableArray<byte> counts, uint gil)
    {
        if (gil < 10u * recipe.CostBase)
        {
            return false;
        }
        foreach (NativeItemStock material in recipe.Materials)
        {
            if (material.ItemId != 0)
            {
                if (material.ItemId >= counts.Length)
                {
                    throw new NotSupportedException("Native weapon recipe reads outside its retained two-hundred-byte inventory cache.");
                }
                if (counts[material.ItemId] < material.Count)
                {
                    return false;
                }
            }
        }
        return true;
    }

    public void Validate()
    {
        if (Definitions is null || InitOutSha256 is not { Length: 64 } || InitOutSha256.Any(value => !Uri.IsHexDigit(value)) || Counts.IsDefault || Counts.Length != 200 || AvailableRecipes.IsDefault || (EligibleMask & ~0x3F) != 0 || SelectedCharacter < -1 || SelectedCharacter >= 6 || (SelectedCharacter == -1) != (EligibleMask == 0) || SelectedCharacter >= 0 && (EligibleMask & 1 << SelectedCharacter) == 0 || RemodelRate is not (750 or 1000))
        {
            throw new InvalidDataException("Native weapon shop lost its actual constructor/cache/selection inputs.");
        }
        Definitions.Validate();
        if (Definitions.ArgumentMap[unchecked((byte)RawArgument)] != 21 || AvailableRecipes.Select(offer => offer & 0x3F).Distinct().Count() != AvailableRecipes.Length || AvailableRecipes.Any(offer => (offer & 0x3F) >= 28 || (offer & 0xC0) == 0 || Definitions.Recipes[offer & 0x3F].Character != SelectedCharacter))
        {
            throw new InvalidDataException("Native weapon offers disagree with their actual selected character/recipe list.");
        }
    }

    public uint Price(int recipeId)
    {
        Validate();
        if ((uint)recipeId >= 28)
        {
            throw new NotSupportedException("Native weapon fee requires one of the twenty-eight ordinary recipes.");
        }
        return (uint)(Definitions.Recipes[recipeId].CostBase * RemodelRate / 100);
    }

    public NativeEconomy Commit(NativeEconomy economy)
    {
        Validate();
        return NativeShopTransaction.NormalizeInventory(NativeShopTransaction.CommitCounts(economy, InitOutSha256, Counts, Gil, Past));
    }
}
