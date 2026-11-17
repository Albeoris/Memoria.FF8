using Albeoris.Games.FF8.Jsm;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private int[] ExecuteNativeWait(Fiber fiber)
    {
        int count = fiber.Stack.Peek();
        int remaining = unchecked(count - 1);
        Pop(fiber, 1);
        if (remaining != 0)
        {
            // 51D870 decrements the retained DWORD only when this request is selected.
            fiber.Stack.Push(remaining);
            fiber.StackExpressions.Push("WAIT countdown");
            fiber.Position--;
        }
        return [count];
    }

    private void ValidateNativeWait(Fiber fiber)
    {
        if (NativeRuntimeProfile is not { IsVerified: true } || fiber.Complete)
        {
            return;
        }
        if (fiber.WaitReason == WaitReason.Frames && fiber.Script.ContainsOperation(fiber.Position - 1) && program!.Operations[fiber.Position - 1].Opcode == Jsm.Opcode.WAIT)
        {
            throw new InvalidDataException("The saved WAIT uses an elapsed deadline instead of its retained DWORD countdown. Explicitly migrate this pending execution contract before use.");
        }
        if (fiber.WaitReason == WaitReason.None && fiber.Script.ContainsOperation(fiber.Position) && program!.Operations[fiber.Position].Opcode == Jsm.Opcode.WAIT && (fiber.Stack.Count == 0 || !fiber.Stack.PeekValue().IsExact))
        {
            throw new InvalidDataException("The saved WAIT lost its exact retained DWORD countdown.");
        }
    }
}
