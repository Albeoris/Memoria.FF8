using Memoria.FF8.Runtime;
using Memoria.FF8.Events;

namespace Memoria.FF8.GameUi;

/// <summary>Read-only UI rows from the actual native shop cache and its authenticated definitions.</summary>
public static class NativeShopMenuProjection
{
    public static void AugmentAbilities(MenuData data, GameState state, LiveMenuCatalog catalog)
    {
        if (state.CharacterVitals is not { } vitals)
        {
            return;
        }
        uint learned = vitals.ShopAbilityMask();
        for (int id = 92; id <= NativeShopMenuAbilities.JunkShop; id++)
        {
            if ((learned & (1u << (id - 92))) == 0)
            {
                continue;
            }
            string name = catalog.AbilityName(id) ?? catalog.FormatSystemText("fallback.ability.name", "Ability {0}", id);
            string description = catalog.AbilityDescription(id) ?? catalog.SystemText("projection.ability.description-unavailable", "Ability description unavailable.");
            string? destination = id switch
            {
                NativeShopMenuAbilities.CallShop => "ability-call-shop",
                NativeShopMenuAbilities.JunkShop => "shop-junk",
                _ => null
            };
            bool enabled = id >= NativeShopMenuAbilities.CallShop && NativeShopMenuAbilities.CanUse(vitals, state.ShopHistory, id);
            data.ShopAbilities.Add(new MenuEntry($"ability-{id}", name, description, destination, id == NativeShopMenuAbilities.JunkShop ? new MenuAction("junk-shop-open") : null, enabled)
            {
                DisabledReason = id >= NativeShopMenuAbilities.CallShop && !enabled ? catalog.ShopAbilityUnavailableReason(id) : null
            });
            if (id == NativeShopMenuAbilities.CallShop && enabled)
            {
                foreach (int shop in NativeShopMenuAbilities.CallableCatalogs(state.ShopHistory!, vitals.DreamEnabled))
                {
                    string subject = shop.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    string shopName = catalog.ShopName(shop);
                    data.CallableShops.Add(new MenuEntry(subject, shopName, shopName, "shop", new MenuAction("call-shop-open", subject)));
                    data.SupportedActions.Add($"call-shop-open:{subject}");
                }
            }
            if (id == NativeShopMenuAbilities.JunkShop && enabled)
            {
                data.SupportedActions.Add("junk-shop-open");
            }
        }
    }

    public static PlayerAction PurchaseAction(MenuAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (action.Kind is not ("shop-buy" or "shop-sell") || !action.SubjectId.StartsWith("item-", StringComparison.Ordinal) || !int.TryParse(action.SubjectId.AsSpan(5), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int itemId) || !int.TryParse(action.Value, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int quantity))
        {
            throw new ArgumentException("Shop purchase requires an explicit item and quantity.", nameof(action));
        }
        return new PlayerAction(action.Kind == "shop-buy" ? PlayerActionKind.BuyShopItem : PlayerActionKind.SellShopItem, itemId, Quantity: quantity);
    }

    public static MenuData Build(NativeShopTransaction shop, LiveMenuCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(shop);
        ArgumentNullException.ThrowIfNull(catalog);
        shop.Validate();
        MenuData data = new() { Location = catalog.ShopName(shop.Catalog), Currency = checked((int)shop.Gil), SourceDescription = catalog.SystemText("shop.title", "Shop"), UnavailableReason = catalog.SystemText("shop.action-unavailable", "This shop action is unavailable.") };
        foreach (byte itemId in shop.AvailableItems.Concat(Enumerable.Range(1, shop.Definitions.Prices.Length - 1).Where(id => shop.Counts[id] > 0).Select(id => (byte)id)).Distinct())
        {
            string id = $"item-{itemId}";
            string name = catalog.ItemName(itemId) ?? shop.Definitions.ResolvePrice(itemId).Name!;
            data.Items.Add(new MenuItem { Id = id, Icon = catalog.ItemIcon(itemId), Name = name, Quantity = shop.Counts[itemId], Price = shop.AvailableItems.Contains(itemId) ? checked((int)shop.Price(itemId)) : null, SalePrice = checked((int)shop.Definitions.SellPrice(itemId, shop.AbilityMask)), Description = catalog.ItemDescription(itemId) ?? catalog.SystemText("shop.item.description-unavailable", "Item description unavailable.") });
            if (shop.AvailableItems.Contains(itemId))
            {
                data.SupportedActions.Add($"shop-buy:{id}");
            }
            data.SupportedActions.Add($"shop-sell:{id}");
        }
        return data;
    }
}
