using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Runtime;

internal static class NativeBattleInventoryTests
{
    public static void Run()
    {
        CheckResourceFlags();
        CheckOrderedRemoval();
        CheckEmptySelectedSlot();
        CheckAcquisition();
        CheckValidationAndColdRestoration();
    }

    private static void CheckResourceFlags()
    {
        KernelSection[] kernel = Kernel();
        SetRestriction(kernel, 4, 4 * 28 + 10, 0xFFF2);
        SetRestriction(kernel, 5, 2 * 44 + 42, 0xFFFF);
        SetRestriction(kernel, 6, 1 * 36 + 32, 0xFFF8);
        SetRestriction(kernel, 7, 2 * 16 + 14, 0xFFF6);
        Ff7InventorySlot[] source = [new(4, 99), new(130, 1), new(257, 3), new(290, 2), new(400, 1)];
        Ff7BattleInventorySlot[] slots = Ff7NativeBattleInventory.Create(source, kernel);
        Check(slots.Length == 320 && slots.Take(5).Select(slot => slot.Flags).SequenceEqual(new byte[] { 2, 0x0B, 8, 2, 0x0B }), "Working inventory lost its resource-backed restriction directory, category offsets or native flag mask.");
        Check(slots.Skip(5).All(slot => slot == new Ff7BattleInventorySlot(ushort.MaxValue, 0, 0x0B)), "Initial native inventory holes lost their sentinel or metadata.");
        Check(source.SequenceEqual(Ff7NativeBattleInventory.Project(slots)), "Initial native inventory projection changed item order or quantities.");
        Ff7BattleInventorySlot[] stolen = Ff7NativeBattleInventory.SelectStealable(slots, 1, out ushort item);
        Check(item == 257 && stolen[2].Quantity == 2 && stolen[0].Quantity == 99, "Theft ignored the native restriction bit or selected by quantity rather than ordered eligible slots.");
        Check(Ff7NativeBattleInventory.Spend(slots, 130)[1] == new Ff7BattleInventorySlot(ushort.MaxValue, 0, 0x0A), "Ordinary consumption incorrectly applied the theft eligibility restriction.");
    }

    private static void CheckOrderedRemoval()
    {
        Ff7BattleInventorySlot[] slots = Ff7NativeBattleInventory.Create([new(90, 99), new(20, 1), new(70, 2)], Kernel());
        string before = JsonSerializer.Serialize(slots);
        Ff7BattleInventorySlot[] first = Ff7NativeBattleInventory.SelectStealable(slots, 1, out ushort firstItem);
        Check(firstItem == 20 && first[1] == new Ff7BattleInventorySlot(ushort.MaxValue, 0, 0x0A) && first[2].ItemId == 70, "Native item theft reordered slots or failed to preserve the depleted gap.");
        Ff7BattleInventorySlot[] wrapped = Ff7NativeBattleInventory.SelectStealable(slots, 4, out ushort wrappedItem);
        Check(firstItem == wrappedItem && first.SequenceEqual(wrapped), "The retained theft WORD was not reduced modulo eligible slot count.");
        Ff7BattleInventorySlot[] next = Ff7NativeBattleInventory.SelectStealable(first, ushort.MaxValue, out ushort nextItem);
        Check(nextItem == 70 && next[2].Quantity == 1 && next[1].ItemId == ushort.MaxValue, "A depleted native slot was counted by the next cached-roll selection.");
        Check(before == JsonSerializer.Serialize(slots), "Native inventory selection mutated its immutable source array.");
        Check(Ff7NativeBattleInventory.Project(first).SequenceEqual(new Ff7InventorySlot[] { new(90, 99), new(70, 2) }), "Field publication sorted or retained an empty native working slot.");
        Ff7BattleInventorySlot[] spent = Ff7NativeBattleInventory.Spend(slots, 70);
        Check(spent[2] == (slots[2] with { Quantity = 1 }) && spent[0] == slots[0], "Ordinary native item consumption did not remove exactly one selected item.");
        Check(Ff7NativeBattleInventory.Spend(slots, 10).SequenceEqual(slots), "Consuming an absent native item changed working inventory.");
    }

    private static void CheckAcquisition()
    {
        KernelSection[] kernel = Kernel();
        SetRestriction(kernel, 4, 15 * 28 + 10, 0xFFFA);
        Ff7BattleInventorySlot[] initial = Ff7NativeBattleInventory.Create([new(90, 99), new(20, 1), new(70, 2)], kernel);
        Ff7BattleInventorySlot[] depleted = Ff7NativeBattleInventory.Spend(initial, 20);
        Ff7BattleInventorySlot[] acquired = Ff7NativeBattleInventory.Acquire(depleted, 15, kernel);
        Check(acquired[1] == new Ff7BattleInventorySlot(15, 1, 0x0A) && acquired[2] == initial[2] && acquired[3].ItemId == ushort.MaxValue, "Native item acquisition did not reuse the first working gap with resource-backed metadata.");
        Ff7BattleInventorySlot[] incremented = Ff7NativeBattleInventory.Acquire(depleted, 70, kernel);
        Check(incremented[2].Quantity == 3 && incremented[1].ItemId == ushort.MaxValue, "Native item acquisition used an empty slot before an existing stack.");
        Check(Ff7NativeBattleInventory.Acquire(depleted, 90, kernel).SequenceEqual(depleted), "A full native stack allocated another slot or exceeded quantity99.");
        Ff7BattleInventorySlot[] excessive = initial.ToArray();
        excessive[0] = excessive[0] with { Quantity = 127 };
        Check(Ff7NativeBattleInventory.Acquire(excessive, 90, kernel).SequenceEqual(excessive), "Acquisition rewrote an existing native quantity above the acquisition cap.");
        Ff7BattleInventorySlot[] full = Enumerable.Range(0, 320).Select(index => new Ff7BattleInventorySlot((ushort)index, 1, 0)).ToArray();
        full[15] = full[15] with { ItemId = 400, Flags = 0x0B };
        Check(Ff7NativeBattleInventory.Acquire(full, 15, kernel).SequenceEqual(full), "Native acquisition replaced a live slot when no existing stack or working gap was available.");
        Check(depleted[1].ItemId == ushort.MaxValue && initial[1].ItemId == 20, "Acquiring a native item changed an earlier immutable working inventory.");
        try
        {
            Ff7NativeBattleInventory.Acquire(depleted, 320, kernel);
        }
        catch (NotSupportedException)
        {
            return;
        }
        throw new InvalidOperationException("Unproved reserved native acquisition metadata was guessed.");
    }

    private static void CheckEmptySelectedSlot()
    {
        Ff7BattleInventorySlot[] slots = Ff7NativeBattleInventory.Create([new(10, 0), new(20, 2)], Kernel());
        Ff7BattleInventorySlot[] failed = Ff7NativeBattleInventory.SelectStealable(slots, 0, out ushort missing);
        Check(missing == ushort.MaxValue && failed.SequenceEqual(slots), "An eligible zero-quantity slot was filtered out or retried instead of failing its native removal.");
        Ff7BattleInventorySlot[] selected = Ff7NativeBattleInventory.SelectStealable(slots, 1, out ushort item);
        Check(item == 20 && selected[1].Quantity == 1 && selected[0].Quantity == 0, "Theft lost an explicit native zero-quantity slot's ordered position.");
        Ff7BattleInventorySlot[] empty = Ff7NativeBattleInventory.Create([], Kernel());
        Check(Ff7NativeBattleInventory.SelectStealable(empty, ushort.MaxValue, out ushort emptyItem).SequenceEqual(empty) && emptyItem == ushort.MaxValue, "An empty working inventory fabricated an item or modulo operation.");
        Check(Ff7NativeBattleInventory.Project(slots).SequenceEqual(new Ff7InventorySlot[] { new(10, 0), new(20, 2) }), "Publication silently discarded an explicit nonempty native item record with zero quantity.");
    }

    private static void CheckValidationAndColdRestoration()
    {
        KernelSection[] kernel = Kernel();
        Ff7BattleInventorySlot[] slots = Ff7NativeBattleInventory.Spend(Ff7NativeBattleInventory.Create([new(1, 1), new(4, 6)], kernel), 1);
        Ff7BattleInventorySlot[] cold = JsonSerializer.Deserialize<Ff7BattleInventorySlot[]>(JsonSerializer.Serialize(slots))!;
        Ff7NativeBattleInventory.Validate(cold);
        Check(slots.SequenceEqual(cold), "Cold restoration lost native working inventory gaps or flags.");
        Check(Ff7NativeBattleInventory.SelectStealable(slots, 7, out ushort liveItem).SequenceEqual(Ff7NativeBattleInventory.SelectStealable(cold, 7, out ushort coldItem)) && liveItem == coldItem, "Cold restoration changed cached-roll theft selection or removal.");
        Reject(() => Ff7NativeBattleInventory.Create(Enumerable.Range(0, 321).Select(index => new Ff7InventorySlot(index, 1)).ToArray(), kernel));
        Reject(() => Ff7NativeBattleInventory.Create([new(1, 1), new(1, 2)], kernel));
        Reject(() => Ff7NativeBattleInventory.Create([new(512, 1)], kernel));
        Reject(() => Ff7NativeBattleInventory.Create([new(1, 128)], kernel));
        Reject(() => Ff7NativeBattleInventory.Create([new(511, 127)], kernel));
        Reject(() => Ff7NativeBattleInventory.Project(slots[..319]));
        Ff7BattleInventorySlot[] corrupt = slots.ToArray();
        corrupt[0] = new(ushort.MaxValue, 1, 0x0A);
        Reject(() => Ff7NativeBattleInventory.Validate(corrupt));
        KernelSection[] truncated = kernel.ToArray();
        truncated[6] = new(6, new byte[32 * 36 - 1]);
        Reject(() => Ff7NativeBattleInventory.Create([], truncated));
    }

    private static KernelSection[] Kernel()
    {
        KernelSection[] sections = Enumerable.Range(0, 8).Select(index => new KernelSection((ushort)index, ReadOnlyMemory<byte>.Empty)).ToArray();
        sections[4] = new(4, new byte[128 * 28]);
        sections[5] = new(5, new byte[128 * 44]);
        sections[6] = new(6, new byte[32 * 36]);
        sections[7] = new(7, new byte[32 * 16]);
        return sections;
    }

    private static void SetRestriction(KernelSection[] kernel, int section, int offset, ushort flags)
    {
        byte[] data = kernel[section].Data.ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(offset, 2), flags);
        kernel[section] = kernel[section] with { Data = data };
    }

    private static void Reject(Action action)
    {
        try
        {
            action();
        }
        catch (InvalidDataException)
        {
            return;
        }
        throw new InvalidOperationException("Corrupt native battle inventory input was accepted.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
