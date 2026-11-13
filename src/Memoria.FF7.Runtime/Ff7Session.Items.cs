using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7Session
{
    private FieldHostReply UpdateInventory(FieldItemCommand command)
    {
        Ff7InventorySlot[] inventory = command.Remove ? Ff7NativeInventory.Remove(Progress.Inventory, command.Encoded) : Ff7NativeInventory.Acquire(Progress.Inventory, command.Encoded);
        Progress = Progress with { Inventory = inventory };
        return new(true);
    }
}
