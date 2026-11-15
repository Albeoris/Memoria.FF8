using System.Buffers.Binary;
using Memoria.FF7.Formats;

namespace Memoria.FF7.Runtime;

public sealed record Ff7BattleInventorySlot(ushort ItemId, byte Quantity, byte Flags);

/// <summary>Ordered native battle inventory. Mutations preserve working slots until field publication.</summary>
public static class Ff7NativeBattleInventory
{
    public const int SlotCount = 320;
    public const ushort EmptyItem = ushort.MaxValue;

    public static Ff7BattleInventorySlot[] Create(IReadOnlyList<Ff7InventorySlot> inventory, IReadOnlyList<KernelSection> kernel)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(kernel);
        ValidateKernel(kernel);
        Ff7InventorySlot[] normalized = Ff7NativeInventory.Normalize(inventory);
        Ff7BattleInventorySlot[] working = Enumerable.Range(0, SlotCount).Select(_ => new Ff7BattleInventorySlot(EmptyItem, 0, 0x0B)).ToArray();
        foreach (Ff7InventorySlot slot in normalized)
        {
            working[slot.NativeSlot!.Value] = new(checked((ushort)slot.ItemId), checked((byte)slot.Quantity), ReadFlags(slot.ItemId, kernel));
        }
        return working;
    }

    public static Ff7InventorySlot[] Project(IReadOnlyList<Ff7BattleInventorySlot> inventory)
    {
        Validate(inventory);
        return inventory.Select((slot, index) => (slot, index)).Where(value => value.slot.ItemId != EmptyItem).Select(value => new Ff7InventorySlot(value.slot.ItemId, value.slot.Quantity, value.index)).ToArray();
    }

    public static Ff7BattleInventorySlot[] Spend(IReadOnlyList<Ff7BattleInventorySlot> inventory, ushort itemId)
    {
        Validate(inventory);
        if (itemId > 511)
        {
            throw new ArgumentOutOfRangeException(nameof(itemId));
        }
        int index = Enumerable.Range(0, inventory.Count).FirstOrDefault(index => inventory[index].ItemId == itemId, -1);
        return RemoveAt(inventory, index, out _);
    }

    public static Ff7BattleInventorySlot[] SelectStealable(IReadOnlyList<Ff7BattleInventorySlot> inventory, ushort cachedRoll, out ushort stolenItem)
    {
        Validate(inventory);
        int count = inventory.Count(slot => slot.ItemId != EmptyItem && (slot.Flags & 1) == 0);
        if (count == 0)
        {
            stolenItem = EmptyItem;
            return inventory.ToArray();
        }
        int selected = cachedRoll % count;
        for (int index = 0; index < inventory.Count; index++)
        {
            Ff7BattleInventorySlot slot = inventory[index];
            if (slot.ItemId == EmptyItem || (slot.Flags & 1) != 0)
            {
                continue;
            }
            if (selected-- == 0)
            {
                return RemoveAt(inventory, index, out stolenItem);
            }
        }
        throw new InvalidOperationException("The ordered battle inventory lost its selected native slot.");
    }

    public static Ff7BattleInventorySlot[] Acquire(IReadOnlyList<Ff7BattleInventorySlot> inventory, ushort itemId, IReadOnlyList<KernelSection> kernel)
    {
        Validate(inventory);
        ArgumentNullException.ThrowIfNull(kernel);
        ValidateKernel(kernel);
        if (itemId >= 320)
        {
            throw new NotSupportedException("The native battle item acquisition requires a supported item, weapon, armor or accessory record.");
        }
        Ff7BattleInventorySlot[] result = inventory.ToArray();
        int existing = Array.FindIndex(result, slot => slot.ItemId == itemId);
        if (existing >= 0)
        {
            if (result[existing].Quantity < 99)
            {
                result[existing] = result[existing] with { Quantity = checked((byte)(result[existing].Quantity + 1)) };
            }
            return result;
        }
        int empty = Array.FindIndex(result, slot => slot.ItemId == EmptyItem);
        if (empty >= 0)
        {
            result[empty] = new(itemId, 1, ReadFlags(itemId, kernel));
        }
        return result;
    }

    public static void Validate(IReadOnlyList<Ff7BattleInventorySlot> inventory)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        if (inventory.Count != SlotCount || inventory.Any(slot => slot is null || slot.ItemId != EmptyItem && slot.ItemId > 511 || slot.Quantity > 127 || slot.ItemId == EmptyItem && slot.Quantity != 0 || slot.ItemId == 511 && slot.Quantity == 127))
        {
            throw new InvalidDataException("FFVII working battle inventory requires 320 valid native slots.");
        }
    }

    private static Ff7BattleInventorySlot[] RemoveAt(IReadOnlyList<Ff7BattleInventorySlot> inventory, int index, out ushort itemId)
    {
        Ff7BattleInventorySlot[] result = inventory.ToArray();
        itemId = EmptyItem;
        if (index < 0 || result[index].Quantity == 0)
        {
            return result;
        }
        Ff7BattleInventorySlot selected = result[index];
        itemId = selected.ItemId;
        result[index] = selected.Quantity == 1 ? new(EmptyItem, 0, 0x0A) : selected with { Quantity = checked((byte)(selected.Quantity - 1)) };
        return result;
    }

    private static byte ReadFlags(int itemId, IReadOnlyList<KernelSection> kernel)
    {
        var (section, first, stride, offset) = itemId switch
        {
            < 128 => (4, 0, 28, 10),
            < 256 => (5, 128, 44, 42),
            < 288 => (6, 256, 36, 32),
            < 320 => (7, 288, 16, 14),
            _ => (-1, 0, 0, 0)
        };
        return section < 0 ? (byte)0x0B : (byte)(BinaryPrimitives.ReadUInt16LittleEndian(kernel[section].Data.Span.Slice((itemId - first) * stride + offset, 2)) & 0x0B);
    }

    private static void ValidateKernel(IReadOnlyList<KernelSection> kernel)
    {
        if (kernel.Count <= 7 || kernel[4] is null || kernel[5] is null || kernel[6] is null || kernel[7] is null || kernel[4].Data.Length != 128 * 28 || kernel[5].Data.Length != 128 * 44 || kernel[6].Data.Length != 32 * 36 || kernel[7].Data.Length != 32 * 16)
        {
            throw new InvalidDataException("FFVII battle inventory requires the native item, weapon, armor and accessory restriction directories.");
        }
    }
}
