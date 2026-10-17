using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private void SetNativeBattleMusic203(int value)
    {
        //51F700 writes only the low BYTE of its original DWORD operand.
        Scene.NativeBattleMusic203 = unchecked((byte)value);
    }

    private string? UnsupportedNativeBattleMusic203(Fiber fiber, JsmOperation operation)
    {
        if (operation.Opcode != Jsm.Opcode.SETBATTLEMUSIC && !(operation.Opcode == Jsm.Opcode.POPM_B && operation.Parameter == 203))
        {
            return null;
        }
        if (fiber.Stack.Count == 0 || !fiber.Stack.PeekValue().IsExact)
        {
            return "Native battle music BYTE203 store requires its exact original DWORD operand; PC and stack are preserved.";
        }
        try
        {
            NativeEventMemoryOwnership.Validate(EventMemory, MemoryRegions());
            if (ResolveMemoryOwner(203, 1) != NativeEventMemoryOwner.BattleMusic)
            {
                return "Native battle music BYTE203 lacks its canonical scene owner.";
            }
        }
        catch (Exception exception) when (exception is NotSupportedException or InvalidDataException or ArgumentOutOfRangeException)
        {
            return exception.Message;
        }
        return null;
    }
}
