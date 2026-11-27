using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    public NativeShopTransaction? CalledShop { get; private set; }
    public NativeWeaponShopTransaction? CalledWorkshop { get; private set; }
    public bool HasAbilityShop => CalledShop is not null || CalledWorkshop is not null;

    public void RequireClosedAbilityShop()
    {
        if (HasAbilityShop)
        {
            throw new InvalidOperationException("Close the shop before saving its transactions.");
        }
    }

    public bool ApplyAbilityShop(PlayerAction action, bool worldMenuAvailable)
    {
        bool scriptMenu = partyMenu is { Kind: NativeFieldMenuKind.Normal, Phase: NativePartyMenuPhase.Active };
        if (!HasAbilityShop && !worldMenuAvailable && !scriptMenu && (partyMenu is not null || !CanOpenFieldMenu))
        {
            return false;
        }
        NativeCharacterVitals vitals = services.CharacterVitals ?? throw new NotSupportedException("Ability shops require saved character and GF data.");
        NativeEconomy economy = services.Economy ?? throw new NotSupportedException("Ability shops require saved inventory.");
        NativeWeaponRecipes Recipes() => services.WeaponRecipes ?? throw new NotSupportedException("Weapon remodeling requires saved recipes.");

        void Publish(NativeCharacterVitals characters, NativeEconomy inventory, NativeShopHistory? history = null, NativeMenuRandomState? random = null, NativeWeaponRecipes? recipes = null, BattleLoadoutOverrides? loadouts = null)
        {
            if (scriptMenu)
            {
                PublishPartyMenuCallback(characters, inventory, services.ShotAvailability!, services.SalaryPresentation!.Advance(1), advanceVibration: true, shopHistory: history, menuRandom: random, weaponRecipes: recipes, battleLoadouts: loadouts);
                partyMenu = partyMenu! with { Callbacks = checked(partyMenu.Callbacks + 1) };
            }
            else
            {
                services.SetNativePartyMenuOwners(characters, inventory, services.ShotAvailability!, services.SalaryPresentation!, shopHistory: history, menuRandom: random, weaponRecipes: recipes, battleLoadouts: loadouts);
            }
        }

        switch (action.Kind)
        {
            case PlayerActionKind.OpenCallShop when !HasAbilityShop && NativeShopMenuAbilities.CanUse(vitals, services.ShopHistory, NativeShopMenuAbilities.CallShop, action.Value):
                NativeShopDefinitions definitions = ResolveNativeShopDefinitions?.Invoke() ?? throw new NotSupportedException("Call Shop requires installed shop definitions.");
                var opened = NativeShopTransaction.Open(action.Value, definitions, economy, vitals, services.ShopHistory!, services.MenuRandom ?? throw new NotSupportedException("Call Shop requires the menu random state."));
                Publish(vitals, opened.Economy, opened.History, opened.MenuRandom);
                CalledShop = opened.Shop;
                return true;
            case PlayerActionKind.OpenJunkShop when !HasAbilityShop && NativeShopMenuAbilities.CanUse(vitals, services.ShopHistory, NativeShopMenuAbilities.JunkShop):
                NativeWeaponShopDefinitions weapons = ResolveNativeWeaponShopDefinitions?.Invoke() ?? throw new NotSupportedException("Junk Shop requires installed weapon definitions.");
                var workshop = NativeWeaponShopTransaction.Begin(null, weapons, economy, vitals, Recipes(), services.GameplayPreferences, services.CurrentBattleLoadouts);
                Publish(workshop.Vitals, workshop.Economy, recipes: workshop.Recipes);
                CalledWorkshop = workshop.Shop;
                return true;
            case PlayerActionKind.BuyShopItem or PlayerActionKind.SellShopItem when CalledShop is { } shop && action.Quantity is int quantity:
                bool accepted;
                NativeShopTransaction traded = action.Kind == PlayerActionKind.BuyShopItem ? shop.Buy(action.Value, quantity, out accepted) : shop.Sell(action.Value, quantity, out accepted);
                if (accepted)
                {
                    if (scriptMenu)
                    {
                        Publish(vitals, economy);
                    }
                    CalledShop = traded;
                }
                return accepted;
            case PlayerActionKind.SelectWeaponShopCharacter when CalledWorkshop is { } current && (uint)action.Value < (uint)vitals.Records.Length && (current.EligibleMask & 1 << action.Value) != 0:
                var selected = current.SelectCharacter(action.Value, Recipes());
                Publish(vitals, economy, recipes: selected.Recipes);
                CalledWorkshop = selected.Shop;
                return true;
            case PlayerActionKind.RemodelWeapon when CalledWorkshop is { SelectedCharacter: >= 0 } current:
                var remodeled = current.Remodel(action.Value, vitals, Recipes(), out bool upgraded);
                if (upgraded)
                {
                    Publish(remodeled.Vitals, economy, recipes: remodeled.Recipes, loadouts: current.RemodelLoadout(action.Value, remodeled.Vitals, services.CurrentBattleLoadouts));
                    CalledWorkshop = remodeled.Shop;
                }
                return upgraded;
            case PlayerActionKind.Wait when HasAbilityShop && scriptMenu:
                StepPartyMenu();
                return true;
            case PlayerActionKind.CloseAbilityShop when HasAbilityShop:
                NativeItemOrderAliases aliases = NativeItemOrderAliases.From(services);
                NativeEconomy committed = CalledShop?.Commit(economy, aliases) ?? CalledWorkshop!.Commit(economy, aliases);
                Publish(vitals, committed);
                CalledShop = null;
                CalledWorkshop = null;
                return true;
            default:
                return false;
        }
    }
}
