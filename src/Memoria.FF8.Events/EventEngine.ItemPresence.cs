using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private string? UnsupportedNativeItemPresence(Fiber fiber)
    {
        if (NativeRuntimeProfile is not { IsVerified: true } || services.Economy is not NativeEconomy economy || fiber.Stack.Count == 0 || !fiber.Stack.PeekValue().IsExact)
        {
            return "Native522350 item presence requires the authenticated saved item pairs and one exact DWORD operand";
        }
        economy.Validate();
        return null;
    }

    private int[] ExecuteNativeItemPresence(Fiber fiber)
    {
        int[] args = Pop(fiber, 1);
        //522350 ->56DAF0 compares each zero-extended itemID BYTE with the full
        //DWORD argument, independently of the paired quantity, then writes R0.
        fiber.Results[0] = services.Economy!.Items.Any(item => item.ItemId == args[0]) ? 1 : 0;
        return args;
    }
}
