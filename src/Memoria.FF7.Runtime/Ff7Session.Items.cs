using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7Session
{
    private FieldHostReply AcquireItem(FieldItemCommand command)
    {
        Progress = Progress with { Inventory = Ff7NativeInventory.Acquire(Progress.Inventory, command.Encoded) };
        return new(true);
    }
}
