using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    public Func<NativeWeaponShopDefinitions>? ResolveNativeWeaponShopDefinitions { get; set; }

    private (NativeCharacterVitals Vitals, NativeEconomy Economy, NativeWeaponShopTransaction Shop, NativeWeaponRecipes Recipes) PrepareNativeWeaponShopEntry(NativePartyMenuState current)
    {
        return PrepareNativeWeaponShopEntry(current.ShopRawArgument ?? throw new InvalidDataException("Weapon shop lost its original DWORD request."));
    }

    private (NativeCharacterVitals Vitals, NativeEconomy Economy, NativeWeaponShopTransaction Shop, NativeWeaponRecipes Recipes) PrepareNativeWeaponShopEntry(int raw)
    {
        NativeWeaponShopDefinitions definitions = ResolveNativeWeaponShopDefinitions?.Invoke() ?? throw new NotSupportedException("Native weapon shop requires its authenticated recipe and weapon definitions.");
        NativeWeaponRecipes recipes = services.WeaponRecipes ?? throw new NotSupportedException("Native weapon shop requires its actual saved known-recipe DWORD.");
        if (!services.CanStoreWeaponRecipes)
        {
            throw new NotSupportedException("Native weapon shop cannot publish its genuine recipe discovery owner.");
        }
        return NativeWeaponShopTransaction.Begin(raw, definitions, services.Economy!, services.CharacterVitals!, recipes, services.GameplayPreferences);
    }

    private bool CloseNativeWeaponShop()
    {
        if (partyMenu is not { Phase: NativePartyMenuPhase.Active, Kind: NativeFieldMenuKind.WeaponShop, WeaponShop: not null } current)
        {
            return false;
        }
        if (current.WeaponShop.Past != services.CharacterVitals!.DreamEnabled)
        {
            throw new NotSupportedException("Native weapon shop cannot commit after its actual saved dream/gil selector changed.");
        }
        //4EAF56 commits the same local counts/gil;4EAF5B normalizes the saved pairs.
        //The weapon window never invokes4ABF10, so Cancel retains result0/kind6.
        NativeEconomy economy = current.WeaponShop.Commit(services.Economy!, NativeItemOrderAliases.From(services));
        NativePartyMenuState next = current with { Phase = NativePartyMenuPhase.Closing, Callbacks = checked(current.Callbacks + 1) };
        PublishPartyMenuCallback(services.CharacterVitals!, economy, services.ShotAvailability!, services.SalaryPresentation!.Advance(1), advanceVibration: true);
        partyMenu = next;
        return true;
    }

    private bool SelectNativeWeaponShopCharacter(int character)
    {
        if (partyMenu is not { Phase: NativePartyMenuPhase.Active, Kind: NativeFieldMenuKind.WeaponShop, WeaponShop: not null } current || (uint)character >= 6 || (current.WeaponShop.EligibleMask & 1 << character) == 0)
        {
            return false;
        }
        var selected = current.WeaponShop.SelectCharacter(character, services.WeaponRecipes!);
        PublishPartyMenuCallback(services.CharacterVitals!, services.Economy!, services.ShotAvailability!, services.SalaryPresentation!.Advance(1), advanceVibration: true, weaponRecipes: selected.Recipes);
        partyMenu = current with { WeaponShop = selected.Shop, Callbacks = checked(current.Callbacks + 1) };
        return true;
    }

    private bool RemodelNativeWeapon(int recipeId)
    {
        if (partyMenu is not { Phase: NativePartyMenuPhase.Active, Kind: NativeFieldMenuKind.WeaponShop, WeaponShop: not null } current)
        {
            return false;
        }
        var remodeled = current.WeaponShop.Remodel(recipeId, services.CharacterVitals!, services.WeaponRecipes!, out bool accepted);
        if (!accepted)
        {
            return false;
        }
        PublishPartyMenuCallback(remodeled.Vitals, services.Economy!, services.ShotAvailability!, services.SalaryPresentation!.Advance(1), advanceVibration: true, weaponRecipes: remodeled.Recipes);
        partyMenu = current with { WeaponShop = remodeled.Shop, Callbacks = checked(current.Callbacks + 1) };
        return true;
    }

    private void ValidateNativeWeaponShopCheckpoint(Checkpoint saved, NativePartyMenuState menu)
    {
        if (menu.ShopRawArgument is not int raw || menu.NativeEntrySelection != unchecked((byte)raw) || menu.Shop is not null || menu.TemporaryCombatParty is not null || menu.EligibleMask != 0 || menu.LockedMask != 0 || menu.ForcedFieldMask != 0 || saved.WeaponRecipes is null || saved.Economy is null || saved.CharacterVitals is null)
        {
            throw new InvalidDataException("Saved weapon shop lost its original request or contains an unrelated constructor.");
        }
        if (menu.Phase is NativePartyMenuPhase.Requested or NativePartyMenuPhase.Entering)
        {
            if (menu.WeaponShop is not null)
            {
                throw new InvalidDataException("Weapon-local cache appeared before its actual constructor.");
            }
            return;
        }
        NativeWeaponShopTransaction shop = menu.WeaponShop ?? throw new InvalidDataException("Active weapon shop lacks its actual native recipe/cache owner.");
        shop.Validate();
        if (shop.RawArgument != raw || shop.InitOutSha256 != saved.Economy.InitOutSha256 || shop.Definitions.KernelHash != saved.CharacterVitals.KernelSha256 || shop.Past != saved.CharacterVitals.DreamEnabled || saved.WeaponRecipes.InitOutSha256 != shop.InitOutSha256 || shop.AvailableRecipes.Any(offer => (saved.WeaponRecipes.Bits & 1u << (offer & 0x3F)) == 0))
        {
            throw new InvalidDataException("Saved weapon shop changed its genuine source or known-recipe constructor effects.");
        }
    }
}
