using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeItemTests
{
    public static void Run()
    {
        Ff7InventorySlot[] source = [new(0, 98), new(2, 1)];
        Ff7InventorySlot[] merged = Ff7NativeInventory.Acquire(source, 0x0400);
        Check(merged[0].Quantity == 99 && source[0].Quantity == 98, "Existing native stacks must saturate at99 without mutating the prior snapshot.");
        Check(merged[1] == source[1], "An acquisition changed a different item.");
        Ff7InventorySlot[] inserted = Ff7NativeInventory.Acquire(source, (127 << 9) | 400);
        Check(inserted[^1] == new Ff7InventorySlot(400, 127), "A new native stack must preserve all7 quantity bits.");
        Check(Ff7NativeInventory.Acquire([], 1).Single() == new Ff7InventorySlot(1, 0), "A zero-quantity nonempty native slot was dropped.");
        Check(Ff7NativeInventory.Acquire([], ushort.MaxValue).Length == 0, "Native FFFF insertion must remain an empty slot.");
        Check(Ff7NativeInventory.Acquire([new(511, 1)], ushort.MaxValue).Single().Quantity == 99, "Native ID511 matching must occur before empty insertion handling.");
        Check(Ff7NativeInventory.Acquire([new(1, 127)], 1).Single().Quantity == 99, "Even a zero addition reclamps a matching oversized native stack.");
        Ff7InventorySlot[] full = Enumerable.Range(0, Ff7NativeInventory.SlotCount).Select(id => new Ff7InventorySlot(id, 1)).ToArray();
        Check(Ff7NativeInventory.Acquire(full, (1 << 9) | 400).SequenceEqual(full), "A full inventory must silently retain unmatched offers.");
        Check(Ff7NativeInventory.Acquire(full, 1 << 9)[0].Quantity == 2, "A full inventory must still merge an existing item.");
        InstalledContinuation();
    }

    private static void InstalledContinuation()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_AUTOPLAY_CHECKPOINT");
        if (string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(path))
        {
            Console.WriteLine("SKIP original-resource item continuation: optional fixture inputs are unset.");
            return;
        }
        CombinedSessionSave envelope;
        using (FileStream input = File.OpenRead(path))
        {
            envelope = CombinedSessionCodec.Read(input);
        }
        Ff7Installation installation = Ff7Installation.Discover(directory);
        using Ff7Session session = Ff7Session.Restore(installation, envelope.Ff7!);
        string vm = JsonSerializer.Serialize(session.Capture().Vm);
        Ff7Progress before = session.Progress;
        FieldHostRequest request = new(long.MaxValue - 200, 0, 1, 0, new FieldItemCommand(0x0200));
        Ff7InventorySlot[] expected = Ff7NativeInventory.Acquire(before.Inventory, 0x0200);
        Check(session.Host.Execute(request) == new FieldHostReply(true), "STITM invented a status return.");
        Check(session.Progress.Inventory.SequenceEqual(expected), "Native item host dispatch changed the packed acquisition.");
        session.Host.Execute(request);
        Check(session.Progress.Inventory.SequenceEqual(expected), "Repeated item host request duplicated acquisition.");
        Check(JsonSerializer.Serialize(before with { Inventory = expected }) == JsonSerializer.Serialize(session.Progress), "Acquiring an item changed unrelated progression.");
        Check(vm == JsonSerializer.Serialize(session.Capture().Vm), "The item host wrote an invented variable/status result.");
        using Ff7Session cold = Ff7Session.Restore(installation, JsonSerializer.Deserialize<Ff7SessionSave>(JsonSerializer.Serialize(session.Capture()))!);
        cold.Host.Execute(request);
        Check(cold.Progress.Inventory.SequenceEqual(expected), "Cold item request replay duplicated acquisition.");
        Ff7SessionSave zero = session.Capture() with { Progress = session.Progress with { Inventory = [new(1, 0), new(2, 127)] } };
        using Ff7Session nativeQuantities = Ff7Session.Restore(installation, zero);
        Check(nativeQuantities.Progress.Inventory.SequenceEqual(zero.Progress.Inventory), "Native7-bit inventory quantities did not survive cold restoration.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new Exception(message);
        }
    }
}
