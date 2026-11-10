using Memoria.FF7.Resources;

namespace Memoria.FF7.Runtime;

public sealed record Ff7ShopTransactionResult(Ff7Progress Progress, Ff7ShopState State, Ff7ShopTransactionReceipt Receipt, byte MateriaFlags = 0);

public static class Ff7NativeShopTransactions
{
    public static bool Supports(Ff7ShopStockKind stockKind, int nativeId) => stockKind == Ff7ShopStockKind.Item && nativeId is >= 0 and < 128 || stockKind == Ff7ShopStockKind.Materia && nativeId is >= 0 and < 96;

    public static uint UnitPrice(Ff7Progress progress, Ff7ShopResources resources, Ff7ShopTransactionKind kind, Ff7ShopStockKind stockKind, int nativeId, int nativeSlot = -1)
    {
        RequireSupportedShopItem(stockKind, nativeId);
        if (kind is < Ff7ShopTransactionKind.Buy or > Ff7ShopTransactionKind.Sell)
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }
        if (stockKind == Ff7ShopStockKind.Materia)
        {
            return kind == Ff7ShopTransactionKind.Buy ? resources.DescribeMateria(nativeId).Price : resources.MateriaSalePrice(nativeId, SelectMateria(progress, nativeSlot, nativeId).AbilityPoints);
        }
        uint price = resources.DescribeItem(nativeId).Price;
        return kind == Ff7ShopTransactionKind.Buy ? price : price >> 1;
    }

    public static int MaximumQuantity(Ff7Progress progress, Ff7ShopResources resources, int shopId, Ff7ShopTransactionKind kind, Ff7ShopStockKind stockKind, int nativeId, int nativeSlot = -1)
    {
        RequireSupportedShopItem(stockKind, nativeId);
        if (stockKind == Ff7ShopStockKind.Materia)
        {
            if (kind == Ff7ShopTransactionKind.Sell)
            {
                _ = SelectMateria(progress, nativeSlot, nativeId);
                return 1;
            }
            Ff7ShopStockEntry materia = resources.Get(shopId).Stock.Single(item => item.Kind == stockKind && item.NativeId == nativeId);
            return progress.Materia.Length < 200 && progress.Gil >= materia.Price ? 1 : 0;
        }
        int owned = progress.Inventory.SingleOrDefault(item => item.ItemId == nativeId)?.Quantity ?? 0;
        if (kind == Ff7ShopTransactionKind.Sell)
        {
            return resources.DescribeItem(nativeId).CanSell ? owned : 0;
        }
        if (kind != Ff7ShopTransactionKind.Buy)
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }
        Ff7ShopStockEntry entry = resources.Get(shopId).Stock.Single(item => item.Kind == stockKind && item.NativeId == nativeId);
        return Math.Max(0, Math.Min(99 - owned, entry.Price == 0 ? 99 : checked((int)Math.Min(99u, progress.Gil / entry.Price))));
    }

    public static Ff7ShopTransactionResult Apply(Ff7Progress progress, Ff7ShopState state, Ff7ShopResources resources, Ff7ShopTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        state.Validate();
        if (state.LastTransaction is { } previous && previous.Transaction.Id == transaction.Id)
        {
            if (previous.Transaction != transaction)
            {
                throw new InvalidDataException("A native shop transaction changed its identity or operands.");
            }
            return new(progress, state, previous);
        }
        if (state.Phase != Ff7ShopPhase.Interactive || transaction.Id != state.NextTransactionId || transaction.Quantity is < 1 or > 127 || transaction.Kind is < Ff7ShopTransactionKind.Buy or > Ff7ShopTransactionKind.Sell)
        {
            throw new InvalidOperationException("The native shop transaction does not own an interactive command.");
        }
        RequireSupportedShopItem(transaction.StockKind, transaction.NativeId);
        Ff7ShopStockEntry item = transaction.Kind == Ff7ShopTransactionKind.Buy
            ? resources.Get(state.ShopId).Stock.Single(entry => entry.Kind == transaction.StockKind && entry.NativeId == transaction.NativeId)
            : transaction.StockKind == Ff7ShopStockKind.Item ? resources.DescribeItem(transaction.NativeId) : resources.DescribeMateria(transaction.NativeId);
        uint unitPrice = UnitPrice(progress, resources, transaction.Kind, transaction.StockKind, transaction.NativeId, transaction.NativeSlot);
        uint total = checked(unitPrice * (uint)transaction.Quantity);
        int maximum = MaximumQuantity(progress, resources, state.ShopId, transaction.Kind, transaction.StockKind, transaction.NativeId, transaction.NativeSlot);
        string? refusal = transaction.Quantity > maximum ? "The native quantity, stock or Gil constraint rejects this transaction." : null;
        Ff7Progress updated = progress;
        byte materiaFlags = 0;
        if (refusal is null)
        {
            if (transaction.StockKind == Ff7ShopStockKind.Materia)
            {
                if (transaction.Kind == Ff7ShopTransactionKind.Buy)
                {
                    updated = progress with { Gil = progress.Gil - total, Materia = Ff7NativeMateriaInventory.Acquire(progress.Materia, transaction.NativeId, 0) };
                    materiaFlags = resources.MateriaAcquisitionFlags(transaction.NativeId);
                }
                else
                {
                    updated = progress with { Gil = unchecked(progress.Gil + total), Materia = Ff7NativeMateriaInventory.Erase(progress.Materia, transaction.NativeSlot, transaction.NativeId) };
                }
            }
            else if (transaction.Kind == Ff7ShopTransactionKind.Buy)
            {
                updated = progress with { Gil = progress.Gil - total, Inventory = Ff7NativeInventory.Acquire(progress.Inventory, checked((ushort)(transaction.NativeId | transaction.Quantity << 9))) };
            }
            else
            {
                updated = progress with { Gil = (uint)Math.Min(uint.MaxValue, (ulong)progress.Gil + total), Inventory = progress.Inventory.Where(entry => entry.ItemId != transaction.NativeId || entry.Quantity != transaction.Quantity).Select(entry => entry.ItemId == transaction.NativeId ? entry with { Quantity = entry.Quantity - transaction.Quantity } : entry).ToArray() };
            }
            updated.Validate();
        }
        int quantityAfter = transaction.StockKind == Ff7ShopStockKind.Item ? updated.Inventory.SingleOrDefault(entry => entry.ItemId == transaction.NativeId)?.Quantity ?? 0 : updated.Materia.Count(materia => materia.MateriaId == transaction.NativeId);
        Ff7ShopTransactionReceipt receipt = new(transaction, refusal is null, refusal, unitPrice, total, updated.Gil, quantityAfter);
        return new(updated, state with { LastTransaction = receipt }, receipt, materiaFlags);
    }

    private static Ff7MateriaState SelectMateria(Ff7Progress progress, int nativeSlot, int materiaId)
    {
        Ff7MateriaState selected = Ff7NativeMateriaInventory.Normalize(progress.Materia).Single(materia => materia.NativeSlot == nativeSlot);
        if (selected.MateriaId != materiaId)
        {
            throw new InvalidDataException("The native shop materia slot changed its selected identity.");
        }
        return selected;
    }

    private static void RequireSupportedShopItem(Ff7ShopStockKind stockKind, int nativeId)
    {
        if (!Supports(stockKind, nativeId))
        {
            throw new NotSupportedException("This native shop transaction requires its materia or equipment inventory contract.");
        }
    }
}
