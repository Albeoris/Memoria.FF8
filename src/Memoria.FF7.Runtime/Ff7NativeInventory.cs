namespace Memoria.FF7.Runtime;

public static class Ff7NativeInventory
{
    public const int SlotCount = 320;

    public static Ff7InventorySlot[] Acquire(IReadOnlyList<Ff7InventorySlot> inventory, ushort encoded)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        if (inventory.Count > SlotCount)
        {
            throw new InvalidDataException("FFVII inventory exceeds its native slot capacity.");
        }
        int itemId = encoded & 511;
        int quantity = encoded >> 9;
        Ff7InventorySlot[] result = inventory.ToArray();
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
        return [.. result, new(itemId, quantity)];
    }
}
