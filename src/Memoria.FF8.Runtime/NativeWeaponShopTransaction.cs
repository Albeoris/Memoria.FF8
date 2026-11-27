using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeWeaponShopTransaction([property: JsonRequired] int? RawArgument, [property: JsonRequired] NativeWeaponShopDefinitions Definitions, [property: JsonRequired] string InitOutSha256, [property: JsonRequired] ImmutableArray<byte> Counts, [property: JsonRequired] uint Gil, [property: JsonRequired] bool Past, [property: JsonRequired] ushort EligibleMask, [property: JsonRequired] int SelectedCharacter, [property: JsonRequired] ushort RemodelRate, [property: JsonRequired] ImmutableArray<byte> AvailableRecipes)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool WeaponMagazineRequired { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool WeaponUpgradeOrder { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public uint ObtainedWeaponsBits { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public uint MagazineRecipeBits { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ImmutableSortedDictionary<int, int>? ManagedWeapons { get; init; }

    private int RecipeFamily(int character)
    {
        return ManagedWeapons?.TryGetValue(character, out int weapon) == true ? Definitions.Recipes[weapon].Character : character;
    }

    public int? EquippedWeapon(NativeCharacterVitals? vitals)
    {
        if (SelectedCharacter < 0)
        {
            return null;
        }
        return ManagedWeapons?.TryGetValue(SelectedCharacter, out int weapon) == true ? weapon : vitals?.Records[SelectedCharacter].Weapon;
    }

    public bool CanUpgrade(int recipeId, NativeCharacterVitals? vitals)
    {
        int? equipped = EquippedWeapon(vitals);
        return equipped is not null && equipped != recipeId && (ManagedWeapons?.ContainsKey(SelectedCharacter) != true || recipeId > equipped);
    }

    public static (NativeCharacterVitals Vitals, NativeEconomy Economy, NativeWeaponShopTransaction Shop, NativeWeaponRecipes Recipes) Begin(int? rawArgument, NativeWeaponShopDefinitions definitions, NativeEconomy economy, NativeCharacterVitals vitals, NativeWeaponRecipes recipes, GameplayPreferences? preferences = null, BattleLoadoutOverrides? loadouts = null)
    {
        preferences ??= new GameplayPreferences();
        preferences.Validate();
        definitions.Validate();
        economy.Validate();
        vitals.Validate();
        recipes.Validate();
        loadouts?.Validate();
        if (rawArgument is int argument && definitions.ArgumentMap[unchecked((byte)argument)] != 21 || economy.InitOutSha256 != vitals.InitOutSha256 || recipes.InitOutSha256 != vitals.InitOutSha256 || definitions.KernelHash != vitals.KernelSha256)
        {
            throw new NotSupportedException("Native weapon shop requires its actual argument mapping and matching original saved/stat inputs.");
        }
        recipes = recipes.RecordEquippedWeapons(vitals);
        NativeEconomy inventory = rawArgument.HasValue ? NativeShopTransaction.NormalizeInventory(economy) : economy;
        NativeCharacterVitals prepared = vitals.DeepCopy();
        for (int weapon = 0; weapon < 30; weapon++)
        {
            prepared = prepared.PreviewWeaponShopRow(definitions.Recipes[weapon].Character, (byte)weapon);
        }
        ImmutableArray<byte> counts = NativeShopTransaction.CreateCounts(inventory);
        uint gil = prepared.DreamEnabled ? inventory.PastGil : inventory.Gil;
        uint known = recipes.Bits;
        ushort eligible = 0;
        var managed = ImmutableSortedDictionary.CreateBuilder<int, int>();
        for (int character = 0; character < prepared.Records.Length; character++)
        {
            if (loadouts?.ResolveWeaponId(vitals.Records[character].CharacterId) is int weapon && weapon is >= 0 and < 28)
            {
                managed.Add(character, weapon);
            }
        }
        if (!prepared.DreamEnabled)
        {
            for (int character = 0; character < prepared.Records.Length; character++)
            {
                if (!prepared.IsRosterMember(character) || character >= (rawArgument.HasValue ? 6 : NativeCharacterVitals.NativeRecordCount) && !managed.ContainsKey(character))
                {
                    continue;
                }
                int? activeWeapon = loadouts?.ResolveWeaponId(prepared.Records[character].CharacterId);
                if (activeWeapon is >= 28 && (rawArgument.HasValue || character < 6))
                {
                    continue;
                }
                if (rawArgument is null)
                {
                    eligible |= (ushort)(1 << character);
                }
                int family = managed.TryGetValue(character, out int equippedWeapon) ? definitions.Recipes[equippedWeapon].Character : character;
                for (int weapon = 0; weapon < 28; weapon++)
                {
                    NativeWeaponRecipe recipe = definitions.Recipes[weapon];
                    if (recipe.Character != family)
                    {
                        continue;
                    }
                    uint bit = 1u << weapon;
                    if ((known & bit) != 0 || !preferences.WeaponMagazineRequired && CanRemodel(recipe, counts, gil))
                    {
                        known |= bit;
                        eligible |= (ushort)(1 << character);
                    }
                }
            }
        }
        int selected = eligible == 0 ? -1 : Enumerable.Range(0, prepared.Records.Length).First(character => (eligible & 1 << character) != 0);
        ImmutableArray<byte>.Builder available = ImmutableArray.CreateBuilder<byte>();
        if (selected >= 0)
        {
            int? equipped = managed.TryGetValue(selected, out int selectedWeapon) ? selectedWeapon : null;
            int family = equipped is int id ? definitions.Recipes[id].Character : selected;
            for (int weapon = 0; weapon < 28; weapon++)
            {
                NativeWeaponRecipe recipe = definitions.Recipes[weapon];
                if (recipe.Character != family)
                {
                    continue;
                }
                uint bit = 1u << weapon;
                byte flags = (known & bit) != 0 ? (byte)0x40 : (byte)0;
                if ((!preferences.WeaponMagazineRequired || (recipes.MagazineRecipeBits & bit) != 0) && (!preferences.WeaponUpgradeOrder || HasPreviousWeapon(definitions, weapon, recipes, equipped)) && CanRemodel(recipe, counts, gil))
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
        for (int character = 0; character < prepared.Records.Length; character++)
        {
            if ((eligible & 1 << character) != 0)
            {
                byte weapon = managed.TryGetValue(character, out int managedWeapon) ? checked((byte)managedWeapon) : prepared.Records[character].Weapon;
                prepared = prepared.PreviewWeaponShopRow(character, weapon);
            }
        }
        NativeWeaponShopTransaction shop = new(rawArgument, definitions, prepared.InitOutSha256, counts, gil, prepared.DreamEnabled, eligible, selected, rate, available.ToImmutable())
        {
            WeaponMagazineRequired = preferences.WeaponMagazineRequired,
            WeaponUpgradeOrder = preferences.WeaponUpgradeOrder,
            MagazineRecipeBits = recipes.MagazineRecipeBits,
            ObtainedWeaponsBits = recipes.ObtainedWeaponsBits,
            ManagedWeapons = managed.Count == 0 ? null : managed.ToImmutable()
        };
        shop.Validate();
        return (prepared, inventory, shop, recipes with { Bits = known });
    }

    private static bool HasPreviousWeapon(NativeWeaponShopDefinitions definitions, int recipeId, NativeWeaponRecipes recipes, int? equippedWeapon = null)
    {
        byte character = definitions.Recipes[recipeId].Character;
        for (int prior = recipeId - 1; prior >= 0; prior--)
        {
            if (definitions.Recipes[prior].Character == character)
            {
                return equippedWeapon is int current ? current >= prior : (recipes.ObtainedWeaponsBits & 1u << prior) != 0;
            }
        }
        return true;
    }

    private bool HasPreviousWeapon(int recipeId, NativeWeaponRecipes recipes)
    {
        return HasPreviousWeapon(Definitions, recipeId, recipes, ManagedWeapons?.TryGetValue(SelectedCharacter, out int weapon) == true ? weapon : null);
    }

    public bool MissingPreviousUpgrade(int recipeId)
    {
        if ((uint)recipeId >= 28)
        {
            throw new ArgumentOutOfRangeException(nameof(recipeId));
        }
        return WeaponUpgradeOrder && !HasPreviousWeapon(recipeId, new NativeWeaponRecipes(InitOutSha256, 0) { ObtainedWeaponsBits = ObtainedWeaponsBits });
    }

    public bool MissingMagazine(int recipeId)
    {
        if ((uint)recipeId >= 28)
        {
            throw new ArgumentOutOfRangeException(nameof(recipeId));
        }
        return WeaponMagazineRequired && (MagazineRecipeBits & 1u << recipeId) == 0;
    }

    private bool CanUseRecipe(int recipeId, NativeWeaponRecipes recipes)
    {
        return (!WeaponMagazineRequired || (recipes.MagazineRecipeBits & 1u << recipeId) != 0) && (!WeaponUpgradeOrder || HasPreviousWeapon(recipeId, recipes));
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
        if ((uint)character >= NativeCharacterVitals.ExtendedRecordCount || (EligibleMask & 1 << character) == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(character), "Weapon selection requires an admitted physical character.");
        }
        uint known = recipes.Bits;
        ImmutableArray<byte>.Builder offers = ImmutableArray.CreateBuilder<byte>();
        int family = RecipeFamily(character);
        int? equipped = ManagedWeapons?.TryGetValue(character, out int currentWeapon) == true ? currentWeapon : null;
        for (int weapon = 0; weapon < 28; weapon++)
        {
            NativeWeaponRecipe recipe = Definitions.Recipes[weapon];
            if (recipe.Character != family)
            {
                continue;
            }
            uint bit = 1u << weapon;
            byte flags = (known & bit) != 0 ? (byte)0x40 : (byte)0;
            if ((!WeaponMagazineRequired || (recipes.MagazineRecipeBits & bit) != 0) && CanRemodel(recipe, Counts, Gil))
            {
                known |= bit;
                if (!WeaponUpgradeOrder || HasPreviousWeapon(Definitions, weapon, recipes, equipped))
                {
                    flags |= 0x80;
                }
            }
            if (flags != 0)
            {
                offers.Add((byte)(flags | weapon));
            }
        }
        return (this with { SelectedCharacter = character, AvailableRecipes = offers.ToImmutable(), MagazineRecipeBits = recipes.MagazineRecipeBits, ObtainedWeaponsBits = recipes.ObtainedWeaponsBits }, recipes with { Bits = known });
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
        if ((uint)recipeId >= 28 || Past || Definitions.Recipes[recipeId].Character != RecipeFamily(SelectedCharacter) || !CanUpgrade(recipeId, vitals) || !AvailableRecipes.Any(offer => (offer & 0x3F) == recipeId && (offer & 0x80) != 0) || !CanUseRecipe(recipeId, recipes) || !CanRemodel(Definitions.Recipes[recipeId], Counts, Gil))
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
        NativeWeaponShopTransaction charged = this with { Counts = counts.ToImmutable(), Gil = Gil - Price(recipeId) };
        if (ManagedWeapons?.ContainsKey(SelectedCharacter) == true)
        {
            charged = charged with { ManagedWeapons = ManagedWeapons.SetItem(SelectedCharacter, recipeId) };
        }
        else
        {
            equipped.Records[SelectedCharacter] = equipped.Records[SelectedCharacter] with { Weapon = (byte)recipeId };
        }
        recipes = recipes with { ObtainedWeaponsBits = recipes.ObtainedWeaponsBits | 1u << recipeId };
        var refreshed = charged.SelectCharacter(SelectedCharacter, recipes);
        accepted = true;
        return (refreshed.Shop, equipped, refreshed.Recipes);
    }

    public BattleLoadoutOverrides? RemodelLoadout(int recipeId, NativeCharacterVitals vitals, BattleLoadoutOverrides? loadouts)
    {
        if (ManagedWeapons?.TryGetValue(SelectedCharacter, out int weapon) != true)
        {
            return loadouts;
        }
        int identity = vitals.Records[SelectedCharacter].CharacterId;
        return (loadouts ?? throw new InvalidDataException("The weapon shop lost its equipped weapon.")).RemodelEquippedWeapon(identity, weapon, recipeId);
    }

    public void Validate()
    {
        if ((MagazineRecipeBits & 0xF0000000u) != 0 || (ObtainedWeaponsBits & 0xF0000000u) != 0 || Definitions is null || InitOutSha256 is not { Length: 64 } || InitOutSha256.Any(value => !Uri.IsHexDigit(value)) || Counts.IsDefault || Counts.Length != 200 || AvailableRecipes.IsDefault || (EligibleMask >> NativeCharacterVitals.ExtendedRecordCount) != 0 || SelectedCharacter < -1 || SelectedCharacter >= NativeCharacterVitals.ExtendedRecordCount || (SelectedCharacter == -1) != (EligibleMask == 0) || SelectedCharacter >= 0 && (EligibleMask & 1 << SelectedCharacter) == 0 || RemodelRate is not (750 or 1000))
        {
            throw new InvalidDataException("Native weapon shop lost its actual constructor/cache/selection inputs.");
        }
        Definitions.Validate();
        if (ManagedWeapons?.Any(pair => pair.Key is < 0 or >= NativeCharacterVitals.ExtendedRecordCount || pair.Value is < 0 or >= 28) == true || Enumerable.Range(6, NativeCharacterVitals.ExtendedRecordCount - 6).Any(row => (RawArgument.HasValue || row >= NativeCharacterVitals.NativeRecordCount) && (EligibleMask & 1 << row) != 0 && ManagedWeapons?.ContainsKey(row) != true))
        {
            throw new InvalidDataException("Invalid equipped weapon in the shop.");
        }
        if (RawArgument is int argument && Definitions.ArgumentMap[unchecked((byte)argument)] != 21 || AvailableRecipes.Select(offer => offer & 0x3F).Distinct().Count() != AvailableRecipes.Length || AvailableRecipes.Any(offer => (offer & 0x3F) >= 28 || (offer & 0xC0) == 0 || Definitions.Recipes[offer & 0x3F].Character != RecipeFamily(SelectedCharacter)))
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

    public NativeEconomy Commit(NativeEconomy economy, NativeItemOrderAliases? aliases = null)
    {
        Validate();
        return NativeShopTransaction.NormalizeInventory(NativeShopTransaction.CommitCounts(economy, InitOutSha256, Counts, Gil, Past, aliases));
    }
}
