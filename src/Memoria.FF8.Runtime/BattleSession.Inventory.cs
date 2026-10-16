namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private readonly NativeItemStock[] nativeItems = Enumerable.Repeat(new NativeItemStock(0, 0), 198).ToArray();

    private static NativeItemStock[] ExpandItemStocks(IEnumerable<BattleStock> stocks)
    {
        NativeItemStock[] slots = Enumerable.Repeat(new NativeItemStock(0, 0), 198).ToArray();
        int index = 0;
        foreach (BattleStock stock in stocks)
        {
            slots[index++] = new NativeItemStock(checked((byte)stock.Id), checked((byte)stock.Count));
        }
        return slots;
    }

    private void RestoreNativeItems(NativeItemStock[] slots)
    {
        if (slots is not { Length: 198 } || slots.Any(stock => stock is null || stock.Count > 100 || (stock.ItemId == 0) != (stock.Count == 0)) || slots.Where(stock => stock.ItemId != 0).Select(stock => stock.ItemId).Distinct().Count() != slots.Count(stock => stock.ItemId != 0))
        {
            throw new InvalidDataException("Native battle item inventory requires198 physical slots with unique nonempty items.");
        }
        Array.Copy(slots, nativeItems, 198);
    }

    private void ConsumeNativeItem(int id)
    {
        int index = Array.FindIndex(nativeItems, stock => stock.ItemId == id && stock.Count > 0);
        if (index < 0)
        {
            throw new InvalidOperationException("The selected battle item stock is exhausted.");
        }
        NativeItemStock stock = nativeItems[index];
        nativeItems[index] = stock.Count == 1 ? new NativeItemStock(0, 0) : stock with { Count = (byte)(stock.Count - 1) };
    }
}
