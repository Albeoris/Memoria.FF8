using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private string? UnsupportedNativeWhoAmI(Fiber fiber)
    {
        if (NativeRuntimeProfile is not { IsVerified: true } || services.CharacterVitals is not NativeCharacterVitals vitals || fiber.Stack.Count == 0 || !fiber.Stack.PeekValue().IsExact)
        {
            return "Native51EF70 WHOAMI requires the authenticated saved character rows and one exact DWORD selector";
        }
        vitals.Validate();
        return null;
    }

    private int[] ExecuteNativeWhoAmI(Fiber fiber)
    {
        int[] operands = Pop(fiber, 1);
        //51EF70 zero-extends52CC20's AL. The lookup consumes only the selector's
        //low BYTE and scans physical saved rows independently of field bindings.
        fiber.Results[0] = services.CharacterVitals!.FindPhysicalRow(unchecked((byte)operands[0]));
        return operands;
    }
}
