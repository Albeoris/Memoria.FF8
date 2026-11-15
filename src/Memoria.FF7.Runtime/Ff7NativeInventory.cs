namespace Memoria.FF7.Runtime;

public static class Ff7NativeInventory
{
    public const int SlotCount = 320;

    public static int Quantity(IReadOnlyList<Ff7InventorySlot> inventory, int itemId) => inventory.FirstOrDefault(slot => slot.ItemId == itemId)?.Quantity ?? 0;

    public static Ff7InventorySlot[] Normalize(IReadOnlyList<Ff7InventorySlot> inventory)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        if (inventory.Count > SlotCount || inventory.Any(slot => slot is null || slot.ItemId is < 0 or > 511 || slot.Quantity is < 0 or > 127 || slot.ItemId == 511 && slot.Quantity == 127) || inventory.Select(slot => slot.ItemId).Distinct().Count() != inventory.Count)
        {
            throw new InvalidDataException("The native inventory contains invalid or duplicated item records.");
        }
        HashSet<int> occupied = [];
        foreach (Ff7InventorySlot item in inventory)
        {
            if (item.NativeSlot is int slot && (slot is < 0 or >= SlotCount || !occupied.Add(slot)))
            {
                throw new InvalidDataException("The native inventory slot identity is invalid or duplicated.");
            }
        }
        List<Ff7InventorySlot> normalized = new(inventory.Count);
        foreach (Ff7InventorySlot item in inventory)
        {
            int slot = item.NativeSlot ?? Enumerable.Range(0, SlotCount).First(candidate => !occupied.Contains(candidate));
            occupied.Add(slot);
            normalized.Add(item with { NativeSlot = slot });
        }
        return normalized.OrderBy(item => item.NativeSlot).ToArray();
    }

    public static Ff7InventorySlot[] Remove(Ff7InventorySlot[] inventory, ushort encoded)
    {
        Ff7InventorySlot[] normalized = Normalize(inventory);
        int itemId = encoded & 511;
        int index = Array.FindIndex(normalized, slot => slot.ItemId == itemId);
        if (index < 0)
        {
            return normalized;
        }
        int remaining = normalized[index].Quantity - (encoded >> 9);
        if (remaining <= 0)
        {
            return [.. normalized.AsSpan(0, index), .. normalized.AsSpan(index + 1)];
        }
        Ff7InventorySlot[] result = normalized;
        result[index] = result[index] with { Quantity = remaining };
        return result;
    }

    public static Ff7InventorySlot[] Acquire(IReadOnlyList<Ff7InventorySlot> inventory, ushort encoded)
    {
        Ff7InventorySlot[] normalized = Normalize(inventory);
        int itemId = encoded & 511;
        int quantity = encoded >> 9;
        Ff7InventorySlot[] result = normalized;
        int existing = Array.FindIndex(result, slot => slot.ItemId == itemId);
        if (existing >= 0)
        {
            result[existing] = result[existing] with { Quantity = Math.Min(99, result[existing].Quantity + quantity) };
            return result;
        }
        if (result.Length == SlotCount || encoded == ushort.MaxValue)
        {
            return result;
        }
        int nativeSlot = Enumerable.Range(0, SlotCount).First(candidate => result.All(slot => slot.NativeSlot != candidate));
        return result.Append(new(itemId, quantity, nativeSlot)).OrderBy(slot => slot.NativeSlot).ToArray();
    }
}
