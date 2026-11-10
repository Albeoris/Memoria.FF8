using System.Globalization;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;
using Memoria.FF8.GameUi;

namespace Memoria.FF8;

internal static class Ff7ShopMenuProjection
{
    internal static MenuData Create(Ff7Session session)
    {
        Ff7ShopState state = session.Shop ?? throw new InvalidOperationException("The shop view requires an active native shop.");
        Ff7ShopDefinition shop = session.ShopResources.Get(state.ShopId);
        MenuData data = new() { Location = session.Field.Name, Currency = checked((int)session.Progress.Gil), ShowSeeDRank = false, SourceDescription = "Shop" };
        data.SupportedActions.UnionWith(["shop-buy", "shop-sell", "ff7-shop-exit"]);
        int[] itemIds = shop.Stock.Where(entry => entry.Kind == Ff7ShopStockKind.Item).Select(entry => entry.NativeId).Concat(session.Progress.Inventory.Select(entry => entry.ItemId)).Distinct().ToArray();
        foreach (int id in itemIds)
        {
            Ff7ShopStockEntry item = session.ShopResources.DescribeItem(id);
            int owned = session.Progress.Inventory.SingleOrDefault(entry => entry.ItemId == id)?.Quantity ?? 0;
            bool inStock = shop.Stock.Any(entry => entry.Kind == Ff7ShopStockKind.Item && entry.NativeId == id);
            data.Items.Add(new()
            {
                Id = ItemId(id),
                Name = item.Name,
                Description = item.Description,
                Quantity = owned,
                Price = inStock ? checked((int)session.ShopUnitPrice(Ff7ShopTransactionKind.Buy, Ff7ShopStockKind.Item, id)) : null,
                SalePrice = checked((int)session.ShopUnitPrice(Ff7ShopTransactionKind.Sell, Ff7ShopStockKind.Item, id)),
                CanSell = item.CanSell,
                MaximumQuantity = inStock ? checked(owned + session.ShopMaximumQuantity(Ff7ShopTransactionKind.Buy, Ff7ShopStockKind.Item, id)) : owned
            });
        }
        foreach (Ff7ShopStockEntry item in shop.Stock.Where(entry => entry.Kind == Ff7ShopStockKind.Materia))
        {
            data.Items.Add(new()
            {
                Id = MateriaId(item.NativeId, -1),
                Name = item.Name,
                Description = item.Description,
                Price = checked((int)session.ShopUnitPrice(Ff7ShopTransactionKind.Buy, Ff7ShopStockKind.Materia, item.NativeId)),
                CanSell = false,
                MaximumQuantity = session.ShopMaximumQuantity(Ff7ShopTransactionKind.Buy, Ff7ShopStockKind.Materia, item.NativeId)
            });
        }
        foreach (Ff7ShopMateriaEntry materia in session.ShopMateriaInventory)
        {
            data.Items.Add(new()
            {
                Id = MateriaId(materia.MateriaId, materia.NativeSlot),
                Name = materia.Name,
                Description = materia.Description + "  AP " + materia.AbilityPoints.ToString(CultureInfo.InvariantCulture),
                Quantity = 1,
                SalePrice = checked((int)materia.SalePrice),
                MaximumQuantity = 1
            });
        }
        return data;
    }

    internal static Ff7ShopTransaction Transaction(Ff7ShopState state, MenuAction action)
    {
        string[] parts = action.SubjectId.Split('/');
        if (action.Kind is not ("shop-buy" or "shop-sell") || parts.Length is < 3 or > 4 || parts[0] != "ff7-shop" || !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out int id) || !int.TryParse(action.Value, NumberStyles.None, CultureInfo.InvariantCulture, out int quantity))
        {
            throw new InvalidDataException("The shop action does not identify a native stock row.");
        }
        Ff7ShopStockKind kind = parts[1] switch
        {
            "item" when parts.Length == 3 => Ff7ShopStockKind.Item,
            "materia" when parts.Length == 4 => Ff7ShopStockKind.Materia,
            _ => throw new InvalidDataException("The shop action has an invalid native stock namespace.")
        };
        int slot = -1;
        if (kind == Ff7ShopStockKind.Materia && !int.TryParse(parts[3], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out slot))
        {
            throw new InvalidDataException("The shop action has an invalid native materia slot.");
        }
        return new(state.NextTransactionId, action.Kind == "shop-buy" ? Ff7ShopTransactionKind.Buy : Ff7ShopTransactionKind.Sell, kind, id, quantity, slot);
    }

    private static string ItemId(int id) => "ff7-shop/item/" + id.ToString(CultureInfo.InvariantCulture);
    private static string MateriaId(int id, int slot) => "ff7-shop/materia/" + id.ToString(CultureInfo.InvariantCulture) + "/" + slot.ToString(CultureInfo.InvariantCulture);
}
