namespace Memoria.FF8.Runtime;

/// <summary>Original battle item physical rows, debit and refund slot policy.</summary>
public static class NativeBattleItemStockRules
{
    public static void Validate(NativeItemStock[] slots)
    {
        if (slots is not { Length: 198 } || slots.Any(stock => stock is null || stock.Count > 100 || (stock.ItemId == 0) != (stock.Count == 0)) || slots.Where(stock => stock.ItemId != 0).Select(stock => stock.ItemId).Distinct().Count() != slots.Count(stock => stock.ItemId != 0))
        {
            throw new InvalidDataException("Native battle item inventory requires198 physical slots with unique nonempty items.");
        }
    }

    public static int Consume(NativeItemStock[] slots, int id)
    {
        int index = Array.FindIndex(slots, stock => stock.ItemId == id && stock.Count > 0);
        if (index < 0)
        {
            throw new InvalidOperationException("The selected battle item stock is exhausted.");
        }
        NativeItemStock stock = slots[index];
        slots[index] = stock.Count == 1 ? new NativeItemStock(0, 0) : stock with { Count = (byte)(stock.Count - 1) };
        return index;
    }

    public static int Add(NativeItemStock[] slots, int id, int count)
    {
        if (id == 0 || count <= 0)
        {
            return 0;
        }
        int index = Array.FindIndex(slots, stock => stock.ItemId == id);
        if (index < 0)
        {
            index = Array.FindIndex(slots, stock => stock.ItemId == 0);
        }
        if (index < 0 || count <= 0)
        {
            return 0;
        }
        int added = Math.Min(100 - slots[index].Count, count);
        if (added > 0)
        {
            slots[index] = new NativeItemStock(checked((byte)id), checked((byte)(slots[index].Count + added)));
        }
        return added;
    }
}
