using Memoria.FF8.Runtime;
using Memoria.FF8.Events;

namespace Memoria.FF8.GameUi;

/// <summary>Read-only UI rows from the actual native shop cache and its authenticated definitions.</summary>
public static class NativeShopMenuProjection
{
    public static PlayerAction PurchaseAction(MenuAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (action.Kind != "shop-buy" || !action.SubjectId.StartsWith("item-", StringComparison.Ordinal) || !int.TryParse(action.SubjectId.AsSpan(5), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int itemId) || !int.TryParse(action.Value, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int quantity))
        {
            throw new ArgumentException("Shop purchase requires an explicit item and quantity.", nameof(action));
        }
        return new PlayerAction(PlayerActionKind.BuyShopItem, itemId, Quantity: quantity);
    }

    public static MenuData Build(NativeShopTransaction shop)
    {
        ArgumentNullException.ThrowIfNull(shop);
        shop.Validate();
        MenuData data = new() { Location = "Shop", Currency = checked((int)shop.Gil), SourceDescription = "Shop", UnavailableReason = "This shop action is unavailable." };
        foreach (byte itemId in shop.AvailableItems)
        {
            string id = $"item-{itemId}";
            string name = shop.Definitions.ResolvePrice(itemId).Name!;
            data.Items.Add(new MenuItem { Id = id, Name = name, Quantity = shop.Counts[itemId], Price = checked((int)shop.Price(itemId)), Description = $"Owned: {shop.Counts[itemId]}" });
            data.SupportedActions.Add($"shop-buy:{id}");
        }
        return data;
    }
}
