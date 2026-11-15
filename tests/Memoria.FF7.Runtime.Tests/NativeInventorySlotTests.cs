using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeInventorySlotTests
{
    public static void Run()
    {
        CheckLegacyDefaults();
        CheckFieldMutations();
        CheckNativeSaveRoundTrip();
        CheckBattlePublication();
        CheckValidation();
        CheckStandaloneCompatibility();
    }

    private static void CheckLegacyDefaults()
    {
        Ff7InventorySlot[] legacy = JsonSerializer.Deserialize<Ff7InventorySlot[]>("[{\"ItemId\":90,\"Quantity\":3},{\"ItemId\":20,\"Quantity\":5}]")!;
        string before = JsonSerializer.Serialize(legacy);
        Ff7InventorySlot[] normalized = Ff7NativeInventory.Normalize(legacy);
        Check(normalized.SequenceEqual(new Ff7InventorySlot[] { new(90, 3, 0), new(20, 5, 1) }), "An older inventory without slot identities did not derive its recorded compact order.");
        Check(before == JsonSerializer.Serialize(legacy) && legacy.All(item => item.NativeSlot is null), "Resolving legacy inventory slots changed recorded source facts.");
        Ff7InventorySlot[] mixed = Ff7NativeInventory.Normalize([new(90, 3), new(20, 5, 5), new(70, 2)]);
        Check(mixed.SequenceEqual(new Ff7InventorySlot[] { new(90, 3, 0), new(70, 2, 1), new(20, 5, 5) }), "Resolving missing inventory identities overwrote an explicit native slot.");
        Check(mixed.SequenceEqual(Ff7NativeInventory.Normalize(mixed)), "Native inventory slot normalization was not idempotent.");
    }

    private static void CheckFieldMutations()
    {
        Ff7InventorySlot[] source = [new(90, 2, 3), new(20, 1, 9)];
        string before = JsonSerializer.Serialize(source);
        Ff7InventorySlot[] acquired = Ff7NativeInventory.Acquire(source, Encode(15, 1));
        Check(acquired.SequenceEqual(new Ff7InventorySlot[] { new(15, 1, 0), new(90, 2, 3), new(20, 1, 9) }), "Field acquisition did not use the first free native slot while preserving existing owners.");
        Ff7InventorySlot[] decremented = Ff7NativeInventory.Remove(acquired, Encode(90, 1));
        Check(decremented.Single(item => item.ItemId == 90) == new Ff7InventorySlot(90, 1, 3), "Partial field removal changed an item's native slot.");
        Ff7InventorySlot[] removed = Ff7NativeInventory.Remove(decremented, Encode(90, 1));
        Check(removed.Single(item => item.ItemId == 20).NativeSlot == 9 && removed.All(item => item.NativeSlot != 3), "Depleting field inventory compacted another native slot.");
        Ff7InventorySlot[] refilled = Ff7NativeInventory.Acquire(removed, Encode(30, 2));
        Check(refilled.Single(item => item.ItemId == 30).NativeSlot == 1 && refilled.Single(item => item.ItemId == 20).NativeSlot == 9, "Reacquisition appended after live stock instead of filling the first native hole.");
        Check(before == JsonSerializer.Serialize(source), "Field inventory mutations changed their immutable source.");
    }

    private static void CheckNativeSaveRoundTrip()
    {
        Ff7Progress progress = Progress([new(90, 3, 4), new(20, 0, 17), new(70, 2, 319)]);
        byte[] staleOriginal = new byte[Ff7NativeSaveSlot.Size];
        BinaryPrimitives.WriteUInt16LittleEndian(staleOriginal.AsSpan(0x4FC), Encode(10, 99));
        Ff7NativeSaveSlot native = Ff7NativeSaveCodec.ExportField(progress, new byte[1280], 123, 0, 0, 0, 0, staleOriginal);
        byte[] encoded = native.Encode();
        Check(BinaryPrimitives.ReadUInt16LittleEndian(encoded.AsSpan(0x4FC)) == ushort.MaxValue && BinaryPrimitives.ReadUInt16LittleEndian(encoded.AsSpan(0x4FC + 4 * 2)) == Encode(90, 3) && BinaryPrimitives.ReadUInt16LittleEndian(encoded.AsSpan(0x4FC + 17 * 2)) == Encode(20, 0), "Native export restored stale source inventory or compacted its current live holes.");
        Ff7Progress restored = Ff7NativeSaveCodec.ImportProgress(native, progress.InitialKernelData);
        Check(restored.Inventory.SequenceEqual(progress.Inventory), "Native inventory import/export lost slot positions, order or explicit zero-quantity records.");
        Check(restored.Inventory.SequenceEqual(JsonSerializer.Deserialize<Ff7InventorySlot[]>(JsonSerializer.Serialize(restored.Inventory))!), "Cold inventory restoration lost its native slot identities.");
    }

    private static void CheckBattlePublication()
    {
        Ff7InventorySlot[] source = [new(90, 2, 4), new(20, 1, 17), new(70, 2, 319)];
        KernelSection[] kernel = Kernel();
        Ff7BattleInventorySlot[] working = Ff7NativeBattleInventory.Create(source, kernel);
        Check(working[0].ItemId == ushort.MaxValue && working[4].ItemId == 90 && working[17].ItemId == 20 && working[319].ItemId == 70, "The native battle builder compacted the field inventory's physical order.");
        Ff7BattleInventorySlot[] stolen = Ff7NativeBattleInventory.SelectStealable(working, 1, out ushort item);
        Check(item == 20 && stolen[17].ItemId == ushort.MaxValue, "Battle theft did not remove the selected ordered native slot.");
        Ff7BattleInventorySlot[] returned = Ff7NativeBattleInventory.Acquire(stolen, 20, kernel);
        Ff7InventorySlot[] published = Ff7NativeBattleInventory.Project(returned);
        Check(published.SequenceEqual(new Ff7InventorySlot[] { new(20, 1, 0), new(90, 2, 4), new(70, 2, 319) }), "Native battle publication compacted working slots or lost the first-hole return position.");
        Ff7BattleInventorySlot[] next = Ff7NativeBattleInventory.Create(published, kernel);
        Check(next[0].ItemId == 20 && next[4].ItemId == 90 && next[17].ItemId == ushort.MaxValue && next[319].ItemId == 70, "A later battle allocation lost the published native working holes.");
        Check(Ff7NativeBattleInventory.SelectStealable(next, 0, out ushort nextItem)[0].ItemId == ushort.MaxValue && nextItem == 20, "Slot-preserving return/publication changed the next native modulo selection.");
    }

    private static void CheckValidation()
    {
        Reject(() => Ff7NativeInventory.Normalize([new(1, 1, -1)]));
        Reject(() => Ff7NativeInventory.Normalize([new(1, 1, 320)]));
        Reject(() => Ff7NativeInventory.Normalize([new(1, 1, 4), new(2, 1, 4)]));
        Reject(() => Progress([new(1, 1, 4), new(2, 1, 4)]).Validate());
        Reject(() => Ff7NativeBattleInventory.Create([new(1, 1, 4), new(2, 1, 4)], Kernel()));
    }

    private static void CheckStandaloneCompatibility()
    {
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_INVENTORY_SAVE");
        if (string.IsNullOrWhiteSpace(path))
        {
            Console.WriteLine("SKIP existing inventory continuation: configure an external standalone save.");
            return;
        }
        byte[] source = File.ReadAllBytes(path);
        using MemoryStream input = new(source, writable: false);
        CombinedSessionSave envelope = CombinedSessionCodec.Read(input);
        Ff7SessionSave saved = envelope.Ff7 ?? throw new InvalidDataException("The inventory fixture requires an FFVII continuation.");
        string before = JsonSerializer.Serialize(saved);
        saved.Progress.Validate();
        Ff7InventorySlot[] normalized = Ff7NativeInventory.Normalize(saved.Progress.Inventory);
        Check(normalized.Sum(item => item.Quantity) == saved.Progress.Inventory.Sum(item => item.Quantity) && normalized.All(item => Ff7NativeInventory.Quantity(saved.Progress.Inventory, item.ItemId) == item.Quantity), "Deriving inventory slot defaults changed recorded item quantities.");
        Check(before == JsonSerializer.Serialize(saved), "Validating an older inventory continuation changed script PCs, RNG, progress or source slot metadata.");
        using MemoryStream output = new();
        CombinedSessionCodec.Write(output, envelope);
        output.Position = 0;
        Check(before == JsonSerializer.Serialize(CombinedSessionCodec.Read(output).Ff7), "A cold inventory continuation roundtrip changed recorded source facts.");
        Check(source.AsSpan().SequenceEqual(File.ReadAllBytes(path)), "Inventory compatibility inspection changed its standalone source.");
    }

    private static Ff7Progress Progress(Ff7InventorySlot[] inventory)
    {
        return new() { InitialKernelData = new byte[2876], Characters = [], Party = [255, 255, 255], Inventory = inventory, Materia = [], Gil = 500 };
    }

    private static KernelSection[] Kernel()
    {
        KernelSection[] kernel = Enumerable.Range(0, 8).Select(index => new KernelSection((ushort)index, ReadOnlyMemory<byte>.Empty)).ToArray();
        kernel[4] = new(4, new byte[128 * 28]);
        kernel[5] = new(5, new byte[128 * 44]);
        kernel[6] = new(6, new byte[32 * 36]);
        kernel[7] = new(7, new byte[32 * 16]);
        return kernel;
    }

    private static ushort Encode(int item, int quantity)
    {
        return checked((ushort)(item | quantity << 9));
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
        throw new InvalidOperationException("Invalid native inventory slot ownership was accepted.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
