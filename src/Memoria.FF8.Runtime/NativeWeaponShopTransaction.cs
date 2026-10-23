using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeWeaponShopTransaction([property: JsonRequired] int RawArgument, [property: JsonRequired] NativeWeaponShopDefinitions Definitions, [property: JsonRequired] string InitOutSha256, [property: JsonRequired] ImmutableArray<byte> Counts, [property: JsonRequired] uint Gil, [property: JsonRequired] bool Past, [property: JsonRequired] byte EligibleMask, [property: JsonRequired] int SelectedCharacter, [property: JsonRequired] ushort RemodelRate, [property: JsonRequired] ImmutableArray<byte> AvailableRecipes)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool WeaponMagazineRequired { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool WeaponUpgradeOrder { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public uint ObtainedWeaponsBits { get; init; }

    public static (NativeCharacterVitals Vitals, NativeEconomy Economy, NativeWeaponShopTransaction Shop, NativeWeaponRecipes Recipes) Begin(int rawArgument, NativeWeaponShopDefinitions definitions, NativeEconomy economy, NativeCharacterVitals vitals, NativeWeaponRecipes recipes, GameplayPreferences? preferences = null)
    {
        preferences ??= new GameplayPreferences();
        preferences.Validate();
        definitions.Validate();
        economy.Validate();
        vitals.Validate();
        recipes.Validate();
        if (definitions.ArgumentMap[unchecked((byte)rawArgument)] != 21 || economy.InitOutSha256 != vitals.InitOutSha256 || recipes.InitOutSha256 != vitals.InitOutSha256 || definitions.KernelHash != vitals.KernelSha256)
        {
            throw new NotSupportedException("Native weapon shop requires its actual argument mapping and matching original saved/stat inputs.");
        }
        recipes = recipes.RecordEquippedWeapons(vitals);
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
                if ((known & bit) != 0 || !preferences.WeaponMagazineRequired && CanRemodel(recipe, counts, gil))
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
                if ((!preferences.WeaponMagazineRequired || (known & bit) != 0) && (!preferences.WeaponUpgradeOrder || HasPreviousWeapon(definitions, weapon, recipes)) && CanRemodel(recipe, counts, gil))
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
        NativeWeaponShopTransaction shop = new(rawArgument, definitions, prepared.InitOutSha256, counts, gil, prepared.DreamEnabled, eligible, selected, rate, available.ToImmutable())
        {
            WeaponMagazineRequired = preferences.WeaponMagazineRequired,
            WeaponUpgradeOrder = preferences.WeaponUpgradeOrder,
            ObtainedWeaponsBits = recipes.ObtainedWeaponsBits
        };
        shop.Validate();
        return (prepared, normalized, shop, recipes with { Bits = known });
    }

    private static bool HasPreviousWeapon(NativeWeaponShopDefinitions definitions, int recipeId, NativeWeaponRecipes recipes)
    {
        byte character = definitions.Recipes[recipeId].Character;
        for (int prior = recipeId - 1; prior >= 0; prior--)
        {
            if (definitions.Recipes[prior].Character == character)
            {
                return (recipes.ObtainedWeaponsBits & 1u << prior) != 0;
            }
        }
        return true;
    }

    public bool MissingPreviousUpgrade(int recipeId)
    {
        if ((uint)recipeId >= 28)
        {
            throw new ArgumentOutOfRangeException(nameof(recipeId));
        }
        return WeaponUpgradeOrder && !HasPreviousWeapon(Definitions, recipeId, new NativeWeaponRecipes(InitOutSha256, 0) { ObtainedWeaponsBits = ObtainedWeaponsBits });
    }

    private bool CanUseRecipe(int recipeId, NativeWeaponRecipes recipes)
    {
        return (!WeaponMagazineRequired || (recipes.Bits & 1u << recipeId) != 0) && (!WeaponUpgradeOrder || HasPreviousWeapon(Definitions, recipeId, recipes));
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

    /// <summary>4EA770 rebuilds the selected physical character's list and discovers affordable recipes.</summary>
    public (NativeWeaponShopTransaction Shop, NativeWeaponRecipes Recipes) SelectCharacter(int character, NativeWeaponRecipes recipes)
    {
        Validate();
        recipes.Validate();
        if (recipes.InitOutSha256 != InitOutSha256)
        {
            throw new InvalidDataException("Weapon selection changed its saved recipe owner.");
        }
        if ((uint)character >= 6 || (EligibleMask & 1 << character) == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(character), "Weapon selection requires an admitted physical character.");
        }
        uint known = recipes.Bits;
        ImmutableArray<byte>.Builder offers = ImmutableArray.CreateBuilder<byte>();
        for (int weapon = 0; weapon < 28; weapon++)
        {
            NativeWeaponRecipe recipe = Definitions.Recipes[weapon];
            if (recipe.Character != character)
            {
                continue;
            }
            uint bit = 1u << weapon;
            byte flags = (known & bit) != 0 ? (byte)0x40 : (byte)0;
            if ((!WeaponMagazineRequired || (known & bit) != 0) && CanRemodel(recipe, Counts, Gil))
            {
                known |= bit;
                if (!WeaponUpgradeOrder || HasPreviousWeapon(Definitions, weapon, recipes))
                {
                    flags |= 0x80;
                }
            }
            if (flags != 0)
            {
                offers.Add((byte)(flags | weapon));
            }
        }
        return (this with { SelectedCharacter = character, AvailableRecipes = offers.ToImmutable(), ObtainedWeaponsBits = recipes.ObtainedWeaponsBits }, recipes with { Bits = known });
    }

    /// <summary>4EAE46/68/8D debit local gil/materials and immediately equip the selected recipe.</summary>
    public (NativeWeaponShopTransaction Shop, NativeCharacterVitals Vitals, NativeWeaponRecipes Recipes) Remodel(int recipeId, NativeCharacterVitals vitals, NativeWeaponRecipes recipes, out bool accepted)
    {
        Validate();
        vitals.Validate();
        recipes.Validate();
        if (vitals.InitOutSha256 != InitOutSha256 || vitals.KernelSha256 != Definitions.KernelHash || vitals.DreamEnabled != Past || recipes.InitOutSha256 != InitOutSha256)
        {
            throw new InvalidDataException("Weapon remodeling changed its actual saved inputs.");
        }
        accepted = false;
        if ((uint)recipeId >= 28 || Past || Definitions.Recipes[recipeId].Character != SelectedCharacter || vitals.Records[SelectedCharacter].Weapon == recipeId || !AvailableRecipes.Any(offer => (offer & 0x3F) == recipeId && (offer & 0x80) != 0) || !CanUseRecipe(recipeId, recipes) || !CanRemodel(Definitions.Recipes[recipeId], Counts, Gil))
        {
            return (this, vitals, recipes);
        }
        ImmutableArray<byte>.Builder counts = Counts.ToBuilder();
        foreach (NativeItemStock material in Definitions.Recipes[recipeId].Materials)
        {
            if (material.ItemId != 0)
            {
                counts[material.ItemId] = unchecked((byte)(counts[material.ItemId] - material.Count));
            }
        }
        NativeCharacterVitals equipped = vitals.DeepCopy();
        equipped.Records[SelectedCharacter] = equipped.Records[SelectedCharacter] with { Weapon = (byte)recipeId };
        NativeWeaponShopTransaction charged = this with { Counts = counts.ToImmutable(), Gil = Gil - Price(recipeId) };
        recipes = recipes with { ObtainedWeaponsBits = recipes.ObtainedWeaponsBits | 1u << recipeId };
        var refreshed = charged.SelectCharacter(SelectedCharacter, recipes);
        accepted = true;
        return (refreshed.Shop, equipped, refreshed.Recipes);
    }

    public void Validate()
    {
        if ((ObtainedWeaponsBits & 0xF0000000u) != 0 || Definitions is null || InitOutSha256 is not { Length: 64 } || InitOutSha256.Any(value => !Uri.IsHexDigit(value)) || Counts.IsDefault || Counts.Length != 200 || AvailableRecipes.IsDefault || (EligibleMask & ~0x3F) != 0 || SelectedCharacter < -1 || SelectedCharacter >= 6 || (SelectedCharacter == -1) != (EligibleMask == 0) || SelectedCharacter >= 0 && (EligibleMask & 1 << SelectedCharacter) == 0 || RemodelRate is not (750 or 1000))
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
