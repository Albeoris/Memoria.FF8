namespace Memoria.FF8.Runtime;

public sealed partial class BattleCombatant
{
    internal static NativeMagicStock[] ExpandMagicStocks(IEnumerable<BattleStock> stocks)
    {
        NativeMagicStock[] slots = Enumerable.Repeat(new NativeMagicStock(0, 0), 32).ToArray();
        int index = 0;
        foreach (BattleStock stock in stocks)
        {
            slots[index++] = new NativeMagicStock(checked((byte)stock.Id), checked((byte)stock.Count));
        }
        return slots;
    }

    internal void RestoreMagicInventory(NativeMagicStock[] slots)
    {
        ValidateMagicInventory(slots);
        Array.Copy(slots, NativeMagicInventory, 32);
    }

    internal static void ValidateMagicInventory(NativeMagicStock[] slots)
    {
        if (slots is not { Length: 32 } || slots.Any(stock => stock is null || stock.SpellId >= 64 || stock.Count > 100 || (stock.SpellId == 0) != (stock.Count == 0)) || slots.Where(stock => stock.SpellId != 0).Select(stock => stock.SpellId).Distinct().Count() != slots.Count(stock => stock.SpellId != 0))
        {
            throw new InvalidDataException("Native battle magic inventory requires thirty-two physical slots with unique nonempty spells.");
        }
    }

    internal void ConsumeMagic(int id)
    {
        if (!TryConsumeMagic(id))
        {
            throw new InvalidOperationException("The selected battle magic stock is exhausted.");
        }
    }

    internal bool TryConsumeMagic(int id)
    {
        if (MagicOwner is not null && SetMagicOwner is not null && MagicRecordId is int row)
        {
            NativeCharacterVitals next = MagicOwner().ChangeMagicStock(row, id, -1, MagicOperationReason.ScriptRemoval, out MagicStockMutation result);
            SetMagicOwner(next);
            return result.Accepted != 0;
        }
        int index = Array.FindIndex(NativeMagicInventory, stock => stock.SpellId == id && stock.Count > 0);
        if (index < 0)
        {
            return false;
        }
        NativeMagicStock stock = NativeMagicInventory[index];
        //486A10 clears the exhausted ID in place; other physical slots never move.
        NativeMagicInventory[index] = stock.Count == 1 ? new NativeMagicStock(0, 0) : stock with { Count = (byte)(stock.Count - 1) };
        return true;
    }

    internal int AddMagic(int id, int quantity)
    {
        if (MagicOwner is not null && SetMagicOwner is not null && MagicRecordId is int row)
        {
            NativeCharacterVitals next = MagicOwner().ChangeMagicStock(row, id, quantity, MagicOperationReason.DrawStock, out MagicStockMutation result);
            SetMagicOwner(next);
            return result.Accepted;
        }
        int index = Array.FindIndex(NativeMagicInventory, stock => stock.SpellId == id);
        if (index < 0)
        {
            index = Array.FindIndex(NativeMagicInventory, stock => stock.SpellId == 0);
        }
        if (index < 0 || quantity <= 0)
        {
            return 0;
        }
        int added = Math.Min(100 - NativeMagicInventory[index].Count, quantity);
        if (added > 0)
        {
            NativeMagicInventory[index] = new NativeMagicStock(checked((byte)id), checked((byte)(NativeMagicInventory[index].Count + added)));
        }
        return added;
    }
}
